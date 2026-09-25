using HarmonyLib;
using Hacknet;
using System;

namespace TAXCoreOnline.Patches
{
    [HarmonyPatch(typeof(OS), "Update")]
    public class OSPatch
    {
        public static void Postfix(OS __instance, Microsoft.Xna.Framework.GameTime gameTime)
        {
            OnlineConnectionScreen.UpdateActions();
        }
    }
}
