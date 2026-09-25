using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Factions;
using Hacknet.Gui;
using Hacknet.Localization;
using Hacknet.Misc;
using Hacknet.Mission;
using Hacknet.Modules.Overlays;
using Hacknet.PlatformAPI.Storage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hacknet;

internal class OS : GameScreen
{
	public struct TrackerDetail
	{
		public Computer comp;

		public float timeLeft;
	}

	public static bool DEBUG_COMMANDS = Settings.debugCommandsEnabled;

	public static float EXE_MODULE_HEIGHT = 250f;

	public static float TCP_STAYALIVE_TIMER = 10f;

	public static float WARNING_FLASH_TIME = 2f;

	public static int TOP_BAR_HEIGHT = 21;

	public static OS currentInstance;

	public static bool WillLoadSave = false;

	public static bool TestingPassOnly = false;

	public bool FirstTimeStartup = Settings.slowOSStartup;

	public bool initShowsTutorial = Settings.initShowsTutorial;

	public bool inputEnabled = false;

	public bool isLoaded = false;

	private Texture2D scanLines;

	private Texture2D cross;

	private Texture2D cog;

	private Texture2D saveIcon;

	public float PorthackCompleteFlashTime = 0f;

	public float MissionCompleteFlashTime = 0f;

	private string locationString = "";

	public string username = "";

	public int totalRam = 800 - (TOP_BAR_HEIGHT + 2) - RamModule.contentStartOffset;

	public int ramAvaliable = 800 - (TOP_BAR_HEIGHT + 2);

	public int currentPID = 0;

	public List<ShellExe> shells;

	public List<string> shellIPs;

	internal bool bootingUp = false;

	public CrashModule crashModule;

	public TraceDangerSequence TraceDangerSequence;

	private IntroTextModule introTextModule;

	public GameTime lastGameTime;

	public UserDetail defaultUser;

	public Terminal terminal;

	public NetworkMap netMap;

	public DisplayModule display;

	public RamModule ram;

	private List<Module> modules;

	public IncomingConnectionOverlay IncConnectionOverlay;

	public AircraftInfoOverlay AircraftInfoOverlay;

	public ActiveMission currentMission;

	public List<ActiveMission> branchMissions = new List<ActiveMission>();

	public TraceTracker traceTracker;

	public List<ExeModule> exes;

	private Rectangle topBar;

	public EndingSequenceModule endingSequence;

	public MailIcon mailicon;

	private AudioVisualizer audioVisualizer = new AudioVisualizer();

	public string connectedIP = "";

	public Computer thisComputer = null;

	public Computer connectedComp = null;

	public Computer opponentComputer = null;

	public float warningFlashTimer = 0f;

	public Faction currentFaction;

	public AllFactions allFactions;

	public List<int> navigationPath = new List<int>();

	public ContentManager content;

	public float gameSavedTextAlpha = -1f;

	public string SaveGameUserName = "";

	public string SaveUserAccountName = null;

	public string SaveUserPassword = "password";

	private bool SaveInProgress = false;

	private bool SaveInQueue = false;

	public bool multiplayer = false;

	private TcpClient client;

	public bool isServer = false;

	private char[] trimChars = new char[3] { ' ', '\n', '\0' };

	private byte[] inBuffer;

	private byte[] outBuffer;

	private ASCIIEncoding encoder;

	private Thread listenerThread;

	private bool DestroyThreads = false;

	private NetworkStream netStream;

	public bool canRunContent = true;

	public float stayAliveTimer = TCP_STAYALIVE_TIMER;

	private bool multiplayerMissionLoaded;

	public string opponentLocation = "";

	public string displayCache = "";

	public string getStringCache = "";

	public static double currentElapsedTime = 0.0;

	public static float operationProgress = 0f;

	public static object displayObjectCache = null;

	public bool commandInvalid = false;

	public Rectangle fullscreen;

	public bool validCommand;

	public ActionDelayer delayer;

	public string connectedIPLastFrame = "";

	public string homeNodeID = "entropy00";

	public string homeAssetServerID = "entropy01";

	public bool DisableTopBarButtons = false;

	public bool DisableEmailIcon = false;

	private string LanguageCreatedIn = "en-us";

	public bool HasExitedAndEnded = false;

	private MessageBoxScreen ExitToMenuMessageBox = null;

	public ProgressionFlags Flags = new ProgressionFlags();

	public List<KeyValuePair<string, string>> ActiveHackers = new List<KeyValuePair<string, string>>();

	public float timer;

	public SoundEffect beepSound;

	private int updateErrorCount = 0;

	private int drawErrorCount = 0;

	public bool terminalOnlyMode = false;

	public bool HasLoadedDLCContent = false;

	public bool IsInDLCMode = false;

	public HubServerAlertsIcon hubServerAlertsIcon;

	public string PreDLCFaction = "entropy";

	public string PreDLCVisibleNodesCache = "";

	public bool IsDLCSave = false;

	public bool IsDLCConventionDemo = false;

	public RunnableConditionalActions ConditionalActions = new RunnableConditionalActions();

	public BootCrashAssistanceModule BootAssitanceModule;

	internal string GibsonIP;

	public ActiveEffectsUpdater EffectsUpdater = new ActiveEffectsUpdater();

	public bool ShowDLCAlertsIcon = false;

	public List<TrackerDetail> TrackersInProgress = new List<TrackerDetail>();

	internal Stream ForceLoadOverrideStream = null;

	public Action postFXDrawActions;

	public Action<float> UpdateSubscriptions;

	public Action traceCompleteOverrideAction;

	public Color defaultHighlightColor = new Color(0, 139, 199, 255);

	public Color defaultTopBarColor = new Color(130, 65, 27);

	public Color warningColor = Color.Red;

	public Color highlightColor = new Color(0, 139, 199, 255);

	public Color subtleTextColor = new Color(90, 90, 90);

	public Color darkBackgroundColor = new Color(8, 8, 8);

	public Color indentBackgroundColor = new Color(12, 12, 12);

	public Color outlineColor = new Color(68, 68, 68);

	public Color lockedColor = new Color(65, 16, 16, 200);

	public Color brightLockedColor = new Color(160, 0, 0);

	public Color brightUnlockedColor = new Color(0, 160, 0);

	public Color unlockedColor = new Color(39, 65, 36);

	public Color lightGray = new Color(180, 180, 180);

	public Color shellColor = new Color(222, 201, 24);

	public Color shellButtonColor = new Color(105, 167, 188);

	public Color moduleColorSolid = new Color(50, 59, 90, 255);

	public Color moduleColorSolidDefault = new Color(50, 59, 90, 255);

	public Color moduleColorStrong = new Color(14, 28, 40, 80);

	public Color moduleColorBacking = new Color(5, 6, 7, 10);

	public Color topBarColor = new Color(0, 139, 199, 255);

	public Color semiTransText = new Color(120, 120, 120, 0);

	public Color terminalTextColor = new Color(213, 245, 255);

	public Color topBarTextColor = new Color(126, 126, 126, 100);

	public Color superLightWhite = new Color(2, 2, 2, 30);

	public Color connectedNodeHighlight = new Color(222, 0, 0, 195);

	public Color exeModuleTopBar = new Color(130, 65, 27, 80);

	public Color exeModuleTitleText = new Color(155, 85, 37, 0);

	public Color netmapToolTipColor = new Color(213, 245, 255, 0);

	public Color netmapToolTipBackground = new Color(0, 0, 0, 150);

	public Color displayModuleExtraLayerBackingColor = new Color(0, 0, 0, 0);

	public Color topBarIconsColor = Color.White;

	public Color BackgroundImageFillColor = Color.Black;

	public bool UseAspectPreserveBackgroundScaling = false;

	public Color AFX_KeyboardMiddle = new Color(77, 145, 255);

	public Color AFX_KeyboardOuter = new Color(105, 138, 255);

	public Color AFX_WordLogo = new Color(105, 138, 255);

	public Color AFX_Other = new Color(0, 178, 255);

	public Color thisComputerNode = new Color(95, 220, 83);

	public Color scanlinesColor = new Color(255, 255, 255, 15);

	public OS()
	{
		multiplayer = false;
		currentInstance = this;
	}

	public OS(TcpClient socket, NetworkStream stream, bool actingServer, ScreenManager sman)
	{
		base.ScreenManager = sman;
		multiplayer = true;
		client = socket;
		isServer = actingServer;
		netStream = stream;
		inBuffer = new byte[4096];
		outBuffer = new byte[4096];
		encoder = new ASCIIEncoding();
		canRunContent = false;
		TextBox.cursorPosition = 0;
		currentInstance = this;
	}

	public override void LoadContent()
	{
		if (canRunContent)
		{
			delayer = new ActionDelayer();
			ComputerLoader.init(this);
			content = base.ScreenManager.Game.Content;
			username = ((SaveUserAccountName != null) ? SaveUserAccountName : (Settings.isConventionDemo ? Settings.ConventionLoginName : Environment.UserName));
			username = FileSanitiser.purifyStringForDisplay(username);
			Vector2 compLocation = new Vector2(0.1f, 0.5f);
			if (multiplayer && !isServer)
			{
				compLocation = new Vector2(0.8f, 0.8f);
			}
			ramAvaliable = totalRam;
			string text = NetworkMap.generateRandomIP();
			text = ((multiplayer && isServer) ? NetworkMap.generateRandomIP() : text);
			thisComputer = new Computer(username + " PC", NetworkMap.generateRandomIP(), compLocation, 5, 4, this);
			thisComputer.adminIP = thisComputer.ip;
			thisComputer.idName = "playerComp";
			thisComputer.Memory = new MemoryContents();
			Folder folder = thisComputer.files.root.searchForFolder("home");
			folder.folders.Add(new Folder("stash"));
			folder.folders.Add(new Folder("misc"));
			GibsonIP = NetworkMap.generateRandomIP();
			UserDetail value = thisComputer.users[0];
			value.known = true;
			thisComputer.users[0] = value;
			defaultUser = new UserDetail(username, "password", 1);
			defaultUser.known = true;
			OSTheme theme = OSTheme.HacknetBlue;
			if (Settings.isConventionDemo && !IsDLCConventionDemo && Settings.ShuffleThemeOnDemoStart)
			{
				double num = Utils.random.NextDouble();
				if (num < 0.25)
				{
					theme = OSTheme.HacknetMint;
				}
				else if (num < 0.5)
				{
					theme = OSTheme.HackerGreen;
				}
				else if (num < 0.75)
				{
					theme = OSTheme.HacknetPurple;
				}
			}
			ThemeManager.setThemeOnComputer(thisComputer, theme);
			if (multiplayer)
			{
				thisComputer.addMultiplayerTargetFile();
				sendMessage("newComp #" + thisComputer.ip + "#" + (int)compLocation.X + "#" + (int)compLocation.Y + "#" + 5 + "#" + thisComputer.name);
				multiplayerMissionLoaded = false;
			}
			if (!WillLoadSave)
			{
				People.init();
			}
			modules = new List<Module>();
			exes = new List<ExeModule>();
			shells = new List<ShellExe>();
			shellIPs = new List<string>();
			Viewport viewport = base.ScreenManager.GraphicsDevice.Viewport;
			int mODULE_WIDTH = RamModule.MODULE_WIDTH;
			int num2 = 205;
			int num3 = (int)((double)(viewport.Width - mODULE_WIDTH - 6) * 0.44420000000000004);
			int num4 = (int)((double)(viewport.Width - mODULE_WIDTH - 6) * 0.5558);
			int height = viewport.Height - num2 - TOP_BAR_HEIGHT - 6;
			terminal = new Terminal(new Rectangle(viewport.Width - 2 - num3, TOP_BAR_HEIGHT, num3, viewport.Height - TOP_BAR_HEIGHT - 2), this);
			terminal.name = "TERMINAL";
			modules.Add(terminal);
			netMap = new NetworkMap(new Rectangle(mODULE_WIDTH + 4, viewport.Height - num2 - 2, num4 - 1, num2), this);
			netMap.name = "netMap v1.7";
			modules.Add(netMap);
			display = new DisplayModule(new Rectangle(mODULE_WIDTH + 4, TOP_BAR_HEIGHT, num4 - 2, height), this);
			display.name = "DISPLAY";
			modules.Add(display);
			ram = new RamModule(new Rectangle(2, TOP_BAR_HEIGHT, mODULE_WIDTH, ramAvaliable + RamModule.contentStartOffset), this);
			ram.name = "RAM";
			modules.Add(ram);
			for (int i = 0; i < modules.Count; i++)
			{
				modules[i].LoadContent();
			}
			if (!Settings.IsInExtensionMode)
			{
				for (int i = 0; i < 2; i++)
				{
					if (isServer || !multiplayer)
					{
						thisComputer.links.Add(i);
					}
					else
					{
						thisComputer.links.Add(netMap.nodes.Count - 1 - i);
					}
				}
			}
			if (allFactions == null)
			{
				allFactions = new AllFactions();
				allFactions.init();
			}
			if (!Settings.IsInExtensionMode)
			{
				currentFaction = allFactions.factions[allFactions.currentFaction];
			}
			bool flag = false;
			if (!WillLoadSave)
			{
				netMap.nodes.Insert(0, thisComputer);
				netMap.visibleNodes.Add(0);
				if (!Settings.IsInExtensionMode)
				{
					MusicManager.loadAsCurrentSong(IsDLCConventionDemo ? "Music\\out_run_the_wolves" : "Music\\Revolve");
				}
				LanguageCreatedIn = Settings.ActiveLocale;
				if (Settings.IsInExtensionMode)
				{
					ExtensionLoader.LoadNewExtensionSession(ExtensionLoader.ActiveExtensionInfo, this);
				}
			}
			else
			{
				loadSaveFile();
				flag = true;
				Settings.initShowsTutorial = false;
				SaveFixHacks.FixSavesWithTerribleHacks(this);
			}
			if (!multiplayer && !flag && !Settings.IsInExtensionMode)
			{
				MailServer.shouldGenerateJunk = false;
				netMap.mailServer.addNewUser(thisComputer.ip, defaultUser);
			}
			topBar = new Rectangle(0, 0, viewport.Width, TOP_BAR_HEIGHT - 1);
			crashModule = new CrashModule(new Rectangle(0, 0, base.ScreenManager.GraphicsDevice.Viewport.Width, base.ScreenManager.GraphicsDevice.Viewport.Height), this);
			crashModule.LoadContent();
			introTextModule = new IntroTextModule(new Rectangle(0, 0, base.ScreenManager.GraphicsDevice.Viewport.Width, base.ScreenManager.GraphicsDevice.Viewport.Height), this);
			introTextModule.LoadContent();
			traceTracker = new TraceTracker(this);
			IncConnectionOverlay = new IncomingConnectionOverlay(this);
			scanLines = content.Load<Texture2D>("ScanLines");
			cross = content.Load<Texture2D>("Cross");
			cog = content.Load<Texture2D>("Cog");
			saveIcon = content.Load<Texture2D>("SaveIcon");
			beepSound = content.Load<SoundEffect>("SFX/beep");
			if (!Settings.IsInExtensionMode)
			{
				if (DLC1SessionUpgrader.HasDLC1Installed && username.ToLower() == "colamaeleon")
				{
					ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/CoelTheme.xml");
					ThemeManager.switchTheme(this, "DLC/Themes/CoelTheme.xml");
				}
				else if (DLC1SessionUpgrader.HasDLC1Installed && username.ToLower() == "rain_shatter")
				{
					ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/RainTheme.xml");
					ThemeManager.switchTheme(this, "DLC/Themes/RainTheme.xml");
				}
				else if (DLC1SessionUpgrader.HasDLC1Installed && username.ToLower() == "orann")
				{
					ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/RiptideThemeStandard.xml");
					ThemeManager.switchTheme(this, "DLC/Themes/RiptideThemeStandard.xml");
				}
				else if (DLC1SessionUpgrader.HasDLC1Installed && username.ToLower() == "hypernexus")
				{
					ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/MiamiThemeLightBlue.xml");
					ThemeManager.switchTheme(this, "DLC/Themes/MiamiThemeLightBlue.xml");
				}
			}
			if (!multiplayer && !flag && !Settings.IsInExtensionMode)
			{
				loadMissionNodes();
			}
			if (!HasLoadedDLCContent && Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed && !Settings.IsInExtensionMode)
			{
				DLC1SessionUpgrader.UpgradeSession(this, flag);
				HasLoadedDLCContent = true;
			}
			mailicon = new MailIcon(this, new Vector2(0f, 0f));
			mailicon.pos.X = viewport.Width - mailicon.getWidth() - 2;
			if (Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed)
			{
				hubServerAlertsIcon = new HubServerAlertsIcon(content, "dhs", new string[3]
				{
					"@channel",
					"@Channel",
					"@" + defaultUser.name
				});
				hubServerAlertsIcon.Init(this);
				if (HasLoadedDLCContent)
				{
					AircraftInfoOverlay = new AircraftInfoOverlay(this);
				}
			}
			SAChangeAlertIcon.UpdateAlertIcon(this);
			if (!flag)
			{
				MusicManager.playSong();
			}
			if (flag || !Settings.slowOSStartup)
			{
				initShowsTutorial = false;
				introTextModule.complete = true;
			}
			inputEnabled = true;
			isLoaded = true;
			fullscreen = new Rectangle(0, 0, base.ScreenManager.GraphicsDevice.Viewport.Width, base.ScreenManager.GraphicsDevice.Viewport.Height);
			TraceDangerSequence = new TraceDangerSequence(content, base.ScreenManager.SpriteBatch, fullscreen, this);
			endingSequence = new EndingSequenceModule(fullscreen, this);
			if (Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed)
			{
				BootAssitanceModule = new BootCrashAssistanceModule(fullscreen, this);
			}
			bool flag2 = true;
			bool flag3 = true;
			flag2 = Settings.slowOSStartup && !flag;
			if (Settings.IsInExtensionMode && !ExtensionLoader.ActiveExtensionInfo.HasIntroStartup)
			{
				flag2 = false;
				introTextModule.complete = true;
			}
			flag3 = Settings.osStartsWithTutorial && (!flag || !Flags.HasFlag("TutorialComplete"));
			if (Settings.IsInExtensionMode && !ExtensionLoader.ActiveExtensionInfo.StartsWithTutorial)
			{
				flag3 = false;
				initShowsTutorial = false;
			}
			if (flag2)
			{
				rebootThisComputer();
				if (Settings.initShowsTutorial)
				{
					display.visible = false;
					ram.visible = false;
					netMap.visible = false;
					terminal.visible = true;
				}
			}
			else if (flag3)
			{
				display.visible = false;
				ram.visible = false;
				netMap.visible = false;
				terminal.visible = true;
				terminal.reset();
				Settings.initShowsTutorial = true;
				initShowsTutorial = true;
				if (!TestingPassOnly)
				{
					execute("FirstTimeInitdswhupwnemfdsiuoewnmdsmffdjsklanfeebfjkalnbmsdakj Init");
				}
			}
			else if (Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed && HostileHackerBreakinSequence.IsInBlockingHostileFileState(this))
			{
				rebootThisComputer();
				BootAssitanceModule.ShouldSkipDialogueTypeout = true;
			}
			else
			{
				if (Settings.EnableDLC && HostileHackerBreakinSequence.IsFirstSuccessfulBootAfterBlockingState(this))
				{
					HostileHackerBreakinSequence.ReactToFirstSuccesfulBoot(this);
					rebootThisComputer();
				}
				if (!TestingPassOnly)
				{
					runCommand("connect " + thisComputer.ip);
				}
				Folder folder2 = thisComputer.files.root.searchForFolder("sys");
				if (folder2.searchForFile("Notes_Reopener.bat") != null)
				{
					runCommand("notes");
				}
			}
			if (!Settings.EnableDLC || !DLC1SessionUpgrader.HasDLC1Installed || !HasLoadedDLCContent)
			{
				return;
			}
			bool flag4 = false;
			if (!Flags.HasFlag("AircraftInfoOverlayDeactivated"))
			{
				if (Flags.HasFlag("AircraftInfoOverlayActivated"))
				{
					flag4 = true;
				}
				if (!flag4)
				{
					Computer computer = Programs.getComputer(this, "dair_crash");
					Folder folder3 = computer.files.root.searchForFolder("FlightSystems");
					bool flag5 = false;
					for (int i = 0; i < folder3.files.Count; i++)
					{
						if (folder3.files[i].name == "747FlightOps.dll")
						{
							flag5 = true;
						}
					}
					AircraftDaemon aircraftDaemon = (AircraftDaemon)computer.getDaemon(typeof(AircraftDaemon));
					if (!flag5 && !Flags.HasFlag("DLC_PlaneResult"))
					{
						flag4 = true;
					}
				}
			}
			if (flag4)
			{
				Computer computer = Programs.getComputer(this, "dair_crash");
				AircraftDaemon aircraftDaemon = (AircraftDaemon)computer.getDaemon(typeof(AircraftDaemon));
				aircraftDaemon.StartReloadFirmware();
				aircraftDaemon.StartUpdating();
				AircraftInfoOverlay.Activate();
				AircraftInfoOverlay.IsMonitoringDLCEndingCases = true;
				MissionFunctions.runCommand(0, "playAirlineCrashSongSequence");
			}
		}
		else if (multiplayer)
		{
			initializeNetwork();
		}
	}

	public void loadMultiplayerMission()
	{
		currentMission = (ActiveMission)ComputerLoader.readMission("Content/Missions/MultiplayerMission.xml");
	}

	public void loadMissionNodes()
	{
		if (multiplayer)
		{
			return;
		}
		List<string> list = BootLoadList.getList();
		for (int i = 0; i < list.Count; i++)
		{
			try
			{
				Computer.loadFromFile(list[i]);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				Console.WriteLine(ex.StackTrace);
				throw ex;
			}
		}
		if (ComputerLoader.postAllLoadedActions != null)
		{
			ComputerLoader.postAllLoadedActions();
		}
		if (Settings.isDemoMode)
		{
			list = BootLoadList.getDemoList();
			for (int i = 0; i < list.Count; i++)
			{
				Computer.loadFromFile(list[i]);
			}
		}
		if (!initShowsTutorial && !Settings.IsInExtensionMode && !IsDLCConventionDemo)
		{
			if (Settings.isSpecialTestBuild)
			{
				ComputerLoader.loadMission("Content/Missions/Misc/TesterCSECIntroMission.xml");
			}
			else
			{
				ComputerLoader.loadMission("Content/Missions/BitMissionIntro.xml");
			}
		}
		else if (!Settings.IsInExtensionMode)
		{
			currentMission = (ActiveMission)ComputerLoader.readMission("Content/Missions/BitMissionIntro.xml");
		}
	}

	public override void UnloadContent()
	{
	}

	public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
	{
		try
		{
			lastGameTime = gameTime;
			base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
			float num = (float)gameTime.ElapsedGameTime.TotalSeconds;
			SFX.Update(num);
			timer += num;
			if (isLoaded && bootingUp)
			{
				canRunContent = false;
				thisComputer.bootupTick(num);
				crashModule.Update(num);
				if (!thisComputer.disabled)
				{
					if (FirstTimeStartup && !introTextModule.complete)
					{
						introTextModule.Update(num);
					}
					if (introTextModule.complete)
					{
						FirstTimeStartup = false;
						bootingUp = false;
						canRunContent = true;
						crashModule.completeReboot();
					}
				}
			}
			delayer.Pump();
			if (canRunContent && isLoaded)
			{
				try
				{
					if (TraceDangerSequence.IsActive)
					{
						TraceDangerSequence.Update(num);
					}
					if (multiplayer)
					{
						if (!multiplayerMissionLoaded && opponentComputer != null)
						{
							loadMultiplayerMission();
							multiplayerMissionLoaded = true;
						}
						stayAliveTimer -= num;
						if (stayAliveTimer <= 0f)
						{
							stayAliveTimer = TCP_STAYALIVE_TIMER;
							sendMessage("stayAlive " + (int)currentElapsedTime);
						}
					}
					else if (IsInDLCMode || ShowDLCAlertsIcon)
					{
						hubServerAlertsIcon.Update(num);
					}
					else
					{
						mailicon.Update(num);
					}
					for (int i = 0; i < modules.Count; i++)
					{
						modules[i].Update(num);
					}
					if (UpdateSubscriptions != null)
					{
						UpdateSubscriptions(num);
					}
					if (connectedComp == null)
					{
						if (connectedIPLastFrame != null)
						{
							handleDisconnection();
						}
					}
					else if (connectedIPLastFrame != connectedComp.ip)
					{
						handleDisconnection();
					}
					for (int i = 0; i < TrackersInProgress.Count; i++)
					{
						if (connectedComp != null && connectedComp == TrackersInProgress[i].comp)
						{
							TrackersInProgress.RemoveAt(i);
							i--;
							continue;
						}
						TrackerDetail value = TrackersInProgress[i];
						value.timeLeft -= num;
						if (value.timeLeft <= 0f)
						{
							TrackerCompleteSequence.TrackComplete(this, value.comp);
							break;
						}
						TrackersInProgress[i] = value;
					}
					ramAvaliable = totalRam;
					if (exes.Count > 0)
					{
						exes[0].bounds.Y = ram.bounds.Y + RamModule.contentStartOffset;
					}
					for (int i = 0; i < exes.Count; i++)
					{
						exes[i].bounds.X = ram.bounds.X;
						if (i > 0 && i < exes.Count)
						{
							exes[i].bounds.Y = exes[i - 1].bounds.Y + exes[i - 1].bounds.Height;
						}
						if (exes[i].needsRemoval)
						{
							exes.RemoveAt(i);
							i--;
						}
						else
						{
							ramAvaliable -= exes[i].ramCost;
						}
					}
					for (int i = 0; i < exes.Count; i++)
					{
						exes[i].Update(num);
					}
					if (currentMission != null)
					{
						currentMission.Update(num);
					}
					for (int i = 0; i < branchMissions.Count; i++)
					{
						branchMissions[i].Update(num);
					}
					traceTracker.Update(num);
					if (gameSavedTextAlpha > 0f)
					{
						gameSavedTextAlpha -= num;
					}
					if (PorthackCompleteFlashTime > 0f)
					{
						PorthackCompleteFlashTime -= num;
					}
					if (MissionCompleteFlashTime > 0f)
					{
						MissionCompleteFlashTime -= num;
					}
					if (warningFlashTimer > 0f)
					{
						warningFlashTimer -= num;
						if (warningFlashTimer <= 0f)
						{
							highlightColor = defaultHighlightColor;
						}
						else
						{
							highlightColor = Color.Lerp(defaultHighlightColor, warningColor, warningFlashTimer / WARNING_FLASH_TIME);
							moduleColorSolid = Color.Lerp(moduleColorSolidDefault, warningColor, warningFlashTimer / WARNING_FLASH_TIME);
						}
					}
					EffectsUpdater.Update(num, this);
					float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
					IncConnectionOverlay.Update(dt);
					if (AircraftInfoOverlay != null && AircraftInfoOverlay.IsActive)
					{
						AircraftInfoOverlay.Update(dt);
					}
					currentElapsedTime += gameTime.ElapsedGameTime.TotalSeconds;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
					updateErrorCount++;
					if (updateErrorCount < 5)
					{
						Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
					}
				}
			}
			else
			{
				endingSequence.Update(num);
				if (IsInDLCMode)
				{
					BootAssitanceModule.Update(num);
				}
			}
			ConditionalActions.Update(num, this);
			connectedIPLastFrame = ((connectedComp != null) ? connectedComp.ip : null);
		}
		catch (Exception ex)
		{
			updateErrorCount++;
			if (updateErrorCount >= 3)
			{
				handleUpdateError();
			}
			else
			{
				Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
			}
		}
	}

	private void handleDisconnection()
	{
		Computer computer = Programs.getComputer(this, connectedIPLastFrame);
		if (computer != null)
		{
			computer.admin?.disconnectionDetected(computer, this);
			if (computer.HasTracker && TrackerCompleteSequence.CompShouldStartTrackerFromLogs(this, computer))
			{
				float timeLeft = TrackerCompleteSequence.MinTrackTime + Utils.randm(TrackerCompleteSequence.MaxTrackTime - TrackerCompleteSequence.MinTrackTime);
				TrackersInProgress.Add(new TrackerDetail
				{
					comp = computer,
					timeLeft = timeLeft
				});
			}
		}
	}

	public override void HandleInput(InputState input)
	{
		GuiData.doInput(input);
		if (Utils.keyPressed(input, Keys.NumLock, base.ScreenManager.controllingPlayer))
		{
			PostProcessor.bloomEnabled = !PostProcessor.bloomEnabled;
		}
	}

	public void drawBackground()
	{
		ThemeManager.drawBackgroundImage(GuiData.spriteBatch, fullscreen);
	}

	public void RequestRemovalOfAllPopups()
	{
		if (ExitToMenuMessageBox != null)
		{
			ExitToMenuMessageBox.ExitScreen();
			ExitToMenuMessageBox = null;
		}
	}

	public void drawScanlines()
	{
		if (!PostProcessor.scanlinesEnabled)
		{
			return;
		}
		Vector2 position = new Vector2(0f, 0f);
		while (position.X < (float)base.ScreenManager.GraphicsDevice.Viewport.Width)
		{
			while (position.Y < (float)base.ScreenManager.GraphicsDevice.Viewport.Height)
			{
				GuiData.spriteBatch.Draw(scanLines, position, scanlinesColor);
				position.Y += scanLines.Height;
			}
			position.Y = 0f;
			position.X += scanLines.Width;
		}
	}

	public void drawModules(GameTime gameTime)
	{
		Vector2 zero = Vector2.Zero;
		GuiData.spriteBatch.Draw(Utils.white, topBar, topBarColor);
		float t = (float)gameTime.ElapsedGameTime.TotalSeconds;
		try
		{
			if (connectedComp != null)
			{
				locationString = LocaleTerms.Loc("Location") + ": " + connectedComp.name + "@" + connectedIP + " ";
			}
			else
			{
				locationString = LocaleTerms.Loc("Location: Not Connected") + " ";
			}
			Vector2 vector = GuiData.UITinyfont.MeasureString(locationString);
			zero.X = (float)topBar.Width - vector.X - (float)mailicon.getWidth();
			zero.Y -= 3f;
			GuiData.spriteBatch.DrawString(GuiData.UITinyfont, locationString, zero, topBarTextColor);
			if (GuiData.ActiveFontConfig.tinyFontCharHeight * 2f <= (float)topBar.Height)
			{
				string text = LocaleTerms.Loc("Home IP:") + " " + thisComputer.ip + " ";
				zero.Y += topBar.Height / 2;
				vector = GuiData.UITinyfont.MeasureString(text);
				zero.X = (float)topBar.Width - vector.X - (float)mailicon.getWidth();
				GuiData.spriteBatch.DrawString(GuiData.UITinyfont, text, zero, topBarTextColor);
			}
			zero.Y = 0f;
		}
		catch (Exception)
		{
		}
		zero.X = 110f;
		if (!Settings.isLockedDemoMode && !DisableTopBarButtons)
		{
			if (Button.doButton(3827178, 3, 0, 20, topBar.Height - 1, "", topBarIconsColor, cross))
			{
				ExitToMenuMessageBox = new MessageBoxScreen(LocaleTerms.Loc("Quit HackNetOS\nCurrent Session?") + "\n", includesUsageText: false, hasEscPrompt: true);
				ExitToMenuMessageBox.OverrideAcceptedText = LocaleTerms.Loc("Exit to Menu");
				ExitToMenuMessageBox.Accepted += quitGame;
				base.ScreenManager.AddScreen(ExitToMenuMessageBox);
			}
			if (!TraceDangerSequence.IsActive && Button.doButton(3827179, 26, 0, 20, topBar.Height - 1, "", topBarIconsColor, cog))
			{
				saveGame();
				base.ScreenManager.AddScreen(new OptionsMenu(startedFromGameContext: true), base.ScreenManager.controllingPlayer);
			}
			bool flag = !initShowsTutorial && !TraceDangerSequence.IsActive;
			if ((flag & (!Settings.IsInExtensionMode || ExtensionLoader.ActiveExtensionInfo.AllowSave)) && Button.doButton(3827180, 49, 0, 20, topBar.Height - 1, "", topBarIconsColor, saveIcon))
			{
				saveGame();
			}
			if (!initShowsTutorial && Settings.debugCommandsEnabled)
			{
				if (Button.doButton(3827190, 72, 0, 20, topBar.Height - 1, "", topBarIconsColor, cog))
				{
					if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 0)
					{
						ThemeManager.switchTheme(this, OSTheme.HacknetBlue);
					}
					else if (ThemeManager.currentTheme == OSTheme.HacknetBlue)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/RiptideClassicTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/RiptideClassicTheme.xml");
						drawErrorCount = 2;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 2)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/MiamiTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/MiamiTheme.xml");
						drawErrorCount = 3;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 3)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/RainTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/RainTheme.xml");
						drawErrorCount = 4;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 4)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/RiptideTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/RiptideTheme.xml");
						drawErrorCount = 5;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 5)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/CautionTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/CautionTheme.xml");
						drawErrorCount = 6;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 6)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/HoraTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/HoraTheme.xml");
						drawErrorCount = 7;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 7)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/Floatvoid.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/FloatVoidTheme.xml");
						drawErrorCount = 8;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 8)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/MiamiThemeLightBlue.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/MiamiThemeLightBlue.xml");
						drawErrorCount = 9;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 9)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/StarfieldClassicTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/StarfieldClassicTheme.xml");
						drawErrorCount = 10;
					}
					else if (ThemeManager.currentTheme == OSTheme.Custom && drawErrorCount == 10)
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/CoelTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/CoelTheme.xml");
						drawErrorCount = 0;
					}
					else if (ThemeManager.currentTheme == OSTheme.HacknetPurple)
					{
						ThemeManager.switchTheme(this, OSTheme.HacknetMint);
					}
					else if (ThemeManager.currentTheme == OSTheme.HacknetMint)
					{
						ThemeManager.switchTheme(this, OSTheme.HackerGreen);
					}
					else
					{
						ThemeManager.setThemeOnComputer(thisComputer, "DLC/Themes/CautionTheme.xml");
						ThemeManager.switchTheme(this, "DLC/Themes/CautionTheme.xml");
					}
				}
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
		zero.Y = 1f;
		string text2 = string.Concat((int)(1.0 / gameTime.ElapsedGameTime.TotalSeconds + 0.5));
		GuiData.spriteBatch.DrawString(GuiData.UITinyfont, text2, zero, topBarTextColor);
		zero.Y = 0f;
		if (!multiplayer && !DisableTopBarButtons && !DisableEmailIcon)
		{
			if (IsInDLCMode || ShowDLCAlertsIcon)
			{
				int width = mailicon.getWidth();
				hubServerAlertsIcon.Draw(new Rectangle((int)mailicon.pos.X, (int)mailicon.pos.Y, width, topBar.Height - 2), GuiData.spriteBatch);
			}
			else
			{
				mailicon.Draw();
			}
		}
		int num = ram.bounds.Height + topBar.Height + 16;
		if (num < fullscreen.Height && ram.visible)
		{
			audioVisualizer.Draw(new Rectangle(ram.bounds.X, num + 1, ram.bounds.Width - 2, fullscreen.Height - num - 4), GuiData.spriteBatch);
		}
		for (int i = 0; i < modules.Count; i++)
		{
			if (modules[i].visible)
			{
				modules[i].PreDrawStep();
				modules[i].Draw(t);
				modules[i].PostDrawStep();
			}
		}
		if (ram.visible)
		{
			for (int i = 0; i < exes.Count; i++)
			{
				exes[i].Draw(t);
			}
		}
		IncConnectionOverlay.Draw(fullscreen, GuiData.spriteBatch);
		if (AircraftInfoOverlay != null && AircraftInfoOverlay.IsActive)
		{
			Rectangle dest = new Rectangle(fullscreen.X, TOP_BAR_HEIGHT + 1 + Module.PANEL_HEIGHT, fullscreen.Width, fullscreen.Height - (TOP_BAR_HEIGHT + 2 + Module.PANEL_HEIGHT));
			AircraftInfoOverlay.Draw(dest, GuiData.spriteBatch);
		}
		traceTracker.Draw(GuiData.spriteBatch);
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		float num2 = 1f - gameSavedTextAlpha;
		int num3 = 45;
		Vector2 pos = new Vector2(0f, base.ScreenManager.GraphicsDevice.Viewport.Height - num3);
		pos -= num2 * new Vector2(0f, 200f);
		TextItem.doFontLabel(pos, "SESSION SAVED", GuiData.titlefont, thisComputerNode * gameSavedTextAlpha, float.MaxValue, num3);
		TextItem.DrawShadow = drawShadow;
	}

	public void quitGame(object sender, PlayerIndexEventArgs e)
	{
		HasExitedAndEnded = true;
		ExitScreen();
		MainMenu.resetOS();
		base.ScreenManager.AddScreen(new MainMenu());
		SaveFileManager.Init(needsOtherSourcesUpdate: false);
	}

	public override void Draw(GameTime gameTime)
	{
		try
		{
			float t = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (lastGameTime == null)
			{
				lastGameTime = gameTime;
			}
			if (canRunContent && isLoaded)
			{
				PostProcessor.begin();
				GuiData.startDraw();
				try
				{
					if (!TraceDangerSequence.PreventOSRendering)
					{
						drawBackground();
						if (terminalOnlyMode)
						{
							terminal.Draw(t);
						}
						else
						{
							drawModules(gameTime);
						}
						SFX.Draw(GuiData.spriteBatch);
					}
					if (TraceDangerSequence.IsActive)
					{
						TraceDangerSequence.Draw();
					}
				}
				catch (Exception ex)
				{
					drawErrorCount++;
					if (drawErrorCount < 5)
					{
						Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex) + "\r\n\r\n");
					}
				}
				GuiData.endDraw();
				PostProcessor.end();
				GuiData.startDraw();
				if (postFXDrawActions != null)
				{
					postFXDrawActions();
					postFXDrawActions = null;
				}
				drawScanlines();
				GuiData.endDraw();
			}
			else if (endingSequence.IsActive)
			{
				PostProcessor.begin();
				base.ScreenManager.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone);
				endingSequence.Draw(t);
				drawScanlines();
				base.ScreenManager.SpriteBatch.End();
				PostProcessor.end();
			}
			else if (BootAssitanceModule != null && BootAssitanceModule.IsActive)
			{
				PostProcessor.begin();
				base.ScreenManager.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone);
				BootAssitanceModule.Draw(t);
				drawScanlines();
				base.ScreenManager.SpriteBatch.End();
				PostProcessor.end();
			}
			else if (bootingUp)
			{
				PostProcessor.begin();
				base.ScreenManager.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone);
				if (thisComputer.disabled)
				{
					RequestRemovalOfAllPopups();
					if (TraceDangerSequence.IsActive)
					{
						TraceDangerSequence.CancelTraceDangerSequence();
					}
					crashModule.Draw(t);
				}
				else
				{
					introTextModule.Draw(t);
				}
				base.ScreenManager.SpriteBatch.End();
				PostProcessor.end();
			}
			else
			{
				GuiData.startDraw();
				TextItem.doSmallLabel(new Vector2(0f, 700f), LocaleTerms.Loc("Loading..."), null);
				GuiData.endDraw();
			}
		}
		catch (Exception ex)
		{
			drawErrorCount++;
			if (drawErrorCount >= 3)
			{
				handleDrawError();
			}
			else
			{
				Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
			}
		}
	}

	private void handleUpdateError()
	{
		DebugLog.data.Add("----------------------Handling Update error");
		connectedComp = null;
	}

	private void handleDrawError()
	{
		connectedComp = null;
		base.ScreenManager.GraphicsDevice.SetRenderTarget(null);
	}

	public void endMultiplayerMatch(bool won)
	{
		if (won)
		{
			sendMessage("mpOpponentWin " + timer);
		}
		base.ScreenManager.AddScreen(new MultiplayerGameOverScreen(won));
	}

	private void initializeNetwork()
	{
		if (isServer)
		{
			string message = "init " + Utils.random.Next();
			sendMessage(message);
			Multiplayer.parseInputMessage(message, this);
		}
		else
		{
			sendMessage("clientConnect Hallo");
		}
		listenerThread = new Thread(listenThread);
		listenerThread.Name = "OS Listener Thread";
		listenerThread.Start();
		Console.WriteLine("Created Network Listener Thread");
	}

	public void sendMessage(string message)
	{
		if (netStream.CanWrite)
		{
			outBuffer = encoder.GetBytes(message + "#&#");
			netStream.Write(outBuffer, 0, outBuffer.Length);
			netStream.Flush();
			for (int i = 0; i < message.Length; i++)
			{
				outBuffer[i] = 0;
			}
		}
	}

	private void listenThread()
	{
		int num = 0;
		while (!DestroyThreads)
		{
			try
			{
				if (netStream.Read(inBuffer, 0, inBuffer.Length) == 0)
				{
					clientDisconnected();
				}
				else
				{
					string text = encoder.GetString(inBuffer).Trim(trimChars);
					string[] separator = new string[1] { "#&#" };
					string[] array = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					for (int i = 0; i < array.Length; i++)
					{
						Multiplayer.parseInputMessage(array[i], this);
					}
					for (int i = 0; i < inBuffer.Length; i++)
					{
						inBuffer[i] = 0;
					}
				}
			}
			catch (IOException ex)
			{
				if (ex.Message.Contains("Unable to read"))
				{
					write("Opponent Disconnected - Victory");
					base.ScreenManager.AddScreen(new MultiplayerGameOverScreen(winner: true));
					netStream.Close();
					return;
				}
				write("NetError: " + DisplayModule.splitForWidth(ex.ToString(), terminal.bounds.Width - 20));
			}
			catch (Exception ex2)
			{
				write("NetError: " + DisplayModule.splitForWidth(ex2.ToString(), terminal.bounds.Width - 20));
				break;
			}
			if (Game1.threadsExiting)
			{
				return;
			}
		}
		Console.WriteLine("Listener Thread Exiting");
	}

	public void clientDisconnected()
	{
		write("DISCONNECTION detected");
		DestroyThreads = true;
		netStream.Close();
		client.Close();
	}

	public void timerExpired()
	{
		if (traceCompleteOverrideAction != null)
		{
			traceCompleteOverrideAction();
			return;
		}
		if (connectedComp != null && connectedComp.admin != null)
		{
			connectedComp.admin.traceEjectionDetected(connectedComp, this);
		}
		if (Flags.HasFlag("CSEC_Member"))
		{
			TraceDangerSequence.BeginTraceDangerSequence();
		}
		else
		{
			thisComputer.crash(thisComputer.ip);
		}
	}

	public void loadSaveFile()
	{
		Stream stream = null;
		stream = ((ForceLoadOverrideStream == null || !TestingPassOnly) ? SaveFileManager.GetSaveReadStream(SaveGameUserName) : ForceLoadOverrideStream);
		if (stream != null)
		{
			XmlReader xmlReader = XmlReader.Create(stream);
			loadTitleSaveData(xmlReader);
			if (Settings.ActiveLocale != LanguageCreatedIn)
			{
				LocaleActivator.ActivateLocale(LanguageCreatedIn, content);
				Settings.ActiveLocale = LanguageCreatedIn;
			}
			LoadExtraTitleSaveData(xmlReader);
			if (base.IsExiting)
			{
				return;
			}
			Flags.Load(xmlReader);
			netMap.load(xmlReader);
			currentMission = (ActiveMission)ActiveMission.load(xmlReader);
			if (currentMission != null && !(currentMission.reloadGoalsSourceFile == "Missions/BitMissionIntro.xml"))
			{
				ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(currentMission.reloadGoalsSourceFile);
			}
			Console.WriteLine(branchMissions.Count);
			allFactions = AllFactions.loadFromSave(xmlReader);
			if (!string.IsNullOrWhiteSpace(allFactions.currentFaction))
			{
				allFactions.setCurrentFaction(allFactions.currentFaction, this);
			}
			loadOtherSaveData(xmlReader);
			WillLoadSave = false;
			stream.Flush();
			stream.Close();
		}
		FirstTimeStartup = false;
	}

	public void saveGame()
	{
		if (!initShowsTutorial && (!Settings.IsInExtensionMode || ExtensionLoader.ActiveExtensionInfo.AllowSave))
		{
			execute("save!(SJN!*SNL8vAewew57WewJdwl89(*4;;;&!)@&(ak'^&#@J3KH@!*");
		}
	}

	public void threadedSaveExecute(bool preventSaveText = false)
	{
		if (SaveInProgress)
		{
			if (SaveInQueue)
			{
				return;
			}
			SaveInQueue = true;
		}
		lock (SaveFileManager.CurrentlySaving)
		{
			SaveInProgress = true;
			if (SaveInQueue)
			{
				SaveInQueue = false;
			}
			if (!preventSaveText)
			{
				gameSavedTextAlpha = 1f;
			}
			writeSaveGame(SaveUserAccountName);
			StatsManager.SaveStatProgress();
			Console.WriteLine("Session Saved");
			SaveInProgress = false;
		}
	}

	private void writeSaveGame(string filename)
	{
		string text = "<?xml version =\"1.0\" encoding =\"UTF-8\" ?>\n";
		object obj = text;
		text = string.Concat(obj, "<HacknetSave generatedMissionCount=\"", MissionGenerator.generationCount, "\" Username=\"", username, "\" Language=\"", LanguageCreatedIn, "\" DLCMode=\"", IsInDLCMode, "\" DisableMailIcon=\"", DisableEmailIcon, "\">\n");
		text += GetDLCSaveString();
		text += Flags.GetSaveString();
		text += netMap.getSaveString();
		text = ((currentMission == null) ? (text + "<mission next=\"NULL_MISSION\" goals=\"none\" activeCheck=\"none\">\n</mission>") : (text + currentMission.getSaveString()));
		text += "<branchMissions>\n";
		for (int i = 0; i < branchMissions.Count; i++)
		{
			text += branchMissions[i].getSaveString();
		}
		text += "</branchMissions>";
		text += allFactions.getSaveString();
		string text2 = text;
		text = text2 + "<other music=\"" + MusicManager.currentSongName + "\" homeNode=\"" + homeNodeID + "\" homeAssetsNode=\"" + homeAssetServerID + "\" />";
		text += "</HacknetSave>";
		SaveFileManager.WriteSaveData(text, filename);
	}

	public void LoadExtraTitleSaveData(XmlReader rdr)
	{
		IsDLCSave = false;
		while (!(rdr.Name == "Flags") || !rdr.IsStartElement())
		{
			if (rdr.Name == "DLC")
			{
				ReadDLCSaveData(rdr);
			}
			rdr.Read();
		}
	}

	private void ReadDLCSaveData(XmlReader rdr)
	{
		IsDLCSave = true;
		while (!(rdr.Name == "DLC") || rdr.IsStartElement())
		{
			if (rdr.Name == "DLC")
			{
				if (rdr.MoveToAttribute("Active"))
				{
					IsInDLCMode = rdr.ReadContentAsString().ToLower() == "true";
				}
				if (rdr.MoveToAttribute("LoadedContent"))
				{
					HasLoadedDLCContent = rdr.ReadContentAsString().ToLower() == "true";
				}
			}
			if (rdr.Name == "Flags" && rdr.MoveToAttribute("OriginalFaction"))
			{
				PreDLCFaction = rdr.ReadContentAsString();
			}
			if (rdr.Name == "OriginalVisibleNodes")
			{
				PreDLCVisibleNodesCache = rdr.ReadElementContentAsString();
			}
			if (rdr.Name == "ConditionalActions")
			{
				ConditionalActions = RunnableConditionalActions.Deserialize(rdr);
			}
			rdr.Read();
		}
		if (HasLoadedDLCContent && !DLC1SessionUpgrader.HasDLC1Installed)
		{
			MainMenu.AccumErrors = "LOAD ERROR: Save " + SaveGameUserName + " is configured for Labyrinths DLC, but it is not installed on this computer.\n\n\n";
			ExitScreen();
			base.IsExiting = true;
		}
	}

	private string GetDLCSaveString()
	{
		string text = "<DLC Active=\"" + IsInDLCMode + "\" LoadedContent=\"" + HasLoadedDLCContent + "\">\n";
		text = text + "<Flags OriginalFaction=\"" + PreDLCFaction + "\"/>\n";
		text = text + "<OriginalVisibleNodes>" + PreDLCVisibleNodesCache + "</OriginalVisibleNodes>\n";
		text = text + ConditionalActions.GetSaveString() + "\n";
		return text + "</DLC>";
	}

	public void loadTitleSaveData(XmlReader reader)
	{
		while (reader.Name != "HacknetSave")
		{
			if (reader.EOF)
			{
				return;
			}
			reader.Read();
		}
		if (reader.MoveToAttribute("generatedMissionCount"))
		{
			int generationCount = reader.ReadContentAsInt();
			MissionGenerator.generationCount = generationCount;
		}
		else
		{
			MissionGenerator.generationCount = 100;
		}
		if (reader.MoveToAttribute("Username"))
		{
			username = reader.ReadContentAsString();
			defaultUser.name = username;
		}
		if (reader.MoveToAttribute("Language"))
		{
			LanguageCreatedIn = reader.ReadContentAsString();
		}
		else
		{
			LanguageCreatedIn = "en-us";
		}
		if (reader.MoveToAttribute("DLCMode"))
		{
			IsInDLCMode = reader.ReadContentAsString().ToLower() == "true" && Settings.EnableDLC;
		}
		if (reader.MoveToAttribute("DisableMailIcon"))
		{
			DisableEmailIcon = reader.ReadContentAsString().ToLower() == "true" && Settings.EnableDLC;
		}
	}

	public void setMouseVisiblity(bool mouseIsVisible)
	{
		delayer.Post(ActionDelayer.NextTick(), delegate
		{
			Game1.getSingleton().IsMouseVisible = mouseIsVisible;
		});
	}

	public void loadBranchMissionsSaveData(XmlReader reader)
	{
		while (reader.Name != "branchMissions")
		{
			if (reader.EOF)
			{
				return;
			}
			reader.Read();
			if (reader.Name == "other")
			{
				return;
			}
		}
		if (!reader.IsStartElement())
		{
			return;
		}
		branchMissions.Clear();
		reader.Read();
		while (true)
		{
			bool flag = true;
			while ((!reader.IsStartElement() || !(reader.Name == "mission")) && (reader.IsStartElement() || !(reader.Name == "branchMissions")))
			{
				reader.Read();
			}
			if (reader.Name == "branchMissions")
			{
				break;
			}
			ActiveMission item = (ActiveMission)ActiveMission.load(reader);
			branchMissions.Add(item);
		}
	}

	public void loadOtherSaveData(XmlReader reader)
	{
		while (reader.Name != "other")
		{
			if (reader.EOF)
			{
				return;
			}
			reader.Read();
		}
		reader.MoveToAttribute("music");
		string songname = reader.ReadContentAsString();
		MusicManager.playSongImmediatley(songname);
		if (reader.MoveToAttribute("homeNode"))
		{
			string text = reader.ReadContentAsString();
			homeNodeID = text;
		}
		if (reader.MoveToAttribute("homeAssetsNode"))
		{
			string text2 = reader.ReadContentAsString();
			homeAssetServerID = text2;
		}
	}

	public override void inputMethodChanged(bool usingGamePad)
	{
	}

	public void write(string text)
	{
		if (terminal != null && text.Length > 0)
		{
			string text2 = DisplayModule.cleanSplitForWidth(text, terminal.bounds.Width - 40);
			if (text2[text2.Length - 1] == '\n')
			{
				text2 = text2.Substring(0, text2.Length - 1);
			}
			terminal.writeLine(text2);
		}
	}

	public void writeSingle(string text)
	{
		terminal.write(text);
	}

	public void runCommand(string text)
	{
		if (!terminal.preventingExecution)
		{
			write("\n" + terminal.prompt + text);
			terminal.lastRunCommand = text;
			execute(text);
		}
	}

	public void execute(string text)
	{
		string[] parameter = text.Split(' ');
		Thread thread = new Thread(threadExecute);
		thread.Name = "exe" + thread.Name;
		if (!text.StartsWith("save"))
		{
			thread.IsBackground = true;
		}
		thread.CurrentCulture = Game1.culture;
		thread.CurrentUICulture = Game1.culture;
		Console.WriteLine("Spawning thread for command " + text);
		thread.Start(parameter);
	}

	public void connectedComputerCrashed(Computer c)
	{
		if (connectedComp != null)
		{
			connectedComp.disconnecting(thisComputer.ip);
		}
		connectedComp = null;
		display.command = "crash";
		terminal.prompt = "> ";
	}

	public void thisComputerCrashed()
	{
		display.command = "";
		connectedComp = null;
		bootingUp = true;
		exes.Clear();
		shellIPs.Clear();
		crashModule.reset();
		setMouseVisiblity(mouseIsVisible: false);
	}

	public void thisComputerIPReset()
	{
		if (traceTracker.active)
		{
			traceTracker.active = false;
		}
		if (TraceDangerSequence.IsActive)
		{
			TraceDangerSequence.CompleteIPResetSucsesfully();
		}
		thisComputer.adminIP = thisComputer.ip;
	}

	public void rebootThisComputer()
	{
		setMouseVisiblity(mouseIsVisible: false);
		crashModule.reset();
		inputEnabled = false;
		bootingUp = true;
		thisComputer.disabled = true;
		canRunContent = false;
		thisComputer.silent = true;
		thisComputer.crash(thisComputer.ip);
		thisComputer.silent = false;
		thisComputer.bootupTick(CrashModule.BLUESCREEN_TIME + 0.5f);
		crashModule.Update(CrashModule.BLUESCREEN_TIME + 0.5f);
	}

	public void RefreshTheme()
	{
		topBarColor = defaultTopBarColor;
		highlightColor = defaultHighlightColor;
		moduleColorSolid = moduleColorSolidDefault;
	}

	private void threadExecute(object threadText)
	{
		try
		{
			string[] arguments = (string[])threadText;
			validCommand = ProgramRunner.ExecuteProgram(this, arguments);
		}
		catch (Exception)
		{
			int num = 0;
			num++;
		}
	}

	public bool hasConnectionPermission(bool admin)
	{
		if (admin)
		{
			if (connectedComp == null || connectedComp.adminIP == thisComputer.ip)
			{
				return true;
			}
			if (connectedComp.currentUser.type == 0 && connectedComp.currentUser.name != null)
			{
				return true;
			}
			return false;
		}
		bool flag = !admin;
		if (flag && connectedComp != null && !connectedComp.userLoggedIn)
		{
			flag = false;
		}
		return connectedComp == null || connectedComp.adminIP == thisComputer.ip || flag;
	}

	public void takeAdmin()
	{
		if (connectedComp != null)
		{
			connectedComp.giveAdmin(thisComputer.ip);
			runCommand("connect " + connectedComp.ip);
		}
	}

	public void takeAdmin(string ip)
	{
		Computer computer = Programs.getComputer(this, ip);
		if (computer != null)
		{
			computer.giveAdmin(thisComputer.ip);
			runCommand("connect " + computer.ip);
		}
	}

	public void warningFlash()
	{
		warningFlashTimer = WARNING_FLASH_TIME;
	}

	public Rectangle getExeBounds()
	{
		int num = ram.bounds.Y + RamModule.contentStartOffset;
		for (int i = 0; i < exes.Count; i++)
		{
			num += exes[i].bounds.Height;
		}
		return new Rectangle(ram.bounds.X, num, 252, (int)EXE_MODULE_HEIGHT);
	}

	public void launchExecutable(string exeName, string exeFileData, int targetPort, string[] allParams = null, string originalName = null)
	{
		int num = ram.bounds.Y + RamModule.contentStartOffset;
		for (int i = 0; i < exes.Count; i++)
		{
			num += exes[i].bounds.Height;
		}
		Rectangle location = new Rectangle(ram.bounds.X, num, RamModule.MODULE_WIDTH, (int)EXE_MODULE_HEIGHT);
		exeName = exeName.ToLower();
		if (exeName.Equals("porthack"))
		{
			bool flag = false;
			bool flag2 = false;
			if (connectedComp != null)
			{
				int num2 = 0;
				for (int i = 0; i < connectedComp.portsOpen.Count; i++)
				{
					num2 += connectedComp.portsOpen[i];
				}
				if (num2 > connectedComp.portsNeededForCrack)
				{
					flag = true;
				}
				if (connectedComp.firewall != null && !connectedComp.firewall.solved)
				{
					if (flag)
					{
						flag2 = true;
					}
					flag = false;
				}
			}
			if (flag)
			{
				addExe(new PortHackExe(location, this));
			}
			else if (flag2)
			{
				write(LocaleTerms.Loc("Target Machine Rejecting Syndicated UDP Traffic") + " -\n" + LocaleTerms.Loc("Bypass Firewall to allow unrestricted traffic"));
			}
			else
			{
				write(LocaleTerms.Loc("Too Few Open Ports to Run") + " - \n" + LocaleTerms.Loc("Open Additional Ports on Target Machine") + "\n");
			}
			return;
		}
		if (exeName.Equals("forkbomb"))
		{
			if (hasConnectionPermission(admin: true))
			{
				if (connectedComp == null || connectedComp.ip.Equals(thisComputer.ip))
				{
					addExe(new ForkBombExe(location, this, thisComputer.ip));
				}
				else if (multiplayer && connectedComp.ip.Equals(opponentComputer.ip))
				{
					sendMessage("eForkBomb " + connectedComp.ip);
				}
				else
				{
					connectedComp.crash(thisComputer.ip);
				}
			}
			else
			{
				write(LocaleTerms.Loc("Requires Administrator Access to Run"));
			}
			return;
		}
		if (exeName.Equals("shell"))
		{
			if (hasConnectionPermission(admin: true))
			{
				bool flag3 = false;
				string value = ((connectedComp == null) ? thisComputer.ip : connectedComp.ip);
				for (int i = 0; i < exes.Count; i++)
				{
					if (exes[i] is ShellExe)
					{
						ShellExe shellExe = (ShellExe)exes[i];
						if (shellExe.targetIP.Equals(value))
						{
							flag3 = true;
						}
					}
				}
				if (!flag3)
				{
					addExe(new ShellExe(location, this));
				}
				else
				{
					write(LocaleTerms.Loc("This computer is already running a shell."));
				}
			}
			else
			{
				write(LocaleTerms.Loc("Requires Administrator Access to Run"));
			}
			return;
		}
		string text = null;
		string filename = ((originalName == null) ? exeName : originalName);
		text = PortExploits.GetExeNameForData(filename, exeFileData);
		if (text != null)
		{
			switch (text)
			{
			case "SSHcrack.exe":
				addExe(new SSHCrackExe(location, this));
				break;
			case "FTPBounce.exe":
				addExe(new FTPBounceExe(location, this));
				break;
			case "SMTPoverflow.exe":
				addExe(new SMTPoverflowExe(location, this));
				break;
			case "WebServerWorm.exe":
				addExe(new HTTPExploitExe(location, this));
				break;
			case "Tutorial.exe":
				addExe(new AdvancedTutorial(location, this));
				break;
			case "Notes.exe":
				addExe(new NotesExe(location, this));
				break;
			case "SecurityTracer.exe":
				addExe(new SecurityTraceExe(location, this));
				break;
			case "SQL_MemCorrupt.exe":
				addExe(new SQLExploitExe(location, this));
				break;
			case "Decypher.exe":
				addExe(new DecypherExe(location, this, allParams));
				break;
			case "DECHead.exe":
				addExe(new DecypherTrackExe(location, this, allParams));
				break;
			case "Clock.exe":
				addExe(new ClockExe(location, this, allParams));
				break;
			case "KBT_PortTest.exe":
				addExe(new MedicalPortExe(location, this, allParams));
				break;
			case "TraceKill.exe":
				addExe(new TraceKillExe(location, this, allParams));
				break;
			case "eosDeviceScan.exe":
				addExe(new EOSDeviceScannerExe(location, this, allParams));
				break;
			case "themechanger.exe":
				addExe(new ThemeChangerExe(location, this, allParams));
				break;
			case "HexClock.exe":
				addExe(new HexClockExe(location, this, allParams));
				break;
			case "Sequencer.exe":
				addExe(new SequencerExe(location, this, allParams));
				break;
			case "hacknet.exe":
				write(" ");
				write(" ----- Error ----- ");
				write(" ");
				write("Program \"hacknet.exe\" is already running!");
				write(" ");
				write(" ----------------- ");
				write(" ");
				break;
			case "TorrentStreamInjector.exe":
				addExe(new TorrentPortExe(location, this, allParams));
				break;
			case "SSLTrojan.exe":
			{
				SSLPortExe sSLPortExe = SSLPortExe.GenerateInstanceOrNullFromArguments(allParams, location, this, (connectedComp == null) ? thisComputer : connectedComp);
				if (sSLPortExe != null)
				{
					addExe(sSLPortExe);
				}
				break;
			}
			case "KaguyaTrial.exe":
			{
				bool flag4 = false;
				for (int i = 0; i < exes.Count; i++)
				{
					if (exes[i] is DLCIntroExe)
					{
						flag4 = true;
					}
				}
				if (!flag4)
				{
					addExe(new DLCIntroExe(location, this, allParams));
				}
				else
				{
					write(LocaleTerms.Loc("Kaguya Trials Already Running!"));
				}
				break;
			}
			case "FTPSprint.exe":
				addExe(new FTPFastExe(location, this, allParams));
				break;
			case "SignalScramble.exe":
			{
				DLCTraceSlower dLCTraceSlower = DLCTraceSlower.GenerateInstanceOrNullFromArguments(allParams, location, this, (connectedComp == null) ? thisComputer : connectedComp);
				if (dLCTraceSlower != null)
				{
					addExe(dLCTraceSlower);
				}
				break;
			}
			case "MemForensics.exe":
			{
				MemoryForensicsExe memoryForensicsExe = MemoryForensicsExe.GenerateInstanceOrNullFromArguments(location, this, allParams);
				if (memoryForensicsExe != null)
				{
					addExe(memoryForensicsExe);
				}
				break;
			}
			case "MemDumpGenerator.exe":
			{
				MemoryDumpDownloader memoryDumpDownloader = MemoryDumpDownloader.GenerateInstanceOrNullFromArguments(allParams, location, this, (connectedComp == null) ? thisComputer : connectedComp);
				if (memoryDumpDownloader != null)
				{
					addExe(memoryDumpDownloader);
				}
				break;
			}
			case "PacificPortcrusher.exe":
				addExe(new PacificPortExe(location, this, allParams));
				break;
			case "NetmapOrganizer.exe":
				addExe(new NetmapOrganizerExe(location, this, allParams));
				break;
			case "ComShell.exe":
				ShellOverloaderExe.RunShellOverloaderExe(allParams, this, (connectedComp == null) ? thisComputer : connectedComp);
				break;
			case "DNotes.exe":
				NotesDumperExe.RunNotesDumperExe(allParams, this, (connectedComp == null) ? thisComputer : connectedComp);
				break;
			case "ClockV2.exe":
				addExe(new Clock2Exe(location, this, allParams));
				break;
			case "Tuneswap.exe":
				addExe(new TuneswapExe(location, this, allParams));
				break;
			case "RTSPCrack.exe":
				addExe(new RTSPPortExe(location, this, allParams));
				break;
			case "ESequencer.exe":
				addExe(new ExtensionSequencerExe(location, this, allParams));
				break;
			case "OpShell.exe":
				ShellReopenerExe.RunShellReopenerExe(allParams, this, (connectedComp == null) ? thisComputer : connectedComp);
				break;
			}
		}
		else
		{
			write(LocaleTerms.Loc("Program not Found"));
		}
	}

	public void addExe(ExeModule exe)
	{
		Computer computer = ((connectedComp == null) ? thisComputer : connectedComp);
		if (exe.needsProxyAccess && computer.proxyActive)
		{
			write(LocaleTerms.Loc("Proxy Active -- Cannot Execute"));
		}
		else if (ramAvaliable >= exe.ramCost)
		{
			exe.LoadContent();
			exes.Add(exe);
		}
		else
		{
			ram.FlashMemoryWarning();
			write(LocaleTerms.Loc("Insufficient Memory"));
		}
	}

	public void failBoot()
	{
		graphicsFailBoot();
	}

	public void graphicsFailBoot()
	{
		ThemeManager.switchTheme(this, OSTheme.TerminalOnlyBlack);
		topBar.Y = -100000;
		terminalOnlyMode = true;
	}

	public void sucsesfulBoot()
	{
		topBar.Y = 0;
		terminalOnlyMode = false;
		connectedComp = null;
	}
}
