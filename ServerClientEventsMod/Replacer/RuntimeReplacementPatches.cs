using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Hacknet;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Event.Saving;

namespace ExtraEventsPlugin;

internal static class RuntimeReplacementPatches
{
    internal static void ResolveSaveText(SaveEvent e)
    {
        RuntimeObjectRewriter.Apply(e.Os);

        foreach (XAttribute attribute in e.Save.DescendantsAndSelf().Attributes())
            attribute.Value = ReplacementCatalog.Resolve(attribute.Value);

        foreach (XText text in e.Save.DescendantNodes().OfType<XText>())
            text.Value = ReplacementCatalog.Resolve(text.Value);
    }
}

[HarmonyPatch(typeof(OS), nameof(OS.LoadContent))]
internal static class OsLoadPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        RuntimeObjectRewriter.ClearSession();
        ReplacementCatalog.BeginSession();
    }

    [HarmonyPostfix]
    private static void Postfix(OS __instance) => RuntimeObjectRewriter.Apply(__instance);
}

[HarmonyPatch(typeof(ComputerLoader), nameof(ComputerLoader.filter))]
internal static class ComputerFilterPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref string __result) =>
        __result = ReplacementCatalog.Resolve(__result);
}

[HarmonyPatch(typeof(LocalizedFileLoader), nameof(LocalizedFileLoader.SafeFilterString))]
internal static class SafeFilterPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref string data) => data = ReplacementCatalog.Resolve(data);
}

[HarmonyPatch]
internal static class WrappingPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Utils), nameof(Utils.SmartTwimForWidth));
        yield return AccessTools.Method(typeof(Utils), nameof(Utils.SuperSmartTwimForWidth));
        yield return AccessTools.Method(typeof(DisplayModule), nameof(DisplayModule.cleanSplitForWidth));
    }

    [HarmonyPrefix]
    private static void Prefix(ref string __0) => __0 = ReplacementCatalog.Resolve(__0);
}

[HarmonyPatch]
internal static class DrawStringPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(SpriteBatch)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(SpriteBatch.DrawString))
            .Where(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length > 1 && parameters[1].ParameterType == typeof(string);
            });
    }

    [HarmonyPrefix]
    private static void Prefix(ref string __1) => __1 = ReplacementCatalog.Resolve(__1);
}

[HarmonyPatch]
internal static class MeasureStringPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(SpriteFont), nameof(SpriteFont.MeasureString), new[] { typeof(string) });

    [HarmonyPrefix]
    private static void Prefix(ref string __0) => __0 = ReplacementCatalog.Resolve(__0);
}
