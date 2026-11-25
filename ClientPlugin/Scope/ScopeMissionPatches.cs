using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Hacknet;
using HarmonyLib;
using Pathfinder.Replacements;

namespace ClientPlugin;

/// <summary>
/// Mission 元素（missionStart/missionEnd/nextMission + 根元素）的 scope 预扫描与门控。
/// Goal 的 scope 已在 ScopePatches.LoadGoalPostfix 中处理。
///
/// 预扫描挂在 MissionLoader.LoadContentMission（Pathfinder 的任务文件统一扫描/解析点）：
/// readMission 被 Pathfinder 替换成它，loadmission 命令也直接调它，
/// 因此所有任务加载路径都会先执行这里的预扫描。
/// </summary>
[HarmonyPatch]
public static class ScopeMissionPatches
{
    private static readonly ConditionalWeakTable<ActiveMission, MissionScopeStore>
        _missionScopes = new();

    public class MissionScopeStore
    {
        /// <summary>任务根元素 <mission players="..."> 的 scope</summary>
        public ScopeInfo RootScope;
        public ScopeInfo StartScope;
        public ScopeInfo EndScope;
        public ScopeInfo NextScope;
    }

    public static MissionScopeStore GetMissionScope(ActiveMission m) =>
        _missionScopes.TryGetValue(m, out var s) ? s : null;

    /// <summary>Pre-scan XML → 提取根元素 + missionStart/missionEnd/nextMission 的 scope</summary>
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

            var sb = new System.Text.StringBuilder();

            // 根元素 scope（整个任务归属）
            var rp = (string)root.Attribute("players") ?? "";
            var rg = (string)root.Attribute("group") ?? "";
            if (!string.IsNullOrEmpty(rp))
                sb.Append($"ROOT|{rp}|{rg}\n");

            void Append(string tag, XElement el)
            {
                if (el == null) return;
                var p = (string)el.Attribute("players") ?? "";
                var g = (string)el.Attribute("group") ?? "";
                if (!string.IsNullOrEmpty(p))
                    sb.Append($"{tag}|{p}|{g}\n");
            }

            Append("START", root.Element("missionStart"));
            Append("END",   root.Element("missionEnd"));
            Append("NEXT",  root.Element("nextMission"));

            __state = sb.ToString();
        }
        catch { }
    }

    /// <summary>Post-match: 将 scope 关联到 ActiveMission</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionLoader), nameof(MissionLoader.LoadContentMission))]
    public static void ReadMissionPostfix(string __state, ref ActiveMission __result)
    {
        if (__result == null || string.IsNullOrEmpty(__state))
            return;

        try
        {
            var store = new MissionScopeStore();
            foreach (var line in __state.Split('\n', (char)StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('|');
                if (p.Length < 3) continue;
                var scope = new ScopeInfo { Players = p[1], Group = p[2] ?? "" };
                switch (p[0])
                {
                    case "ROOT":  store.RootScope  = scope; break;
                    case "START": store.StartScope = scope; break;
                    case "END":   store.EndScope   = scope; break;
                    case "NEXT":  store.NextScope  = scope; break;
                }
            }
            _missionScopes.Add(__result, store);
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

    /// <summary>当前客户端是否允许该 scope</summary>
    private static bool Allows(ScopeInfo scope)
    {
        if (scope == null || scope.IsPublic) return true;

        string myGloble = ClientPluginMain.SelectPlayerConfig ?? "";
        if (string.IsNullOrEmpty(scope.Group))
        {
            return string.Equals(scope.Players, myGloble,
                StringComparison.OrdinalIgnoreCase);
        }

        // 有 group → 检查私有身份映射 (group="m1" players="1")
        return ScopePatches.MyPrivateIdentities.TryGetValue(scope.Group, out var myName)
            && string.Equals(scope.Players, myName, StringComparison.OrdinalIgnoreCase);
    }
}
