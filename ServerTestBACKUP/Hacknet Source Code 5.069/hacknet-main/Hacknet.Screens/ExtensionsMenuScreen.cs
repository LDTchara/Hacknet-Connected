using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Gui;
using Hacknet.Localization;
using Hacknet.Misc;
using Hacknet.PlatformAPI.Storage;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Steamworks;

namespace Hacknet.Screens;

public class ExtensionsMenuScreen
{
	private enum EMSState
	{
		Normal,
		GetUsername,
		ShowAccounts
	}

	public const string ExtensionBaseFolder = "Extensions/";

	public List<ExtensionInfo> Extensions = new List<ExtensionInfo>();

	private bool HasLoaded = false;

	private bool HasStartedLoading = false;

	private ExtensionInfo ExtensionInfoToShow = null;

	private Texture2D DefaultModImage = null;

	private SavefileLoginScreen SaveScreen = new SavefileLoginScreen();

	private SteamWorkshopPublishScreen workshopPublishScreen = null;

	private bool IsInPublishScreen = false;

	private string ReportOverride = null;

	public Action ExitClicked;

	public Action<string, string> CreateNewAccountForExtension_UserAndPass;

	public Action<string, string> LoadAccountForExtension_FileAndUsername;

	private EMSState State = EMSState.Normal;

	public string LoadErrors = "";

	public int ScrollStartIndex = 0;

	public ExtensionsMenuScreen()
	{
		SavefileLoginScreen saveScreen = SaveScreen;
		Action<string, string> startNewGameForUsernameAndPass = saveScreen.StartNewGameForUsernameAndPass;
		Action<string, string> b = delegate(string username, string pass)
		{
			OS.WillLoadSave = false;
			if (CreateNewAccountForExtension_UserAndPass != null)
			{
				CreateNewAccountForExtension_UserAndPass(username, pass);
			}
		};
		saveScreen.StartNewGameForUsernameAndPass = (Action<string, string>)Delegate.Combine(startNewGameForUsernameAndPass, b);
		SavefileLoginScreen saveScreen2 = SaveScreen;
		saveScreen2.LoadGameForUserFileAndUsername = (Action<string, string>)Delegate.Combine(saveScreen2.LoadGameForUserFileAndUsername, (Action<string, string>)delegate(string filename, string username)
		{
			OS.WillLoadSave = true;
			if (LoadAccountForExtension_FileAndUsername != null)
			{
				LoadAccountForExtension_FileAndUsername(filename, username);
			}
		});
		SavefileLoginScreen saveScreen3 = SaveScreen;
		saveScreen3.RequestGoBack = (Action)Delegate.Combine(saveScreen3.RequestGoBack, (Action)delegate
		{
			State = EMSState.Normal;
		});
		SaveScreen.DrawFromTop = true;
		if (Settings.AllowExtensionPublish)
		{
			workshopPublishScreen = new SteamWorkshopPublishScreen(Game1.getSingleton().Content);
			SteamWorkshopPublishScreen steamWorkshopPublishScreen = workshopPublishScreen;
			steamWorkshopPublishScreen.GoBack = (Action)Delegate.Combine(steamWorkshopPublishScreen.GoBack, (Action)delegate
			{
				IsInPublishScreen = false;
			});
		}
	}

	private void LoadExtensions()
	{
		if (!Directory.Exists("Extensions/"))
		{
			Directory.CreateDirectory("Extensions/");
		}
		string[] directories = Directory.GetDirectories("Extensions/");
		for (int i = 0; i < directories.Length; i++)
		{
			AddExtensionSafe(directories[i]);
		}
		DefaultModImage = Game1.getSingleton().Content.Load<Texture2D>("Sprites/Hammer");
		if (PlatformAPISettings.Running)
		{
			PublishedFileId_t[] array = new PublishedFileId_t[200];
			uint subscribedItems = SteamUGC.GetSubscribedItems(array, 200u);
			Console.WriteLine(subscribedItems);
			if (array != null)
			{
				for (int i = 0; i < subscribedItems; i++)
				{
					uint itemState = SteamUGC.GetItemState(array[i]);
					EItemState eItemState = (EItemState)Enum.Parse(typeof(EItemState), string.Concat(itemState));
					if (eItemState.HasFlag(EItemState.k_EItemStateInstalled))
					{
						ulong punSizeOnDisk = 0uL;
						string pchFolder = null;
						uint cchFolderSize = 20000u;
						uint punTimeStamp = 0u;
						Console.WriteLine((SteamUGC.GetItemInstallInfo(array[i], out punSizeOnDisk, out pchFolder, cchFolderSize, out punTimeStamp) ? "Installed" : "Uninstalled") + " Extension at: " + pchFolder);
						AddExtensionSafe(pchFolder);
					}
				}
			}
		}
		HasLoaded = true;
	}

	public void Reset()
	{
		State = EMSState.Normal;
		ScrollStartIndex = 0;
	}

	private void AddExtensionSafe(string folderpath)
	{
		if (ExtensionInfo.ExtensionExists(folderpath))
		{
			try
			{
				Extensions.Add(ExtensionInfo.ReadExtensionInfo(folderpath));
			}
			catch (Exception ex)
			{
				Console.WriteLine(Utils.GenerateReportFromException(ex));
				string loadErrors = LoadErrors;
				LoadErrors = loadErrors + LocaleTerms.Loc("Error loading ExtensionInfo for") + " " + folderpath + "\n" + LocaleTerms.Loc("Error details written to") + " " + folderpath.Replace("\\", "/") + "/error_report.txt\n\n";
				string data = "Error loading ExtensionInfo for extension in folder:\n" + folderpath + "\nError details: \n" + Utils.GenerateReportFromException(ex);
				Utils.writeToFile(data, folderpath + "/error_report.txt");
			}
		}
	}

	private void EnsureHasLoaded()
	{
		if (!HasStartedLoading)
		{
			Thread thread = new Thread(LoadExtensions);
			thread.IsBackground = true;
			thread.Start();
			HasStartedLoading = true;
		}
	}

	public void ShowError(string error)
	{
		ReportOverride = error;
		State = EMSState.Normal;
	}

	private void ActivateExtensionPage(ExtensionInfo info)
	{
		LocaleActivator.ActivateLocale(info.Language, Game1.getSingleton().Content);
		ExtensionInfoToShow = info;
		ReportOverride = null;
		SaveScreen.ProjectName = info.Name;
		Settings.IsInExtensionMode = true;
		ExtensionLoader.ActiveExtensionInfo = info;
		SaveFileManager.Init();
		SaveScreen.ResetForNewAccount();
	}

	private void ExitExtensionsScreen()
	{
		ExtensionInfoToShow = null;
		Settings.IsInExtensionMode = false;
		SaveFileManager.Init();
		if (ExitClicked != null)
		{
			ExitClicked();
		}
	}

	public void Draw(Rectangle dest, SpriteBatch sb, ScreenManager screenman)
	{
		EnsureHasLoaded();
		Rectangle rectangle = new Rectangle(dest.X, dest.Y, dest.Width, 40);
		Color color = Color.Lerp(Utils.AddativeWhite, Utils.AddativeRed, 0.3f + Utils.randm(0.22f));
		SpriteFont font = GuiData.font;
		Vector2 vector = new Vector2(dest.X, (float)dest.Y + 44f);
		string original = "E X T E N S I O N S";
		GuiData.spriteBatch.DrawString(font, Utils.FlipRandomChars(original, 0.017999999225139618), vector, Utils.AddativeWhite * (0.17f + Utils.randm(0.04f)));
		GuiData.spriteBatch.DrawString(font, Utils.FlipRandomChars(original, 0.004000000189989805), vector, color * (0.8f + Utils.randm(0.04f)));
		vector.Y += 35f;
		if (ExtensionInfoToShow == null)
		{
			vector = DrawExtensionList(vector, dest, sb);
		}
		else
		{
			switch (State)
			{
			default:
				vector = DrawExtensionInfoDetail(vector, dest, sb, screenman, ExtensionInfoToShow);
				break;
			case EMSState.GetUsername:
			case EMSState.ShowAccounts:
				vector = DrawExtensionCreateNewUserOrLoadScreen(dest: new Rectangle(dest.X, dest.Y + dest.Height / 4, dest.Width, (int)((float)dest.Height * 0.8f)), drawpos: vector, sb: sb, screenMan: screenman, info: ExtensionInfoToShow);
				break;
			}
		}
		if (Button.doButton(391481, (int)vector.X, (int)vector.Y, 450, 25, LocaleTerms.Loc("Return to Main Menu"), MainMenu.exitButtonColor))
		{
			ExitExtensionsScreen();
		}
	}

	private Vector2 DrawExtensionCreateNewUserOrLoadScreen(Vector2 drawpos, Rectangle dest, SpriteBatch sb, ScreenManager screenMan, ExtensionInfo info)
	{
		SaveScreen.Draw(sb, dest);
		return drawpos;
	}

	private Vector2 DrawExtensionInfoDetail(Vector2 drawpos, Rectangle dest, SpriteBatch sb, ScreenManager screenMan, ExtensionInfo info)
	{
		sb.DrawString(GuiData.titlefont, info.Name.ToUpper(), drawpos, Utils.AddativeWhite * 0.66f);
		drawpos.Y += 80f;
		int height = sb.GraphicsDevice.Viewport.Height;
		int num = 256;
		if (height < 900)
		{
			num = 120;
		}
		Rectangle dest2 = new Rectangle((int)drawpos.X, (int)drawpos.Y, num, num);
		Texture2D texture = DefaultModImage;
		if (info.LogoImage != null)
		{
			texture = info.LogoImage;
		}
		FlickeringTextEffect.DrawFlickeringSprite(sb, dest2, texture, 2f, 0.5f, null, Color.White);
		Vector2 position = drawpos + new Vector2((float)num + 40f, 20f);
		float num2 = (float)dest.Width - (drawpos.X - (float)dest.X);
		string description = info.Description;
		string text = Utils.SuperSmartTwimForWidth(description, (int)num2, GuiData.smallfont);
		sb.DrawString(GuiData.smallfont, text, position, Utils.AddativeWhite * 0.7f);
		drawpos.Y += (float)num + 10f;
		if (IsInPublishScreen)
		{
			Rectangle fullscreen = Utils.GetFullscreen();
			Rectangle dest3 = new Rectangle((int)drawpos.X, (int)drawpos.Y, fullscreen.Width - (int)drawpos.X * 2, fullscreen.Height - ((int)drawpos.Y + 50));
			return workshopPublishScreen.Draw(sb, dest3, info);
		}
		if (ReportOverride != null)
		{
			string text2 = Utils.SuperSmartTwimForWidth(ReportOverride, 800, GuiData.smallfont);
			sb.DrawString(GuiData.smallfont, text2, drawpos + new Vector2(460f, 0f), (ReportOverride.Length > 250) ? Utils.AddativeRed : Utils.AddativeWhite);
		}
		int val = 40;
		int num3 = 5;
		int num4 = (info.AllowSave ? 4 : 2);
		int num5 = height - (int)drawpos.Y - 55;
		val = Math.Min(val, (num5 - num4 * num3) / num4);
		if (Button.doButton(7900010, (int)drawpos.X, (int)drawpos.Y, 450, val, string.Format(LocaleTerms.Loc("New {0} Account"), info.Name), MainMenu.buttonColor))
		{
			State = EMSState.GetUsername;
			SaveScreen.ResetForNewAccount();
		}
		drawpos.Y += val + num3;
		if (info.AllowSave)
		{
			bool flag = !string.IsNullOrWhiteSpace(SaveFileManager.LastLoggedInUser.FileUsername);
			if (Button.doButton(7900019, (int)drawpos.X, (int)drawpos.Y, 450, val, flag ? ("Continue Account : " + SaveFileManager.LastLoggedInUser.Username) : " - No Accounts - ", flag ? MainMenu.buttonColor : Color.Black))
			{
				OS.WillLoadSave = true;
				if (LoadAccountForExtension_FileAndUsername != null)
				{
					LoadAccountForExtension_FileAndUsername(SaveFileManager.LastLoggedInUser.FileUsername, SaveFileManager.LastLoggedInUser.Username);
				}
			}
			drawpos.Y += val + num3;
			if (Button.doButton(7900020, (int)drawpos.X, (int)drawpos.Y, 450, val, LocaleTerms.Loc("Login") + "...", flag ? MainMenu.buttonColor : Color.Black))
			{
				State = EMSState.ShowAccounts;
				SaveScreen.ResetForLogin();
			}
			drawpos.Y += val + num3;
		}
		if (Button.doButton(7900030, (int)drawpos.X, (int)drawpos.Y, 450, val, LocaleTerms.Loc("Run Verification Tests"), MainMenu.buttonColor))
		{
			int errorsAdded = 0;
			string text3 = ExtensionTests.TestExtensionForRuntime(screenMan, info.FolderPath, out errorsAdded);
			try
			{
				ExtensionInfo.VerifyExtensionInfo(info);
			}
			catch (Exception ex)
			{
				text3 = text3 + "\nExtension Metadata Error:\n" + Utils.GenerateReportFromException(ex);
				errorsAdded++;
			}
			if (errorsAdded == 0)
			{
				ReportOverride = LocaleTerms.Loc("Testing...") + "\n" + LocaleTerms.Loc("All tests complete") + "\n0 " + LocaleTerms.Loc("Errors Found");
			}
			else
			{
				ReportOverride = LocaleTerms.Loc("Errors Found") + ". " + string.Format(LocaleTerms.Loc("Writing report to {0}"), (info.FolderPath + "/report.txt\n").Replace("\\", "/")) + text3;
				string text4 = info.FolderPath + "/report.txt";
				if (File.Exists(text4))
				{
					File.Delete(text4);
				}
				Utils.writeToFile(ReportOverride, text4);
			}
			MusicManager.transitionToSong("Music/Ambient/AmbientDrone_Clipped");
			ExtensionLoader.ActiveExtensionInfo = ExtensionInfoToShow;
		}
		drawpos.Y += val + num3;
		if (Settings.AllowExtensionPublish && PlatformAPISettings.Running)
		{
			if (Button.doButton(7900031, (int)drawpos.X, (int)drawpos.Y, 450, val, LocaleTerms.Loc("Steam Workshop Publishing"), MainMenu.buttonColor))
			{
				IsInPublishScreen = true;
			}
			drawpos.Y += val + num3;
		}
		if (Button.doButton(7900040, (int)drawpos.X, (int)drawpos.Y, 450, 25, LocaleTerms.Loc("Back to Extension List"), MainMenu.exitButtonColor))
		{
			ExtensionInfoToShow = null;
		}
		drawpos.Y += 30f;
		return drawpos;
	}

	private Vector2 DrawExtensionList(Vector2 drawpos, Rectangle dest, SpriteBatch sb)
	{
		if (HasLoaded)
		{
			Rectangle fullscreen = Utils.GetFullscreen();
			for (int i = ScrollStartIndex; i <= Extensions.Count; i++)
			{
				if (drawpos.Y + 120f + 20f >= (float)fullscreen.Height || (ScrollStartIndex > 0 && i == Extensions.Count))
				{
					int num = 20;
					int num2 = (50 - num * 2) / 4;
					if (Button.doButton(790001 + i, (int)drawpos.X, (int)drawpos.Y + num2, 450, num, "   ^   ", (ScrollStartIndex > 0) ? MainMenu.buttonColor : Color.Black) && ScrollStartIndex > 0)
					{
						ScrollStartIndex--;
					}
					bool flag = i <= Extensions.Count - 1;
					if (Button.doButton(790101 + i + 1, (int)drawpos.X, (int)drawpos.Y + num + num2 + num2 + 2, 450, num, "   v   ", flag ? MainMenu.buttonColor : Color.Black) && flag)
					{
						ScrollStartIndex++;
					}
					drawpos.Y += 55f;
					break;
				}
				if (i < Extensions.Count)
				{
					ExtensionInfo extensionInfo = Extensions[i];
					if (Button.doButton(780001 + i, (int)drawpos.X, (int)drawpos.Y, 450, 50, extensionInfo.Name, Color.White))
					{
						ActivateExtensionPage(extensionInfo);
					}
					drawpos.Y += 55f;
				}
			}
		}
		else
		{
			TextItem.doFontLabel(drawpos, LocaleTerms.Loc("Loading..."), GuiData.font, Color.White, dest.Width, 20f);
			drawpos.Y += 55f;
		}
		if (!string.IsNullOrWhiteSpace(LoadErrors))
		{
			TextItem.doFontLabel(drawpos + new Vector2(0f, 30f), LoadErrors, GuiData.smallfont, Color.Red);
		}
		return drawpos;
	}
}
