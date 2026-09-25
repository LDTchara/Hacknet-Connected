using Hacknet;
using HarmonyLib;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

public enum FlagAction
{
    Added,
    Removed
}

[EventAutoSubscribe]
public class PlayerFlagEvent : PathfinderEvent
{
    public string Flag { get; }
    public FlagAction Action { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }

    public PlayerFlagEvent(string flag, FlagAction action, OS os)
    {
        Flag = flag;
        Action = action;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
    }
}

internal static class FlagInterceptor
{
    private static HashSet<string> _lastFlags = null!;

    [FrameDetect]
    internal static void Detect(OS os)
    {
        if (os.Flags?.Flags == null) return;
        var current = os.Flags.Flags;
        var currentSet = new HashSet<string>(current);

        // 第一次初始化：只记录快照，不触发事件
        if (_lastFlags == null) { _lastFlags = currentSet; return; }

        // 移除
        foreach (var f in _lastFlags)
            if (!currentSet.Contains(f))
                EventManager<PlayerFlagEvent>.InvokeAll(
                    new PlayerFlagEvent(f, FlagAction.Removed, os));

        // 新增
        foreach (var f in current)
            if (!_lastFlags.Contains(f))
                EventManager<PlayerFlagEvent>.InvokeAll(
                    new PlayerFlagEvent(f, FlagAction.Added, os));

        _lastFlags = currentSet;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProgressionFlags), nameof(ProgressionFlags.AddFlag))]
    private static void OnAddFlag(ProgressionFlags __instance, string flag)
    {
        Console.WriteLine($"[FLAGPATCH] AddFlag called: {flag}");
        if (!__instance.HasFlag(flag))
        {
            var os = OS.currentInstance;
            if (os != null)
                EventManager<PlayerFlagEvent>.InvokeAll(
                    new PlayerFlagEvent(flag, FlagAction.Added, os));
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProgressionFlags), nameof(ProgressionFlags.RemoveFlag))]
    private static void OnRemoveFlag(ProgressionFlags __instance, string flag)
    {
        Console.WriteLine($"[FLAGPATCH] RemoveFlag called: {flag}");
        if (__instance.HasFlag(flag))
        {
            var os = OS.currentInstance;
            if (os != null)
                EventManager<PlayerFlagEvent>.InvokeAll(
                    new PlayerFlagEvent(flag, FlagAction.Removed, os));
        }
    }
}
