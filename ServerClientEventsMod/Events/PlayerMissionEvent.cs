using Hacknet;
using HarmonyLib;
using Pathfinder.Event;
using static Hacknet.DLCHubServer;

namespace ServerClientEventsMod.Events;

public enum MissionAction
{
    Activated,
    Completed,
    BranchAdded,
    Abandoned,
    Failed,
    Claimed
}

[EventAutoSubscribe]
public class PlayerMissionEvent : PathfinderEvent
{
    public ActiveMission Mission { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public MissionAction Action { get; }
    public bool IsCurrent { get; }
    public bool IsBranch { get; }

    // ── 标识 ──
    public string MissionFile { get; }       // reloadGoalsSourceFile（XML 路径）
    public string MissionID { get; }         // 非标准字段，如存在则为 mission id

    // ── 回调函数 ──
    public string StartFunctionName { get; }
    public int StartFunctionValue { get; }
    public string EndFunctionName { get; }
    public int EndFunctionValue { get; }

    // ── 后续任务 ──
    public string NextMission { get; }

    // ── 邮件 ──
    public string EmailSender { get; }
    public string EmailSubject { get; }
    public string EmailContent { get; }   // 邮件正文（完成/失败时附带）

    // ── 任务板 ──
    public string PostingTitle { get; }
    public int RequiredRank { get; }
    public int Difficulty { get; }

    // ── 提交结果 ──
    public string ResultMessage { get; }  // "success"/"failed"/其他

    public PlayerMissionEvent(ActiveMission mission, OS os, MissionAction action,
        bool isCurrent, bool isBranch, string result = "", string emailContent = "")
    {
        Mission = mission;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        Action = action;
        IsCurrent = isCurrent;
        IsBranch = isBranch;
        ResultMessage = result;
        EmailContent = emailContent;

        if (mission != null)
        {
            MissionFile = mission.reloadGoalsSourceFile;
            NextMission = mission.nextMission;
            EndFunctionName = mission.endFunctionName;
            EndFunctionValue = mission.endFunctionValue;
            StartFunctionName = mission.startFunctionName;
            StartFunctionValue = mission.startFunctionValue;
            PostingTitle = mission.postingTitle;
            RequiredRank = mission.requiredRank;
            Difficulty = mission.difficulty;

            EmailSender = mission.email.sender;
            EmailSubject = mission.email.subject;
        }
    }
}

// ================================================================
// 1. 帧检测
// ================================================================
internal static class MissionDetector
{
    private static ActiveMission _lastCurrentMission;
    private static int _lastBranchCount;

    [FrameDetect]
    internal static void Detect(OS os)
    {
        if (os.currentMission != _lastCurrentMission)
        {
            if (os.currentMission != null && _lastCurrentMission == null)
                Emit(os.currentMission, os, MissionAction.Activated, true, false);
            else if (os.currentMission == null && _lastCurrentMission != null)
                Emit(_lastCurrentMission, os, MissionAction.Abandoned, true, false);
            _lastCurrentMission = os.currentMission;
        }

        if (os.branchMissions != null && os.branchMissions.Count > _lastBranchCount
            && os.branchMissions.Count > 0)
        {
            var added = os.branchMissions[os.branchMissions.Count - 1];
            Emit(added, os, MissionAction.BranchAdded, false, true);
            _lastBranchCount = os.branchMissions.Count;
        }

        if (os.branchMissions != null && os.branchMissions.Count != _lastBranchCount)
            _lastBranchCount = os.branchMissions.Count;
    }

    static void Emit(ActiveMission m, OS os, MissionAction a, bool cur, bool br)
    {
        EventManager<PlayerMissionEvent>.InvokeAll(
            new PlayerMissionEvent(m, os, a, cur, br));
    }
}

// ================================================================
// 2. Harmony Patch —— ActiveMission.finish()
// ================================================================
internal static class MissionFinishInterceptor
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActiveMission), nameof(ActiveMission.finish))]
    private static void OnMissionFinish(ActiveMission __instance)
    {
        var os = OS.currentInstance;
        if (os == null) return;
        bool cur = os.currentMission == __instance;
        EventManager<PlayerMissionEvent>.InvokeAll(
            new PlayerMissionEvent(__instance, os, MissionAction.Completed, cur, false));
    }
}

// ================================================================
// 3. MailServer.attemptCompleteMission —— 邮件提交成功/失败
// ================================================================
internal static class MailAttemptCompleteInterceptor
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MailServer), "attemptCompleteMission")]
    private static void OnAttempt(bool __result, ActiveMission mission)
    {
        var os = OS.currentInstance;
        if (os == null || mission == null) return;
        bool cur = os.currentMission == mission;
        var action = __result ? MissionAction.Completed : MissionAction.Failed;
        var resultMsg = __result ? "success" : "failed";
        EventManager<PlayerMissionEvent>.InvokeAll(
            new PlayerMissionEvent(mission, os, action, cur, false, resultMsg));
    }
}

// ================================================================
// 4. DLCHubServer.PlayerAttemptCompleteMission —— 任务板提交成功/失败
// ================================================================
internal static class DHSCompleteInterceptor
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DLCHubServer), "PlayerAttemptCompleteMission")]
    private static void OnDHSResult(object __instance, bool __result, object mission)
    {
        var os = OS.currentInstance;
        if (os == null || mission == null) return;

        // 通过反射拿 Mission 字段
        var missionProp = mission.GetType().GetProperty("Mission");
        var activeMission = missionProp?.GetValue(mission) as ActiveMission;
        if (activeMission == null) return;

        bool cur = os.currentMission == activeMission;
        var action = __result ? MissionAction.Completed : MissionAction.Failed;
        var resultMsg = __result ? "success" : "failed";
        EventManager<PlayerMissionEvent>.InvokeAll(
            new PlayerMissionEvent(activeMission, os, action, cur, false, resultMsg));
    }
}

// ================================================================
// 5. DLCHubServer.ClaimMission —— 任务接取
// ================================================================
internal static class ClaimMissionInterceptor
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DLCHubServer), "ClaimMission")]
    private static void OnClaim(object mission)
    {
        var os = OS.currentInstance;
        if (os == null || mission == null) return;

        var missionProp = mission.GetType().GetProperty("Mission");
        var activeMission = missionProp?.GetValue(mission) as ActiveMission;
        if (activeMission == null) return;

        EventManager<PlayerMissionEvent>.InvokeAll(
            new PlayerMissionEvent(activeMission, os, MissionAction.Claimed, false, false));
    }
}
