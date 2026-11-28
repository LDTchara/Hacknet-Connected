using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Hacknet;
using HNC.NodeSync;
using HNC.Pause;
using Pathfinder.Command;

namespace ServerPlugin;

/// <summary>
/// 共享节点的权威（服务端游戏）。服务端自己那台电脑上的状态就是全局状态：
/// - 客户端的上报（NODE_RPT）直接作用到服务端的电脑上：打开端口、分析次数累加、防火墙破解、代理过载量相减（各人叠加）；
/// - 服务端主机自己的操作、服务端的管理员重置，本来就改的是这台电脑；
/// - 每 0.2 秒检查一次，共享节点状态有变化就广播 NODE_STATE；有玩家加入（SYNC_JOIN）时全部重发。
/// 都在游戏主线程调用（ProcessPendingMessages、GameUpdatePatch 后置补丁）。
/// </summary>
internal static class ServerNodeSync
{
    private static readonly Dictionary<string, string> _lastSent = new(StringComparer.Ordinal);
    private static DateTime _next = DateTime.MinValue;

    private const double IntervalSeconds = 0.2;

    /// <summary>每个客户端当前连着的共享节点（PlayerId → 节点 id；一个玩家同时只连一台）</summary>
    private static readonly Dictionary<int, string> _clientConn = new();

    /// <summary>我们自己调用管理员重置时为 true（补丁放行）</summary>
    private static bool _allowReset;

    private static readonly FieldInfo? AdminField = AccessTools.Field(typeof(Computer), "admin");

    /// <summary>NODE_RPT：客户端上报；NODE_SHARED：别人分享给主机；NODE_MSG：终端对主机 share 的回复</summary>
    public static bool TryHandlePipeMessage(string msg)
    {
        if (msg.StartsWith(NodeProtocol.Shared, StringComparison.Ordinal))
        {
            var o = OS.currentInstance;
            if (o != null && HncPause.GameRunning())
                NodeShare.Receive(o, msg.Substring(NodeProtocol.Shared.Length));
            return true;
        }
        if (msg.StartsWith(NodeProtocol.Msg, StringComparison.Ordinal))
        {
            string text = NodeProtocol.FromB64(msg.Substring(NodeProtocol.Msg.Length));
            var o = OS.currentInstance;
            foreach (var l in text.Split('\n'))
            {
                if (o != null && HncPause.GameRunning()) o.write(l);
                else Console.WriteLine("[NodeSync] " + l);
            }
            return true;
        }
        if (msg.StartsWith(NodeProtocol.Gone, StringComparison.Ordinal))
        {
            if (int.TryParse(msg.Substring(NodeProtocol.Gone.Length), out int gonePid))
                SetClientConnection(gonePid, null);
            return true;
        }
        if (!msg.StartsWith(NodeProtocol.Report, StringComparison.Ordinal)) return false;

        var p = msg.Substring(NodeProtocol.Report.Length).Split(':');
        var os = OS.currentInstance;
        if (p.Length < 3 || os == null || !HncPause.GameRunning()) return true;

        string id = NodeProtocol.FromB64(p[0]);

        // 连接状态：NODE_RPT:{b64 节点id 或空}:C:1:{pid}——客户端现在连着哪台共享节点（空 = 没连共享节点）
        if (p[1] == "C")
        {
            if (p.Length > 3 && int.TryParse(p[3], out int connPid))
                SetClientConnection(connPid, string.IsNullOrEmpty(id) ? null : id);
            return true;
        }

        if (!SharedNodeConfig.Get().TryGetValue(id, out var flags)) return true;
        var c = NodeState.Find(os, id);
        if (c == null) return true;

        switch (p[1])
        {
            case "P":
                if (flags.Ports && int.TryParse(p[2], out int code) && c.ports != null)
                {
                    int idx = c.ports.IndexOf(code);
                    if (idx >= 0 && idx < c.portsOpen.Count) c.portsOpen[idx] = 1;
                }
                break;

            case "F":
                if (flags.Firewall && c.firewall != null && int.TryParse(p[2], out int n) && n > 0)
                    NodeState.SetPasses(c.firewall, NodeState.GetPasses(c.firewall) + n);
                break;

            case "S":
                if (flags.Firewall && c.firewall != null)
                {
                    c.firewall.solved = true;
                    c.firewallAnalysisInProgress = false;
                }
                break;

            case "X":
                if (flags.Proxy && c.hasProxy && c.proxyActive &&
                    float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float delta) && delta > 0)
                {
                    c.proxyOverloadTicks -= delta;
                    if (c.proxyOverloadTicks <= 0f)
                    {
                        c.proxyOverloadTicks = 0f;
                        c.proxyActive = false;
                    }
                }
                break;
        }
        return true;
    }

    /// <summary>观察管道消息（不消费）：有玩家加入时把所有共享节点的状态重发一遍</summary>
    public static void Observe(string msg)
    {
        if (msg.StartsWith("SYNC_JOIN:", StringComparison.Ordinal))
            _lastSent.Clear();
    }

    /// <summary>每帧：节流检查共享节点状态，有变化就广播</summary>
    public static void Update()
    {
        if (!HncPause.GameRunning() || !ServerManager.IsConnected) return;
        var now = DateTime.UtcNow;
        if (now < _next) return;
        _next = now.AddSeconds(IntervalSeconds);

        var cfg = SharedNodeConfig.Get();
        if (cfg.Count == 0) return;
        var os = OS.currentInstance!;

        foreach (var kv in cfg)
        {
            var c = NodeState.Find(os, kv.Key);
            if (c == null) continue;
            string state = NodeState.Encode(kv.Key, c);
            if (_lastSent.TryGetValue(kv.Key, out var last) && last == state) continue;
            _lastSent[kv.Key] = state;
            ServerManager.SendToConsole(state);
        }
    }

    // ═══════════════════════════════════════════════ 有人连着就不重置

    /// <summary>
    /// 共享节点上还有玩家连着吗（主机：服务端的 connectedComp；客户端：上报的连接状态）。
    /// 只要有人连着，管理员就不重置——各人的破解进度取并集，作为权威。
    /// </summary>
    public static bool AnyoneConnected(Computer c)
    {
        var os = OS.currentInstance;
        if (os?.connectedComp == c) return true;
        return _clientConn.Values.Any(v => v == c.idName);
    }

    /// <summary>管理员重置补丁调用：共享节点上还有人连着就跳过重置</summary>
    internal static bool AllowAdminReset(Computer c)
    {
        if (_allowReset || c == null || !HncPause.GameRunning()) return true;
        if (!SharedNodeConfig.Get().ContainsKey(c.idName)) return true;
        if (!AnyoneConnected(c)) return true;
        Console.WriteLine($"[NodeSync] {c.idName}: 还有玩家连着，跳过管理员重置");
        return false;
    }

    private static void SetClientConnection(int pid, string? nodeId)
    {
        _clientConn.TryGetValue(pid, out var prev);
        if (nodeId == null) _clientConn.Remove(pid);
        else _clientConn[pid] = nodeId;
        if (prev != null && prev != nodeId) ResetIfEmpty(prev);
    }

    /// <summary>最后一个人离开共享节点：按单机规则触发一次管理员重置（服务端执行，随后同步给所有人）</summary>
    private static void ResetIfEmpty(string id)
    {
        var os = OS.currentInstance;
        if (os == null || !HncPause.GameRunning() || !SharedNodeConfig.Get().ContainsKey(id)) return;
        var c = NodeState.Find(os, id);
        if (c == null || AnyoneConnected(c)) return;

        var admin = AdminField?.GetValue(c);
        var method = admin?.GetType().GetMethod("disconnectionDetected", BindingFlags.Public | BindingFlags.Instance);
        if (method == null) return;

        _allowReset = true;
        try
        {
            method.Invoke(admin, new object[] { c, os });
            Console.WriteLine($"[NodeSync] {id}: 所有玩家都已离开，执行管理员重置");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NodeSync] {id}: 管理员重置失败: {ex.Message}");
        }
        finally
        {
            _allowReset = false;
        }
    }

    // ═══════════════════════════════════════════════ share 命令（服务端主机）

    public static void RegisterCommand() => CommandManager.RegisterCommand("share", HandleShare);

    /// <summary>share &lt;IP|节点id&gt; [玩家名|P编号|all] [ports|proxy|firewall|all]——主机把自己的破解进度分享给客户端</summary>
    private static void HandleShare(OS os, string[] args)
    {
        if (args.Length < 2)
        {
            os.write(NodeShare.Usage);
            os.write("  把你在这个节点上已经破解的端口 / 代理 / 防火墙分享给客户端玩家（默认：所有人、全部）。");
            return;
        }
        if (!ServerManager.IsConnected)
        {
            os.write("[分享] 没有连接终端。");
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
        ServerManager.SendToConsole($"{NodeProtocol.Share}{NodeProtocol.ToB64(target)}:{payload}");
    }

    /// <summary>开局 / 退出扩展页时清空</summary>
    public static void Reset()
    {
        _lastSent.Clear();
        _next = DateTime.MinValue;
        SharedNodeConfig.Invalidate();
        _clientConn.Clear();
    }
}

/// <summary>
/// 共享节点：只要还有玩家连着，管理员（BasicAdministrator / FastBasicAdministrator / FastProgressOnlyAdministrator …）
/// 在主机断开时不重置端口、防火墙、代理。Administrator 是 Hacknet 的 internal 类，按名字找所有子类的 disconnectionDetected。
/// 只打在服务端（权威）；客户端本机的重置由 ClientNodeSync 恢复。
/// </summary>
[HarmonyPatch]
internal static class SharedNodeAdminPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baseType = AccessTools.TypeByName("Hacknet.Administrator");
        if (baseType == null) yield break;
        foreach (var t in baseType.Assembly.GetTypes())
        {
            if (t.IsAbstract || !baseType.IsAssignableFrom(t)) continue;
            var m = AccessTools.DeclaredMethod(t, "disconnectionDetected");
            if (m != null) yield return m;
        }
    }

    private static bool Prefix(Computer c) => ServerNodeSync.AllowAdminReset(c);
}
