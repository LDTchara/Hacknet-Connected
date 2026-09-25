using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

public enum FirewallState
{
    Added,             // 防火墙被添加/激活（firewall: null→非null）
    Removed,           // 防火墙被移除/禁用（firewall: 非null→null）
    AnalysisStarted,   // 分析开始（firewallAnalysisInProgress: false→true）
    AnalysisCompleted, // 分析完成（firewallAnalysisInProgress: true→false）
    Solved             // 破解成功（firewall.solved: false→true）
}

[EventAutoSubscribe]
public class PlayerFirewallEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public FirewallState State { get; }
    public Firewall Firewall { get; }

    public PlayerFirewallEvent(Computer computer, OS os, FirewallState state)
    {
        Computer = computer;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        State = state;
        Firewall = computer.firewall;
    }
}

internal static class FirewallDetector
{
    private static string _lastComputerIP;
    private static bool _lastHasFirewall;
    private static bool _lastAnalysisInProgress;
    private static bool _lastSolved;

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
            _lastHasFirewall = comp.firewall != null;
            _lastAnalysisInProgress = false;
            _lastSolved = false;
            _lastComputerIP = comp.ip;
            return;
        }

        bool hasFw = comp.firewall != null;

        // Added / Removed
        if (hasFw != _lastHasFirewall)
        {
            EventManager<PlayerFirewallEvent>.InvokeAll(
                new PlayerFirewallEvent(comp, os, hasFw ? FirewallState.Added : FirewallState.Removed)
            );
            _lastHasFirewall = hasFw;
            _lastAnalysisInProgress = false;
            _lastSolved = false;
            return;
        }

        // 没有防火墙则跳过后续检测
        if (!hasFw)
            return;

        // AnalysisStarted / AnalysisCompleted
        if (comp.firewallAnalysisInProgress != _lastAnalysisInProgress)
        {
            var state = comp.firewallAnalysisInProgress
                ? FirewallState.AnalysisStarted
                : FirewallState.AnalysisCompleted;
            EventManager<PlayerFirewallEvent>.InvokeAll(
                new PlayerFirewallEvent(comp, os, state)
            );
            _lastAnalysisInProgress = comp.firewallAnalysisInProgress;
        }

        // Solved
        if (comp.firewall.solved && !_lastSolved)
        {
            EventManager<PlayerFirewallEvent>.InvokeAll(
                new PlayerFirewallEvent(comp, os, FirewallState.Solved)
            );
            _lastSolved = true;
        }
    }
}
