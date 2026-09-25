using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using HarmonyLib;
using Pathfinder.Action;
using Pathfinder.Event;
using Pathfinder.Event.Saving;
using ServerClientEventsMod.Events;

namespace ExtraEventsPlugin;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class ExtraEventsPlugin : HacknetPlugin
{
    internal static ExtraEventsPlugin Instance { get; private set; }

    public const string ModGUID = "com.ExtraEventsPlugin.AprilCrystal";
    public const string ModName = "A Plugin with Extra Events for Cilent and Server";
    public const string ModVer = "1.0.0";

    public override bool Load()
    {
        HarmonyInstance.PatchAll(typeof(ExtraEventsPlugin).Assembly);
        ActionTriggerInterceptor.EnsurePatched();
        Instance = this;
        ReplacementCatalog.Initialize();
        ActionManager.RegisterAction<ReplaceTextAction>("Replace");
        EventManager<SaveEvent>.AddHandler(RuntimeReplacementPatches.ResolveSaveText);
        

        Log.LogInfo("ReplaceSystem loaded. Pathfinder action: <Replace Org=\"#TOKEN#\" Curr=\"value\" />");

        // ── 测试：直接订阅 PlayerEventAPI 的所有事件 ──
        PlayerEventAPI.OnFileSystemChanged += (comp, os, action, path, ip) =>
            Console.WriteLine($"[EVENTEST] FS: {action} {comp.idName}/{path} ip={ip}");

        PlayerEventAPI.OnConnected += (comp, os) =>
            Console.WriteLine($"[EVENTEST] Connect: {comp.idName}");

        PlayerEventAPI.OnDisconnected += (ip, os) =>
            Console.WriteLine($"[EVENTEST] Disconnect: ip={ip}");

        PlayerEventAPI.OnLoggedIn += (comp, os, user, admin) =>
            Console.WriteLine($"[EVENTEST] Login: {comp.idName} user={user} admin={admin}");

        PlayerEventAPI.OnPortChanged += (comp, os, dsp, code, open) =>
            Console.WriteLine($"[EVENTEST] Port: {comp.idName} dsp={dsp} code={code} open={open}");

        PlayerEventAPI.OnFirewallChanged += (comp, os, state, fw) =>
            Console.WriteLine($"[EVENTEST] Firewall: {comp.idName} state={state}");

        // 以下两个订阅导致刷屏（Hacknet 对所有远程节点做 IP 轮转，每秒触发数十次）
        // PlayerEventAPI.OnNodeVisibilityChanged += (comp, os, vis) =>
        //     Console.WriteLine($"[EVENTEST] NodeVis: {comp.idName} vis={vis}");
        // PlayerEventAPI.OnNodeIPChanged += (comp, os, old, nw) =>
        //     Console.WriteLine($"[EVENTEST] NodeIP: {comp.idName} {old}->{nw}");

        PlayerEventAPI.OnActionTriggered += (act, os, type, xml) =>
            Console.WriteLine($"[EVENTEST] Action: type={type}");

        PlayerEventAPI.On<PlayerFlagEvent>(e =>
            Console.WriteLine($"[EVENTEST] Flag: {e.Flag} {e.Action}"));

        PlayerEventAPI.On<PlayerMissionEvent>(e =>
            Console.WriteLine($"[EVENTEST] Mission: {e.Action} id={e.MissionID}"));

        Console.WriteLine("[ExtraEventsPlugin] Test subscriptions active.");
        return true;

    }


    public override bool Unload()
    {
        RuntimeObjectRewriter.ClearSession();
        ReplacementCatalog.Clear();
        Instance = null;
        return base.Unload();
    }
    public static void Trigger(OS os, string Org, string Curr)
    {
        if (os == null || string.IsNullOrWhiteSpace(Org) || string.IsNullOrWhiteSpace(Curr))
        {
            return;
        }
        ReplacementCatalog.Add(Org, Curr);
        RuntimeObjectRewriter.Apply(os);

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





