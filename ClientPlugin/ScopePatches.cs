using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Hacknet;
using Hacknet.Mission;
using HarmonyLib;
using Pathfinder.Action;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace ClientPlugin;

/// <summary>
/// Scope 系统的 Harmony Patch 集合。
/// 覆盖原版 + PF 的四入口（反序列化/序列化/Trigger/Check）+ Goal 门控。
/// 自动由 ClientPluginMain.Load() → _harmony.PatchAll() 注册。
/// </summary>
[HarmonyPatch]
public static class ScopePatches
{
    // ═════════════════════════════════════════════════════════════
    // ① 反序列化入口：原版 + PF
    // ═════════════════════════════════════════════════════════════

    /// <summary>原版 Condition 反序列化 — 读 players/group 属性</summary>
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
                _pendingCondScope = new ScopeInfo { Players = players, Group = group ?? "" };
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

    /// <summary>原版 Action 反序列化 — 读 players/group 属性（覆盖 SA* 全部类型）</summary>
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
                _pendingActionScope = new ScopeInfo { Players = players, Group = group ?? "" };
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

    /// <summary>PF ReadAction — 读 ElementInfo 属性</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionsLoader), nameof(ActionsLoader.ReadAction))]
    public static void ReadActionPostfix(ElementInfo actionInfo, ref SerializableAction __result)
    {
        if (__result == null) return;
        TryRegisterFromElementInfo(actionInfo, __result);
    }

    /// <summary>PF ReadCondition — 同上</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionsLoader), nameof(ActionsLoader.ReadCondition))]
    public static void ReadConditionPostfix(ElementInfo conditionInfo, ref SerializableCondition __result)
    {
        if (__result == null) return;
        TryRegisterFromElementInfo(conditionInfo, __result);
    }

    /// <summary>PF LoadGoal — Goal 的 scope 注册</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionLoader), nameof(MissionLoader.LoadGoal))]
    public static void LoadGoalPostfix(ElementInfo info, ref MisisonGoal __result)
    {
        if (__result == null) return;
        TryRegisterFromElementInfo(info, __result);
    }

    // ═════════════════════════════════════════════════════════════
    // ② 序列化入口：原版 + PF（写回属性）
    // ═════════════════════════════════════════════════════════════

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableAction), nameof(SerializableAction.GetSaveString))]
    public static void GetSaveStringPostfix(SerializableAction __instance, ref string __result)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic) return;
        int pos = __result.IndexOf(' ') + 1;
        if (pos > 0)
            __result = __result.Insert(pos, $"players=\"{scope.Players}\" group=\"{scope.Group}\" ");
    }

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
    // 共享状态（ScopeGatePatches.ScopeGatePrefix 也会访问）
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 当前客户端在其 Private 组中的身份映射。
    /// Key = group name (如 "mission1-private")
    /// Value = 该玩家被分配的 Player name (如 "1")
    /// 在支线任务的角色选择阶段由 UI 代码填充。
    /// </summary>
    public static readonly Dictionary<string, string> MyPrivateIdentities =
        new(StringComparer.OrdinalIgnoreCase);

    // ═════════════════════════════════════════════════════════════
    // 工具方法
    // ═════════════════════════════════════════════════════════════

    private static void TryRegisterFromElementInfo(ElementInfo info, object item)
    {
        if (!info.Attributes.TryGetValue("players", out var players)) return;
        info.Attributes.TryGetValue("group", out var group);
        ScopeManager.Register(item, new ScopeInfo { Players = players, Group = group ?? "" });
    }
}

/// <summary>
/// 批量门控：自动枚举所有 SA*.Trigger(object) / SC*.Check(object) /
/// MisisonGoal.isComplete(List&lt;string&gt;) 并统一在 ScopeGatePrefix 中做 scope 匹配。
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
    /// <summary>枚举所有需要门控的 Trigger/Check/Goal 方法。</summary>
    public static IEnumerable<MethodBase> TargetMethods()
    {
        // SA* Trigger(object)
        foreach (var m in EnumerateConcreteMethods<SerializableAction>(
            nameof(SerializableAction.Trigger), typeof(object)))
            yield return m;

        // SC* Check(object)
        foreach (var m in EnumerateConcreteMethods<SerializableCondition>(
            nameof(SerializableCondition.Check), typeof(object)))
            yield return m;

        // PF DelayablePathfinderAction.Trigger(object) — sealed override
        var dpaMethod = AccessTools.Method(typeof(DelayablePathfinderAction),
            nameof(SerializableAction.Trigger), new[] { typeof(object) });
        if (dpaMethod != null) yield return dpaMethod;

        // 所有 MisisonGoal.isComplete(List<string>)
        foreach (var m in EnumerateConcreteMethods<MisisonGoal>(
            "isComplete", typeof(List<string>)))
            yield return m;
        yield return AccessTools.Method(typeof(MisisonGoal),
            "isComplete", new[] { typeof(List<string>) });
    }

    private static IEnumerable<MethodBase> EnumerateConcreteMethods<T>(string name, params Type[] args)
    {
        var baseType = typeof(T);
        foreach (var t in baseType.Assembly.GetTypes())
        {
            if (!t.IsSubclassOf(baseType) || t.IsAbstract) continue;
            var m = AccessTools.Method(t, name, args);
            if (m != null && m.DeclaringType == t)
                yield return m;
        }
    }

    [HarmonyPrefix]
    public static bool ScopeGatePrefix(object __instance)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic)
            return true; // 默认全局 → 放行

        // 私有 scope → 检查当前客户端是否匹配
        // 当前客户的 Globle 身份
        string myGloble = ClientPluginMain.SelectPlayerConfig ?? "";

        if (string.IsNullOrEmpty(scope.Group))
        {
            // 无 group → 用 Globle 身份匹配 (players="p1")
            return string.Equals(scope.Players, myGloble,
                StringComparison.OrdinalIgnoreCase);
        }

        // 有 group → 检查私有身份映射 (group="m1" players="1")
        if (ScopePatches.MyPrivateIdentities.TryGetValue(scope.Group, out var myName))
        {
            return string.Equals(scope.Players, myName,
                StringComparison.OrdinalIgnoreCase);
        }

        return false; // 不匹配 → 跳过
    }
}
