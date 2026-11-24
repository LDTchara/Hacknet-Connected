using System;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using Hacknet;
using HarmonyLib;
using Pathfinder.Action;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace HNC.RemovableTags;

/// <summary>对象 → 它的 RemoveableTag。弱引用：不会让已经移除的条件 / 动作无法回收。</summary>
internal static class TagStore
{
    private static readonly ConditionalWeakTable<object, string> Table = new();

    public static void Set(object item, string tag)
    {
        if (item == null || string.IsNullOrWhiteSpace(tag)) return;
        Table.Remove(item);
        Table.Add(item, tag);
    }

    public static string? Get(object? item) =>
        item != null && Table.TryGetValue(item, out var tag) ? tag : null;

    public static bool Matches(object? item, string[] removeTags) =>
        TagList.Intersects(Get(item), removeTags);
}

/// <summary>
/// 让条件和动作元素都能带 RemoveableTag 属性——和 Scope 的 players / group 同一套做法：
///   读：反序列化入口读属性（原版走 XmlReader，Pathfinder 自定义的走 ElementInfo）
///   存：GetSaveString / GetSaveElement 把属性写回去，否则读档后标签就丢了
/// </summary>
[HarmonyPatch]
internal static class RemovableTagPatches
{
    [ThreadStatic] private static string? _pendingCondition;
    [ThreadStatic] private static string? _pendingAction;

    private static string? ReadTag(XmlReader rdr)
    {
        try
        {
            string? tag = null;
            if (rdr.MoveToAttribute(TagList.AttributeName)) tag = rdr.ReadContentAsString();
            rdr.MoveToElement();
            return tag;
        }
        catch
        {
            return null;
        }
    }

    // ── 读：原版条件 ──

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SerializableCondition), nameof(SerializableCondition.Deserialize),
        new[] { typeof(XmlReader), typeof(Action<XmlReader, string>) })]
    private static void ConditionPrefix(XmlReader rdr) => _pendingCondition = ReadTag(rdr);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableCondition), nameof(SerializableCondition.Deserialize),
        new[] { typeof(XmlReader), typeof(Action<XmlReader, string>) })]
    private static void ConditionPostfix(SerializableCondition __result)
    {
        if (__result != null && _pendingCondition != null) TagStore.Set(__result, _pendingCondition);
        _pendingCondition = null;
    }

    // ── 读：原版动作 ──

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SerializableAction), nameof(SerializableAction.Deserialize), new[] { typeof(XmlReader) })]
    private static void ActionPrefix(XmlReader rdr) => _pendingAction = ReadTag(rdr);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableAction), nameof(SerializableAction.Deserialize), new[] { typeof(XmlReader) })]
    private static void ActionPostfix(SerializableAction __result)
    {
        if (__result != null && _pendingAction != null) TagStore.Set(__result, _pendingAction);
        _pendingAction = null;
    }

    // ── 读：Pathfinder 自定义的条件 / 动作 ──

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionsLoader), nameof(ActionsLoader.ReadAction))]
    private static void PfActionPostfix(ElementInfo actionInfo, SerializableAction __result) =>
        FromElementInfo(actionInfo, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionsLoader), nameof(ActionsLoader.ReadCondition))]
    private static void PfConditionPostfix(ElementInfo conditionInfo, SerializableCondition __result) =>
        FromElementInfo(conditionInfo, __result);

    private static void FromElementInfo(ElementInfo info, object? item)
    {
        if (item == null) return;
        if (info.Attributes.TryGetValue(TagList.AttributeName, out var tag)) TagStore.Set(item, tag);
    }

    // ── 存：写回存档 ──
    // 原版走字符串；Pathfinder 的走 XElement。字符串里已经有属性就不再加，
    // 避免 Pathfinder 的 GetSaveString 内部又走了 GetSaveElement 而写出重复属性。

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableAction), nameof(SerializableAction.GetSaveString))]
    private static void ActionSaveString(SerializableAction __instance, ref string __result) =>
        InsertAttribute(__instance, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializableCondition), nameof(SerializableCondition.GetSaveString))]
    private static void ConditionSaveString(SerializableCondition __instance, ref string __result) =>
        InsertAttribute(__instance, ref __result);

    private static void InsertAttribute(object item, ref string result)
    {
        string? tag = TagStore.Get(item);
        if (tag == null || result == null) return;
        if (result.IndexOf(TagList.AttributeName + "=", StringComparison.Ordinal) >= 0) return;
        int pos = result.IndexOf(' ') + 1;
        if (pos > 0)
            result = result.Insert(pos, $"{TagList.AttributeName}=\"{System.Security.SecurityElement.Escape(tag)}\" ");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathfinderAction), nameof(PathfinderAction.GetSaveElement))]
    private static void PfActionSaveElement(PathfinderAction __instance, ref XElement __result) =>
        SetElementAttribute(__instance, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathfinderCondition), nameof(PathfinderCondition.GetSaveElement))]
    private static void PfConditionSaveElement(PathfinderCondition __instance, ref XElement __result) =>
        SetElementAttribute(__instance, __result);

    private static void SetElementAttribute(object item, XElement? element)
    {
        string? tag = TagStore.Get(item);
        if (tag != null && element != null) element.SetAttributeValue(TagList.AttributeName, tag);
    }
}
