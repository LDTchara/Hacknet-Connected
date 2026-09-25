using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

public enum ProxyState
{
    Added,          // 代理被添加（hasProxy: false→true）
    Activated,      // 代理激活（proxyActive: false→true）
    Overloading,    // 正在过载（proxyActive==true, ticks↓）
    Recovering,     // 正在恢复（proxyActive==true, ticks↑）
    Deactivated,    // 代理失效/过载完成（proxyActive: true→false）
    Removed         // 代理被移除（hasProxy: true→false）
}

[EventAutoSubscribe]
public class PlayerProxyEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public ProxyState State { get; }
    public float OverloadTicksRemaining { get; }
    public float StartingOverloadTicks { get; }

    public PlayerProxyEvent(Computer computer, OS os, ProxyState state)
    {
        Computer = computer;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        State = state;
        OverloadTicksRemaining = computer.proxyOverloadTicks;
        StartingOverloadTicks = computer.startingOverloadTicks;
    }
}

internal static class ProxyDetector
{
    private static string _lastComputerIP;
    private static bool _lastHasProxy;
    private static bool _lastProxyActive;
    private static float _lastProxyOverloadTicks;

    [FrameDetect]
    internal static void Detect(OS os)
    {
        Computer comp = os.connectedComp;
        if (comp == null)
        {
            _lastComputerIP = null;
            return;
        }

        // 切换节点 → 重置
        if (comp.ip != _lastComputerIP)
        {
            _lastHasProxy = comp.hasProxy;
            _lastProxyActive = comp.proxyActive;
            _lastProxyOverloadTicks = comp.proxyOverloadTicks;
            _lastComputerIP = comp.ip;
            return;
        }

        // --- hasProxy 变化 → Added / Removed ---
        if (comp.hasProxy != _lastHasProxy)
        {
            EventManager<PlayerProxyEvent>.InvokeAll(
                new PlayerProxyEvent(comp, os, comp.hasProxy ? ProxyState.Added : ProxyState.Removed)
            );
            _lastHasProxy = comp.hasProxy;
            _lastProxyActive = comp.proxyActive;
            _lastProxyOverloadTicks = comp.proxyOverloadTicks;
            return;
        }

        // 没有代理 → 跳过
        if (!comp.hasProxy)
            return;

        // --- proxyActive 变化 → Activated / Deactivated ---
        if (comp.proxyActive != _lastProxyActive)
        {
            EventManager<PlayerProxyEvent>.InvokeAll(
                new PlayerProxyEvent(comp, os, comp.proxyActive ? ProxyState.Activated : ProxyState.Deactivated)
            );
            _lastProxyActive = comp.proxyActive;
            _lastProxyOverloadTicks = comp.proxyOverloadTicks;
            return;
        }

        // --- proxyActive 为 true 时，检测 ticks 增减方向 ---
        if (comp.proxyActive)
        {
            float diff = comp.proxyOverloadTicks - _lastProxyOverloadTicks;
            if (Math.Abs(diff) > 0.0001f)
            {
                ProxyState state = diff > 0 ? ProxyState.Recovering : ProxyState.Overloading;
                EventManager<PlayerProxyEvent>.InvokeAll(
                    new PlayerProxyEvent(comp, os, state)
                );
            }
            _lastProxyOverloadTicks = comp.proxyOverloadTicks;
        }
    }
}
