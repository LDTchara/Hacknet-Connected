using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

public enum ModuleAction
{
    Added,
    Removed,
    VisibilityChanged,
    NameChanged
}

[EventAutoSubscribe]
public class PlayerModuleEvent : PathfinderEvent
{
    /// <summary>模块当前名称</summary>
    public string Name { get; }
    /// <summary>是否可见</summary>
    public bool Visible { get; }
    /// <summary>操作类型</summary>
    public ModuleAction Action { get; }
    /// <summary>变更前的旧名称（仅 NameChanged）</summary>
    public string OldName { get; }
    /// <summary>玩家 OS 实例</summary>
    public OS Os { get; }
    public string CurrentPlayerIP { get; }

    public PlayerModuleEvent(string name, bool visible, ModuleAction action,
        OS os, string oldName = null)
    {
        Name = name;
        Visible = visible;
        Action = action;
        OldName = oldName;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
    }
}

internal static class ModuleDetector
{
    // 用模块 name 跟踪（因 Module 为 internal，不能用引用）
    private static List<string> _lastModuleNames = new();
    private static Dictionary<string, bool> _lastModuleVisibility = new();

    [FrameDetect]
    internal static void Detect(OS os)
    {
        var currentNames = new List<string>();
        var currentVisibility = new Dictionary<string, bool>();

        foreach (var m in os.modules)
        {
            currentNames.Add(m.name);
            currentVisibility[m.name] = m.visible;
        }

        // ── Added / Removed ──
        foreach (var name in currentNames)
        {
            if (!_lastModuleNames.Contains(name))
                Emit(name, currentVisibility[name], ModuleAction.Added, os);
        }
        foreach (var name in _lastModuleNames)
        {
            if (!currentNames.Contains(name))
                Emit(name, false, ModuleAction.Removed, os);
        }

        // ── Visibility / Name 变化 ──
        foreach (var name in currentNames)
        {
            bool curVis = currentVisibility[name];
            if (_lastModuleVisibility.TryGetValue(name, out bool lastVis))
            {
                if (curVis != lastVis)
                    Emit(name, curVis, ModuleAction.VisibilityChanged, os);
            }
        }

        _lastModuleNames = currentNames;
        _lastModuleVisibility = currentVisibility;
    }

    static void Emit(string name, bool visible, ModuleAction action, OS os, string oldName = null)
    {
        EventManager<PlayerModuleEvent>.InvokeAll(
            new PlayerModuleEvent(name, visible, action, os, oldName));
    }
}
