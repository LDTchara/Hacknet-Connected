using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hacknet;
using HNC.NodeSync;
using HNC.Pause;
using Pathfinder.Command;

namespace ClientPlugin;

/// <summary>
/// 客户端这一侧的节点同步。都在游戏主线程调用（GameUpdatePatch 后置补丁、EventReceiver、命令）。
///
/// 共享节点（扩展节点 XML 带 &lt;HNCShared/&gt;）：
/// - 上报本机进展：新打开的端口、防火墙分析次数和破解、Shell 对代理的过载量（每 0.2 秒累计一次）；
/// - 收到服务端的权威状态（NODE_STATE）就写到本机电脑上；
/// - 本机出现"倒退"（本机管理员重置代理、关端口、重置防火墙进度）时恢复回去——共享节点只认服务端的重置。
/// 非共享节点：完全不同步，每人自己破解；可以用 share 命令把自己的破解状态分享给别人。
/// </summary>
internal static class ClientNodeSync
{
    /// <summary>上一次写到 / 看到的本机状态（用来判断本机发生了什么变化）</summary>
    private sealed class Track
    {
        public string Ports = "";
        public int Passes = -1;
        public bool Solved;
        public float Ticks;
        public bool ProxyActive;
        public float PendingDelta;
    }

    private static readonly Dictionary<string, Track> _tracks = new(StringComparer.Ordinal);

    /// <summary>收到的最新权威状态（游戏还没加载时先存着，进游戏后再应用）</summary>
    private static readonly Dictionary<string, NodeState.Decoded> _latest = new(StringComparer.Ordinal);

    private static DateTime _nextFlush = DateTime.MinValue;

    /// <summary>上次报告的"我连着的共享节点"（null = 没连；Unknown = 还没报过，下一帧必报）</summary>
    private const string Unknown = "\0";
    private static string? _reportedConn = Unknown;
    private const double FlushSeconds = 0.2;

    private static bool InGame => ClientLobby.InSession && HncPause.GameRunning();

    /// <summary>进入大厅时清空（终端会在加入时让服务端重发状态）</summary>
    public static void Reset()
    {
        _tracks.Clear();
        _latest.Clear();
        SharedNodeConfig.Invalidate();
        _reportedConn = Unknown;
    }

    /// <summary>掉线后原地重连成功：重新报告连接状态（服务端在断线时清掉过）</summary>
    public static void ResendConnection() => _reportedConn = Unknown;

    // ═══════════════════════════════════════════════ 消息（EventReceiver）

    public static bool TryHandle(string msg)
    {
        if (!msg.StartsWith(NodeProtocol.Prefix, StringComparison.Ordinal)) return false;

        if (msg.StartsWith(NodeProtocol.State, StringComparison.Ordinal))
        {
            var d = NodeState.Decode(msg);
            if (d == null) return true;
            _latest[d.Id] = d;
            if (InGame) ApplyLatest(d.Id);
        }
        else if (msg.StartsWith(NodeProtocol.Shared, StringComparison.Ordinal))
        {
            ReceiveShare(msg.Substring(NodeProtocol.Shared.Length));
        }
        else if (msg.StartsWith(NodeProtocol.Msg, StringComparison.Ordinal))
        {
            Write(NodeProtocol.FromB64(msg.Substring(NodeProtocol.Msg.Length)));
        }
        return true;
    }

    // ═══════════════════════════════════════════════ 每帧

    public static void Update()
    {
        if (!InGame) return;
        var cfg = SharedNodeConfig.Get();
        if (cfg.Count == 0) return;
        var os = OS.currentInstance!;

        // 连接状态：服务端据此判断"共享节点上还有没有人"，有人连着就不让管理员重置
        string? conn = os.connectedComp != null && cfg.ContainsKey(os.connectedComp.idName) ? os.connectedComp.idName : null;
        if (conn != _reportedConn && ClientNetworkManager.IsConnected)
        {
            _reportedConn = conn;
            ClientNetworkManager.Send($"{NodeProtocol.Report}{NodeProtocol.ToB64(conn ?? "")}:C:1");
        }

        foreach (var kv in cfg)
        {
            var c = NodeState.Find(os, kv.Key);
            if (c == null) continue;

            if (!_tracks.TryGetValue(kv.Key, out var t))
            {
                // 第一次看到：先应用已收到的权威状态，再以本机现状为基线
                if (_latest.ContainsKey(kv.Key)) NodeState.Apply(c, kv.Value, _latest[kv.Key]);
                _tracks[kv.Key] = Snapshot(c);
                continue;
            }
            Detect(kv.Key, kv.Value, c, t);
        }

        var now = DateTime.UtcNow;
        if (now >= _nextFlush)
        {
            _nextFlush = now.AddSeconds(FlushSeconds);
            foreach (var kv in _tracks)
            {
                if (kv.Value.PendingDelta <= 0f) continue;
                Report(kv.Key, "X", kv.Value.PendingDelta.ToString("R", CultureInfo.InvariantCulture));
                kv.Value.PendingDelta = 0f;
            }
        }
    }

    /// <summary>比较本机现状和上一次的状态：进展就上报，倒退就恢复</summary>
    private static void Detect(string id, SharedFlags flags, Computer c, Track t)
    {
        // ── 端口：新打开的上报；本机关掉的恢复（关端口只认服务端） ──
        if (flags.Ports && c.portsOpen != null && c.ports != null)
        {
            var sb = t.Ports.ToCharArray();
            for (int i = 0; i < c.portsOpen.Count && i < sb.Length; i++)
            {
                bool open = c.portsOpen[i] != 0;
                if (open && sb[i] == '0')
                {
                    Report(id, "P", c.ports[i].ToString(CultureInfo.InvariantCulture));
                    sb[i] = '1';
                }
                else if (!open && sb[i] == '1')
                {
                    c.portsOpen[i] = 1;
                }
            }
            t.Ports = new string(sb);
        }

        // ── 防火墙：分析次数增加 / 破解上报；被重置的恢复 ──
        if (flags.Firewall && c.firewall != null)
        {
            int passes = NodeState.GetPasses(c.firewall);
            if (passes > t.Passes)
            {
                Report(id, "F", (passes - t.Passes).ToString(CultureInfo.InvariantCulture));
                t.Passes = passes;
            }
            else if (passes < t.Passes)
            {
                NodeState.SetPasses(c.firewall, t.Passes);
            }

            if (c.firewall.solved && !t.Solved)
            {
                Report(id, "S", "1");
                t.Solved = true;
            }
            else if (!c.firewall.solved && t.Solved)
            {
                c.firewall.solved = true;
            }
        }

        // ── 代理：本机 Shell 的过载量累计上报；被重置（变多）的恢复 ──
        if (flags.Proxy && c.hasProxy)
        {
            float cur = c.proxyOverloadTicks;
            if (cur < t.Ticks)
            {
                t.PendingDelta += t.Ticks - cur;
                t.Ticks = cur;
            }
            else if (cur > t.Ticks)
            {
                c.proxyOverloadTicks = t.Ticks;
            }

            if (!c.proxyActive && t.ProxyActive) t.ProxyActive = false;      // 本机打掉了（服务端随后跟上）
            else if (c.proxyActive && !t.ProxyActive) c.proxyActive = false; // 本机重新开启：只认服务端
        }
    }

    private static void ApplyLatest(string id)
    {
        var os = OS.currentInstance;
        if (os == null || !_latest.TryGetValue(id, out var d)) return;
        if (!SharedNodeConfig.Get().TryGetValue(id, out var flags)) return;
        var c = NodeState.Find(os, id);
        if (c == null) return;

        NodeState.Apply(c, flags, d);
        // 权威状态写进来后，以它为新基线（不会被当成本机变化再报回去）
        var fresh = Snapshot(c);
        if (_tracks.TryGetValue(id, out var old)) fresh.PendingDelta = old.PendingDelta;
        _tracks[id] = fresh;
    }

    private static Track Snapshot(Computer c) => new()
    {
        Ports = NodeState.PortsString(c),
        Passes = c.firewall != null ? NodeState.GetPasses(c.firewall) : -1,
        Solved = c.firewall?.solved == true,
        Ticks = c.proxyOverloadTicks,
        ProxyActive = c.proxyActive
    };

    private static void Report(string id, string kind, string value) =>
        ClientNetworkManager.Send($"{NodeProtocol.Report}{NodeProtocol.ToB64(id)}:{kind}:{value}");

    // ═══════════════════════════════════════════════ share 命令

    public static void RegisterCommand() => CommandManager.RegisterCommand("share", HandleShare);

    /// <summary>share &lt;IP|节点id&gt; [玩家名|P编号|all] [ports|proxy|firewall|all]（服务端主机 = 0 号 / Server）</summary>
    private static void HandleShare(OS os, string[] args)
    {
        if (args.Length < 2)
        {
            os.write(NodeShare.Usage);
            os.write("  把你在这个节点上已经破解的端口 / 代理 / 防火墙分享给别人（默认：所有人、全部）。服务端主机是 P0 / Server。");
            return;
        }
        if (!ClientNetworkManager.IsConnected)
        {
            os.write("[分享] 没有连接服务器。");
            return;
        }

        var c = NodeShare.FindDiscovered(os, args[1]);
        if (c == null)
        {
            os.write($"[分享] 找不到你已发现的节点 {args[1]}。");
            return;
        }

        string target = args.Length > 2 ? args[2] : "all";
        string? payload = NodeShare.BuildPayload(c, args.Length > 3 ? args[3] : "all", out string reason);
        if (payload == null)
        {
            os.write(reason);
            return;
        }
        ClientNetworkManager.Send($"{NodeProtocol.Share}{NodeProtocol.ToB64(target)}:{payload}");
    }

    /// <summary>NODE_SHARED（去掉前缀）——在本机应用</summary>
    private static void ReceiveShare(string payload)
    {
        var os = OS.currentInstance;
        if (os == null || !HncPause.GameRunning()) return;
        NodeShare.Receive(os, payload);
    }

    private static void Write(string text)
    {
        var os = OS.currentInstance;
        foreach (var l in text.Split('\n'))
        {
            if (os != null && HncPause.GameRunning()) os.write(l);
            else Console.WriteLine("[NodeSync] " + l);
        }
    }
}
