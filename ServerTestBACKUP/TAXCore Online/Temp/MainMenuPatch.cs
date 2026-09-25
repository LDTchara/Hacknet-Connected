using HarmonyLib;
using Hacknet;
using Hacknet.Gui;
using Hacknet.PlatformAPI.Storage;
using Microsoft.Xna.Framework;
using System;
using TAXCoreOnline;

namespace TAXCoreOnline.Patches
{
    [HarmonyPatch(typeof(MainMenu), "drawMainMenuButtons")]
    public class MainMenuPatch
    {
        // We use a Prefix that returns false to completely replace the method logic.
        // This allows us to insert our button and use the same incrementing 'num' logic
        // to avoid any overlap with subsequent buttons.
        [HarmonyPrefix]
        public static bool Prefix(MainMenu __instance, bool canRun)
        {
            int num = 135;
            if (Button.doButton(1, 180, num += 65, 450, 50, LocaleTerms.Loc("New Session"), new Color?(MainMenu.buttonColor)))
            {
                if (canRun)
                {
                    __instance.NextStartedGameShouldBeDLCAccelerated = false;
                    __instance.State = MainMenu.MainMenuState.NewUser;
                    __instance.loginScreen.ClearTextBox();
                    __instance.loginScreen.ResetForNewAccount();
                }
            }
            bool hasSaves = SaveFileManager.HasSaves;
            string text = LocaleTerms.Loc("No Accounts");
            if (hasSaves)
            {
                if (__instance.canLoad)
                {
                    text = string.Format(LocaleTerms.Loc("Continue with account [{0}]"), SaveFileManager.LastLoggedInUser.Username);
                }
                else
                {
                    text = LocaleTerms.Loc("Invalid Last Account : Login Manually");
                }
            }
            if (Button.doButton(1102, 180, num += 65, 450, 28, text, new Color?(__instance.canLoad ? MainMenu.buttonColor : Color.Black)))
            {
                if (__instance.canLoad)
                {
                    __instance.loginScreen.ClearTextBox();
                    __instance.loginScreen.LoadGameForUserFileAndUsername.Invoke(SaveFileManager.LastLoggedInUser.FileUsername, SaveFileManager.LastLoggedInUser.Username);
                }
            }

            // --- OUR CUSTOM BUTTON ---
            // Inserted right after "Load Session"
            if (Button.doButton(999901, 180, num += 35, 450, 50, "Connect to server", new Color?(MainMenu.buttonColor)))
            {
                __instance.ScreenManager.AddScreen(new OnlineConnectionScreen());
            }
            // -------------------------

            if (Button.doButton(11, 180, num += 55, 450, 50, LocaleTerms.Loc("Login"), new Color?(hasSaves ? MainMenu.buttonColor : Color.Black)))
            {
                if (hasSaves)
                {
                    try
                    {
                        __instance.State = MainMenu.MainMenuState.Login;
                        __instance.loginScreen.ClearTextBox();
                        __instance.loginScreen.ResetForLogin();
                    }
                    catch (Exception ex)
                    {
                        Utils.writeToFile(string.Concat(new string[]
                        {
                            LocaleTerms.Loc("OS Load Error"),
                            ": ",
                            ex.ToString(),
                            "\n\n",
                            ex.StackTrace
                        }), "crashLog.txt");
                    }
                }
            }
            if (Button.doButton(3, 180, num += 65, 450, 50, LocaleTerms.Loc("Settings"), new Color?(MainMenu.buttonColor)))
            {
                __instance.ScreenManager.AddScreen(new OptionsMenu(), new PlayerIndex?(__instance.ScreenManager.controllingPlayer));
            }
            int num2;
            num = (num2 = num + 65);
            if (Settings.isServerMode)
            {
                if (Button.doButton(4, 180, num2, 450, 50, "Start Relay Server", new Color?(MainMenu.buttonColor)))
                {
                    __instance.ScreenManager.AddScreen(new ServerScreen(), new PlayerIndex?(__instance.ScreenManager.controllingPlayer));
                }
                num2 += 65;
            }
            if (Settings.AllowExtensionMode)
            {
                if (Button.doButton(5, 180, num2, 450, 50, "Extensions", new Color?(MainMenu.buttonColor)))
                {
                    __instance.State = MainMenu.MainMenuState.Extensions;
                    __instance.extensionsScreen.Reset();
                }
                num2 += 65;
            }
            if (Settings.HasLabyrinthsDemoStartMainMenuButton && DLC1SessionUpgrader.HasDLC1Installed)
            {
                if (Button.doButton(7, 180, num2, 450, 28, "New Labyrinths Accelerated Session", new Color?(Color.Lerp(Utils.AddativeWhite, new Color(68, 162, 194), 1f - Utils.rand(0.3f)))))
                {
                    if (canRun)
                    {
                        __instance.NextStartedGameShouldBeDLCAccelerated = true;
                        __instance.State = MainMenu.MainMenuState.NewUser;
                        __instance.loginScreen.ClearTextBox();
                        __instance.loginScreen.ResetForNewAccount();
                    }
                }
                num2 += 65;
            }
            if (Button.doButton(15, 180, num2, 450, 28, LocaleTerms.Loc("Exit"), new Color?(MainMenu.exitButtonColor)))
            {
                MusicManager.stop();
                Game1.threadsExiting = true;
                Game1.getSingleton().Exit();
            }
            num2 += 30;
            if (!PlatformAPISettings.RemoteStorageRunning)
            {
                TextItem.doFontLabel(new Vector2(180f, (float)num2), LocaleTerms.Loc("WARNING: Error connecting to Steam Cloud"), GuiData.smallfont, new Color?(Color.DarkRed), float.MaxValue, float.MaxValue, false);
                num2 += 20;
            }
            if (!string.IsNullOrWhiteSpace(MainMenu.AccumErrors))
            {
                TextItem.doFontLabel(new Vector2(180f, (float)num2), MainMenu.AccumErrors, GuiData.smallfont, new Color?(Color.DarkRed), float.MaxValue, float.MaxValue, false);
                num2 += 20;
            }

            return false; // Return false to skip the original method
        }
    }
}
