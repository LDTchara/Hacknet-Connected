using System;
using System.Reflection;
using System.Xml;
using Hacknet;
using HarmonyLib;

namespace ClientPlugin;

/// <summary>
/// P1-4：Faction Action（&lt;Action ValueRequired="10"&gt;）的 scope 支持。
/// Hacknet.Factions.CustomFactionAction 是 internal 且不继承 SerializableAction，
/// 无法用 typeof 写注解，也进不了 ScopeGatePatches 的枚举，因此全部手动补丁。
/// 本类没有任何 Harmony 注解，不会被 PatchAll 误处理。
/// </summary>
internal static class ScopeFactionPatches
{
    [ThreadStatic] private static ScopeInfo _pending;

    internal static void Apply(Harmony harmony)
    {
        var t = AccessTools.TypeByName("Hacknet.Factions.CustomFactionAction");
        if (t == null)
        {
            Console.WriteLine("[ScopeFaction] 找不到 CustomFactionAction，跳过");
            return;
        }

        int n = 0;
        n += Patch(harmony, AccessTools.Method(t, "Deserialize", new[] { typeof(XmlReader) }),
            prefix: nameof(DeserializePrefix), postfix: nameof(DeserializePostfix));
        n += Patch(harmony, AccessTools.Method(t, "Trigger", new[] { typeof(object) }),
            prefix: null, postfix: null, gate: true);
        n += Patch(harmony, AccessTools.Method(t, "GetSaveString", Type.EmptyTypes),
            prefix: null, postfix: nameof(GetSaveStringPostfix));
        Console.WriteLine($"[ScopeFaction] CustomFactionAction 补丁 {n} 个");
    }

    private static int Patch(Harmony h, MethodBase m, string prefix, string postfix, bool gate = false)
    {
        if (m == null) return 0;
        try
        {
            HarmonyMethod pre = gate
                ? new HarmonyMethod(typeof(ScopeGatePatches).GetMethod(
                    nameof(ScopeGatePatches.ScopeGatePrefix), BindingFlags.Public | BindingFlags.Static))
                : prefix == null ? null : new HarmonyMethod(typeof(ScopeFactionPatches).GetMethod(
                    prefix, BindingFlags.Public | BindingFlags.Static));
            HarmonyMethod post = postfix == null ? null : new HarmonyMethod(
                typeof(ScopeFactionPatches).GetMethod(postfix, BindingFlags.Public | BindingFlags.Static));
            h.Patch(m, prefix: pre, postfix: post);
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ScopeFaction] 补丁失败 {m.Name}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 原方法一开始会把 reader 推进到 &lt;Action&gt; 起始元素之后才读属性，
    /// 所以这里先重复同样的"定位"循环（幂等），再读 players/group 并复位到元素上。
    /// </summary>
    public static void DeserializePrefix(XmlReader rdr)
    {
        _pending = null;
        try
        {
            while (!rdr.EOF && (rdr.Name != "Action" || !rdr.IsStartElement()))
                rdr.Read();
            if (rdr.EOF) return;

            string players = null, group = null;
            if (rdr.MoveToAttribute("players")) players = rdr.ReadContentAsString();
            if (rdr.MoveToAttribute("group")) group = rdr.ReadContentAsString();
            rdr.MoveToElement();
            if (players != null)
                _pending = new ScopeInfo { Players = players, Group = group ?? "" };
        }
        catch { }
    }

    public static void DeserializePostfix(object __result)
    {
        if (__result != null && _pending != null)
            ScopeManager.Register(__result, _pending);
        _pending = null;
    }

    public static void GetSaveStringPostfix(object __instance, ref string __result)
    {
        var scope = ScopeManager.GetScope(__instance);
        if (scope == null || scope.IsPublic || __result == null) return;
        int pos = __result.IndexOf(' ') + 1; // "<Action ValueRequired=..."
        if (pos > 0)
            __result = __result.Insert(pos, $"players=\"{scope.Players}\" group=\"{scope.Group}\" ");
    }
}
