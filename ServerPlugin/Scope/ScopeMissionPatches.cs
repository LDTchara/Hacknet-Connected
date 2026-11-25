using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Mission;
using HarmonyLib;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace ServerPlugin;

/// <summary>
/// Mission 元素（goal / missionStart / missionEnd / nextMission + 根元素）的 scope 处理。
///
/// 处理策略:
///   A. Goal:          Patch MissionLoader.LoadGoal (PF) + MisisonGoal.isComplete (门控)
///   B. Mission 元素:   Pre-scan XML 文件 + Post-match 到 ActiveMission + 各触发点门控
///
/// 预扫描挂在 MissionLoader.LoadContentMission（Pathfinder 的任务文件统一扫描/解析点）：
/// readMission 被 Pathfinder 替换成它，loadmission 命令也直接调它，
/// 因此所有任务加载路径都会先执行这里的预扫描。
/// </summary>
[HarmonyPatch]
public static class ScopeMissionPatches
{
    // ═════════════════════════════════════════════════════════════
    // A. Goal scope
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// PF LoadGoal — 读取 goal 元素上的 players/group 属性，关联到 Goal 对象。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionLoader), nameof(MissionLoader.LoadGoal))]

    public static void LoadGoalPostfix(ElementInfo info, ref MisisonGoal __result)
    {
        if (__result == null) return;

        if (info.Attributes.TryGetValue("players", out var players))
        {
            info.Attributes.TryGetValue("group", out var group);
            ScopeManager.Register(__result, new ScopeInfo
            {
                Players = players,
                Group = group ?? ""
            });
        }
    }

    /// <summary>
    /// 将 MisisonGoal.isComplete 加入门控体系。
    /// 由 ScopePatches.ScopeGatePrefix 统一拦截。
    /// </summary>
    public static IEnumerable<System.Reflection.MethodBase> GetGoalMethods()
    {
        var goalBase = typeof(MisisonGoal);
        foreach (var t in goalBase.Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(goalBase) && !t.IsAbstract))
        {
            var m = AccessTools.Method(t, "isComplete",
                new[] { typeof(List<string>) });
            if (m != null && m.DeclaringType == t)
                yield return m;
        }
        // 也 patch 基类本身（某些情况直接调用基类 isComplete）
        yield return AccessTools.Method(goalBase, "isComplete",
            new[] { typeof(List<string>) });
    }

    // ═════════════════════════════════════════════════════════════
    // B. Mission 元素 scope（根元素 / missionStart / missionEnd / nextMission）
    // ═════════════════════════════════════════════════════════════

    /// <summary>每任务级存储：ActiveMission → 其各元素的 scope</summary>
    private static readonly ConditionalWeakTable<ActiveMission, MissionScopeStore>
        _missionScope = new();

    public class MissionScopeStore
    {
        /// <summary>任务根元素 <mission players="..."> 的 scope</summary>
        public ScopeInfo RootScope;
        public ScopeInfo StartScope;
        public ScopeInfo EndScope;
        public ScopeInfo NextScope;
    }

    /// <summary>获取任务的元素 scope</summary>
    public static MissionScopeStore GetMissionScope(ActiveMission mission) =>
        _missionScope.TryGetValue(mission, out var s) ? s : null;

    /// <summary>
    /// Pre-scan: 在任务文件被扫描（LoadContentMission）时，读取根元素 +
    /// missionStart/missionEnd/nextMission 的 scope 属性。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionLoader), nameof(MissionLoader.LoadContentMission))]
    [HarmonyPriority(Priority.First)]
    public static void ReadMissionPrefix(string filename, out string __state)
    {
        __state = null;
        try
        {
            string path = LocalizedFileLoader.GetLocalizedFilepath(filename);
            if (!System.IO.File.Exists(path)) return;

            var doc = XDocument.Load(path);
            var root = doc.Root ?? doc.Element("mission");
            if (root == null) return;

            var result = new System.Text.StringBuilder();

            // 根元素 scope（整个任务归属）
            var rp = (string)root.Attribute("players") ?? "";
            var rg = (string)root.Attribute("group") ?? "";
            if (!string.IsNullOrEmpty(rp))
                result.Append($"ROOT|{rp}|{rg}\n");

            // missionStart
            var startEl = root.Element("missionStart");
            if (startEl != null)
            {
                var p = (string)startEl.Attribute("players") ?? "";
                var g = (string)startEl.Attribute("group") ?? "";
                result.Append($"START|{p}|{g}\n");
            }

            // missionEnd
            var endEl = root.Element("missionEnd");
            if (endEl != null)
            {
                var p = (string)endEl.Attribute("players") ?? "";
                var g = (string)endEl.Attribute("group") ?? "";
                result.Append($"END|{p}|{g}\n");
            }

            // nextMission
            var nextEl = root.Element("nextMission");
            if (nextEl != null)
            {
                var p = (string)nextEl.Attribute("players") ?? "";
                var g = (string)nextEl.Attribute("group") ?? "";
                result.Append($"NEXT|{p}|{g}\n");
            }

            __state = result.ToString();
        }
        catch { /* 非关键，忽略 */ }
    }

    /// <summary>
    /// Post-match: 任务扫描完成后，将预扫描的 scope 关联到 ActiveMission。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionLoader), nameof(MissionLoader.LoadContentMission))]
    public static void ReadMissionPostfix(string __state, ref ActiveMission __result)
    {
        if (__result == null || string.IsNullOrEmpty(__state))
            return;

        try
        {
            var store = new MissionScopeStore();

            foreach (var line in __state.Split('\n',
                (char)StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 3) continue;

                var scope = !string.IsNullOrEmpty(parts[1])
                    ? new ScopeInfo { Players = parts[1], Group = parts[2] ?? "" }
                    : null;

                switch (parts[0])
                {
                    case "ROOT":  store.RootScope  = scope; break;
                    case "START": store.StartScope = scope; break;
                    case "END":   store.EndScope   = scope; break;
                    case "NEXT":  store.NextScope  = scope; break;
                }
            }

            if (store.RootScope != null || store.StartScope != null || store.EndScope != null || store.NextScope != null)
                _missionScope.Add(__result, store);
        }
        catch { }
    }

    /// <summary>
    /// loadMission 门控：任务根元素 players/group 不匹配 → 跳过整个任务加载
    /// （不设置 currentMission、不发邮件、不触发 missionStart）。
    /// 覆盖走 ComputerLoader.loadMission 的入口（任务接取、nextMission 推进等）。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ComputerLoader), nameof(ComputerLoader.loadMission))]
    public static bool LoadMissionPrefix(string filename)
    {
        try
        {
            string path = LocalizedFileLoader.GetLocalizedFilepath(filename);
            if (!System.IO.File.Exists(path)) return true;

            var doc = XDocument.Load(path);
            var root = doc.Root ?? doc.Element("mission");
            if (root == null) return true;

            var players = (string)root.Attribute("players");
            if (string.IsNullOrEmpty(players)) return true; // 无根 scope → 放行

            var rootScope = new ScopeInfo { Players = players, Group = (string)root.Attribute("group") ?? "" };
            if (!Allows(rootScope))
            {
                Console.WriteLine($"[ScopeMission] 跳过任务 {filename}（根 players={players} 不匹配）");
                return false;
            }
        }
        catch { }
        return true;
    }

    /// <summary>
    /// sendEmail 门控：覆盖 MissionLoader.LoadContentMission 路径（任务板、loadmissions 命令等）。
    /// 根 scope 不匹配 → 不发任务邮件。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActiveMission), nameof(ActiveMission.sendEmail))]
    public static bool SendEmailPrefix(ActiveMission __instance)
    {
        var store = GetMissionScope(__instance);
        if (store?.RootScope != null && !Allows(store.RootScope))
        {
            Console.WriteLine($"[ScopeMission] 跳过任务邮件（根 players={store.RootScope.Players} 不匹配）");
            return false;
        }
        return true;
    }

    /// <summary>
    /// ActiveMission.finish — missionEnd / nextMission 的 scope 门控。
    /// 不匹配时把字段置 null/"NONE"，原版 finish 逻辑自然跳过对应部分。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActiveMission), "finish")]
    public static bool MissionFinishPrefix(ActiveMission __instance)
    {
        var store = GetMissionScope(__instance);
        if (store == null) return true;

        // missionEnd：不匹配 → 屏蔽，原版 finish 的 if (endFunctionName != null) 自动跳过
        if (store.EndScope != null && !Allows(store.EndScope))
            __instance.endFunctionName = null;

        // nextMission：不匹配 → 屏蔽，原版 finish 的 if (!nextMission.Equals("NONE")) 自动跳过加载
        if (store.NextScope != null && !Allows(store.NextScope))
            __instance.nextMission = "NONE";

        return true;
    }

    /// <summary>missionStart 门控：根 scope 或 missionStart 子元素不匹配 → 屏蔽启动函数</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActiveMission), nameof(ActiveMission.ActivateSuppressedStartFunctionIfPresent))]
    public static bool StartFunctionPrefix(ActiveMission __instance)
    {
        var store = GetMissionScope(__instance);
        if (store == null) return true;

        // 根 scope 不匹配 → 整个任务不激活
        if (store.RootScope != null && !Allows(store.RootScope))
        {
            Console.WriteLine($"[ScopeMission] 跳过任务启动函数（根 players={store.RootScope.Players} 不匹配）");
            __instance.startFunctionName = null;
            return true;
        }

        // missionStart 子元素 scope 不匹配
        if (store.StartScope != null && !Allows(store.StartScope))
            __instance.startFunctionName = null;

        return true;
    }

    /// <summary>服务端（特殊玩家角色）是否允许该 scope</summary>
    private static bool Allows(ScopeInfo scope)
    {
        if (scope == null || scope.IsPublic) return true;

        string myRole = ServerPlugin.SelectPlayerConfig ?? "";
        if (string.IsNullOrEmpty(scope.Group))
        {
            return string.Equals(scope.Players, myRole,
                StringComparison.OrdinalIgnoreCase);
        }

        // 组私有（players="1" group="..."）：服务端不参与 selectplayer 分配 → 不匹配
        return false;
    }
}
