using System;
using Hacknet;
using HarmonyLib;

namespace ClientPlugin;

/// <summary>
/// 检测私有任务加载并设置 selectplayer 上下文。
///
/// 当接取或加载一个私有支线任务时（missionStart/missionEnd/nextMission
/// 带有 group 属性），自动设置 SelectPlayerCommand.CurrentGroup。
/// </summary>
[HarmonyPatch]
public static class PrivateMissionContext
{
    /// <summary>
    /// 从 ActiveMission 的 scope 信息中提取私有 group 名。
    /// 由任务加载 hook 调用。
    /// </summary>
    public static void DetectAndSetGroup(ActiveMission mission)
    {
        if (mission == null) return;

        var store = ScopeMissionPatches.GetMissionScope(mission);
        if (store == null) return;

        // 检查 missionStart/missionEnd/nextMission 是否有私有 group
        string group = null;
        if (store.StartScope != null && !string.IsNullOrEmpty(store.StartScope.Group))
            group = store.StartScope.Group;
        else if (store.EndScope != null && !string.IsNullOrEmpty(store.EndScope.Group))
            group = store.EndScope.Group;
        else if (store.NextScope != null && !string.IsNullOrEmpty(store.NextScope.Group))
            group = store.NextScope.Group;

        if (!string.IsNullOrEmpty(group))
        {
            SelectPlayerCommand.CurrentGroup = group;
            Console.WriteLine($"[PrivateMission] 检测到私有支线任务，group={group}");
            Console.WriteLine($"[PrivateMission] 使用 selectplayer list 查看可选角色");
        }
    }

    /// <summary>
    /// ComputerLoader.loadMission — 加载任务后检测私有上下文
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ComputerLoader), nameof(ComputerLoader.loadMission))]
    public static void OnLoadMission(string filename)
    {
        var os = OS.currentInstance;
        if (os?.currentMission != null)
            DetectAndSetGroup(os.currentMission);
    }

    /// <summary>
    /// ActivateSuppressedStartFunction — 任务启动时检测上下文（捕捉所有接取路径）
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActiveMission), nameof(ActiveMission.ActivateSuppressedStartFunctionIfPresent))]
    public static void OnMissionStart()
    {
        var os = OS.currentInstance;
        if (os?.currentMission != null)
            DetectAndSetGroup(os.currentMission);
    }

    /// <summary>
    /// ActiveMission.finish — 任务完成时清除上下文
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActiveMission), "finish")]
    public static void OnMissionFinish(ActiveMission __instance)
    {
        var store = ScopeMissionPatches.GetMissionScope(__instance);
        if (store != null)
        {
            // 清理当前私有的 group 上下文
            SelectPlayerCommand.CurrentGroup = "";
            PrivateMissionManager.AllSessions.Clear();
            Console.WriteLine("[PrivateMission] 任务完成，清除角色选择上下文");
        }
    }
}
