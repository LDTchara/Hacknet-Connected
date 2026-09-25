using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hacknet;

internal class OptionsMenu : GameScreen
{
	private string[] resolutions;

	private int currentResIndex;

	private string[] fontConfigs;

	private int currentFontIndex = -1;

	private bool resolutionChanged;

	private bool windowed;

	private char[] xArray = new char[1] { 'x' };

	private bool needsApply = false;

	private bool mouseHasBeenReleasedOnThisScreen = false;

	private string originallyActiveLocale = "en-us";

	private string[] localeNames;

	private int currentLocaleIndex = 0;

	private bool startedFromGameContext = false;

	public OptionsMenu()
	{
		resolutionChanged = false;
	}

	public OptionsMenu(bool startedFromGameContext)
	{
		resolutionChanged = false;
		this.startedFromGameContext = startedFromGameContext;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		resolutions = new string[20];
		List<string> list = new List<string>();
		list.Add("1152x768");
		list.Add("1280x854");
		list.Add("1440x960");
		list.Add("2880x1920");
		list.Add("1152x864");
		list.Add("1280x960");
		list.Add("1400x1050");
		list.Add("1600x1200");
		list.Add("1280x760");
		list.Add("1280x1024");
		list.Add("1280x720");
		list.Add("1365x760");
		list.Add("1366x768");
		list.Add("1408x792");
		list.Add("1600x900");
		list.Add("1920x1080");
		list.Add("2560x1440");
		list.Add("1280x800");
		list.Add("1440x900");
		list.Add("1680x1050");
		list.Add("1920x1200");
		list.Add("1792x1008");
		list.Add("2560x1600");
		list.Add("2560x1080");
		list.Add("3440x1440");
		list.Add("3440x1440");
		list.Add("3840x2160");
		list.Add("4096x2160");
		list.Sort();
		resolutions = list.ToArray();
		fontConfigs = new string[GuiData.FontConfigs.Count];
		for (int i = 0; i < GuiData.FontConfigs.Count; i++)
		{
			fontConfigs[i] = GuiData.FontConfigs[i].name;
			if (GuiData.ActiveFontConfig.name == fontConfigs[i])
			{
				currentFontIndex = i;
			}
		}
		for (int i = 0; i < resolutions.Length; i++)
		{
			if (resolutions[i].Equals(getCurrentResolution()))
			{
				currentResIndex = i;
				break;
			}
		}
		windowed = getIfWindowed();
		localeNames = new string[LocaleActivator.SupportedLanguages.Count];
		for (int i = 0; i < LocaleActivator.SupportedLanguages.Count; i++)
		{
			localeNames[i] = LocaleActivator.SupportedLanguages[i].Name;
			if (LocaleActivator.SupportedLanguages[i].Code == Settings.ActiveLocale)
			{
				currentLocaleIndex = i;
			}
		}
		originallyActiveLocale = Settings.ActiveLocale;
	}

	public string getCurrentResolution()
	{
		string text = "";
		text += base.ScreenManager.GraphicsDevice.Viewport.Width;
		text += "x";
		return text + base.ScreenManager.GraphicsDevice.Viewport.Height;
	}

	public bool getIfWindowed()
	{
		return Game1.getSingleton().graphics.IsFullScreen;
	}

	public void apply()
	{
		bool flag = false;
		if (windowed != getIfWindowed())
		{
			Game1.getSingleton().graphics.ToggleFullScreen();
			Settings.windowed = getIfWindowed();
			flag = true;
		}
		if (resolutionChanged)
		{
			string[] array = resolutions[currentResIndex].Split(xArray);
			int preferredBackBufferWidth = Convert.ToInt32(array[0]);
			int preferredBackBufferHeight = Convert.ToInt32(array[1]);
			Game1.getSingleton().graphics.PreferredBackBufferWidth = preferredBackBufferWidth;
			Game1.getSingleton().graphics.PreferredBackBufferHeight = preferredBackBufferHeight;
			Game1.getSingleton().graphics.PreferMultiSampling = SettingsLoader.ShouldMultisample;
			PostProcessor.GenerateMainTarget(Game1.getSingleton().graphics.GraphicsDevice);
		}
		GuiData.ActivateFontConfig(fontConfigs[currentFontIndex]);
		if (resolutionChanged || flag)
		{
			Game1.getSingleton().graphics.ApplyChanges();
			Game1.getSingleton().setNewGraphics();
		}
		else
		{
			ExitScreen();
		}
		SettingsLoader.writeStatusFile();
	}

	public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
	{
		base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
		if (needsApply)
		{
			base.ScreenManager.GraphicsDevice.SetRenderTarget(null);
			apply();
			needsApply = false;
		}
		if (GuiData.mouse.LeftButton == ButtonState.Released)
		{
			mouseHasBeenReleasedOnThisScreen = true;
		}
	}

	public override void HandleInput(InputState input)
	{
		base.HandleInput(input);
		GuiData.doInput(input);
		if (Settings.debugCommandsEnabled && Utils.keyPressed(input, Keys.F8, null))
		{
			ExitScreen();
			Game1.getSingleton().Exit();
		}
	}

	public override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);
		PostProcessor.begin();
		base.ScreenManager.FadeBackBufferToBlack(255);
		GuiData.startDraw();
		PatternDrawer.draw(new Rectangle(0, 0, base.ScreenManager.GraphicsDevice.Viewport.Width, base.ScreenManager.GraphicsDevice.Viewport.Height), 0.5f, Color.Black, new Color(2, 2, 2), GuiData.spriteBatch);
		if (Button.doButton(999, 10, 10, 220, 30, "<- " + LocaleTerms.Loc("Back"), Color.Gray))
		{
			SettingsLoader.writeStatusFile();
			ExitScreen();
		}
		if (Button.doButton(9907, 10, 44, 220, 20, LocaleTerms.Loc("Apply Changes"), Color.LightBlue))
		{
			needsApply = true;
		}
		int num = 100;
		TextItem.doLabel(new Vector2(400f, num), LocaleTerms.Loc("Resolutions"), null, 200f);
		int num2 = currentResIndex;
		currentResIndex = SelectableTextList.doFancyList(10, 400, num + 36, 200, 450, resolutions, currentResIndex, null);
		if (!mouseHasBeenReleasedOnThisScreen)
		{
			currentResIndex = num2;
		}
		else if (SelectableTextList.wasActivated)
		{
			resolutionChanged = true;
		}
		if (!startedFromGameContext)
		{
			TextItem.doLabel(new Vector2(620f, num), LocaleTerms.Loc("Language"), null, 200f);
			int num3 = currentLocaleIndex;
			currentLocaleIndex = SelectableTextList.doFancyList(1013, 620, num + 36, 200, 450, localeNames, currentLocaleIndex, null);
			if (!mouseHasBeenReleasedOnThisScreen)
			{
				currentLocaleIndex = num3;
			}
			else if (SelectableTextList.wasActivated)
			{
				LocaleActivator.ActivateLocale(LocaleActivator.SupportedLanguages[currentLocaleIndex].Code, Game1.getSingleton().Content);
				Settings.ActiveLocale = LocaleActivator.SupportedLanguages[currentLocaleIndex].Code;
			}
		}
		int num4 = 64;
		float maxWidth = 280f;
		TextItem.doLabel(new Vector2(100f, num4 += 36), LocaleTerms.Loc("Fullscreen"), null, maxWidth);
		windowed = CheckBox.doCheckBox(20, 100, num4 += 34, windowed, null);
		TextItem.doLabel(new Vector2(100f, num4 += 32), LocaleTerms.Loc("Bloom"), null, maxWidth);
		PostProcessor.bloomEnabled = CheckBox.doCheckBox(21, 100, num4 += 34, PostProcessor.bloomEnabled, null);
		TextItem.doLabel(new Vector2(100f, num4 += 32), LocaleTerms.Loc("Scanlines"), null, maxWidth);
		PostProcessor.scanlinesEnabled = CheckBox.doCheckBox(22, 100, num4 += 34, PostProcessor.scanlinesEnabled, null);
		TextItem.doLabel(new Vector2(100f, num4 += 32), LocaleTerms.Loc("Multisampling"), null, maxWidth);
		bool shouldMultisample = SettingsLoader.ShouldMultisample;
		SettingsLoader.ShouldMultisample = CheckBox.doCheckBox(221, 100, num4 += 34, SettingsLoader.ShouldMultisample, null);
		if (shouldMultisample != SettingsLoader.ShouldMultisample)
		{
			resolutionChanged = true;
		}
		TextItem.doLabel(new Vector2(100f, num4 += 32), LocaleTerms.Loc("Audio Visualiser"), null, maxWidth);
		SettingsLoader.ShouldDrawMusicVis = CheckBox.doCheckBox(223, 100, num4 += 34, SettingsLoader.ShouldDrawMusicVis, null);
		TextItem.doLabel(new Vector2(100f, num4 += 32), LocaleTerms.Loc("Sound Enabled"), null, maxWidth);
		MusicManager.setIsMuted(!CheckBox.doCheckBox(23, 100, num4 += 34, !MusicManager.isMuted, null));
		TextItem.doLabel(new Vector2(100f, num4 += 32), LocaleTerms.Loc("Music Volume"), null, maxWidth);
		MusicManager.setVolume(SliderBar.doSliderBar(24, 100, num4 += 34, 210, 30, 1f, 0f, MusicManager.getVolume(), 0.001f));
		TextItem.doLabel(new Vector2(100f, num4 += 32), LocaleTerms.Loc("Text Size"), null, maxWidth);
		int num5 = currentFontIndex;
		currentFontIndex = SelectableTextList.doFancyList(25, 100, num4 += 34, 200, 160, fontConfigs, currentFontIndex, null);
		if (currentFontIndex != num5 && startedFromGameContext)
		{
			try
			{
				if (OS.currentInstance != null)
				{
					OS.currentInstance.terminal.reset();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
			}
		}
		if (Button.doButton(990, 10, num4 + 150, 220, 30, LocaleTerms.Loc("Apply Changes"), Color.LightBlue))
		{
			needsApply = true;
		}
		GuiData.endDraw();
		PostProcessor.end();
	}
}
