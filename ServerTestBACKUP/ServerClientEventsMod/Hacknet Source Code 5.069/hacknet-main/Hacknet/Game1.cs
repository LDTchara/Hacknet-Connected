using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Localization;
using Hacknet.PlatformAPI;
using Hacknet.PlatformAPI.Storage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SDL2;

namespace Hacknet;

public class Game1 : Game
{
	private static Game1 singleton;

	public static bool threadsExiting;

	public static CultureInfo culture;

	public static CultureInfo OriginalCultureInfo;

	public GraphicsDeviceManager graphics;

	public GraphicsDeviceInformation graphicsInfo;

	private SpriteBatch spriteBatch;

	public ScreenManager sman;

	private bool resolutionSet = true;

	private bool IsDrawing = false;

	private bool CanLoadContent = false;

	private bool HasLoadedContent = false;

	private bool NeedsSettingsLocaleActivation = false;

	private EventHandler<PreparingDeviceSettingsEventArgs> graphicsPreparedHandler;

	public static string AutoLoadExtensionPath = null;

	public Game1()
	{
		OriginalCultureInfo = CultureInfo.CurrentCulture;
		culture = new CultureInfo("en-US");
		Thread.CurrentThread.CurrentCulture = culture;
		Thread.CurrentThread.CurrentUICulture = culture;
		threadsExiting = false;
		graphics = new GraphicsDeviceManager(this);
		base.Content.RootDirectory = "Content";
		graphics.DeviceDisposing += graphics_DeviceDisposing;
		graphics.DeviceResetting += graphics_DeviceResetting;
		graphics.DeviceReset += graphics_DeviceReset;
		PlatformAPISettings.InitPlatformAPI();
		SettingsLoader.checkStatus();
		if (SettingsLoader.didLoad)
		{
			CanLoadContent = true;
			graphics.PreferredBackBufferWidth = Math.Min(SettingsLoader.resWidth, 4096);
			graphics.PreferredBackBufferHeight = Math.Min(SettingsLoader.resHeight, 4096);
			graphics.IsFullScreen = SettingsLoader.isFullscreen;
			graphics.PreferMultiSampling = SettingsLoader.ShouldMultisample;
			if (Settings.StartOnAltMonitor & !SettingsLoader.isFullscreen)
			{
				EventHandler<PreparingDeviceSettingsEventArgs> value = graphics_PreparingDeviceSettingsForAltMonitor;
				graphics.PreparingDeviceSettings += value;
			}
			Console.WriteLine("Loaded Settings - Language Code: " + Settings.ActiveLocale);
		}
		else
		{
			graphics.PreferMultiSampling = true;
			if (Settings.windowed)
			{
				graphics.PreferredBackBufferWidth = 1280;
				graphics.PreferredBackBufferHeight = 800;
				CanLoadContent = true;
			}
			else
			{
				graphicsPreparedHandler = graphics_PreparingDeviceSettings;
				graphics.PreparingDeviceSettings += graphicsPreparedHandler;
				graphics.PreferredBackBufferWidth = 1280;
				graphics.PreferredBackBufferHeight = 800;
				graphics.IsFullScreen = true;
			}
			NeedsSettingsLocaleActivation = true;
			Console.WriteLine("Settings file not found, setting defaults.");
		}
		CheckAndFixWindowPosition();
		base.IsMouseVisible = true;
		base.IsFixedTimeStep = false;
		singleton = this;
		base.Exiting += handleExit;
		StatsManager.InitStats();
	}

	private void GraphicsDevice_DeviceLost(object sender, EventArgs e)
	{
		Console.WriteLine("Device Lost...");
	}

	private void graphics_DeviceReset(object sender, EventArgs e)
	{
		Program.GraphicsDeviceResetLog = Program.GraphicsDeviceResetLog + "Reset at " + DateTime.Now.ToShortTimeString();
		Console.WriteLine("Graphics Device Reset Started");
		if (Utils.white != null)
		{
			Utils.white.Dispose();
			Utils.white = null;
		}
		LoadRegenSafeContent();
		HasLoadedContent = true;
		Console.WriteLine("Graphics Device Reset Complete");
	}

	private void graphics_DeviceResetting(object sender, EventArgs e)
	{
		HasLoadedContent = false;
		Console.WriteLine("Graphics Device Resetting");
	}

	private void graphics_DeviceDisposing(object sender, EventArgs e)
	{
		HasLoadedContent = false;
		Console.WriteLine("Graphics Device Disposing");
	}

	private void graphics_PreparingDeviceSettingsForAltMonitor(object sender, PreparingDeviceSettingsEventArgs e)
	{
		foreach (GraphicsAdapter adapter in GraphicsAdapter.Adapters)
		{
			if (!adapter.IsDefaultAdapter)
			{
				e.GraphicsDeviceInformation.Adapter = adapter;
				break;
			}
		}
	}

	private void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
	{
		try
		{
			DisplayMode currentDisplayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
			int width = currentDisplayMode.Width;
			int height = currentDisplayMode.Height;
			graphics.PreferredBackBufferWidth = Math.Min(width, 4096);
			graphics.PreferredBackBufferHeight = Math.Min(height, 4096);
			graphics.PreferMultiSampling = true;
			resolutionSet = false;
			graphics.PreparingDeviceSettings -= graphicsPreparedHandler;
			CanLoadContent = true;
		}
		catch (Exception ex)
		{
			string data = Utils.GenerateReportFromException(ex);
			string text = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/My Games/Hacknet/Reports/";
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			Utils.writeToFile(data, text + "Graphics_CrashReport_" + Guid.NewGuid().ToString().Replace(" ", "_") + ".txt");
			graphics.PreferredBackBufferWidth = 1280;
			graphics.PreferredBackBufferHeight = 800;
			graphics.PreferMultiSampling = true;
			graphics.IsFullScreen = false;
		}
	}

	public void setNewGraphics()
	{
		graphics.ApplyChanges();
		GameScreen[] screens = sman.GetScreens();
		bool flag = false;
		string saveGameUserName = null;
		string saveUserAccountName = null;
		for (int i = 0; i < screens.Length; i++)
		{
			if (screens[i] is OS oS)
			{
				oS.threadedSaveExecute();
				flag = true;
				saveGameUserName = oS.SaveGameUserName;
				saveUserAccountName = oS.SaveUserAccountName;
				break;
			}
		}
		base.Components.Remove(sman);
		sman = new ScreenManager(this);
		base.Components.Add(sman);
		LoadGraphicsContent();
		if (flag)
		{
			OS.WillLoadSave = true;
			OS oS2 = new OS();
			oS2.SaveGameUserName = saveGameUserName;
			oS2.SaveUserAccountName = saveUserAccountName;
			bool isInExtensionMode = Settings.IsInExtensionMode;
			MainMenu.resetOS();
			Settings.IsInExtensionMode = isInExtensionMode;
			sman.AddScreen(oS2, sman.controllingPlayer);
		}
		GuiData.spriteBatch = sman.SpriteBatch;
		if (sman.GetScreens().Length == 0)
		{
			LoadInitialScreens();
		}
	}

	private void CheckAndFixWindowPosition()
	{
		SDL.SDL_GetWindowPosition(base.Window.Handle, out var x, out var y);
		if (!graphics.IsFullScreen)
		{
			SDL.SDL_SetWindowPosition(base.Window.Handle, Math.Max(x, 10), Math.Max(y, 10));
		}
	}

	public void setWindowPosition(Vector2 pos)
	{
		if (!SettingsLoader.isFullscreen)
		{
			base.Window.IsBorderlessEXT = true;
			SDL.SDL_SetWindowPosition(base.Window.Handle, (int)pos.X, (int)pos.Y);
		}
	}

	protected override void Initialize()
	{
		sman = new ScreenManager(this);
		base.Components.Add(sman);
		graphics.PreferMultiSampling = true;
		NameGenerator.init();
		PatternDrawer.init(base.Content);
		ProgramList.init();
		Cube3D.Initilize(graphics.GraphicsDevice);
		AlienwareFXManager.Init();
		graphics.GraphicsDevice.DeviceLost += GraphicsDevice_DeviceLost;
		base.Initialize();
	}

	private void handleExit(object sender, EventArgs e)
	{
		threadsExiting = true;
		MusicManager.stop();
		AlienwareFXManager.ReleaseHandle();
	}

	private void LoadRegenSafeContent()
	{
		Utils.white = new Texture2D(graphics.GraphicsDevice, 1, 1);
		Color[] data = new Color[1] { Color.White };
		Utils.white.SetData(data);
		ContentManager content = base.Content;
		Utils.gradient = content.Load<Texture2D>("Gradient");
		Utils.gradientLeftRight = content.Load<Texture2D>("GradientHorizontal");
	}

	protected override void LoadContent()
	{
		if (!CanLoadContent)
		{
			return;
		}
		PortExploits.populate();
		sman.controllingPlayer = PlayerIndex.One;
		if (Settings.isConventionDemo)
		{
			setWindowPosition(new Vector2(200f, 200f));
		}
		LoadGraphicsContent();
		LoadRegenSafeContent();
		ContentManager content = base.Content;
		GuiData.font = content.Load<SpriteFont>("Font23");
		GuiData.titlefont = content.Load<SpriteFont>("Kremlin");
		GuiData.smallfont = base.Content.Load<SpriteFont>("Font12");
		GuiData.UISmallfont = GuiData.smallfont;
		GuiData.tinyfont = base.Content.Load<SpriteFont>("Font10");
		GuiData.UITinyfont = base.Content.Load<SpriteFont>("Font10");
		GuiData.detailfont = base.Content.Load<SpriteFont>("Font7");
		GuiData.spriteBatch = sman.SpriteBatch;
		GuiData.InitFontOptions(base.Content);
		GuiData.init(base.Window);
		DLC1SessionUpgrader.CheckForDLCFiles();
		VehicleInfo.init();
		WorldLocationLoader.init();
		ThemeManager.init(content);
		MissionGenerationParser.init();
		MissionGenerator.init(content);
		UsernameGenerator.init();
		MusicManager.init(content);
		SFX.init(content);
		OldSystemSaveFileManifest.Load();
		try
		{
			SaveFileManager.Init();
		}
		catch (UnauthorizedAccessException ex)
		{
			MainMenu.AccumErrors += " ---- WARNING ---\nHacknet cannot access the Save File Folder (Path Below) to read/write save files.\nNO PROGRESS WILL BE SAVED.\n";
			MainMenu.AccumErrors += "Check folder permissions, run Hacknet.exe as Administrator, and try again.\n";
			MainMenu.AccumErrors += "If Errors Persist, search for \"Hacknet Workaround\" for a steam forums thread with more options.\n";
			MainMenu.AccumErrors = MainMenu.AccumErrors + ":: Error Details ::\n" + Utils.GenerateReportFromException(ex);
		}
		if (NeedsSettingsLocaleActivation)
		{
			if (!Settings.ForceEnglish)
			{
				string codeForActiveLanguage = PlatformAPISettings.GetCodeForActiveLanguage(LocaleActivator.SupportedLanguages);
				LocaleActivator.ActivateLocale(codeForActiveLanguage, base.Content);
				Settings.ActiveLocale = codeForActiveLanguage;
			}
		}
		else if (SettingsLoader.didLoad && Settings.ActiveLocale != "en-us")
		{
			LocaleActivator.ActivateLocale(Settings.ActiveLocale, base.Content);
		}
		Helpfile.init();
		FileEntry.init(base.Content);
		HasLoadedContent = true;
		LoadInitialScreens();
		if (WebRenderer.Enabled)
		{
			XNAWebRenderer.XNAWR_Initialize("file:///nope.html", WebRenderer.textureUpdated, 512, 512);
			WebRenderer.setSize(512, 512);
		}
	}

	protected void LoadGraphicsContent()
	{
		spriteBatch = new SpriteBatch(base.GraphicsDevice);
		if (NeedsSettingsLocaleActivation)
		{
			SettingsLoader.ShouldMultisample = base.GraphicsDevice.Adapter.IsProfileSupported(GraphicsProfile.HiDef);
		}
		PostProcessor.init(graphics.GraphicsDevice, spriteBatch, base.Content);
		WebRenderer.init(graphics.GraphicsDevice);
	}

	protected void LoadInitialScreens()
	{
		if (Settings.ForceEnglish)
		{
			LocaleActivator.ActivateLocale(Settings.ActiveLocale, base.Content);
		}
		if (AutoLoadExtensionPath != null)
		{
			ExtensionLoader.ActiveExtensionInfo = ExtensionInfo.ReadExtensionInfo(AutoLoadExtensionPath);
			SaveFileManager.Init();
			SaveFileManager.DeleteUser("test");
			MainMenu.CreateNewAccountForExtensionAndStart("test", "test", sman);
		}
		else if (Settings.MenuStartup)
		{
			sman.AddScreen(new MainMenu(), sman.controllingPlayer);
		}
		else
		{
			sman.AddScreen(new OS(), sman.controllingPlayer);
		}
	}

	protected override void UnloadContent()
	{
		if (WebRenderer.Enabled)
		{
			XNAWebRenderer.XNAWR_Shutdown();
		}
	}

	protected override void Update(GameTime gameTime)
	{
		if (!HasLoadedContent)
		{
			LoadContent();
		}
		if (WebRenderer.Enabled)
		{
			XNAWebRenderer.XNAWR_Update();
		}
		if (!resolutionSet)
		{
			setNewGraphics();
			resolutionSet = true;
		}
		PatternDrawer.update((float)gameTime.ElapsedGameTime.TotalSeconds);
		GuiData.setTimeStep((float)gameTime.ElapsedGameTime.TotalSeconds);
		MusicManager.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
		ThemeManager.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
		AlienwareFXManager.UpdateForOS(OS.currentInstance);
		base.Update(gameTime);
	}

	protected override void Draw(GameTime gameTime)
	{
		if (HasLoadedContent)
		{
			IsDrawing = true;
			base.Draw(gameTime);
			IsDrawing = false;
		}
	}

	public static Game1 getSingleton()
	{
		return singleton;
	}
}
