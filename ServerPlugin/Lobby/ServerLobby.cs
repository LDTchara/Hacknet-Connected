using System;
using Hacknet;
using HNC.Lobby;

namespace ServerPlugin;

/// <summary>
/// 服务端游戏这一侧的准备大厅：接收终端发来的大厅状态、打开/关闭大厅界面，
/// 把主机的操作（准备 / 移出 / 强制开始）发给终端，开局后在准备完毕时通知终端放客户端进来。
/// 大厅的权威状态在终端（LobbyManager），这里只是显示和转发。
/// 所有方法都在游戏主线程调用（ProcessPendingMessages、GameUpdatePatch、界面）。
/// </summary>
internal static class ServerLobby
{
    /// <summary>终端发来的最新大厅状态；还没收到时为 null</summary>
    public static LobbyState? State;

    public static string ServerName = "";

    /// <summary>"人数不足，是否强制开始"的询问（人数, 上限）；没有询问时为 null</summary>
    public static (int Count, int Capacity)? ForcePrompt;

    /// <summary>大厅聊天记录</summary>
    public static readonly LobbyChatLog Chat = new();

    private static bool _wantScreen;
    private static ServerLobbyScreen? _screen;

    /// <summary>开局后等待"准备完毕"：OS 加载完成且世界种子已发出</summary>
    private static bool _awaitingGameReady;
    private static DateTime? _readySince;

    /// <summary>OS 加载完、种子发出后再等这么久才放客户端进来（让 HNC.Sync 等首帧逻辑先跑完）</summary>
    private const double GameReadyDelaySeconds = 1.5;

    // ═══════════════════════════════════════════════ 终端 → 这里

    /// <summary>处理终端发来的 LOBBY_* 消息；不是大厅消息返回 false</summary>
    public static bool TryHandlePipeMessage(string msg)
    {
        if (!msg.StartsWith(LobbyProtocol.Prefix, StringComparison.Ordinal)) return false;

        if (msg.StartsWith(LobbyProtocol.Open, StringComparison.Ordinal))
        {
            var p = msg.Substring(LobbyProtocol.Open.Length).Split(new[] { ':' }, 2);
            ServerName = p.Length > 1 ? LobbyProtocol.FromB64(p[1]) : "";
            ForcePrompt = null;
            Chat.Clear();
            _wantScreen = true;
            Console.WriteLine($"[Lobby] 大厅已开放: {ServerName}");
        }
        else if (msg.StartsWith(LobbyProtocol.State, StringComparison.Ordinal))
        {
            var s = LobbyState.Decode(msg);
            if (s != null)
            {
                State = s;
                // 万一 LOBBY_OPEN 晚于第一条状态到达，也照样打开界面
                if (s.Phase == LobbyPhase.Lobby) _wantScreen = true;
            }
        }
        else if (msg.StartsWith(LobbyProtocol.ConfirmForce, StringComparison.Ordinal))
        {
            var p = msg.Substring(LobbyProtocol.ConfirmForce.Length).Split(':');
            if (p.Length == 2 && int.TryParse(p[0], out int n) && int.TryParse(p[1], out int cap))
                ForcePrompt = (n, cap);
        }
        else if (msg == LobbyProtocol.ConfirmCancel)
        {
            ForcePrompt = null;
        }
        else if (msg.StartsWith(LobbyProtocol.ChatMsg, StringComparison.Ordinal))
        {
            Chat.Add(msg);
        }
        return true;
    }

    /// <summary>收到 LAUNCH_SERVER、即将进入游戏时调用（ServerManager.HandleLaunchServer）</summary>
    public static void OnLaunch()
    {
        CloseScreen();
        _wantScreen = false;
        ForcePrompt = null;
        _awaitingGameReady = true;
        _readySince = null;
        ServerPhases.Reset();
    }

    // ═══════════════════════════════════════════════ 每帧（GameUpdatePatch）

    public static void Update()
    {
        if (_wantScreen && _screen == null)
        {
            var sman = Game1.getSingleton()?.sman;
            if (sman != null)
            {
                _screen = new ServerLobbyScreen();
                sman.AddScreen(_screen);
            }
        }

        if (_awaitingGameReady)
        {
            var os = OS.currentInstance;
            if (os != null && os.isLoaded && WorldSeedSync.SeedSent && ServerManager.IsConnected)
            {
                _readySince ??= DateTime.UtcNow;
                if ((DateTime.UtcNow - _readySince.Value).TotalSeconds >= GameReadyDelaySeconds)
                {
                    _awaitingGameReady = false;
                    ServerManager.SendToConsole(LobbyProtocol.GameReady);
                    Console.WriteLine("[Lobby] 服务端已准备完毕，通知终端放客户端进入");
                }
            }
        }
    }

    // ═══════════════════════════════════════════════ 主机操作（界面调用）

    public static void SetReady(bool ready) =>
        ServerManager.SendToConsole($"{LobbyProtocol.Ready}{(ready ? 1 : 0)}");

    public static void Kick(int playerId) =>
        ServerManager.SendToConsole($"{LobbyProtocol.Kick}{playerId}");

    /// <summary>主机发聊天（终端盖上发送者后再发回来，所以这里不直接加进记录）</summary>
    public static void SendChat(string text) =>
        ServerManager.SendToConsole(LobbyProtocol.Chat + LobbyProtocol.ToB64(text));

    public static void ForceStart()
    {
        ForcePrompt = null;
        ServerManager.SendToConsole(LobbyProtocol.ForceStart);
    }

    /// <summary>不强制开始：主机取消准备（终端随即撤回询问；之后再点准备会重新询问）</summary>
    public static void DeclineForceStart()
    {
        ForcePrompt = null;
        SetReady(false);
    }

    /// <summary>退出扩展页 / 与终端断开后关闭界面</summary>
    public static void Reset()
    {
        CloseScreen();
        State = null;
        ForcePrompt = null;
        Chat.Clear();
        ServerName = "";
        _wantScreen = false;
        _awaitingGameReady = false;
        _readySince = null;
    }

    internal static void OnScreenUnloaded(ServerLobbyScreen screen)
    {
        if (_screen == screen) _screen = null;
    }

    private static void CloseScreen()
    {
        var s = _screen;
        _screen = null;
        if (s != null && !s.IsExiting) s.ExitScreen();
    }
}
