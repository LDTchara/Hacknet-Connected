using System;
using System.Collections.Generic;
using System.Linq;
using HNC.Lobby;
using HNC.Pause;
using LiteNetLib;

namespace HacknetServerTerminal;

/// <summary>
/// 全员暂停（终端持有权威状态）。
///
/// 暂停原因：
/// - 手动：服务端主机按 F8 或终端 pause 命令；
/// - 等人：游戏中的客户端超过 6 秒没有心跳（或连接断开），全员暂停等他重连。
///
/// 解除：
/// - 掉线的人重连并重新进入游戏（心跳里报告"在游戏中"）后，自动解除对他的等待；
/// - 服务端主机按 F9 或终端 resume 命令：强制解除全部暂停，不再等掉线的人
///   （这些人记为"已放弃等待"，他们回来之前不会再触发暂停）。
///
/// 只在大厅阶段为"游戏中"时检测超时；状态有变化才广播 HNC_PAUSE 给所有已加入的客户端和服务端游戏。
/// 线程：LiteNetLib 轮询线程（心跳、断线、Tick）、Pipe 读线程（主机请求）、命令行线程，都在 _lock 里改。
/// </summary>
internal static class PauseManager
{
    private static readonly object _lock = new();

    private static bool _manual;

    /// <summary>正在等待重连的玩家：名字 → PlayerId</summary>
    private static readonly Dictionary<string, int> _waiting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>主机已强制继续、不再等待的玩家（回到游戏后移出）</summary>
    private static readonly HashSet<string> _abandoned = new(StringComparer.OrdinalIgnoreCase);

    private static string _lastBroadcast = PauseProtocol.EncodeState(false, "");

    /// <summary>每次 start 开放大厅时清空</summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _manual = false;
            _waiting.Clear();
            _abandoned.Clear();
            _lastBroadcast = PauseProtocol.EncodeState(false, "");
        }
    }

    public static bool IsPaused
    {
        get { lock (_lock) return _manual || _waiting.Count > 0; }
    }

    // ═══════════════════════════════════════════════ 心跳 / 断线（LiteNetLib 线程）

    /// <summary>PING:{playerId}:{0|1}：回 PONG，记录心跳时间和"是否在游戏中"</summary>
    public static void OnPing(NetPeer peer, string msg)
    {
        NetworkManager.SendTo(peer, PauseProtocol.Pong);

        var client = NetworkManager.GetClient(peer.Id);
        if (client == null) return;

        var p = msg.Split(':');
        bool inGame = p.Length >= 3 && p[2] == "1";
        client.LastPing = DateTime.UtcNow;
        client.InGame = inGame && client.Joined;
        if (!client.InGame) return;

        lock (_lock)
        {
            bool changed = _waiting.Remove(client.PlayerName);
            if (_abandoned.Remove(client.PlayerName))
                Console.WriteLine($"[暂停] {client.PlayerName} 已回到游戏");
            if (changed)
            {
                Console.WriteLine($"[暂停] {client.PlayerName}(P{client.PlayerId}) 已重连回游戏");
                BroadcastIfChanged();
            }
        }
    }

    /// <summary>连接断开（在 NetworkManager.RemoveClient 之前调用）</summary>
    public static void OnPeerDisconnected(int peerId)
    {
        var client = NetworkManager.GetClient(peerId);
        if (client == null || !client.InGame) return;
        StartWaiting(client, "连接断开");
    }

    /// <summary>每秒一次（LiteNetLib 轮询线程）：检查游戏中客户端的心跳是否超时</summary>
    public static void Tick()
    {
        if (LobbyManager.Phase != LobbyPhase.InGame) return;
        var now = DateTime.UtcNow;
        foreach (var c in NetworkManager.GetAllClients())
        {
            if (!c.Joined || !c.InGame) continue;
            if ((now - c.LastPing).TotalSeconds > PauseProtocol.TimeoutSeconds)
                StartWaiting(c, $"超过 {PauseProtocol.TimeoutSeconds:0} 秒没有心跳");
        }
    }

    private static void StartWaiting(ClientInfo c, string why)
    {
        if (LobbyManager.Phase != LobbyPhase.InGame) return;
        lock (_lock)
        {
            // 心跳恢复前不再重复计（重新进游戏时 OnPing 会把 InGame 置回 true）
            c.InGame = false;
            if (_abandoned.Contains(c.PlayerName) || _waiting.ContainsKey(c.PlayerName)) return;
            _waiting[c.PlayerName] = c.PlayerId;
            Console.WriteLine($"[暂停] {c.PlayerName}(P{c.PlayerId}) 掉线（{why}），全员暂停等待重连。" +
                              "不想等可以在服务端按 F9 或输入 resume。");
            BroadcastIfChanged();
        }
    }

    // ═══════════════════════════════════════════════ 主机 / 命令

    /// <summary>Pipe 读线程：HNC_PAUSE_REQ:{1|0}</summary>
    public static void OnModRequest(string msg)
    {
        string v = msg.Substring(PauseProtocol.Request.Length).Trim();
        if (v == "1") SetManual(true);
        else ForceResume();
    }

    public static void SetManual(bool paused)
    {
        lock (_lock)
        {
            if (_manual == paused) return;
            _manual = paused;
            Console.WriteLine(paused ? "[暂停] 服务端手动暂停" : "[暂停] 取消手动暂停");
            BroadcastIfChanged();
        }
    }

    /// <summary>强制继续：解除手动暂停和所有等待（掉线的人记为已放弃等待）</summary>
    public static void ForceResume()
    {
        lock (_lock)
        {
            foreach (var name in _waiting.Keys) _abandoned.Add(name);
            if (_waiting.Count > 0)
                Console.WriteLine($"[暂停] 强制继续，不再等待: {string.Join(", ", _waiting.Keys)}");
            _waiting.Clear();
            _manual = false;
            BroadcastIfChanged();
        }
    }

    /// <summary>加入大厅的客户端补发一次当前状态（他进入游戏时会照此暂停）</summary>
    public static void SendStateTo(NetPeer peer)
    {
        string s;
        lock (_lock) s = _lastBroadcast;
        NetworkManager.SendTo(peer, s);
    }

    public static void PrintStatus()
    {
        lock (_lock)
        {
            if (!_manual && _waiting.Count == 0)
            {
                Console.WriteLine("游戏未暂停。");
            }
            else
            {
                if (_manual) Console.WriteLine("服务端手动暂停中。");
                foreach (var kv in _waiting) Console.WriteLine($"  等待 P{kv.Value} {kv.Key} 重连");
            }
            if (_abandoned.Count > 0)
                Console.WriteLine($"  已放弃等待: {string.Join(", ", _abandoned)}");
        }
    }

    // ═══════════════════════════════════════════════ 内部

    private static string Describe()
    {
        var lines = new List<string>();
        if (_manual) lines.Add("Paused by the server.");
        if (_waiting.Count > 0)
            lines.Add("Waiting for " + string.Join(", ", _waiting.Select(kv => $"P{kv.Value} {kv.Key}")) + " to reconnect...");
        return string.Join("  ", lines);
    }

    /// <summary>状态有变化才广播（必须在 _lock 内调用）</summary>
    private static void BroadcastIfChanged()
    {
        string s = PauseProtocol.EncodeState(_manual || _waiting.Count > 0, Describe());
        if (s == _lastBroadcast) return;
        _lastBroadcast = s;

        _ = Program.SendToModFireForget(s);
        foreach (var c in NetworkManager.GetAllClients())
            if (c.Joined && c.Peer != null) NetworkManager.SendTo(c.Peer, s);
    }
}
