using System;
using Hacknet;
using HNC.Lobby;
using HNC.Pause;

namespace ClientPlugin;

/// <summary>
/// 客户端这一侧的全员暂停 + 掉线处理。都在游戏主线程调用（GameUpdatePatch 后置补丁——暂停时照常执行）。
///
/// - 心跳：每 2 秒 PING:{id}:{0|1}（1 = 在游戏中），终端回 PONG。
/// - 终端下发 HNC_PAUSE：按它暂停 / 恢复本机（全员暂停）。
/// - 掉线（6 秒没收到 PONG，或连接断开）：立刻冻结本机并本地存档，后台每 5 秒重连，
///   连上后以 resume 方式重新加入（不传存档）：
///     - 服务器还在这一局（大厅阶段为 InGame）→ 原地解冻继续，进度都在内存里，不用读档；
///     - 服务器已经不在这一局（终端重开、重新 start 了）→ 退回准备大厅。
/// </summary>
internal static class ClientPauseSync
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private static DateTime _lastPing = DateTime.MinValue;
    private static DateTime _lastPong = DateTime.UtcNow;
    private static bool _wasInGame;

    /// <summary>掉线中（本机已冻结，正在重连）</summary>
    private static bool _lost;
    private static bool _resumeSent;
    private static DateTime _nextRetry = DateTime.MinValue;

    /// <summary>当前是否在游戏中（通过大厅进入、且游戏已加载）</summary>
    private static bool InGameNow => ClientLobby.InSession && HncPause.GameRunning();

    // ═══════════════════════════════════════════════ 消息（EventReceiver，最先处理）

    public static bool TryHandle(string msg)
    {
        if (msg == PauseProtocol.Pong)
        {
            _lastPong = DateTime.UtcNow;
            return true;
        }

        if (PauseProtocol.TryDecodeState(msg, out bool paused, out string text))
        {
            if (paused) HncPause.Set(HncPause.ReasonGlobal, text);
            else HncPause.Clear(HncPause.ReasonGlobal);
            return true;
        }

        // 重连中：拦下重新加入的回复（大厅界面此时没打开）
        if (_lost && _resumeSent && msg.StartsWith(LobbyProtocol.Prefix, StringComparison.Ordinal))
        {
            HandleResumeReply(msg);
            return true;
        }
        return false;
    }

    private static void HandleResumeReply(string msg)
    {
        if (msg.StartsWith(LobbyProtocol.Joined, StringComparison.Ordinal))
        {
            if (int.TryParse(msg.Substring(LobbyProtocol.Joined.Length), out int id))
                ClientNetworkManager.PlayerId = id;
        }
        else if (msg.StartsWith(LobbyProtocol.State, StringComparison.Ordinal))
        {
            var s = LobbyState.Decode(msg);
            if (s == null) return;
            if (s.Phase == LobbyPhase.InGame)
            {
                // 服务器还在这一局：原地继续（全员暂停由终端随后解除）
                _lost = false;
                _resumeSent = false;
                _lastPong = DateTime.UtcNow;
                HncPause.Clear(HncPause.ReasonNet);
                ClientNodeSync.ResendConnection();
                Console.WriteLine("[Pause] 已重连，原地继续");
            }
            else
            {
                Abandon("服务器已重新开局");
            }
        }
        else if (msg == LobbyProtocol.Closed)
        {
            Abandon("服务器还没开放大厅（终端可能重开了）");
        }
        else if (msg.StartsWith(LobbyProtocol.Full, StringComparison.Ordinal))
        {
            HncPause.Set(HncPause.ReasonNet, "Reconnected, but the server is full. Retrying...");
            _resumeSent = false;
            _nextRetry = DateTime.UtcNow + RetryInterval;
            ClientNetworkManager.Disconnect();
        }
        else if (msg == LobbyProtocol.Kicked)
        {
            Abandon("被服务端移出");
        }
    }

    // ═══════════════════════════════════════════════ 每帧

    public static void Update()
    {
        var now = DateTime.UtcNow;
        bool inGame = InGameNow;

        // 刚进入游戏：从现在开始计心跳超时
        if (inGame && !_wasInGame)
        {
            _lastPong = now;
            _lost = false;
            _resumeSent = false;
        }
        _wasInGame = inGame;

        // 心跳
        if (ClientNetworkManager.IsConnected && (now - _lastPing).TotalSeconds >= PauseProtocol.HeartbeatSeconds)
        {
            _lastPing = now;
            ClientNetworkManager.Send($"{PauseProtocol.Ping}{ClientNetworkManager.PlayerId}:{(inGame && !_lost ? 1 : 0)}");
        }

        if (!inGame) return;

        // 掉线检测
        if (!_lost)
        {
            bool disconnected = !ClientNetworkManager.IsConnected && !ClientNetworkManager.IsConnecting;
            bool timedOut = ClientNetworkManager.IsConnected &&
                            (now - _lastPong).TotalSeconds > PauseProtocol.TimeoutSeconds;
            if (disconnected || timedOut)
                OnLost(timedOut ? "心跳超时" : "连接断开");
            return;
        }

        // 重连
        if (!ClientNetworkManager.IsConnected)
        {
            if (!ClientNetworkManager.IsConnecting && now >= _nextRetry)
            {
                _nextRetry = now + RetryInterval;
                var server = ClientLobby.Server;
                var account = ClientLobby.Account;
                if (server != null && account != null)
                {
                    Console.WriteLine("[Pause] 尝试重连...");
                    ClientNetworkManager.Connect(server.IP, server.Port, account.Name);
                }
            }
        }
        else if (!_resumeSent && ClientLobby.Account != null)
        {
            _resumeSent = true;
            ClientNetworkManager.Send($"{LobbyProtocol.Join}{LobbyProtocol.ToB64(ClientLobby.Account.Name)}:resume");
            HncPause.Set(HncPause.ReasonNet, "Reconnected. Rejoining the game...");
        }
    }

    private static void OnLost(string why)
    {
        _lost = true;
        _resumeSent = false;
        _nextRetry = DateTime.UtcNow;
        Console.WriteLine($"[Pause] 与服务器失去联系（{why}）：冻结本机并本地存档，开始重连");
        HncPause.Set(HncPause.ReasonNet, "Lost connection to the server. Your progress is saved. Reconnecting...");

        try { OS.currentInstance?.saveGame(); }
        catch (Exception ex) { Console.WriteLine($"[Pause] 本地存档失败: {ex.Message}"); }

        // 连接对象还在但心跳不通：断开重来
        if (ClientNetworkManager.IsConnected) ClientNetworkManager.Disconnect();
    }

    /// <summary>重连后发现回不到这一局：退回准备大厅</summary>
    private static void Abandon(string why)
    {
        Console.WriteLine($"[Pause] 无法原地继续（{why}），退回准备大厅");
        _lost = false;
        _resumeSent = false;
        HncPause.ClearAll();
        ClientLobby.ReturnToLobby();
    }
}
