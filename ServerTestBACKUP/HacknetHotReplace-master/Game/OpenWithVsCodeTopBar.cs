using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Hacknet.Extensions;
using HacknetHotReplace.Properties;
using HacknetHotReplace.Util;
using Microsoft.Xna.Framework.Graphics;

namespace HacknetHotReplace.Game
{
    [HarmonyPatch]
    internal static class OpenWithVsCodeTopBar
    {
        private static readonly Texture2D VsCode;

        static OpenWithVsCodeTopBar()
        {
            using (var ms = new MemoryStream(Resources.vscode))
            {
                VsCode = Texture2D.FromStream(Game1.singleton.GraphicsDevice, ms);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(OS), nameof(OS.drawModules))]
        public static void PostfixOsDraw(OS __instance)
        {
            if (ExtensionLoader.ActiveExtensionInfo == null) return;

            var os = __instance;
            var zero = new Vector2(110, 0);
            if (!Settings.isLockedDemoMode && !os.DisableTopBarButtons)
            {
                if (!os.initShowsTutorial && Settings.debugCommandsEnabled)
                {
                    // ignore:源游戏逻辑
                }
                else
                {
                    zero.X = 80f;
                }
            }
            else
            {
                zero.X = 2f;
            }

            zero.X += 30f;
            if (Button.doButton(90827178, (int)zero.X, 0, 20, os.topBar.Height - 1, "", os.topBarIconsColor, VsCode))
            {
                CommonUtils.OpenWithVsCode(Path.GetFullPath(ExtensionLoader.ActiveExtensionInfo.FolderPath));
            }
        }
    }
}
