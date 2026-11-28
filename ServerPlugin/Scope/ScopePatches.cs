using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Hacknet;
using HarmonyLib;
using Pathfinder.Action;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace ServerPlugin;

/// <summary>
/// 所有与 Scope 相关的 Harmony Patch。
/// 
/// 覆盖范围:
///   原版: SerializableAction.Deserialize + SerializableAction.GetSaveString
///         + 所有 SA* Trigger / SC* Check（由 ScopeGatePatches 批量门控）
///   PF:   ActionsLoader.ReadAction + ReadCondition + PathfinderAction.GetSaveElement
///   调用点: RunnableConditionalActions.Update + ActiveMission.finish
/// </summary>
[HarmonyPatch]
public static class ScopePatches
{
    // ═════════════════════════════════════════════════════════════
    // ① 反序列化入口：原版 + PF
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 原版 SerializableCondition.Deserialize(XmlReader, Action)
    /// 这是 vanilla ConditionalActions 解析 Condition 时的入口。
    /// XmlReader 此时定位在条件元素上（如 <HasFlags flag="X" />），
    /// 可以安全读取 players/group，之后 MoveToElement() 复位。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SerializableCondition), nameof(SerializableCondition.Deserialize),
        new[] { typeof(XmlReader), typeof(Action<XmlReader, string>) })]
    public static void ConditionDeserializePrefix(XmlReader rdr)
    {
        _pendingCondScope = null;
        try
        {
            string players = null, group = null;
            if (rdr.MoveToAttribute("players"))
                players = rdr.ReadContentAsString();
            if (rdr.MoveToAttribute("group"))
                group = rdr.ReadContentAsString();
            rdr.MoveToElement();

            if (players != null)
            {
                _pendingCondScope = new ScopeInfo
                {
                    Players = players,
                    Group = group ?? ""
                };
            }
        }
        catch { }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableCondition), nameof(SerializableCondition.Deserialize),
        new[] { typeof(XmlReader), typeof(Action<XmlReader, string>) })]
    public static void ConditionDeserializePostfix(ref SerializableCondition __result)
    {
        if (__result != null && _pendingCondScope != null)
            ScopeManager.Register(__result, _pendingCondScope);
        _pendingCondScope = null;
    }

    [ThreadStatic] private static ScopeInfo _pendingCondScope;

    /// <summary>
    /// 原版 SerializableAction.Deserialize(XmlReader) — 读 players/group 属性。
    /// 覆盖原版任务解析、DelayableActionSystem、FastDelayableActionSystem、
    /// SAAddConditionalActions 嵌套动作等全部路径。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SerializableAction), nameof(SerializableAction.Deserialize),
        new[] { typeof(XmlReader) })]
    public static void ActionDeserializePrefix(XmlReader rdr)
    {
        _pendingActionScope = null;
        try
        {
            string players = null, group = null;
            if (rdr.MoveToAttribute("players"))
                players = rdr.ReadContentAsString();
            if (rdr.MoveToAttribute("group"))
                group = rdr.ReadContentAsString();
            rdr.MoveToElement();

            if (players != null)
            {
                _pendingActionScope = new ScopeInfo
                {
                    Players = players,
                    Group = group ?? ""
                };
            }
        }
        catch { }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableAction), nameof(SerializableAction.Deserialize),
        new[] { typeof(XmlReader) })]
    public static void ActionDeserializePostfix(ref SerializableAction __result)
    {
        if (__result != null && _pendingActionScope != null)
            ScopeManager.Register(__result, _pendingActionScope);
        _pendingActionScope = null;
    }

    [ThreadStatic] private static ScopeInfo _pendingActionScope;

    /// <summary>
    /// PF ActionsLoader.ReadAction — 读取 ElementInfo 中的 players/group 属性。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionsLoader), nameof(ActionsLoader.ReadAction))]
    public static void ReadActionPostfix(ElementInfo actionInfo, ref SerializableAction __result)
    {
        if (__result == null) return;
        TryRegisterFromElementInfo(actionInfo, __result);
    }

    /// <summary>
    /// PF ActionsLoader.ReadCondition — 同上。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionsLoader), nameof(ActionsLoader.ReadCondition))]
    public static void ReadConditionPostfix(ElementInfo conditionInfo, ref SerializableCondition __result)
    {
        if (__result == null) return;
        TryRegisterFromElementInfo(conditionInfo, __result);
    }

    // ═════════════════════════════════════════════════════════════
    // ② 序列化入口：原版 + PF（写回 players/group 属性）
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 原版 GetSaveString — 在输出 XML 中追加 players="..." group="..."
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableAction), nameof(SerializableAction.GetSaveString))]
    public static void GetSaveStringPostfix(SerializableAction __instance, ref string __result)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic) return;

        // 在 <TagName ... 后面插入 players="X" group="Y"
        int insertPos = __result.IndexOf(' ') + 1;
        if (insertPos > 0)
        {
            __result = __result.Insert(insertPos,
                $"players=\"{scope.Players}\" group=\"{scope.Group}\" ");
        }
    }

    /// <summary>原版 Condition 存档写回 players/group（P0-3）</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableCondition), nameof(SerializableCondition.GetSaveString))]
    public static void ConditionGetSaveStringPostfix(SerializableCondition __instance, ref string __result)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic || __result == null) return;
        int insertPos = __result.IndexOf(' ') + 1;
        if (insertPos > 0)
            __result = __result.Insert(insertPos,
                $"players=\"{scope.Players}\" group=\"{scope.Group}\" ");
    }

    /// <summary>PF Condition 存档写回 players/group（P0-2）</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathfinderCondition), nameof(PathfinderCondition.GetSaveElement))]
    public static void ConditionGetSaveElementPostfix(PathfinderCondition __instance, ref XElement __result)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic || __result == null) return;
        __result.SetAttributeValue("players", scope.Players);
        __result.SetAttributeValue("group", scope.Group);
    }

    /// <summary>
    /// PF PathfinderAction.GetSaveElement — 在 XElement 中添加属性
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathfinderAction), nameof(PathfinderAction.GetSaveElement))]
    public static void GetSaveElementPostfix(PathfinderAction __instance, ref XElement __result)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic) return;

        __result.SetAttributeValue("players", scope.Players);
        __result.SetAttributeValue("group", scope.Group);
    }

    // ═════════════════════════════════════════════════════════════
    // ③ 调用点门控
    // ═════════════════════════════════════════════════════════════
    // 已移除两个 "return true" 占位补丁（P1-5 / P1-6）：
    //  - RunnableConditionalActions.Update：其内部调用的 Condition.Check 与
    //    Action.Trigger 已被 ScopeGatePatches 门控，无需再拦截整个循环。
    //  - ActiveMission.finish：真实实现在 ScopeMissionPatches（missionEnd / nextMission）。

    // ═════════════════════════════════════════════════════════════
    // 工具方法
    // ═════════════════════════════════════════════════════════════

    private static void TryRegisterFromElementInfo(ElementInfo info, object item)
    {
        if (!info.Attributes.TryGetValue("players", out var players))
            return;

        info.Attributes.TryGetValue("group", out var group);
        ScopeManager.Register(item, new ScopeInfo
        {
            Players = players,
            Group = group ?? ""
        });
    }
}

/// <summary>
/// 批量门控：自动枚举所有 SA*.Trigger(object) / SC*.Check(object) /
/// DelayablePathfinderAction.Trigger(object) / MisisonGoal.isComplete
/// 并统一在 ScopeGatePrefix 中拦截。
///
/// ⚠ 此类必须保持"命名约定模式"：
///   - 方法名必须是 TargetMethods()（Harmony 命名约定，旧版 Harmony 无
///     [HarmonyTargetMethods("...")] 字符串构造函数，只能靠约定名）
///   - 类内不能有任何带方法级 [HarmonyPatch] 注解的方法
/// 否则 Harmony 会抛 "You cannot combine TargetMethod, TargetMethods or
/// PatchAll with individual annotations"。
/// </summary>
[HarmonyPatch]
internal static class ScopeGatePatches
{
    /// <summary>
    /// 自动枚举所有需要门控的 Trigger/Check 方法：
    ///   原版 26 个 SA* 的 Trigger(object)
    ///   原版 6 个 SC* 的 Check(object)
    ///   PF DelayablePathfinderAction.Trigger(object) — sealed override 唯一入口
    /// </summary>
    public static IEnumerable<MethodBase> TargetMethods()
    {
        // ① 原版所有 SA* 的 Trigger(object)
        var saBase = typeof(SerializableAction);
        foreach (var t in saBase.Assembly.GetTypes())
        {
            if (!t.IsSubclassOf(saBase) || t.IsAbstract) continue;
            var m = AccessTools.Method(t, nameof(SerializableAction.Trigger),
                                       new[] { typeof(object) });
            if (m != null && m.DeclaringType == t)
                yield return m;
        }

        // ② 原版所有 SC* 的 Check(object)
        var scBase = typeof(SerializableCondition);
        foreach (var t in scBase.Assembly.GetTypes())
        {
            if (!t.IsSubclassOf(scBase) || t.IsAbstract) continue;
            var m = AccessTools.Method(t, nameof(SerializableCondition.Check),
                                       new[] { typeof(object) });
            if (m != null && m.DeclaringType == t)
                yield return m;
        }

        // ③ PF DelayablePathfinderAction.Trigger(object) — sealed override
        var dpa = typeof(DelayablePathfinderAction);
        var triggerMethod = AccessTools.Method(dpa, nameof(SerializableAction.Trigger),
                                               new[] { typeof(object) });
        if (triggerMethod != null)
            yield return triggerMethod;

        // ④ 所有 MisisonGoal.isComplete（goal 检查门控）
        foreach (var m in ScopeMissionPatches.GetGoalMethods())
            yield return m;
    }

    [HarmonyPrefix]
    public static bool ScopeGatePrefix(object __instance)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic)
            return true; // 无 scope / PUBLIC → 放行

        // 服务端作为特殊玩家（Config.tmp 角色，如 p0）参与过滤：
        // 无 group 的私有 scope → 匹配 Config.tmp 角色
        string myRole = ServerPlugin.SelectPlayerConfig ?? "";
        if (string.IsNullOrEmpty(scope.Group))
        {
            return string.Equals(scope.Players, myRole,
                StringComparison.OrdinalIgnoreCase);
        }

        // 组私有（players="1" group="..."）：主机通过组队任务（team）分到的位置
        return HNC.Teams.TeamPlayer.MyIdentities.TryGetValue(scope.Group, out var mySlot)
            && string.Equals(scope.Players, mySlot, StringComparison.OrdinalIgnoreCase);
    }
}
