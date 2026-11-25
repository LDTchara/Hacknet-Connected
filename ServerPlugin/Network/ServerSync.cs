using System;
using System.Diagnostics;
using Hacknet;
using HNC.Sync;
using HNC.Sync.Adapters;

namespace ServerPlugin;

/// <summary>
/// HNC.Sync 的服务端传输：消息经 Pipe 发给终端，由终端按头部路由给客户端。
/// 不能用 ServerManager.SendEvent——它对"同类型消息 2 秒内只发一次"做了折叠，会吞掉连续的 SYNC。
/// </summary>
internal sealed class ServerSyncTransport : ISyncTransport
{
    public bool IsServer => true;
    public string LocalPlayerId => "server";

    // toPlayerId 已经写在消息头里（SYNC:OP:scope:{playerId|*}:...），终端据此路由
    public void Send(string message, string? toPlayerId) => ServerManager.SendToConsole(message);

    public event Action<string, string>? MessageReceived;
    internal void Raise(string message, string fromPlayerId) => MessageReceived?.Invoke(message, fromPlayerId);
}

/// <summary>
/// 服务端同步宿主：创建引擎、注入 HncSync.Runtime、驱动 Tick、定期扫描并注册原版 Daemon。
/// 所有方法都在游戏主线程调用（Game1.Update 补丁 / ProcessPendingMessages）。
/// </summary>
internal static class ServerSync
{
    private const long TickIntervalMs = 100;
    private const long ScanIntervalMs = 3000;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static ServerSyncTransport? _transport;
    private static SyncEngine? _engine;
    private static long _lastTick;
    private static long _lastScan;

    /// <summary>Pipe 连接成功后由后台线程置位，真正的启动在主线程的 Update 里做。</summary>
    private static volatile bool _startRequested;

    public static bool IsRunning => _engine != null;

    public static void RequestStart() => _startRequested = true;

    public static void Stop()
    {
        _startRequested = false;
        if (_engine == null) return;
        HncSync.Runtime = null;
        _engine = null;
        _transport = null;
        Console.WriteLine("[ServerSync] 同步引擎已停止");
    }

    /// <summary>每帧调用。</summary>
    public static void Update()
    {
        if (_engine == null)
        {
            if (!_startRequested) return;
            _startRequested = false;
            _transport = new ServerSyncTransport();
            _engine = new SyncEngine(_transport) { Log = m => Console.WriteLine(m) };
            HncSync.Runtime = _engine;
            DaemonSync.Install();
            Console.WriteLine("[ServerSync] 同步引擎已启动");
        }

        long now = Clock.ElapsedMilliseconds;
        if (now - _lastTick >= TickIntervalMs)
        {
            _lastTick = now;
            try { _engine.Tick(); }
            catch (Exception ex) { Console.WriteLine($"[ServerSync] Tick 异常: {ex.Message}"); }
        }

        if (now - _lastScan >= ScanIntervalMs)
        {
            _lastScan = now;
            var os = OS.currentInstance;
            if (os?.netMap != null)
            {
                try
                {
                    int n = DaemonSync.RegisterAll(os);
                    if (n > 0) Console.WriteLine($"[ServerSync] 新注册 {n} 个 Daemon");
                }
                catch (Exception ex) { Console.WriteLine($"[ServerSync] 扫描 Daemon 异常: {ex.Message}"); }
            }
        }
    }

    /// <summary>
    /// 处理终端经 Pipe 发来的同步相关消息。返回 true 表示已处理（调用方不要再按普通命令解析）。
    ///   SYNC_FROM:{playerId}:{原始 SYNC 消息}  客户端发来的 PROPOSE / REQ / MSG
    ///   SYNC_JOIN:{playerId}                    有玩家连入，发全量
    /// 必须在 ServerManager 用 ':' 拆分消息之前调用（SYNC 消息里有冒号）。
    /// </summary>
    public static bool TryHandlePipeMessage(string msg)
    {
        if (msg.StartsWith("SYNC_FROM:", StringComparison.Ordinal))
        {
            int i = msg.IndexOf(':', "SYNC_FROM:".Length);
            if (i > 0)
                _transport?.Raise(msg.Substring(i + 1), msg.Substring("SYNC_FROM:".Length, i - "SYNC_FROM:".Length));
            return true;
        }

        if (msg.StartsWith("SYNC_JOIN:", StringComparison.Ordinal))
        {
            string playerId = msg.Substring("SYNC_JOIN:".Length).Trim();
            if (_engine != null && playerId.Length > 0)
            {
                Console.WriteLine($"[ServerSync] 玩家 {playerId} 连入，发送全量状态");
                _engine.OnPlayerConnected(playerId);
            }
            return true;
        }

        return false;
    }
}
