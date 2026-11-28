using System;
using System.Collections.Generic;
using System.Linq;
using HNC.NodeSync;
using LiteNetLib;

namespace HacknetServerTerminal;

/// <summary>
/// 节点同步的转发（终端不保存节点状态，权威在服务端游戏）：
/// - 客户端 NODE_RPT（共享节点上的进展）→ 原样转给服务端游戏；
/// - 服务端游戏 NODE_STATE（共享节点的权威状态）→ 广播给所有已加入的客户端；
/// - NODE_SHARE（分享自己的破解进度）→ 按目标盖上发送者身份后转发，并回复发送者。
///   发送者和接收者都可以是客户端或服务端主机（主机算 0 号玩家，走管道，和玩家邮件一样）。
/// </summary>
internal static class NodeRelay
{
    private const int HostId = 0;

    /// <summary>LiteNetLib 线程：客户端发来的 NODE_* 消息</summary>
    public static void OnClientMessage(NetPeer peer, string msg)
    {
        var sender = NetworkManager.GetClient(peer.Id);
        if (sender is not { Joined: true }) return;

        if (msg.StartsWith(NodeProtocol.Report, StringComparison.Ordinal))
        {
            // 末尾追加发送者，服务端据此记录"谁连着哪台共享节点"
            _ = Program.SendToModFireForget($"{msg}:{sender.PlayerId}");
        }
        else if (msg.StartsWith(NodeProtocol.Share, StringComparison.Ordinal))
        {
            HandleShare(sender.PlayerId, sender.PlayerName,
                text => NetworkManager.SendTo(peer, NodeProtocol.Msg + NodeProtocol.ToB64(text)),
                msg.Substring(NodeProtocol.Share.Length));
        }
    }

    /// <summary>LiteNetLib 线程：已加入的客户端断开（在 RemoveClient 之前），清除它在共享节点上的连接状态</summary>
    public static void OnClientGone(int peerId)
    {
        var c = NetworkManager.GetClient(peerId);
        if (c is { Joined: true })
            _ = Program.SendToModFireForget($"{NodeProtocol.Gone}{c.PlayerId}");
    }

    /// <summary>Pipe 读线程：服务端游戏发来的 NODE_STATE / NODE_SHARE</summary>
    public static void OnModMessage(string msg)
    {
        if (msg.StartsWith(NodeProtocol.State, StringComparison.Ordinal))
        {
            foreach (var c in NetworkManager.GetAllClients())
                if (c.Joined && c.Peer != null) NetworkManager.SendTo(c.Peer, msg);
        }
        else if (msg.StartsWith(NodeProtocol.Share, StringComparison.Ordinal))
        {
            HandleShare(HostId, HostName,
                text => _ = Program.SendToModFireForget(NodeProtocol.Msg + NodeProtocol.ToB64(text)),
                msg.Substring(NodeProtocol.Share.Length));
        }
    }

    /// <summary>主机在名单上显示的名字（大厅里的主机账户名；没有时用 Server）</summary>
    private static string HostName
    {
        get
        {
            string n = LobbyManager.HostName;
            return string.IsNullOrWhiteSpace(n) ? "Server" : n;
        }
    }

    /// <summary>
    /// NODE_SHARE:{b64 目标}:{载荷}。目标：all / * / 玩家名 / P编号；主机可以用 Server、P0、0 或主机账户名。
    /// 转发 NODE_SHARED:{发送者id}:{b64 发送者名}:{载荷}。
    /// </summary>
    private static void HandleShare(int senderId, string senderName, Action<string> reply, string payload)
    {
        var p = payload.Split(new[] { ':' }, 2);
        if (p.Length < 2) return;
        string target = NodeProtocol.FromB64(p[0]).Trim();
        string forward = $"{NodeProtocol.Shared}{senderId}:{NodeProtocol.ToB64(senderName)}:{p[1]}";
        bool all = target.Equals("all", StringComparison.OrdinalIgnoreCase) || target == "*";

        var names = new List<string>();

        // 客户端
        foreach (var c in NetworkManager.GetAllClients())
        {
            if (!c.Joined || c.Peer == null || c.PlayerId == senderId) continue;
            if (!all && !Matches(target, c.PlayerId, c.PlayerName)) continue;
            NetworkManager.SendTo(c.Peer, forward);
            names.Add(c.PlayerName);
        }

        // 服务端主机（0 号）
        if (senderId != HostId && Program.IsModConnected &&
            (all || Matches(target, HostId, HostName) || target.Equals("server", StringComparison.OrdinalIgnoreCase)))
        {
            _ = Program.SendToModFireForget(forward);
            names.Add($"{HostName}（服务端）");
        }

        if (names.Count == 0)
        {
            reply(all
                ? "[分享] 现在没有其他在线玩家。"
                : $"[分享] 找不到在线玩家 {target}（sendmail list 可以查看在线玩家）。");
            return;
        }

        reply($"[分享] 已分享给 {string.Join(", ", names)}。");
        Console.WriteLine($"[分享] {senderName}(P{senderId}) → {string.Join(", ", names)}");
    }

    private static bool Matches(string target, int playerId, string name) =>
        name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
        (int.TryParse(target.TrimStart('P', 'p'), out int pid) && pid == playerId);
}
