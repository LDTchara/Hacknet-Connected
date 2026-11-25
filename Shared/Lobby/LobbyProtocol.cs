using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HNC.Lobby;

// 准备大厅协议：终端（HacknetServerTerminal）、ServerPlugin、ClientPlugin 三方共用这一份源码。
// 只用纯 C#，不依赖游戏程序集（终端是 net8，插件是 net472）。
//
// 大厅状态由终端持有；两边游戏里的大厅界面只负责显示和发操作。
//
// 终端 → 服务端游戏（Pipe）
//   LOBBY_OPEN:{上限}:{b64 服务器名}       打开大厅界面
//   LOBBY_STATE:...                       名单与准备状态（见 LobbyState.Encode）
//   LOBBY_CONFIRM_FORCE:{人数}:{上限}       全员已准备但人数不足，询问是否强制开始
//   LOBBY_CONFIRM_CANCEL                  撤回上面的询问
//   LAUNCH_SERVER:...                     （原有消息）开局
// 服务端游戏 → 终端（Pipe）
//   LOBBY_READY:{0|1}                     主机准备 / 取消准备
//   LOBBY_KICK:{playerId}                 把某个客户端移出大厅
//   LOBBY_FORCE_START                     确认强制开始
//   LOBBY_GAME_READY                      服务端已进入游戏并准备完毕，可以放客户端进来
// 客户端 → 终端（LiteNetLib）
//   LOBBY_JOIN:{b64 账户名}                加入大厅（同时确定玩家身份）
//   LOBBY_LEAVE                           离开大厅
//   LOBBY_READY:{0|1}                     准备 / 取消准备
// 终端 → 客户端（LiteNetLib）
//   LOBBY_JOINED:{playerId}               已加入
//   LOBBY_CLOSED                          大厅还没开放（终端还没 start）
//   LOBBY_OPENED                          大厅开放了（广播给所有连接，等待中的客户端据此重新加入）
//   LOBBY_FULL:{人数}:{上限}                满员，拒绝加入
//   LOBBY_KICKED                          被服务端移出
//   LOBBY_STATE:...                       同上
//   LOBBY_START                           服务端已准备完毕，进入游戏
//
// 聊天（任何阶段，只有已加入的成员和服务端能发）
//   LOBBY_CHAT:{b64 文字}                  客户端 → 终端、服务端游戏 → 终端
//   LOBBY_CHAT_MSG:{playerId}:{b64 名字}:{b64 文字}
//                                         终端 → 所有成员 + 服务端游戏；发送者由终端盖上（服务端主机 id 为 0）。
//                                         加入大厅时终端会补发最近的聊天记录。

internal enum LobbyPhase
{
    /// <summary>终端还没 start</summary>
    Closed,
    /// <summary>等待玩家加入、准备</summary>
    Lobby,
    /// <summary>全员准备完毕，服务端正在进入游戏</summary>
    Launching,
    /// <summary>服务端已准备完毕，客户端可以进入（中途加入、重连也走这里）</summary>
    InGame
}

internal sealed class LobbyMember
{
    public int PlayerId;
    public string Name = "";
    public bool Ready;
}

internal sealed class LobbyState
{
    public LobbyPhase Phase = LobbyPhase.Closed;

    /// <summary>客户端人数上限（profile 的 PlayersCount，只算客户端）；0 = 不限</summary>
    public int Capacity;

    public bool HostReady;
    public string HostName = "";
    public List<LobbyMember> Members = new();

    public int Count => Members.Count;

    /// <summary>服务端主机和所有已加入的客户端都已准备</summary>
    public bool AllReady => HostReady && Members.All(m => m.Ready);

    /// <summary>人数已达上限（上限为 0 时视为永远满员，即不限人数也不提示人数不足）</summary>
    public bool IsFull => Capacity <= 0 || Members.Count >= Capacity;

    /// <summary>
    /// LOBBY_STATE:{阶段}:{上限}:{主机已准备 0|1}:{b64 主机名}:{成员列表}
    /// 成员列表：{playerId}.{0|1}.{b64 名字}，用逗号分隔；没有成员时为空。
    /// 名字用 base64，避免名字里的冒号、逗号、点号破坏格式。
    /// </summary>
    public string Encode()
    {
        var list = string.Join(",", Members.Select(m =>
            $"{m.PlayerId}.{(m.Ready ? 1 : 0)}.{LobbyProtocol.ToB64(m.Name)}"));
        return $"{LobbyProtocol.State}{Phase}:{Capacity}:{(HostReady ? 1 : 0)}:{LobbyProtocol.ToB64(HostName)}:{list}";
    }

    public static LobbyState? Decode(string msg)
    {
        if (!msg.StartsWith(LobbyProtocol.State, StringComparison.Ordinal)) return null;
        var p = msg.Substring(LobbyProtocol.State.Length).Split(new[] { ':' }, 5);
        if (p.Length < 5) return null;

        var state = new LobbyState();
        if (!Enum.TryParse(p[0], out state.Phase)) return null;
        int.TryParse(p[1], out state.Capacity);
        state.HostReady = p[2] == "1";
        state.HostName = LobbyProtocol.FromB64(p[3]);

        foreach (var item in p[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = item.Split('.');
            if (f.Length != 3 || !int.TryParse(f[0], out int id)) continue;
            state.Members.Add(new LobbyMember
            {
                PlayerId = id,
                Ready = f[1] == "1",
                Name = LobbyProtocol.FromB64(f[2])
            });
        }
        return state;
    }
}

internal static class LobbyProtocol
{
    public const string Prefix = "LOBBY_";

    public const string Open = "LOBBY_OPEN:";
    public const string State = "LOBBY_STATE:";
    public const string ConfirmForce = "LOBBY_CONFIRM_FORCE:";
    public const string ConfirmCancel = "LOBBY_CONFIRM_CANCEL";
    public const string Ready = "LOBBY_READY:";
    public const string Kick = "LOBBY_KICK:";
    public const string ForceStart = "LOBBY_FORCE_START";
    public const string GameReady = "LOBBY_GAME_READY";

    public const string Join = "LOBBY_JOIN:";
    public const string Leave = "LOBBY_LEAVE";
    public const string Joined = "LOBBY_JOINED:";
    public const string Closed = "LOBBY_CLOSED";
    public const string Opened = "LOBBY_OPENED";
    public const string Full = "LOBBY_FULL:";
    public const string Kicked = "LOBBY_KICKED";
    public const string Start = "LOBBY_START";

    public const string Chat = "LOBBY_CHAT:";
    public const string ChatMsg = "LOBBY_CHAT_MSG:";

    /// <summary>一条聊天的最大字数</summary>
    public const int ChatMaxLength = 200;

    /// <summary>去掉控制字符、截断长度；空消息返回空串</summary>
    public static string CleanChat(string text)
    {
        var sb = new StringBuilder();
        foreach (char c in text ?? "")
            if (!char.IsControl(c)) sb.Append(c);
        string s = sb.ToString().Trim();
        return s.Length > ChatMaxLength ? s.Substring(0, ChatMaxLength) : s;
    }

    public static string EncodeChat(int playerId, string name, string text) =>
        $"{ChatMsg}{playerId}:{ToB64(name)}:{ToB64(text)}";

    public static string ToB64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));

    public static string FromB64(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch { return ""; }
    }
}
