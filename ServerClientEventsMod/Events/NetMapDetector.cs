using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

/// <summary>
/// 统一节点检测器 —— 合并 NodeIP + NodeVisibility 两类事件对 netMap 的遍历到一次 pass。
/// 后续所有遍历 netMap.nodes 的检测逻辑应加在此处，避免独立循环。
/// </summary>
internal static class NetMapDetector
{
    // ---- 可见性 ----
    private static HashSet<int> _lastVisibleSet = new();
    private static bool _visInitialized;

    // ---- IP ----
    private static Dictionary<string, string> _lastKnownIPs = new();
    
    /// <summary>每个节点第一次被观察到时的 IP（用于判断 bounce-back）</summary>
    private static Dictionary<string, string> _firstSeenIPs = new();

    [FrameDetect]
    internal static void Detect(OS os)
    {
        if (os.netMap == null) return;
        var nodes = os.netMap.nodes;
        var visible = os.netMap.visibleNodes;

        // ── 一次 netMap.nodes 遍历 ──
        // IP 检测 + 后续全局节点检测的逻辑都放在这个循环里
        foreach (var comp in nodes)
        {
            if (comp?.idName == null) continue;

            // IP 变更（带 bounce-back 去重）
            if (!_lastKnownIPs.TryGetValue(comp.idName, out var lastIP))
            {
                // 首次发现：记录初态和末态
                _lastKnownIPs[comp.idName] = comp.ip;
                _firstSeenIPs[comp.idName] = comp.ip;
                continue;
            }
            if (comp.ip != lastIP)
            {
                // 检查是否是 bounce-back（跳回最初的值）→ 静默跳过
                if (_firstSeenIPs.TryGetValue(comp.idName, out var firstIP) && comp.ip == firstIP)
                {
                    _lastKnownIPs[comp.idName] = comp.ip;
                    continue;
                }

                EventManager<PlayerNodeIPChangedEvent>.InvokeAll(
                    new PlayerNodeIPChangedEvent(comp, os, lastIP, comp.ip));
                _lastKnownIPs[comp.idName] = comp.ip;
                _firstSeenIPs[comp.idName] = comp.ip;
            }

            // 后续全局节点检测（actions/missions 等）追加在此
        }

        // 清理 IP 缓存中的死节点（同时清理 firstSeen 缓存）
        if (_lastKnownIPs.Count != nodes.Count)
        {
            var stale = _lastKnownIPs.Keys
                .Where(k => !nodes.Any(c => c?.idName == k))
                .ToList();
            foreach (var k in stale)
            {
                _lastKnownIPs.Remove(k);
                _firstSeenIPs.Remove(k);
            }
        }

        // ── visibleNodes 对比 ──
        if (!_visInitialized)
        {
            _lastVisibleSet = new HashSet<int>(visible);
            _visInitialized = true;
            return;
        }

        var currentSet = new HashSet<int>(visible);
        foreach (int idx in currentSet)
        {
            if (!_lastVisibleSet.Contains(idx) && idx >= 0 && idx < nodes.Count)
                EventManager<PlayerNodeVisibilityEvent>.InvokeAll(
                    new PlayerNodeVisibilityEvent(nodes[idx], os, true));
        }
        foreach (int idx in _lastVisibleSet)
        {
            if (!currentSet.Contains(idx) && idx >= 0 && idx < nodes.Count)
                EventManager<PlayerNodeVisibilityEvent>.InvokeAll(
                    new PlayerNodeVisibilityEvent(nodes[idx], os, false));
        }
        _lastVisibleSet = currentSet;
    }
}
