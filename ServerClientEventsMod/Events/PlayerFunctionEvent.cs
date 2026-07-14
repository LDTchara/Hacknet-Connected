using Hacknet;
using HarmonyLib;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerFunctionEvent : PathfinderEvent
{
    /// <summary>调用的函数名（如 "addRank", "changeSong", "assignPlayerToEntropyFaction"）</summary>
    public string FunctionName { get; }
    public int Value { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }

    public PlayerFunctionEvent(string functionName, int value, OS os)
    {
        FunctionName = functionName;
        Value = value;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
    }
}

internal static class FunctionInterceptor
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionFunctions), nameof(MissionFunctions.runCommand))]
    private static void OnRunCommand(int value, string name)
    {
        if (name == null || name.ToLower().Trim() == "none") return;
        var os = OS.currentInstance;
        if (os == null) return;
        EventManager<PlayerFunctionEvent>.InvokeAll(
            new PlayerFunctionEvent(name, value, os));
    }
}
