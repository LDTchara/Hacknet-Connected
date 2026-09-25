using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hacknet.Effects;
using Hacknet.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public static class ThemeManager
{
	private static string CustomThemeIDSeperator = "___";

	private static Texture2D backgroundImage;

	private static Texture2D lastLoadedCustomBackground;

	private static string customBackgroundImageLoadPath = null;

	private static bool backgroundNeedsDisposal = false;

	public static OSTheme currentTheme;

	public static CustomTheme LastLoadedCustomTheme = null;

	public static string LastLoadedCustomThemePath = null;

	private static HexGridBackground hexGrid;

	private static Dictionary<OSTheme, string> fileData;

	private static int webWidth;

	private static int webHeight;

	private static int framesTillWebUpdate = -1;

	private static bool HasNeverSwappedThemeBefore = true;

	public static void init(ContentManager content)
	{
		fileData = new Dictionary<OSTheme, string>();
		UTF8Encoding uTF8Encoding = new UTF8Encoding();
		hexGrid = new HexGridBackground(content);
		hexGrid.HexScale = 0.12f;
		foreach (OSTheme value in Enum.GetValues(typeof(OSTheme)))
		{
			byte[] bytes = uTF8Encoding.GetBytes(value.ToString() + value.GetHashCode());
			string text = "";
			for (int i = 0; i < bytes.Length; i++)
			{
				text += bytes[i];
			}
			fileData.Add(value, text);
		}
	}

	public static void Update(float dt)
	{
		if (hexGrid != null)
		{
			hexGrid.Update(dt * 0.4f);
			hexGrid.HexScale = 0.2f;
		}
		if (framesTillWebUpdate < 0)
		{
			return;
		}
		framesTillWebUpdate--;
		if (framesTillWebUpdate == -1)
		{
			if (OS.currentInstance.connectedComp != null && OS.currentInstance.connectedComp.getDaemon(typeof(WebServerDaemon)) != null)
			{
				WebRenderer.setSize(webWidth, webHeight);
			}
			else
			{
				framesTillWebUpdate = 0;
			}
		}
	}

	public static void switchTheme(object osObject, string customThemePath)
	{
		string text = "Content/";
		if (Settings.IsInExtensionMode)
		{
			text = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
		}
		if (File.Exists(text + customThemePath))
		{
			LastLoadedCustomTheme = CustomTheme.Deserialize(text + customThemePath);
			LastLoadedCustomThemePath = customThemePath;
		}
		else if (File.Exists("Content/" + customThemePath))
		{
			text = "Content/";
			LastLoadedCustomTheme = CustomTheme.Deserialize(text + customThemePath);
		}
		switchTheme(osObject, OSTheme.Custom);
	}

	public static void switchTheme(object osObject, OSTheme theme)
	{
		OS oS = (OS)osObject;
		if (theme == OSTheme.Custom)
		{
			string text = customBackgroundImageLoadPath;
			switchTheme(oS, OSTheme.HacknetBlue);
			customBackgroundImageLoadPath = text;
			switchThemeLayout(oS, LastLoadedCustomTheme.GetThemeForLayout());
			loadCustomThemeBackground(oS, LastLoadedCustomTheme.backgroundImagePath);
			LastLoadedCustomTheme.LoadIntoOS(oS);
		}
		else
		{
			switchThemeColors(oS, theme);
			loadThemeBackground(oS, theme);
			switchThemeLayout(oS, theme);
		}
		currentTheme = theme;
		oS.RefreshTheme();
	}

	private static void switchThemeLayout(OS os, OSTheme theme)
	{
		int width = os.ScreenManager.GraphicsDevice.Viewport.Width;
		int height = os.ScreenManager.GraphicsDevice.Viewport.Height;
		int width2 = os.display.bounds.Width;
		int height2 = os.display.bounds.Height;
		switch (theme)
		{
		case OSTheme.HacknetWhite:
		{
			int num = 205;
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.44420000000000004);
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.5558);
			int height3 = height - num - OS.TOP_BAR_HEIGHT - 6;
			os.terminal.Bounds = new Rectangle(width - num3 - 2, OS.TOP_BAR_HEIGHT, num3, height3);
			os.netMap.Bounds = new Rectangle(RamModule.MODULE_WIDTH + 4 + num4 + 4, height - num - 2, os.terminal.bounds.Width - 4, num);
			os.display.Bounds = new Rectangle(RamModule.MODULE_WIDTH + 4, OS.TOP_BAR_HEIGHT, num4, height - OS.TOP_BAR_HEIGHT - 2);
			os.ram.Bounds = new Rectangle(2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		default:
		{
			int num = 205;
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.44420000000000004);
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.5558) + 1;
			int num5 = height - num - OS.TOP_BAR_HEIGHT - 6;
			if (theme == OSTheme.HacknetPurple)
			{
				num5 += 2;
				num4++;
			}
			os.terminal.Bounds = new Rectangle(width - num3 - 2, OS.TOP_BAR_HEIGHT, num3, height - OS.TOP_BAR_HEIGHT - 2);
			os.netMap.Bounds = new Rectangle(RamModule.MODULE_WIDTH + 4, height - num - 2, num4 - 2, num);
			os.display.Bounds = new Rectangle(RamModule.MODULE_WIDTH + 4, OS.TOP_BAR_HEIGHT, num4 - 2, num5);
			os.ram.Bounds = new Rectangle(2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		case OSTheme.HacknetMint:
		{
			int num = 205;
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.5058);
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.4942);
			int height3 = height - num - OS.TOP_BAR_HEIGHT - 6;
			os.terminal.Bounds = new Rectangle(RamModule.MODULE_WIDTH + 4, OS.TOP_BAR_HEIGHT, num3, height3);
			os.netMap.Bounds = new Rectangle(RamModule.MODULE_WIDTH + 4, height - num - 2, num3 - 1, num);
			os.display.Bounds = new Rectangle(width - num4 - 2, OS.TOP_BAR_HEIGHT, num4 - 1, height - OS.TOP_BAR_HEIGHT - 2);
			os.ram.Bounds = new Rectangle(2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		case OSTheme.HackerGreen:
		{
			int num = 205;
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.44420000000000004);
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.5558);
			int num5 = height - num - OS.TOP_BAR_HEIGHT - 6;
			os.terminal.Bounds = new Rectangle(width - num3 - RamModule.MODULE_WIDTH - 4, OS.TOP_BAR_HEIGHT, num3, height - OS.TOP_BAR_HEIGHT - 2);
			os.netMap.Bounds = new Rectangle(2, height - num - 2, num4 - 1, num);
			os.display.Bounds = new Rectangle(2, OS.TOP_BAR_HEIGHT, num4 - 2, num5);
			os.ram.Bounds = new Rectangle(width - RamModule.MODULE_WIDTH - 2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		case OSTheme.GreenCompact:
		{
			int num = 205;
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.44420000000000004);
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * 0.5558);
			int num5 = height - num - OS.TOP_BAR_HEIGHT - 3;
			os.terminal.Bounds = new Rectangle(width - num3 - RamModule.MODULE_WIDTH - 4 - 2, OS.TOP_BAR_HEIGHT, num3 + 3, height - OS.TOP_BAR_HEIGHT - 2);
			os.netMap.Bounds = new Rectangle(1, height - num - 2, num4 - 1, num);
			os.display.Bounds = new Rectangle(1, OS.TOP_BAR_HEIGHT, num4 - 1, num5);
			os.ram.Bounds = new Rectangle(width - RamModule.MODULE_WIDTH - 2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		case OSTheme.TerminalOnlyBlack:
		{
			Rectangle bounds = new Rectangle(-100000, -100000, 16, 16);
			os.terminal.bounds = new Rectangle(0, 0, width, height);
			os.netMap.bounds = bounds;
			os.display.bounds = bounds;
			os.ram.bounds = bounds;
			break;
		}
		case OSTheme.Riptide:
		{
			int num = (int)((double)height * 0.33);
			double num2 = 0.51;
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * (1.0 - num2)) - 2;
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * num2);
			int num5 = height - num - OS.TOP_BAR_HEIGHT - 6 + 2;
			os.terminal.Bounds = new Rectangle(width - num3 - RamModule.MODULE_WIDTH - 6, OS.TOP_BAR_HEIGHT, num3 + 2, height - OS.TOP_BAR_HEIGHT - 2);
			os.netMap.Bounds = new Rectangle(2, OS.TOP_BAR_HEIGHT, num4, num);
			os.display.Bounds = new Rectangle(2, os.netMap.bounds.Y + os.netMap.bounds.Height + 2, num4, num5);
			os.ram.Bounds = new Rectangle(width - RamModule.MODULE_WIDTH - 2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		case OSTheme.Colamaeleon:
		{
			int num = (int)((double)height * 0.33);
			double num2 = 0.4;
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * (1.0 - num2)) - 6;
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * num2);
			int num5 = height - num - OS.TOP_BAR_HEIGHT - 6 + 1;
			os.terminal.Bounds = new Rectangle(width - num3 - RamModule.MODULE_WIDTH - 4, OS.TOP_BAR_HEIGHT, num3, height - OS.TOP_BAR_HEIGHT - 2);
			os.netMap.Bounds = new Rectangle(2, OS.TOP_BAR_HEIGHT, num4, num);
			os.display.Bounds = new Rectangle(2, os.netMap.bounds.Y + os.netMap.bounds.Height + 3, num4, num5);
			os.ram.Bounds = new Rectangle(width - RamModule.MODULE_WIDTH - 2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		case OSTheme.Riptide2:
		{
			int num = (int)((double)height * 0.33);
			double num2 = 0.55;
			int num3 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * (1.0 - num2)) - 6;
			int num4 = (int)((double)(width - RamModule.MODULE_WIDTH - 6) * num2);
			int num5 = height - num - OS.TOP_BAR_HEIGHT - 6 + 1;
			int x = width - num3 - RamModule.MODULE_WIDTH - 4;
			os.display.Bounds = new Rectangle(2, OS.TOP_BAR_HEIGHT, num4 + 4, height - OS.TOP_BAR_HEIGHT - 2);
			os.netMap.Bounds = new Rectangle(x, OS.TOP_BAR_HEIGHT, num3, num);
			os.terminal.Bounds = new Rectangle(x, os.netMap.bounds.Y + os.netMap.bounds.Height, num3, num5 + 3);
			os.ram.Bounds = new Rectangle(width - RamModule.MODULE_WIDTH - 2, OS.TOP_BAR_HEIGHT, RamModule.MODULE_WIDTH, os.totalRam + RamModule.contentStartOffset);
			break;
		}
		}
		if (HasNeverSwappedThemeBefore || os.display.bounds.Width != width2 || os.display.bounds.Height != height2)
		{
			webWidth = os.display.bounds.Width;
			webHeight = os.display.bounds.Height;
			framesTillWebUpdate = 600;
			HasNeverSwappedThemeBefore = false;
		}
	}

	internal static void loadCustomThemeBackground(OS os, string imagePathName)
	{
		if (imagePathName == customBackgroundImageLoadPath)
		{
			backgroundImage = lastLoadedCustomBackground;
			return;
		}
		if (backgroundNeedsDisposal)
		{
			Texture2D oldBackground = backgroundImage;
			os.delayer.Post(ActionDelayer.NextTick(), delegate
			{
				oldBackground.Dispose();
			});
		}
		if (string.IsNullOrWhiteSpace(imagePathName))
		{
			backgroundImage = null;
			return;
		}
		string path = Utils.GetFileLoadPrefix() + imagePathName;
		if (!File.Exists(path))
		{
			path = "Content/" + imagePathName;
		}
		if (File.Exists(path))
		{
			try
			{
				using (FileStream stream = File.OpenRead(path))
				{
					backgroundImage = Texture2D.FromStream(GuiData.spriteBatch.GraphicsDevice, stream);
					backgroundNeedsDisposal = true;
				}
				lastLoadedCustomBackground = backgroundImage;
				customBackgroundImageLoadPath = imagePathName;
				return;
			}
			catch (Exception)
			{
				backgroundImage = null;
				return;
			}
		}
		backgroundImage = null;
	}

	internal static void loadThemeBackground(OS os, OSTheme theme)
	{
		switch (theme)
		{
		default:
			backgroundImage = os.content.Load<Texture2D>("Themes/Razzamataz2");
			break;
		case OSTheme.HacknetTeal:
			backgroundImage = os.content.Load<Texture2D>("Themes/AbstractWaves");
			break;
		case OSTheme.HacknetYellow:
			backgroundImage = os.content.Load<Texture2D>("Themes/DarkenedAway");
			break;
		case OSTheme.HackerGreen:
			backgroundImage = os.content.Load<Texture2D>("Themes/GreenAbstract");
			break;
		case OSTheme.HacknetWhite:
			backgroundImage = os.content.Load<Texture2D>("Themes/AwayTooLong");
			break;
		case OSTheme.TerminalOnlyBlack:
			backgroundImage = os.content.Load<Texture2D>("Themes/NoThemeWallpaper");
			break;
		case OSTheme.HacknetPurple:
			backgroundImage = os.content.Load<Texture2D>("Themes/BlueCirclesBackground");
			break;
		case OSTheme.HacknetMint:
			backgroundImage = os.content.Load<Texture2D>("Themes/WaterWashGreen2");
			break;
		}
		backgroundNeedsDisposal = false;
	}

	internal static void switchThemeColors(OS os, OSTheme theme)
	{
		os.displayModuleExtraLayerBackingColor = Color.Transparent;
		os.UseAspectPreserveBackgroundScaling = false;
		os.BackgroundImageFillColor = Color.Black;
		switch (theme)
		{
		case OSTheme.HacknetBlue:
			os.defaultHighlightColor = new Color(0, 139, 199, 255);
			os.defaultTopBarColor = new Color(130, 65, 27);
			os.warningColor = Color.Red;
			os.highlightColor = os.defaultHighlightColor;
			os.subtleTextColor = new Color(90, 90, 90);
			os.darkBackgroundColor = new Color(8, 8, 8);
			os.indentBackgroundColor = new Color(12, 12, 12);
			os.outlineColor = new Color(68, 68, 68);
			os.lockedColor = new Color(65, 16, 16, 200);
			os.brightLockedColor = new Color(160, 0, 0);
			os.brightUnlockedColor = new Color(0, 160, 0);
			os.unlockedColor = new Color(39, 65, 36);
			os.lightGray = new Color(180, 180, 180);
			os.shellColor = new Color(222, 201, 24);
			os.shellButtonColor = new Color(105, 167, 188);
			os.moduleColorSolid = new Color(50, 59, 90, 255);
			os.moduleColorSolidDefault = new Color(50, 59, 90, 255);
			os.moduleColorStrong = new Color(14, 28, 40, 80);
			os.moduleColorBacking = new Color(5, 6, 7, 10);
			os.topBarColor = os.defaultTopBarColor;
			os.semiTransText = new Color(120, 120, 120, 0);
			os.terminalTextColor = new Color(213, 245, 255);
			os.topBarTextColor = new Color(126, 126, 126, 100);
			os.superLightWhite = new Color(2, 2, 2, 30);
			os.connectedNodeHighlight = new Color(222, 0, 0, 195);
			os.exeModuleTopBar = new Color(130, 65, 27, 80);
			os.exeModuleTitleText = new Color(155, 85, 37, 0);
			os.netmapToolTipColor = new Color(213, 245, 255, 0);
			os.netmapToolTipBackground = new Color(0, 0, 0, 70);
			os.topBarIconsColor = Color.White;
			os.AFX_KeyboardMiddle = new Color(0, 120, 255);
			os.AFX_KeyboardOuter = new Color(255, 150, 0);
			os.AFX_WordLogo = new Color(0, 120, 255);
			os.AFX_Other = new Color(0, 100, 255);
			os.thisComputerNode = new Color(95, 220, 83);
			os.scanlinesColor = new Color(255, 255, 255, 15);
			break;
		case OSTheme.HacknetTeal:
			os.defaultHighlightColor = new Color(59, 134, 134, 255);
			os.defaultTopBarColor = new Color(11, 72, 107);
			os.warningColor = Color.Red;
			os.highlightColor = os.defaultHighlightColor;
			os.subtleTextColor = new Color(90, 90, 90);
			os.darkBackgroundColor = new Color(8, 8, 8);
			os.indentBackgroundColor = new Color(12, 12, 12);
			os.outlineColor = new Color(68, 68, 68);
			os.lockedColor = new Color(65, 16, 16, 200);
			os.brightLockedColor = new Color(160, 0, 0);
			os.brightUnlockedColor = new Color(0, 160, 0);
			os.unlockedColor = new Color(39, 65, 36);
			os.lightGray = new Color(180, 180, 180);
			os.shellColor = new Color(121, 189, 154);
			os.shellButtonColor = new Color(207, 240, 158);
			os.moduleColorSolid = new Color(59, 134, 134);
			os.moduleColorSolidDefault = new Color(59, 134, 134);
			os.moduleColorStrong = new Color(14, 28, 40, 80);
			os.moduleColorBacking = new Color(5, 7, 6, 200);
			os.topBarColor = os.defaultTopBarColor;
			os.semiTransText = new Color(120, 120, 120, 0);
			os.terminalTextColor = new Color(213, 245, 255);
			os.topBarTextColor = new Color(126, 126, 126, 100);
			os.superLightWhite = new Color(2, 2, 2, 30);
			os.connectedNodeHighlight = new Color(222, 0, 0, 195);
			os.exeModuleTopBar = new Color(12, 33, 33, 80);
			os.exeModuleTitleText = new Color(11, 72, 107, 0);
			os.netmapToolTipColor = new Color(213, 245, 255, 0);
			os.netmapToolTipBackground = new Color(0, 0, 0, 70);
			os.topBarIconsColor = Color.White;
			os.thisComputerNode = new Color(95, 220, 83);
			os.AFX_KeyboardMiddle = new Color(168, 219, 168);
			os.AFX_KeyboardOuter = new Color(121, 189, 154);
			os.AFX_WordLogo = new Color(59, 134, 134);
			os.AFX_Other = new Color(207, 240, 158);
			os.scanlinesColor = new Color(255, 255, 255, 15);
			break;
		case OSTheme.HacknetYellow:
			os.defaultHighlightColor = new Color(186, 98, 9, 255);
			os.defaultTopBarColor = new Color(89, 48, 6);
			os.highlightColor = os.defaultHighlightColor;
			os.shellColor = new Color(60, 107, 85);
			os.shellButtonColor = new Color(207, 240, 158);
			os.moduleColorSolid = new Color(186, 170, 67, 10);
			os.moduleColorStrong = new Color(201, 154, 10, 10);
			os.moduleColorBacking = new Color(5, 7, 6, 200);
			os.topBarColor = os.defaultTopBarColor;
			os.AFX_KeyboardMiddle = new Color(255, 150, 0);
			os.AFX_KeyboardOuter = new Color(255, 204, 0);
			os.AFX_WordLogo = new Color(255, 179, 0);
			os.AFX_Other = new Color(255, 221, 0);
			os.exeModuleTopBar = new Color(12, 33, 33, 80);
			os.exeModuleTitleText = new Color(11, 72, 107, 0);
			break;
		case OSTheme.HackerGreen:
			os.defaultHighlightColor = new Color(135, 222, 109, 200);
			os.defaultTopBarColor = new Color(6, 40, 16);
			os.highlightColor = os.defaultHighlightColor;
			os.shellColor = new Color(135, 222, 109);
			os.shellButtonColor = new Color(207, 240, 158);
			os.moduleColorSolid = new Color(60, 222, 10, 100);
			os.moduleColorSolidDefault = new Color(60, 222, 10, 100);
			os.moduleColorStrong = new Color(10, 80, 20, 50);
			os.moduleColorBacking = new Color(6, 6, 6, 200);
			os.topBarColor = os.defaultTopBarColor;
			os.terminalTextColor = new Color(95, 255, 70);
			os.AFX_KeyboardMiddle = new Color(60, 255, 10);
			os.AFX_KeyboardOuter = new Color(60, 222, 10);
			os.AFX_WordLogo = new Color(0, 255, 20);
			os.AFX_Other = new Color(0, 255, 0);
			os.exeModuleTopBar = new Color(12, 33, 33, 80);
			os.exeModuleTitleText = new Color(11, 107, 20, 0);
			os.topBarIconsColor = Color.White;
			break;
		case OSTheme.HacknetWhite:
			os.defaultHighlightColor = new Color(185, 219, 255, 255);
			os.defaultTopBarColor = new Color(20, 20, 20);
			os.highlightColor = os.defaultHighlightColor;
			os.shellColor = new Color(156, 185, 190);
			os.shellButtonColor = new Color(159, 220, 231);
			os.moduleColorSolid = new Color(220, 222, 220, 100);
			os.moduleColorSolidDefault = new Color(220, 222, 220, 100);
			os.moduleColorStrong = new Color(71, 71, 71, 50);
			os.moduleColorBacking = new Color(6, 6, 6, 205);
			os.topBarColor = os.defaultTopBarColor;
			os.terminalTextColor = new Color(255, 255, 255);
			os.AFX_KeyboardMiddle = new Color(220, 220, 220);
			os.AFX_KeyboardOuter = new Color(180, 180, 180);
			os.AFX_WordLogo = new Color(170, 80, 80);
			os.AFX_Other = new Color(255, 255, 255);
			os.exeModuleTopBar = new Color(20, 20, 20, 80);
			os.exeModuleTitleText = new Color(200, 200, 200, 0);
			os.displayModuleExtraLayerBackingColor = new Color(0, 0, 0, 140);
			break;
		case OSTheme.TerminalOnlyBlack:
		{
			Color color = new Color(0, 0, 0, 0);
			os.defaultHighlightColor = new Color(0, 0, 0, 200);
			os.defaultTopBarColor = new Color(0, 0, 0, 0);
			os.highlightColor = os.defaultHighlightColor;
			os.shellColor = color;
			os.shellButtonColor = color;
			os.moduleColorSolid = new Color(0, 0, 0, 0);
			os.moduleColorStrong = new Color(0, 0, 0, 0);
			os.moduleColorSolidDefault = new Color(0, 0, 0, 0);
			os.moduleColorBacking = color;
			os.topBarColor = os.defaultTopBarColor;
			os.terminalTextColor = new Color(255, 254, 235);
			os.AFX_KeyboardMiddle = new Color(0, 0, 0);
			os.AFX_KeyboardOuter = new Color(0, 0, 0);
			os.AFX_WordLogo = new Color(0, 0, 0);
			os.AFX_Other = new Color(0, 0, 0);
			os.exeModuleTopBar = color;
			os.exeModuleTitleText = color;
			break;
		}
		case OSTheme.HacknetPurple:
			os.defaultHighlightColor = new Color(35, 158, 121);
			os.defaultTopBarColor = new Color(0, 0, 0, 60);
			os.highlightColor = os.defaultHighlightColor;
			os.highlightColor = os.defaultHighlightColor;
			os.moduleColorSolid = new Color(154, 119, 189, 255);
			os.moduleColorSolidDefault = new Color(154, 119, 189, 255);
			os.moduleColorStrong = new Color(27, 14, 40, 80);
			os.moduleColorBacking = new Color(6, 5, 7, 205);
			os.topBarColor = os.defaultTopBarColor;
			os.exeModuleTopBar = new Color(32, 22, 40, 80);
			os.exeModuleTitleText = new Color(91, 132, 207, 0);
			os.netmapToolTipColor = new Color(213, 245, 255, 0);
			os.netmapToolTipBackground = new Color(0, 0, 0, 70);
			os.displayModuleExtraLayerBackingColor = new Color(0, 0, 0, 60);
			os.AFX_KeyboardMiddle = new Color(190, 0, 255);
			os.AFX_KeyboardOuter = new Color(20, 90, 255);
			os.AFX_WordLogo = new Color(0, 255, 60);
			os.AFX_Other = new Color(0, 201, 141);
			os.thisComputerNode = new Color(95, 220, 83);
			os.scanlinesColor = new Color(255, 255, 255, 15);
			break;
		case OSTheme.HacknetMint:
			os.defaultHighlightColor = new Color(35, 158, 121);
			os.defaultTopBarColor = new Color(0, 0, 0, 40);
			os.topBarColor = os.defaultTopBarColor;
			os.highlightColor = os.defaultHighlightColor;
			os.moduleColorSolid = new Color(150, 150, 150, 255);
			os.moduleColorSolidDefault = new Color(150, 150, 150, 255);
			os.moduleColorStrong = new Color(43, 43, 43, 80);
			os.moduleColorBacking = new Color(6, 6, 6, 145);
			os.displayModuleExtraLayerBackingColor = new Color(0, 0, 0, 70);
			os.thisComputerNode = new Color(95, 220, 83);
			os.topBarIconsColor = new Color(49, 224, 172);
			os.AFX_KeyboardMiddle = new Color(35, 255, 173);
			os.AFX_KeyboardOuter = new Color(180, 255, 255);
			os.AFX_WordLogo = new Color(0, 255, 60);
			os.AFX_Other = new Color(220, 220, 220);
			os.exeModuleTopBar = new Color(20, 20, 20, 80);
			os.exeModuleTitleText = new Color(49, 224, 172, 0);
			os.scanlinesColor = new Color(255, 255, 255, 15);
			break;
		}
	}

	public static Color GetRepresentativeColorForTheme(OSTheme theme)
	{
		return theme switch
		{
			OSTheme.HacknetBlue => new Color(0, 139, 199, 255), 
			OSTheme.HacknetTeal => new Color(59, 134, 134, 255), 
			OSTheme.HacknetYellow => new Color(186, 98, 9, 255), 
			OSTheme.HackerGreen => new Color(135, 222, 109, 200), 
			OSTheme.HacknetWhite => new Color(185, 219, 255, 255), 
			OSTheme.HacknetPurple => new Color(111, 89, 171, 255), 
			OSTheme.HacknetMint => new Color(35, 158, 121), 
			_ => Utils.VeryDarkGray, 
		};
	}

	public static void drawBackgroundImage(SpriteBatch sb, Rectangle area)
	{
		switch (currentTheme)
		{
		default:
			sb.Draw(backgroundImage, area, (currentTheme == OSTheme.HackerGreen) ? new Color(100, 150, 100) : Color.White);
			break;
		case OSTheme.HacknetYellow:
		{
			sb.Draw(Utils.white, area, new Color(51, 38, 0, 255));
			Rectangle dest = new Rectangle(area.X - 30, area.Y - 20, area.Width + 60, area.Height + 40);
			hexGrid.Draw(dest, sb, Color.Transparent, new Color(255, 217, 105) * 0.2f);
			break;
		}
		case OSTheme.Custom:
			if (backgroundImage != null)
			{
				if (OS.currentInstance.UseAspectPreserveBackgroundScaling)
				{
					sb.Draw(Utils.white, area, OS.currentInstance.BackgroundImageFillColor);
					Utils.DrawSpriteAspectCorrect(area, sb, backgroundImage, Color.White);
				}
				else
				{
					sb.Draw(backgroundImage, area, Color.White);
				}
			}
			else
			{
				sb.Draw(Utils.white, area, Color.Lerp(Color.Black, LastLoadedCustomTheme.moduleColorBacking, 0.33f));
				Rectangle dest = new Rectangle(area.X - 30, area.Y - 20, area.Width + 60, area.Height + 40);
				hexGrid.Draw(dest, sb, Color.Transparent, Color.Lerp(Color.Transparent, LastLoadedCustomTheme.moduleColorStrong, 0.2f));
			}
			break;
		}
	}

	public static string getThemeDataString(OSTheme theme)
	{
		return fileData[theme];
	}

	public static string getThemeDataStringForCustomTheme(string customThemePath)
	{
		return getThemeDataString(OSTheme.Custom) + CustomThemeIDSeperator + FileEncrypter.EncryptString(customThemePath, "", "");
	}

	public static OSTheme getThemeForDataString(string data)
	{
		if (data.Contains(CustomThemeIDSeperator))
		{
			string[] seperated = data.Split(new string[1] { CustomThemeIDSeperator }, StringSplitOptions.RemoveEmptyEntries);
			OSTheme key = fileData.FirstOrDefault((KeyValuePair<OSTheme, string> x) => x.Value == seperated[0]).Key;
			try
			{
				string text = FileEncrypter.DecryptString(seperated[1])[2];
				string text2 = "Content/";
				if (Settings.IsInExtensionMode)
				{
					text2 = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
				}
				LastLoadedCustomTheme = CustomTheme.Deserialize(text2 + text);
			}
			catch (Exception)
			{
				LastLoadedCustomTheme = null;
				return OSTheme.TerminalOnlyBlack;
			}
			return key;
		}
		return fileData.FirstOrDefault((KeyValuePair<OSTheme, string> x) => x.Value == data).Key;
	}

	public static void setThemeOnComputer(object computerObject, OSTheme theme)
	{
		Computer computer = (Computer)computerObject;
		Folder folder = computer.files.root.searchForFolder("sys");
		if (folder.containsFile("x-server.sys"))
		{
			FileEntry fileEntry = folder.searchForFile("x-server.sys");
			fileEntry.data = getThemeDataString(theme);
		}
		else
		{
			FileEntry item = new FileEntry(getThemeDataString(theme), "x-server.sys");
			folder.files.Add(item);
		}
	}

	public static void setThemeOnComputer(object computerObject, string customThemePath)
	{
		Computer computer = (Computer)computerObject;
		Folder folder = computer.files.root.searchForFolder("sys");
		string themeDataStringForCustomTheme = getThemeDataStringForCustomTheme(customThemePath);
		if (folder.containsFile("x-server.sys"))
		{
			FileEntry fileEntry = folder.searchForFile("x-server.sys");
			fileEntry.data = themeDataStringForCustomTheme;
		}
		else
		{
			FileEntry item = new FileEntry(themeDataStringForCustomTheme, "x-server.sys");
			folder.files.Add(item);
		}
	}
}
