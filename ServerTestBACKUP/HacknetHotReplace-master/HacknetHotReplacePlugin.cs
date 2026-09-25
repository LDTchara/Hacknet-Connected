using BepInEx;
using BepInEx.Hacknet;
using BepInEx.Logging;
using HacknetHotReplace.Server;

namespace HacknetHotReplace
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class HacknetHotReplacePlugin : HacknetPlugin
    {
        public const string ModGUID = "HacknetHotReplace-CV4AW1FE5QAG85THY";
        public const string ModName = "HacknetHotReplace";
        public const string ModVer = "1.0.6";
        public static HacknetHotReplacePlugin Instance { get; private set; }
        public static ManualLogSource Logger => Instance.Log;

        public override bool Load()
        {
            Instance = this;
            HarmonyInstance.PatchAll(Instance.GetType().Assembly);
            HotReplaceServer.Init();
            return true;
        }

        public override bool Unload()
        {
            HotReplaceServer.DeInit();
            return base.Unload();
        }
    }
}
