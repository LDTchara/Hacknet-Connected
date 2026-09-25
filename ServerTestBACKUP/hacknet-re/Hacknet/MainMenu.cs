using System;
using System.IO;
using System.Threading;
using System.Xml;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.Misc;
using Hacknet.PlatformAPI.Storage;
using Hacknet.Screens;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MainMenu : GameScreen
{
	private enum MainMenuState
	{
		Normal,
		NewUser,
		Login,
		Extensions
	}

	private const int CharCountForTestPassedMessage = 950;

	public static string OSVersion = "v5.069";

	public static string AccumErrors = "";

	public static Color buttonColor;

	public static Color exitButtonColor;

	private SpriteFont titleFont;

	private Color titleColor;

	private bool canLoad = false;

	private bool hasSentErrorEmail = false;

	private int framecount = 0;

	private HexGridBackground hexBackground;

	private string testSuiteResult = null;

	private SavefileLoginScreen loginScreen = new SavefileLoginScreen();

	private MainMenuState State = MainMenuState.Normal;

	private AttractModeMenuScreen attractModeScreen = new AttractModeMenuScreen();

	private ExtensionsMenuScreen extensionsScreen = new ExtensionsMenuScreen();

	private bool NextStartedGameShouldBeDLCAccelerated = false;

	public MainMenu()
	{
		base.TransitionOffTime = TimeSpan.FromSeconds(0.5);
	}

	public override void LoadContent()
	{
		buttonColor = new Color(124, 137, 149);
		exitButtonColor = new Color(105, 82, 82);
		titleColor = new Color(190, 190, 190, 0);
		titleFont = base.ScreenManager.Game.Content.Load<SpriteFont>("Kremlin");
		canLoad = SaveFileManager.StorageMethods[0].GetSaveManifest().LastLoggedInUser.Username != null;
		hexBackground = new HexGridBackground(base.ScreenManager.Game.Content);
		HookUpCreationEvents();
		MusicManager.transitionToSong("Music/Ambient/AmbientDrone_Clipped");
		Console.WriteLine("Menu load complete");
	}

	private void HookUpCreationEvents()
	{
		SavefileLoginScreen savefileLoginScreen = loginScreen;
		savefileLoginScreen.RequestGoBack = (Action)Delegate.Combine(savefileLoginScreen.RequestGoBack, (Action)delegate
		{
			State = MainMenuState.Normal;
		});
		SavefileLoginScreen savefileLoginScreen2 = loginScreen;
		savefileLoginScreen2.StartNewGameForUsernameAndPass = (Action<string, string>)Delegate.Combine(savefileLoginScreen2.StartNewGameForUsernameAndPass, (Action<string, string>)delegate(string username, string pass)
		{
			if (SaveFileManager.AddUser(username, pass))
			{
				string filePathForLogin = SaveFileManager.GetFilePathForLogin(username, pass);
				ExitScreen();
				resetOS();
				if (!Settings.soundDisabled)
				{
					base.ScreenManager.playAlertSound();
				}
				try
				{
					OS oS = new OS
					{
						SaveGameUserName = filePathForLogin,
						SaveUserAccountName = username
					};
					if (NextStartedGameShouldBeDLCAccelerated)
					{
						oS.IsDLCConventionDemo = true;
						oS.Flags.AddFlag("TutorialComplete");
						Settings.EnableDLC = true;
						Settings.initShowsTutorial = false;
						oS.initShowsTutorial = false;
					}
					base.ScreenManager.AddScreen(oS, base.ScreenManager.controllingPlayer);
					oS.Flags.AddFlag("startVer:" + OSVersion);
					if (NextStartedGameShouldBeDLCAccelerated)
					{
						SessionAccelerator.AccelerateSessionToDLCStart(oS);
						oS.delayer.Post(ActionDelayer.Wait(0.15), delegate
						{
							Game1.getSingleton().IsMouseVisible = true;
						});
						NextStartedGameShouldBeDLCAccelerated = false;
					}
					return;
				}
				catch (Exception ex9)
				{
					UpdateUIForSaveCreationFailed(ex9);
					return;
				}
			}
			loginScreen.ResetForNewAccount();
			loginScreen.WriteToHistory(" ERROR: Username invalid or already in use.");
		});
		SavefileLoginScreen savefileLoginScreen3 = loginScreen;
		savefileLoginScreen3.LoadGameForUserFileAndUsername = (Action<string, string>)Delegate.Combine(savefileLoginScreen3.LoadGameForUserFileAndUsername, (Action<string, string>)delegate(string userFile, string username)
		{
			ExitScreen();
			resetOS();
			if (!SaveFileManager.StorageMethods[0].FileExists(userFile))
			{
				OS.WillLoadSave = false;
				UpdateUIForSaveMissing(userFile, new FileNotFoundException());
				return;
			}
			OS.WillLoadSave = true;
			OS screen2 = new OS
			{
				SaveGameUserName = userFile,
				SaveUserAccountName = username
			};
			try
			{
				base.ScreenManager.AddScreen(screen2, base.ScreenManager.controllingPlayer);
			}
			catch (XmlException ex3)
			{
				UpdateUIForSaveCorruption(userFile, ex3);
			}
			catch (FormatException ex4)
			{
				UpdateUIForSaveCorruption(userFile, ex4);
			}
			catch (NullReferenceException ex5)
			{
				UpdateUIForSaveCorruption(userFile, ex5);
			}
			catch (FileNotFoundException ex6)
			{
				UpdateUIForSaveMissing(userFile, ex6);
			}
			catch (ContentLoadException ex8)
			{
				string text = Utils.ReadEntireContentsOfStream(SaveFileManager.StorageMethods[0].GetFileReadStream(userFile));
				if (text.Contains("DigiPets"))
				{
					text = text.Replace("DigiPets", "Neopals");
					text = text.Replace("DigiPoints", "Neopoints");
					for (int j = 0; j < 3; Thread.Sleep(500), j++)
					{
						try
						{
							Thread.Sleep(200);
							SaveFileManager.StorageMethods[0].WriteFileData(userFile, text);
						}
						catch (IOException)
						{
							continue;
						}
						break;
					}
					AccumErrors = "-- Savefile Automatically Upgraded - Try again! --";
				}
				else
				{
					UpdateUIForSaveCorruption(userFile, ex8);
				}
			}
		});
		AttractModeMenuScreen attractModeMenuScreen = attractModeScreen;
		attractModeMenuScreen.Start = (Action)Delegate.Combine(attractModeMenuScreen.Start, (Action)delegate
		{
			try
			{
				ExitScreen();
				resetOS();
				if (!Settings.soundDisabled)
				{
					base.ScreenManager.playAlertSound();
				}
				base.ScreenManager.AddScreen(new OS(), base.ScreenManager.controllingPlayer);
			}
			catch (Exception ex2)
			{
				Utils.writeToFile("OS Load Error: " + ex2.ToString() + "\n\n" + ex2.StackTrace, "crashLog.txt");
			}
		});
		AttractModeMenuScreen attractModeMenuScreen2 = attractModeScreen;
		attractModeMenuScreen2.StartDLC = (Action)Delegate.Combine(attractModeMenuScreen2.StartDLC, (Action)delegate
		{
			try
			{
				ExitScreen();
				resetOS();
				Settings.EnableDLC = true;
				Settings.initShowsTutorial = false;
				if (!Settings.soundDisabled)
				{
					base.ScreenManager.playAlertSound();
				}
				OS os = new OS();
				os.IsDLCConventionDemo = true;
				os.Flags.AddFlag("TutorialComplete");
				os.SaveGameUserName = "save_" + Settings.ConventionLoginName + ".xml";
				os.SaveUserAccountName = Settings.ConventionLoginName;
				base.ScreenManager.AddScreen(os, base.ScreenManager.controllingPlayer);
				os.allFactions.setCurrentFaction("Bibliotheque", os);
				ThemeManager.setThemeOnComputer(os.thisComputer, "DLC/Themes/RiptideClassicTheme.xml");
				ThemeManager.switchTheme(os, "DLC/Themes/RiptideClassicTheme.xml");
				for (int i = 0; i < 60; i++)
				{
					int num = 0;
					do
					{
						num = Utils.random.Next(os.netMap.nodes.Count);
					}
					while (os.netMap.nodes[num].idName == "mainHub" || os.netMap.nodes[num].idName == "entropy00" || os.netMap.nodes[num].idName == "entropy01");
					os.netMap.discoverNode(os.netMap.nodes[num]);
				}
				os.delayer.Post(ActionDelayer.Wait(0.15), delegate
				{
					Game1.getSingleton().IsMouseVisible = true;
					os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[22], "SSHCrack.exe"));
					os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[21], "FTPBounce.exe"));
					MissionFunctions.runCommand(7, "changeSong");
					MusicManager.stop();
				});
				os.delayer.Post(ActionDelayer.Wait(38.0), delegate
				{
					ComputerLoader.loadMission("Content/DLC/Missions/Demo/DLCDemointroMission1.xml");
				});
			}
			catch (Exception ex)
			{
				Utils.writeToFile("OS Load Error: " + ex.ToString() + "\n\n" + ex.StackTrace, "crashLog.txt");
			}
		});
		ExtensionsMenuScreen extensionsMenuScreen = extensionsScreen;
		extensionsMenuScreen.ExitClicked = (Action)Delegate.Combine(extensionsMenuScreen.ExitClicked, (Action)delegate
		{
			State = MainMenuState.Normal;
		});
		ExtensionsMenuScreen extensionsMenuScreen2 = extensionsScreen;
		extensionsMenuScreen2.CreateNewAccountForExtension_UserAndPass = (Action<string, string>)Delegate.Combine(extensionsMenuScreen2.CreateNewAccountForExtension_UserAndPass, (Action<string, string>)delegate(string user, string pass)
		{
			CreateNewAccountForExtensionAndStart(user, pass, base.ScreenManager, this, extensionsScreen);
		});
		ExtensionsMenuScreen extensionsMenuScreen3 = extensionsScreen;
		extensionsMenuScreen3.LoadAccountForExtension_FileAndUsername = (Action<string, string>)Delegate.Combine(extensionsMenuScreen3.LoadAccountForExtension_FileAndUsername, (Action<string, string>)delegate(string userFile, string username)
		{
			ExitScreen();
			resetOS();
			Settings.IsInExtensionMode = true;
			if (SaveFileManager.StorageMethods[0].FileExists(userFile))
			{
				OS.WillLoadSave = true;
			}
			else
			{
				OS.WillLoadSave = false;
			}
			OS screen = new OS
			{
				SaveGameUserName = userFile,
				SaveUserAccountName = username
			};
			base.ScreenManager.AddScreen(screen, base.ScreenManager.controllingPlayer);
		});
	}

	public static void CreateNewAccountForExtensionAndStart(string username, string pass, ScreenManager sman, GameScreen currentScreen = null, ExtensionsMenuScreen extensionsScreen = null)
	{
		if (SaveFileManager.AddUser(username, pass))
		{
			OS oS = new OS();
			string filePathForLogin = SaveFileManager.GetFilePathForLogin(username, pass);
			currentScreen?.ExitScreen();
			resetOS();
			Settings.IsInExtensionMode = true;
			if (!Settings.soundDisabled)
			{
				sman.playAlertSound();
			}
			oS.SaveGameUserName = filePathForLogin;
			oS.SaveUserAccountName = username;
			sman.AddScreen(oS, sman.controllingPlayer);
		}
		else if (extensionsScreen != null)
		{
			extensionsScreen.ShowError("Error Creating UserAccount for username :" + username);
		}
		else
		{
			AccumErrors = AccumErrors + "Error auto-loading Extension " + Game1.AutoLoadExtensionPath;
		}
	}

	private void UpdateUIForSaveCorruption(string saveName, Exception ex)
	{
		State = MainMenuState.Normal;
		string accumErrors = AccumErrors;
		AccumErrors = accumErrors + string.Format(LocaleTerms.Loc("ACCOUNT FILE CORRUPTION: Account {0} appears to be corrupted, and will not load."), saveName) + " Reported Error:\r\n" + Utils.GenerateReportFromException(ex) + "\r\n";
		if (!hasSentErrorEmail)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				Utils.SendErrorEmail(ex, "Save Corruption ");
			});
			thread.IsBackground = true;
			thread.Name = "SaveCorruptErrorReportThread";
			thread.Start();
			hasSentErrorEmail = true;
		}
	}

	private void UpdateUIForSaveMissing(string saveName, Exception ex)
	{
		State = MainMenuState.Normal;
		string accumErrors = AccumErrors;
		AccumErrors = accumErrors + "ACCOUNT FILE NOT FOUND: Account " + saveName + " appears to be missing. It may have been moved or deleted. Reported Error:\r\n" + Utils.GenerateReportFromException(ex) + "\r\n";
		if (!hasSentErrorEmail)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				Utils.SendErrorEmail(ex, "Save Missing ");
			});
			thread.IsBackground = true;
			thread.Name = "SaveMissingErrorReportThread";
			thread.Start();
			hasSentErrorEmail = true;
		}
	}

	private void UpdateUIForSaveCreationFailed(Exception ex)
	{
		State = MainMenuState.Normal;
		AccumErrors = AccumErrors + "CRITICAL ERROR CREATING ACCOUNT: Reported Error:\r\n" + Utils.GenerateReportFromException(ex) + "\r\n";
		if (!hasSentErrorEmail)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				Utils.SendErrorEmail(ex, "Account Creation Error ");
			});
			thread.IsBackground = true;
			thread.Name = "SaveAccCreationErrorReportThread";
			thread.Start();
			hasSentErrorEmail = true;
		}
	}

	public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
	{
		base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
		framecount++;
		hexBackground.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
	}

	public override void HandleInput(InputState input)
	{
		base.HandleInput(input);
		GuiData.doInput(input);
	}

	public static void resetOS()
	{
		if (!Settings.isSpecialTestBuild)
		{
			TextBox.cursorPosition = 0;
			Settings.initShowsTutorial = Settings.osStartsWithTutorial;
			Settings.IsInExtensionMode = false;
			ScrollablePanel.ClearCache();
			PostProcessor.dangerModeEnabled = false;
			PostProcessor.dangerModePercentComplete = 0f;
			PostProcessor.EndingSequenceFlashOutActive = false;
			PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
		}
	}

	public override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);
		try
		{
			PostProcessor.begin();
			base.ScreenManager.FadeBackBufferToBlack(255);
			GuiData.startDraw();
			Rectangle dest = new Rectangle(0, 0, base.ScreenManager.GraphicsDevice.Viewport.Width, base.ScreenManager.GraphicsDevice.Viewport.Height);
			Rectangle destinationRectangle = new Rectangle(-20, -20, base.ScreenManager.GraphicsDevice.Viewport.Width + 40, base.ScreenManager.GraphicsDevice.Viewport.Height + 40);
			Rectangle dest2 = new Rectangle(dest.X + dest.Width / 4, dest.Height / 4, dest.Width / 2, dest.Height / 4);
			GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, Color.Black);
			if (Settings.DrawHexBackground)
			{
				hexBackground.Draw(dest, GuiData.spriteBatch, Color.Transparent, Settings.lighterColorHexBackground ? new Color(20, 20, 20) : new Color(15, 15, 15, 0), HexGridBackground.ColoringAlgorithm.NegaitiveSinWash);
			}
			TextItem.DrawShadow = false;
			switch (State)
			{
			case MainMenuState.NewUser:
				DrawLoginScreen(dest2, needsNewUser: true);
				break;
			case MainMenuState.Login:
				DrawLoginScreen(dest2);
				break;
			default:
			{
				if (Settings.isLockedDemoMode)
				{
					attractModeScreen.Draw(dest, GuiData.spriteBatch);
					break;
				}
				bool flag = DrawBackgroundAndTitle();
				if (Settings.isLockedDemoMode)
				{
					drawDemoModeButtons(flag);
					break;
				}
				drawMainMenuButtons(flag);
				if (Settings.testingMenuItemsEnabled)
				{
					drawTestingMainMenuButtons(flag);
				}
				break;
			}
			case MainMenuState.Extensions:
			{
				bool flag = DrawBackgroundAndTitle();
				Rectangle dest3 = new Rectangle(180, 150, Math.Min(700, dest.Width / 2), (int)((float)dest.Height * 0.7f));
				extensionsScreen.Draw(dest3, GuiData.spriteBatch, base.ScreenManager);
				break;
			}
			}
			GuiData.endDraw();
			PostProcessor.end();
			base.ScreenManager.FadeBackBufferToBlack(255 - base.TransitionAlpha);
		}
		catch (ObjectDisposedException ex)
		{
			if (!hasSentErrorEmail)
			{
				string text = Utils.GenerateReportFromException(ex);
				text = text + "\r\n Font:" + titleFont;
				text = text + "\r\n White:" + Utils.white;
				text = text + "\r\n WhiteDisposed:" + Utils.white.IsDisposed;
				text = text + "\r\n SmallFont:" + GuiData.smallfont;
				text = text + "\r\n TinyFont:" + GuiData.tinyfont;
				text = text + "\r\n LineEffectTarget:" + FlickeringTextEffect.GetReportString();
				text = text + "\r\n PostProcessort stuff:" + PostProcessor.GetStatusReportString();
				object obj = text;
				text = string.Concat(obj, "\r\nRESOLUTION:\r\n ", Game1.getSingleton().GraphicsDevice.PresentationParameters.BackBufferWidth, "x");
				text = text + Game1.getSingleton().GraphicsDevice.PresentationParameters.BackBufferHeight + "\r\nFullscreen: ";
				text += (Game1.getSingleton().graphics.IsFullScreen ? "true" : "false");
				text = text + "\r\n Adapter: " + Game1.getSingleton().GraphicsDevice.Adapter.Description;
				text = text + "\r\n Device Name: " + Game1.getSingleton().GraphicsDevice.Adapter.DeviceName;
				text = text + "\r\n Status: " + Game1.getSingleton().GraphicsDevice.GraphicsDeviceStatus;
				Utils.SendRealWorldEmail("Hacknet " + OSVersion + " Crash " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString(), "hacknetbugs+Hacknet@gmail.com", text);
				hasSentErrorEmail = true;
				SettingsLoader.writeStatusFile();
				return;
			}
			throw ex;
		}
	}

	private bool DrawBackgroundAndTitle()
	{
		Rectangle dest = new Rectangle(180, 120, 340, 100);
		FlickeringTextEffect.DrawLinedFlickeringText(dest, "HACKNET", 7f, 0.55f, titleFont, null, titleColor);
		string text = "";
		string text2 = "OS" + (DLC1SessionUpgrader.HasDLC1Installed ? "+Labyrinths " : " ") + OSVersion + " " + text;
		TextItem.doFontLabel(new Vector2(520f, 178f), text2, GuiData.smallfont, titleColor * 0.5f, 600f, 26f);
		bool result = true;
		if (Settings.IsExpireLocked)
		{
			TimeSpan timeSpan = Settings.ExpireTime - DateTime.Now;
			string text3 = "";
			if (timeSpan.TotalSeconds < 1.0)
			{
				text3 = LocaleTerms.Loc("TEST BUILD EXPIRED - EXECUTION DISABLED");
				result = false;
			}
			else
			{
				text3 = "Test Build : Expires in " + timeSpan.ToString();
			}
			TextItem.doFontLabel(new Vector2(180f, 105f), text3, GuiData.smallfont, Color.Red * 0.8f, 600f, 26f);
		}
		return result;
	}

	private void DrawLoginScreen(Rectangle dest, bool needsNewUser = false)
	{
		loginScreen.Draw(GuiData.spriteBatch, dest);
	}

	private void drawDemoModeButtons(bool canRun)
	{
		if (Button.doButton(1, 180, 200, 450, 50, LocaleTerms.Loc("New Session"), buttonColor) && canRun)
		{
			try
			{
				ExitScreen();
				resetOS();
				base.ScreenManager.playAlertSound();
				base.ScreenManager.AddScreen(new OS(), base.ScreenManager.controllingPlayer);
			}
			catch (Exception ex)
			{
				Utils.writeToFile(LocaleTerms.Loc("OS Load Error") + ": " + ex.ToString() + "\n\n" + ex.StackTrace, "crashLog.txt");
			}
		}
		if (Button.doButton(11, 180, 265, 450, 50, LocaleTerms.Loc("Load Session"), canLoad ? buttonColor : Color.Black) && canLoad)
		{
			try
			{
				if (canRun)
				{
					ExitScreen();
					resetOS();
					OS.WillLoadSave = true;
					OS screen = new OS();
					base.ScreenManager.AddScreen(screen, base.ScreenManager.controllingPlayer);
				}
			}
			catch (Exception ex)
			{
				Utils.writeToFile(LocaleTerms.Loc("OS Load Error") + ": " + ex.ToString() + "\n\n" + ex.StackTrace, "crashLog.txt");
			}
		}
		if (Button.doButton(15, 180, 330, 450, 28, LocaleTerms.Loc("Exit"), exitButtonColor))
		{
			MusicManager.stop();
			Game1.threadsExiting = true;
			Game1.getSingleton().Exit();
		}
	}

	private void drawTestingMainMenuButtons(bool canRun)
	{
		SpriteFont tinyfont = GuiData.tinyfont;
		string text = "FONT:";
		for (int i = 0; i < tinyfont.Characters.Count; i++)
		{
			text += tinyfont.Characters[i];
			if (i % 20 == 0)
			{
				text += "\n";
			}
		}
		if (true)
		{
			text = "Labyrinths Testers:\nPress \"Start Full DLC Test\" to begin\n\n" + text;
		}
		GuiData.spriteBatch.DrawString(tinyfont, text, new Vector2(867f, 200f), Color.White);
		if (Button.doButton(8801, 634, 200, 225, 23, "New Test Session", buttonColor) && canRun && canRun)
		{
			ExitScreen();
			resetOS();
			if (!Settings.soundDisabled)
			{
				base.ScreenManager.playAlertSound();
			}
			OS os = new OS();
			os.SaveGameUserName = "save_--test.xml";
			os.SaveUserAccountName = "__test";
			base.ScreenManager.AddScreen(os, base.ScreenManager.controllingPlayer);
			os.Flags.AddFlag("TutorialComplete");
			os.delayer.RunAllDelayedActions();
			os.threadedSaveExecute();
			base.ScreenManager.RemoveScreen(os);
			OS.WillLoadSave = true;
			resetOS();
			os = new OS();
			os.SaveGameUserName = "save_--test.xml";
			os.SaveUserAccountName = "__test";
			base.ScreenManager.AddScreen(os, base.ScreenManager.controllingPlayer);
			os.delayer.Post(ActionDelayer.Wait(0.1), delegate
			{
				Game1.getSingleton().IsMouseVisible = true;
			});
			os.delayer.Post(ActionDelayer.Wait(0.4), delegate
			{
				os.runCommand("debug");
				ComputerLoader.loadMission("Content/Missions/MainHub/Intro/Intro01.xml");
			});
			if (!Settings.EnableDLC)
			{
				ComputerLoader.loadMission("Content/Missions/BitMission0.xml");
			}
		}
		if (Button.doButton(8803, 634, 225, 225, 23, "New DLC Test Session", Settings.EnableDLC ? Color.Gray : buttonColor) && canRun && canRun)
		{
			ExitScreen();
			resetOS();
			if (!Settings.soundDisabled)
			{
				base.ScreenManager.playAlertSound();
			}
			OS oS = new OS();
			oS.SaveGameUserName = "save_--test.xml";
			oS.SaveUserAccountName = "__test";
			base.ScreenManager.AddScreen(oS, base.ScreenManager.controllingPlayer);
			SessionAccelerator.AccelerateSessionToDLCHA(oS);
			oS.threadedSaveExecute();
			base.ScreenManager.RemoveScreen(oS);
			OS.WillLoadSave = true;
			resetOS();
			Settings.initShowsTutorial = false;
			oS = new OS();
			oS.SaveGameUserName = "save_--test.xml";
			oS.SaveUserAccountName = "__test";
			base.ScreenManager.AddScreen(oS, base.ScreenManager.controllingPlayer);
			oS.delayer.Post(ActionDelayer.Wait(0.15), delegate
			{
				Game1.getSingleton().IsMouseVisible = true;
			});
		}
		if (Button.doButton(8806, 634, 250, 225, 23, "Run Test Suite", buttonColor))
		{
			testSuiteResult = TestSuite.RunTestSuite(base.ScreenManager);
		}
		if (Button.doButton(8809, 634, 275, 225, 23, "Run Quick Tests", buttonColor))
		{
			testSuiteResult = TestSuite.RunTestSuite(base.ScreenManager, IsQuickTestMode: true);
			return;
		}
		if (Button.doButton(8812, 634, 300, 225, 23, "Start Full DLC Test", buttonColor) && canRun)
		{
			StartFullDLCTest();
		}
		if (testSuiteResult != null)
		{
			TextItem.doFontLabel(new Vector2(635f, 325f), Utils.SuperSmartTwimForWidth(testSuiteResult, 600, GuiData.tinyfont), GuiData.tinyfont, (testSuiteResult.Length > 950) ? Utils.AddativeRed : Utils.AddativeWhite);
		}
	}

	private void StartFullDLCTest()
	{
		ExitScreen();
		resetOS();
		if (!Settings.soundDisabled)
		{
			base.ScreenManager.playAlertSound();
		}
		OS oS = new OS();
		string text = "MediaPreview";
		SaveFileManager.AddUser(text, "test");
		string saveFileNameForUsername = SaveFileManager.GetSaveFileNameForUsername(text);
		oS.IsDLCConventionDemo = true;
		oS.Flags.AddFlag("TutorialComplete");
		Settings.EnableDLC = true;
		Settings.initShowsTutorial = false;
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text;
		oS.initShowsTutorial = false;
		base.ScreenManager.AddScreen(oS, base.ScreenManager.controllingPlayer);
		SessionAccelerator.AccelerateSessionToDLCStart(oS);
		oS.delayer.Post(ActionDelayer.Wait(0.15), delegate
		{
			Game1.getSingleton().IsMouseVisible = true;
		});
	}

	private void drawMainMenuButtons(bool canRun)
	{
		int num = 135;
		if (Button.doButton(1, 180, num += 65, 450, 50, LocaleTerms.Loc("New Session"), buttonColor) && canRun)
		{
			NextStartedGameShouldBeDLCAccelerated = false;
			State = MainMenuState.NewUser;
			loginScreen.ClearTextBox();
			loginScreen.ResetForNewAccount();
		}
		bool hasSaves = SaveFileManager.HasSaves;
		string text = LocaleTerms.Loc("No Accounts");
		if (hasSaves)
		{
			text = ((!canLoad) ? LocaleTerms.Loc("Invalid Last Account : Login Manually") : string.Format(LocaleTerms.Loc("Continue with account [{0}]"), SaveFileManager.LastLoggedInUser.Username));
		}
		if (Button.doButton(1102, 180, num += 65, 450, 28, text, canLoad ? buttonColor : Color.Black) && canLoad)
		{
			loginScreen.ClearTextBox();
			loginScreen.LoadGameForUserFileAndUsername(SaveFileManager.LastLoggedInUser.FileUsername, SaveFileManager.LastLoggedInUser.Username);
		}
		if (Button.doButton(11, 180, num += 39, 450, 50, LocaleTerms.Loc("Login"), hasSaves ? buttonColor : Color.Black) && hasSaves)
		{
			try
			{
				State = MainMenuState.Login;
				loginScreen.ClearTextBox();
				loginScreen.ResetForLogin();
			}
			catch (Exception ex)
			{
				Utils.writeToFile(LocaleTerms.Loc("OS Load Error") + ": " + ex.ToString() + "\n\n" + ex.StackTrace, "crashLog.txt");
			}
		}
		if (Button.doButton(3, 180, num += 65, 450, 50, LocaleTerms.Loc("Settings"), buttonColor))
		{
			base.ScreenManager.AddScreen(new OptionsMenu(), base.ScreenManager.controllingPlayer);
		}
		int num2 = (num += 65);
		if (Settings.isServerMode)
		{
			if (Button.doButton(4, 180, num2, 450, 50, "Start Relay Server", buttonColor))
			{
				base.ScreenManager.AddScreen(new ServerScreen(), base.ScreenManager.controllingPlayer);
			}
			num2 += 65;
		}
		if (Settings.AllowExtensionMode)
		{
			if (Button.doButton(5, 180, num2, 450, 50, "Extensions", buttonColor))
			{
				State = MainMenuState.Extensions;
				extensionsScreen.Reset();
			}
			num2 += 65;
		}
		if (Settings.HasLabyrinthsDemoStartMainMenuButton && DLC1SessionUpgrader.HasDLC1Installed)
		{
			if (Button.doButton(7, 180, num2, 450, 28, "New Labyrinths Accelerated Session", Color.Lerp(Utils.AddativeWhite, new Color(68, 162, 194), 1f - Utils.rand(0.3f))) && canRun)
			{
				NextStartedGameShouldBeDLCAccelerated = true;
				State = MainMenuState.NewUser;
				loginScreen.ClearTextBox();
				loginScreen.ResetForNewAccount();
			}
			num2 += 65;
		}
		if (Button.doButton(15, 180, num2, 450, 28, LocaleTerms.Loc("Exit"), exitButtonColor))
		{
			MusicManager.stop();
			Game1.threadsExiting = true;
			Game1.getSingleton().Exit();
		}
		num2 += 30;
		if (!PlatformAPISettings.RemoteStorageRunning)
		{
			TextItem.doFontLabel(new Vector2(180f, num2), LocaleTerms.Loc("WARNING: Error connecting to Steam Cloud"), GuiData.smallfont, Color.DarkRed);
			num2 += 20;
		}
		if (!string.IsNullOrWhiteSpace(AccumErrors))
		{
			TextItem.doFontLabel(new Vector2(180f, num2), AccumErrors, GuiData.smallfont, Color.DarkRed);
			num2 += 20;
		}
	}
}
