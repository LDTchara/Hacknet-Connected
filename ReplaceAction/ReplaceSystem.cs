using System;
using BepInEx;
using BepInEx.Hacknet;
using Pathfinder.Action;
using Pathfinder.Event;
using Pathfinder.Event.Saving;

namespace ReplaceSystem;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public sealed class ReplaceSystemPlugin : HacknetPlugin
{
    public const string ModGuid = "com.AP.ReplaceSystem";
    public const string ModName = "ReplaceSystem";
    public const string ModVersion = "1.0.0";

    internal static ReplaceSystemPlugin Instance { get; private set; }

    public override bool Load()
    {
        Instance = this;
        ReplacementCatalog.Initialize();
        ActionManager.RegisterAction<ReplaceTextAction>("Replace");
        EventManager<SaveEvent>.AddHandler(RuntimeReplacementPatches.ResolveSaveText);
        HarmonyInstance.PatchAll(typeof(ReplaceSystemPlugin).Assembly);

        Log.LogInfo("ReplaceSystem loaded. Pathfinder action: <Replace Org=\"#TOKEN#\" Curr=\"value\" />");
        return true;
    }

    public override bool Unload()
    {
        RuntimeObjectRewriter.ClearSession();
        ReplacementCatalog.Clear();
        Instance = null;
        return base.Unload();
    }

    internal static void LogWarning(string message) => Instance?.Log.LogWarning(message);

    internal static void LogDebug(string message) => Instance?.Log.LogDebug(message);

    internal static Exception CatalogError(string message, Exception inner = null)
    {
        return inner == null
            ? new FormatException($"[ReplaceSystem] {message}")
            : new FormatException($"[ReplaceSystem] {message}", inner);
    }
}
