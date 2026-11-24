using System;
using System.Diagnostics;
using Hacknet;
using HNC.Sync;
using HNC.Sync.Adapters;

namespace ClientPlugin;

/// <summary>HNC.Sync 的客户端传输：收发都走已有的 LiteNetLib 连接。</summary>
internal sealed class ClientSyncTransport : ISyncTransport
{
    public bool IsServer => false;

    // 由 WELCOME 设置；未连接时为 "0"
    public string LocalPlayerId => ClientNetworkManager.PlayerId.ToString();

    // 客户端发出的消息恒送服务端，toPlayerId 无意义
    public void Send(string message, string? toPlayerId) => ClientNetworkManager.Send(message);

    public event Action<string, string>? MessageReceived;
    internal void Raise(string message, string fromPlayerId) => MessageReceived?.Invoke(message, fromPlayerId);
}

/// <summary>
/// 客户端同步宿主：创建引擎、注入 HncSync.Runtime、驱动 Tick、定期扫描并注册原版 Daemon。
/// 引擎在插件 Load 时就创建，所以服务端在世界加载完成前发来的消息（全量/增量）会被引擎暂存，
/// 等对应对象注册时重放。所有方法都在游戏主线程调用。
/// </summary>
internal static class ClientSync
{
    private const long TickIntervalMs = 100;
    private const long ScanIntervalMs = 3000;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static ClientSyncTransport? _transport;
    private static SyncEngine? _engine;
    private static long _lastTick;
    private static long _lastScan;

    public static void Init()
    {
        if (_engine != null) return;
        _transport = new ClientSyncTransport();
        _engine = new SyncEngine(_transport) { Log = m => Console.WriteLine(m) };
        HncSync.Runtime = _engine;
        DaemonSync.Install();
        Console.WriteLine("[ClientSync] 同步引擎已创建");
    }

    /// <summary>EventReceiver 收到 SYNC: 消息时调用。</summary>
    public static void OnMessage(string msg) => _transport?.Raise(msg, "server");

    /// <summary>每帧调用（Game1.Update 之后，已 PollEvents）。</summary>
    public static void Update()
    {
        if (_engine == null || !ClientNetworkManager.IsConnected) return;

        long now = Clock.ElapsedMilliseconds;
        if (now - _lastTick >= TickIntervalMs)
        {
            _lastTick = now;
            try { _engine.Tick(); }
            catch (Exception ex) { Console.WriteLine($"[ClientSync] Tick 异常: {ex.Message}"); }
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
                    if (n > 0) Console.WriteLine($"[ClientSync] 新注册 {n} 个 Daemon");
                }
                catch (Exception ex) { Console.WriteLine($"[ClientSync] 扫描 Daemon 异常: {ex.Message}"); }
            }
        }
    }
}
