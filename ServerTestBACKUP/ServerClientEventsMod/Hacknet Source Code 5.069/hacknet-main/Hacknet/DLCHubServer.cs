using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hacknet.Daemons.Helpers;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class DLCHubServer : Daemon, IMonitorableDaemon
{
	private enum DHSState
	{
		Welcome,
		Login,
		Home,
		ArchiveList,
		MissionSelectView,
		ContractDetailView
	}

	internal class ClaimableMission
	{
		public string AgentClaim;

		public bool IsComplete;

		public ActiveMission Mission;

		public float UITextScrollDown;
	}

	public const string ROOT_FOLDERNAME = "HomeBase";

	public const string MISSION_FOLDERNAME = "contracts";

	public const string ARCHIVE_FOLDERNAME = "archive";

	public const string ACTIONS_FOLDERNAME = "runtime";

	public const string CONFIG_FILENAME = "dhs_config.sys";

	public List<string> Agents = new List<string>();

	public Color themeColor = Color.MediumVioletRed;

	private string groupName = "Bibliotheca";

	public bool AddsFactionPointForMissionCompleteion = true;

	public bool AutoClearMissionsOnSingleComplete = true;

	public bool AllowContractAbbandon = false;

	public DelayableActionSystem DelayedActions;

	private Folder rootFolder;

	private Folder missionFolder;

	private Folder archivesFolder;

	private Folder actionsFolder;

	private DHSState State = DHSState.Welcome;

	private ClaimableMission SelectedMission = null;

	private bool isAddingTextResponse = false;

	private List<string> MissionTextResponses = new List<string>();

	private string inProgressTextResponse = null;

	private float BaseWelcomeFadeoutTime = 4f;

	private float WelcomeFadeoutTimerLeft = 4f;

	public IRCSystem IRCSystem;

	private Dictionary<string, Color> HighlightedWords = new Dictionary<string, Color>();

	private SoundEffect ButtonPressSound;

	private SoundEffect WooshBuildup;

	private SoundEffect USIntro;

	internal List<ClaimableMission> ActiveMissions = new List<ClaimableMission>(3);

	private Texture2D MissionAvaliableIcon;

	private Texture2D MissionTakenIcon;

	private Texture2D MissionPlayersIcon;

	private Texture2D LoadingSpinner;

	private int UIButtonOffset = 0;

	private ScrollableTextRegion ScrollableTextPanel;

	private bool ShouldShowMissionIncompleteMessage = false;

	private bool AbandonMissionShowConfirmation = false;

	private HexGridBackground HexBackground;

	private float timeSpentInLoading = 0f;

	private bool HasStartedWoosh = false;

	public DLCHubServer(Computer c, string serviceName, string group, OS _os)
		: base(c, serviceName, _os)
	{
		groupName = group;
		MissionAvaliableIcon = os.content.Load<Texture2D>("DLC/Icons/focus_icon");
		MissionTakenIcon = os.content.Load<Texture2D>("DLC/Icons/cross_icon");
		MissionPlayersIcon = os.content.Load<Texture2D>("DLC/Icons/focus_icon");
		LoadingSpinner = os.content.Load<Texture2D>("Sprites/Spinner");
		ScrollableTextPanel = new ScrollableTextRegion(GuiData.spriteBatch.GraphicsDevice);
		HexBackground = new HexGridBackground(os.content);
		ButtonPressSound = os.content.Load<SoundEffect>("SFX/Bip");
		WooshBuildup = os.content.Load<SoundEffect>("DLC/SFX/Kilmer_Woosh");
		USIntro = os.content.Load<SoundEffect>("DLC/SFX/UserspaceIntro");
		InitDefaults();
	}

	public override void initFiles()
	{
		initFilesystem();
	}

	private void InitDefaults()
	{
		Agents.Add("Channel");
		HighlightedWords.Add("Channel", new Color(240, 234, 81));
	}

	public void initFilesystem()
	{
		rootFolder = new Folder("HomeBase");
		missionFolder = new Folder("contracts");
		rootFolder.folders.Add(missionFolder);
		archivesFolder = new Folder("archive");
		rootFolder.folders.Add(archivesFolder);
		actionsFolder = new Folder("runtime");
		rootFolder.folders.Add(actionsFolder);
		DelayedActions = new DelayableActionSystem(actionsFolder, os);
		FileEntry item = new FileEntry(Computer.generateBinaryString(512), "runtime.dll");
		rootFolder.files.Add(item);
		ReGenerateConfigFile();
		IRCSystem = new IRCSystem(rootFolder);
		IRCSystem.AttachmentPressedSound = ButtonPressSound;
		InitTests();
		comp.files.root.folders.Add(rootFolder);
	}

	private void InitTests()
	{
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		ReadActiveMissions();
		if (!os.Flags.HasFlag("DLC_Player_IRC_Authenticated"))
		{
			State = DHSState.Welcome;
		}
		else
		{
			State = DHSState.Home;
		}
		ShouldShowMissionIncompleteMessage = false;
		if (!(comp.idName == "dhs"))
		{
			return;
		}
		if (ActiveMissions.Count == 0 && !os.Flags.HasFlag("dlc_complete"))
		{
			MissionFunctions.runCommand(1, "addRankSilent");
		}
		Computer computer = Programs.getComputer(os, "dhsDrop");
		if (computer == null)
		{
			return;
		}
		Folder folder = computer.files.root.searchForFolder("bin");
		bool flag = false;
		for (int i = 0; i < folder.files.Count; i++)
		{
			if (folder.files[i].data == PortExploits.crackExeData[13])
			{
				flag = true;
			}
		}
		if (!flag)
		{
			folder.files.Add(new FileEntry(PortExploits.crackExeData[13], PortExploits.cracks[13]));
		}
	}

	public void SubscribeToAlertActionFroNewMessage(Action<string, string> act)
	{
		IRCSystem iRCSystem = IRCSystem;
		iRCSystem.LogAdded = (Action<string, string>)Delegate.Combine(iRCSystem.LogAdded, act);
	}

	public void UnSubscribeToAlertActionFroNewMessage(Action<string, string> act)
	{
		IRCSystem iRCSystem = IRCSystem;
		iRCSystem.LogAdded = (Action<string, string>)Delegate.Remove(iRCSystem.LogAdded, act);
	}

	public string GetName()
	{
		return name;
	}

	private void ReGenerateConfigFile()
	{
		FileEntry fileEntry = rootFolder.searchForFile("dhs_config.sys");
		if (fileEntry != null)
		{
			rootFolder.files.Remove(fileEntry);
			fileEntry = null;
		}
		FileEntry item = generateConfigFile();
		rootFolder.files.Add(item);
	}

	private FileEntry generateConfigFile()
	{
		string text = "##DHS_CONFIG\n";
		text = text + "ThemeColor = " + Utils.convertColorToParseableString(themeColor) + "\n";
		text = text + "ServiceName = " + name + "\n";
		text = text + "GroupName = " + groupName + "\n";
		text = text + "Agents = " + Utils.SerializeListToCSV(Agents) + "\n";
		object obj = text;
		text = string.Concat(obj, "FactionReferral = ", AddsFactionPointForMissionCompleteion, "\n");
		obj = text;
		text = string.Concat(obj, "AutoClearMissions = ", AutoClearMissionsOnSingleComplete, "\n");
		text = text + "HighlightedWords = " + GetSerializedWordHighlightList() + "\n";
		obj = text;
		text = string.Concat(obj, "AllowContractAbbandon = ", AllowContractAbbandon, "\n");
		text += "\n";
		return new FileEntry(text, "dhs_config.sys");
	}

	private void loadFromConfigFileData(string config)
	{
		string[] array = config.Split(Utils.newlineDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].StartsWith("//"))
			{
				string text = array[i];
				if (text.StartsWith("ThemeColor"))
				{
					themeColor = Utils.convertStringToColor(getDataFromConfigLine(text));
				}
				else if (text.StartsWith("ServiceName"))
				{
					name = getDataFromConfigLine(text);
				}
				else if (text.StartsWith("GroupName"))
				{
					groupName = getDataFromConfigLine(text);
				}
				else if (text.StartsWith("Agents"))
				{
					Agents = new List<string>(getDataFromConfigLine(text).Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries));
				}
				else if (text.StartsWith("FactionReferral"))
				{
					AddsFactionPointForMissionCompleteion = getDataFromConfigLine(text).ToLower() == "true";
				}
				else if (text.StartsWith("AutoClearMissions"))
				{
					AutoClearMissionsOnSingleComplete = getDataFromConfigLine(text).ToLower() == "true";
				}
				else if (text.StartsWith("HighlightedWords"))
				{
					DeserializeWordHighlightList(getDataFromConfigLine(text));
				}
				else if (text.StartsWith("AllowContractAbbandon"))
				{
					AllowContractAbbandon = getDataFromConfigLine(text).ToLower() == "true";
				}
			}
		}
	}

	private string getDataFromConfigLine(string line, string sentinel = "= ")
	{
		return line.Substring(line.IndexOf(sentinel) + 2);
	}

	private string GetSerializedWordHighlightList()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, Color> highlightedWord in HighlightedWords)
		{
			stringBuilder.Append(highlightedWord.Key + "=" + Utils.convertColorToParseableString(highlightedWord.Value));
			stringBuilder.Append("/");
		}
		return stringBuilder.ToString();
	}

	private void DeserializeWordHighlightList(string input)
	{
		HighlightedWords = new Dictionary<string, Color>();
		string[] array = input.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { '=' }, StringSplitOptions.RemoveEmptyEntries);
			HighlightedWords.Add(array2[0], Utils.convertStringToColor(array2[1]));
		}
	}

	public override void loadInit()
	{
		base.loadInit();
		rootFolder = comp.files.root.searchForFolder("HomeBase");
		missionFolder = rootFolder.searchForFolder("contracts");
		archivesFolder = rootFolder.searchForFolder("archive");
		actionsFolder = rootFolder.searchForFolder("runtime");
		DelayedActions = new DelayableActionSystem(actionsFolder, os);
		FileEntry fileEntry = rootFolder.searchForFile("dhs_config.sys");
		if (fileEntry != null)
		{
			loadFromConfigFileData(fileEntry.data);
		}
		IRCSystem = new IRCSystem(rootFolder);
		IRCSystem.AttachmentPressedSound = ButtonPressSound;
	}

	public override string getSaveString()
	{
		return "<DHSDaemon />";
	}

	public void AddAgent(string AgentName, string agentPassword, Color color)
	{
		AgentName = AgentName.Replace(" ", "_").Replace("/", "").Replace("=", "_");
		if (AgentName.Contains(' ') || AgentName.Contains('/') || AgentName.Contains('=') || string.IsNullOrWhiteSpace(AgentName))
		{
			throw new InvalidOperationException("Invalid Agent Name \"" + AgentName + "\"!");
		}
		if (Agents.Contains(AgentName))
		{
			HighlightedWords[AgentName] = color;
		}
		else
		{
			Agents.Add(AgentName);
			HighlightedWords.Add(AgentName, color);
		}
		if (rootFolder != null)
		{
			ReGenerateConfigFile();
		}
		bool flag = false;
		for (int i = 0; i < comp.users.Count; i++)
		{
			flag |= comp.users[i].name == AgentName;
		}
		if (!flag)
		{
			comp.users.Add(new UserDetail(AgentName, agentPassword, 3));
		}
	}

	public void AddMission(string missionPath, string AgentClaimName = null, bool startsComplete = false)
	{
		List<ActiveMission> branchMissions = os.branchMissions;
		ActiveMission mission = (ActiveMission)ComputerLoader.readMission(missionPath);
		os.branchMissions = branchMissions;
		AddMission(mission, AgentClaimName, startsComplete);
	}

	public void AddMission(ActiveMission mission, string AgentClaimName = null, bool startsComplete = false)
	{
		ActiveMissions.Add(new ClaimableMission
		{
			Mission = mission,
			IsComplete = startsComplete,
			AgentClaim = AgentClaimName
		});
		ReSerializeActiveMissions();
	}

	public void RemoveMission(string missionPath)
	{
		for (int i = 0; i < ActiveMissions.Count; i++)
		{
			if (ActiveMissions[i].Mission.reloadGoalsSourceFile == LocalizedFileLoader.GetLocalizedFilepath(missionPath))
			{
				ActiveMissions.RemoveAt(i);
				i--;
			}
		}
		ReSerializeActiveMissions();
	}

	private void ReadActiveMissions()
	{
		ActiveMissions.Clear();
		for (int i = 0; i < missionFolder.files.Count; i++)
		{
			if (!missionFolder.files[i].name.EndsWith(".ctc"))
			{
				continue;
			}
			int contractRegistryNumber = 0;
			string Tag = null;
			try
			{
				ActiveMission mission = (ActiveMission)MissionSerializer.restoreMissionFromFile(missionFolder.files[i].data, out contractRegistryNumber, out Tag);
				ClaimableMission claimableMission = new ClaimableMission();
				claimableMission.AgentClaim = Tag;
				claimableMission.Mission = mission;
				claimableMission.IsComplete = contractRegistryNumber >= 1;
				ClaimableMission claimableMission2 = claimableMission;
				ActiveMissions.Add(claimableMission2);
				if (SelectedMission == null && claimableMission2.AgentClaim == os.defaultUser.name)
				{
					SelectedMission = claimableMission2;
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private void ReSerializeActiveMissions()
	{
		missionFolder.files.Clear();
		for (int i = 0; i < ActiveMissions.Count; i++)
		{
			string dataEntry = MissionSerializer.generateMissionFile(ActiveMissions[i].Mission, ActiveMissions[i].IsComplete ? 1 : 0, groupName, ActiveMissions[i].AgentClaim);
			missionFolder.files.Add(new FileEntry(dataEntry, GetFilenameForMission(ActiveMissions[i].Mission)));
		}
	}

	private string GetFilenameForMission(ActiveMission mission)
	{
		if (mission.client == null || mission.client == "UNKNOWN")
		{
			mission.client = string.Concat(Utils.getRandomByte());
		}
		return Utils.GetNonRepeatingFilename("Contact_" + mission.client, ".ctc", missionFolder);
	}

	private bool PlayerHasClaimedMission()
	{
		for (int i = 0; i < ActiveMissions.Count; i++)
		{
			if (ActiveMissions[i].AgentClaim == os.defaultUser.name)
			{
				return true;
			}
		}
		return false;
	}

	private void PlayerAcceptMission(ClaimableMission mission)
	{
		mission.AgentClaim = os.defaultUser.name;
		os.currentMission = mission.Mission;
		mission.Mission.ActivateSuppressedStartFunctionIfPresent();
		ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(mission.Mission.reloadGoalsSourceFile);
		IRCSystem.AddLog("Channel", string.Format(LocaleTerms.Loc("CONTRACT CLAIMED: @{0} claimed contract \"{1}\""), os.defaultUser.name, mission.Mission.postingTitle));
		ReSerializeActiveMissions();
	}

	private void PlayerAbandonedMission(ClaimableMission mission)
	{
	}

	private bool PlayerAttemptCompleteMission(ClaimableMission mission, bool ForceComplete = false)
	{
		ForceComplete = ForceComplete && Settings.forceCompleteEnabled;
		List<ClaimableMission> list = new List<ClaimableMission>();
		for (int i = 0; i < ActiveMissions.Count; i++)
		{
			list.Add(ActiveMissions[i]);
		}
		ActiveMissions.Clear();
		missionFolder.files.Clear();
		if (ForceComplete || os.currentMission.isComplete(MissionTextResponses))
		{
			ActiveMission currentMission = os.currentMission;
			os.currentMission = null;
			if (currentMission.endFunctionName != null)
			{
				MissionFunctions.runCommand(currentMission.endFunctionValue, currentMission.endFunctionName);
			}
			if (AddsFactionPointForMissionCompleteion)
			{
				MissionFunctions.runCommand(1, "addRankSilent");
			}
			IRCSystem.AddLog("Channel", string.Format(LocaleTerms.Loc("CONTRACT COMPLETE: @{0} completed contract \"{1}\""), os.defaultUser.name, mission.Mission.postingTitle));
			if (MissionTextResponses.Count > 0)
			{
				IRCSystem.AddLog("Channel", LocaleTerms.Loc("Additional details provided:"));
				for (int i = 0; i < MissionTextResponses.Count; i++)
				{
					IRCSystem.AddLog("Channel", "[" + MissionTextResponses[i] + "]");
				}
			}
			MissionTextResponses.Clear();
			mission.IsComplete = true;
			if (AutoClearMissionsOnSingleComplete)
			{
				CompleteAndArchiveMissionSet(list);
			}
			os.saveGame();
			return true;
		}
		ActiveMissions = list;
		ReSerializeActiveMissions();
		MissionTextResponses.Clear();
		return false;
	}

	private void CompleteAndArchiveMissionSet(List<ClaimableMission> missionsToArchive)
	{
		for (int i = 0; i < missionsToArchive.Count; i++)
		{
			ClaimableMission claimableMission = missionsToArchive[i];
			claimableMission.IsComplete = true;
			string dataEntry = MissionSerializer.generateMissionFile(claimableMission.Mission, claimableMission.IsComplete ? 1 : 0, groupName, claimableMission.AgentClaim);
			archivesFolder.files.Add(new FileEntry(dataEntry, GetFilenameForMission(claimableMission.Mission)));
		}
	}

	public void ClearAllActiveMissions()
	{
		CompleteAndArchiveMissionSet(ActiveMissions);
		ActiveMissions.Clear();
	}

	private void Update()
	{
		os.delayer.Post(ActionDelayer.Wait(os.lastGameTime.ElapsedGameTime.TotalSeconds * 1.999), delegate
		{
			if (os.display.command != name)
			{
				IRCSystem.LeftView();
			}
		});
	}

	public bool ShouldDisplayNotifications()
	{
		if (!os.Flags.HasFlag("DLC_Player_IRC_Authenticated") && WelcomeFadeoutTimerLeft > 0f)
		{
			return false;
		}
		return true;
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		UIButtonOffset = 0;
		Update();
		Rectangle rectangle = Utils.InsetRectangle(bounds, 2);
		Rectangle rectangle2 = new Rectangle(rectangle.X, rectangle.Y + 10, rectangle.Width, rectangle.Height - 10);
		HexBackground.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		sb.Draw(Utils.white, rectangle2, Color.Black * 0.2f);
		HexBackground.Draw(rectangle2, sb, Color.Black * 0.1f, themeColor * 0.02f);
		int num = 30;
		Rectangle bounds2 = new Rectangle(rectangle.X, rectangle.Y + 2 + num, rectangle.Width, rectangle.Height - num - 2);
		Rectangle bounds3 = new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, num);
		DrawOptionsPanel(bounds3, sb);
		switch (State)
		{
		default:
			if (!os.Flags.HasFlag("DLC_Player_IRC_Authenticated"))
			{
				DoLoadingPlayerInScreen(bounds, sb);
			}
			else
			{
				State = DHSState.Home;
			}
			break;
		case DHSState.Login:
			break;
		case DHSState.Home:
			DrawHomeScreen(bounds2, sb);
			if (!os.Flags.HasFlag("DLC_Player_IRC_Authenticated"))
			{
				DoLoadingPlayerInScreen(bounds, sb);
				WelcomeFadeoutTimerLeft -= (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
			}
			break;
		case DHSState.MissionSelectView:
			DrawMissionSelectView(bounds2, sb);
			break;
		case DHSState.ArchiveList:
			break;
		case DHSState.ContractDetailView:
			DrawMissionDetailsPanel(bounds2, sb, SelectedMission);
			break;
		}
	}

	private void DoLoadingPlayerInScreen(Rectangle bounds, SpriteBatch sb)
	{
		timeSpentInLoading += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		float num = 10f;
		float num2 = 7f;
		float num3 = WelcomeFadeoutTimerLeft / BaseWelcomeFadeoutTime;
		Rectangle rectangle = Utils.InsetRectangle(bounds, 2);
		Rectangle rectangle2 = new Rectangle(rectangle.X, rectangle.Y + 30, rectangle.Width, rectangle.Height - 30);
		sb.Draw(Utils.white, rectangle2, Color.Black * num3);
		HexBackground.Draw(rectangle2, sb, Color.Black * 0.1f * num3, themeColor * 0.02f * num3);
		if (!HasStartedWoosh && timeSpentInLoading >= 5f)
		{
			WooshBuildup.Play(0.7f, 0f, 0f);
			MusicManager.loadAsCurrentSong("DLC\\Music\\Userspacelike");
			HasStartedWoosh = true;
		}
		if (timeSpentInLoading >= num)
		{
			if (WelcomeFadeoutTimerLeft <= 0f)
			{
				os.Flags.AddFlag("DLC_Player_IRC_Authenticated");
			}
			State = DHSState.Home;
		}
		if (MusicManager.isPlaying && timeSpentInLoading < num2 && MusicManager.currentSongName == "DLC\\Music\\Userspacelike")
		{
			MusicManager.stop();
		}
		Rectangle destinationRectangle = new Rectangle(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, LoadingSpinner.Width, LoadingSpinner.Height);
		bool flag = timeSpentInLoading > num2;
		float amount = (flag ? Utils.QuadraticOutCurve(1f - Math.Min(timeSpentInLoading - num2, 1f)) : 0f);
		sb.Draw(flag ? MissionAvaliableIcon : LoadingSpinner, destinationRectangle, null, Color.Lerp(Utils.makeColorAddative(themeColor) * num3, Utils.AddativeWhite, amount), flag ? 0f : (os.timer * 4f), flag ? MissionAvaliableIcon.GetCentreOrigin() : LoadingSpinner.GetCentreOrigin(), SpriteEffects.None, 0.4f);
		Rectangle dest = new Rectangle(bounds.X + 10, destinationRectangle.Y + LoadingSpinner.Height / 2 + 4, bounds.Width - 20, 26);
		if (timeSpentInLoading > 1f)
		{
			TextItem.doCenteredFontLabel(dest, "Accessing Whitelist...", GuiData.smallfont, Utils.AddativeWhite * 0.5f * num3);
			dest.Y += dest.Height;
		}
		if (timeSpentInLoading > 3f)
		{
			TextItem.doCenteredFontLabel(dest, "Authenticating...", GuiData.smallfont, Utils.AddativeWhite * 0.5f * num3);
			dest.Y += dest.Height;
		}
		if (timeSpentInLoading > 5f)
		{
			TextItem.doCenteredFontLabel(dest, "Registering Connection...", GuiData.smallfont, Utils.AddativeWhite * 0.5f * num3);
			dest.Y += dest.Height;
		}
		if (timeSpentInLoading > 5.5f)
		{
			TextItem.doCenteredFontLabel(dest, "Unlocking...", GuiData.smallfont, Utils.AddativeWhite * 0.5f * num3);
			dest.Y += dest.Height;
		}
		if (timeSpentInLoading > num2)
		{
			if (!MusicManager.isPlaying)
			{
				MusicManager.playSongImmediatley("DLC\\Music\\Userspacelike");
				USIntro.Play(0.4f, 0f, 0f);
			}
			float num4 = Utils.QuadraticOutCurve(Math.Min(1f, (timeSpentInLoading - num2) / 2f));
			string text = " :: " + string.Format(LocaleTerms.Loc("Welcome {0}"), os.SaveUserAccountName) + " :: ";
			Vector2 vector = GuiData.smallfont.MeasureString(text);
			Rectangle dest2 = new Rectangle(dest.X, dest.Y, dest.Width / 2 - (int)(vector.X / 2f), dest.Height);
			int width = dest2.Width;
			dest2.Width = (int)((float)dest2.Width * num4);
			dest2.X += width - dest2.Width;
			Rectangle dest3 = new Rectangle(dest.X + width + (int)vector.X, dest.Y, dest2.Width, dest.Height);
			PatternDrawer.draw(dest2, 1f, Color.Transparent, themeColor * num3, sb);
			PatternDrawer.draw(dest3, 1f, Color.Transparent, themeColor * num3, sb);
			TextItem.doCenteredFontLabel(dest, text, GuiData.smallfont, themeColor * num3);
			dest.Y += dest.Height;
		}
	}

	private void DrawOptionsPanel(Rectangle bounds, SpriteBatch sb)
	{
		int num = Math.Min(200, bounds.Width / 3);
		int num2 = bounds.Height - 8;
		bool flag = PlayerHasClaimedMission();
		int num3 = bounds.X + 2;
		int y = bounds.Y + bounds.Height / 2 - num2 / 2;
		bool flag2 = os.Flags.HasFlag("DLC_Player_IRC_Authenticated") || !(WelcomeFadeoutTimerLeft > 0f);
		switch (State)
		{
		case DHSState.Welcome:
		case DHSState.Login:
		case DHSState.Home:
			if (Button.doButton(393001, num3, y, num, num2, LocaleTerms.Loc("Live Projects"), flag ? Color.Gray : themeColor) && flag2)
			{
				State = DHSState.MissionSelectView;
			}
			num3 += num + 8;
			if (flag && Button.doButton(393003, num3, y, num, num2, LocaleTerms.Loc("View Active Project"), themeColor) && flag2)
			{
				State = DHSState.ContractDetailView;
			}
			num3 += num + 8;
			break;
		case DHSState.ArchiveList:
		case DHSState.MissionSelectView:
		case DHSState.ContractDetailView:
			if (Button.doButton(393001, num3, y, num, num2, LocaleTerms.Loc("Chat"), flag ? Color.Gray : themeColor) && flag2)
			{
				State = DHSState.Home;
			}
			num3 += num + 8;
			break;
		}
		int num4 = 100;
		if (Button.doButton(393909, bounds.X + bounds.Width - num4, y, num4, num2, LocaleTerms.Loc("Close"), os.lockedColor) && flag2)
		{
			os.display.command = "connect";
		}
		Rectangle destinationRectangle = bounds;
		destinationRectangle.Y += destinationRectangle.Height;
		destinationRectangle.Height = 1;
		sb.Draw(Utils.white, destinationRectangle, themeColor);
	}

	private void DrawHomeScreen(Rectangle bounds, SpriteBatch sb)
	{
		sb.Draw(Utils.gradient, bounds, Color.Black * 0.8f);
		bounds = Utils.InsetRectangle(bounds, 2);
		bounds.Height -= 6;
		IRCSystem.Draw(bounds, sb, CanWrite: false, LocaleTerms.Loc("Unknown"), HighlightedWords);
	}

	private void DrawMissionSelectView(Rectangle bounds, SpriteBatch sb)
	{
		int num = 2;
		int num2 = ((ActiveMissions.Count > 0) ? (bounds.Height / ActiveMissions.Count - num) : 0);
		if (ActiveMissions.Count > 0)
		{
			Rectangle bounds2 = new Rectangle(bounds.X, bounds.Y, bounds.Width, num2);
			for (int i = 0; i < ActiveMissions.Count; i++)
			{
				ScrollableTextPanel.SetScrollbarUIIndexOffset(i + 100);
				DrawMissionPanel(bounds2, sb, ActiveMissions[i]);
				bounds2.Y += num + num2;
			}
		}
		else
		{
			int num3 = 60;
			Rectangle rectangle = new Rectangle(bounds.X, bounds.Y + bounds.Height / 2 - num3 / 2, bounds.Width, num3);
			sb.Draw(Utils.white, rectangle, Color.Black * 0.9f);
			rectangle = Utils.InsetRectangle(rectangle, 12);
			TextItem.doFontLabelToSize(rectangle, LocaleTerms.Loc("No Projects Available"), GuiData.font, Color.White, doNotOversize: true);
		}
	}

	private void DrawMissionPanel(Rectangle bounds, SpriteBatch sb, ClaimableMission mission)
	{
		bool flag = false;
		bool flag2 = PlayerHasClaimedMission();
		Texture2D texture = MissionAvaliableIcon;
		Color color = Color.White;
		Color color2 = Color.White;
		if (mission.AgentClaim != null)
		{
			if (mission.AgentClaim == os.defaultUser.name)
			{
				texture = MissionPlayersIcon;
				flag = true;
			}
			else
			{
				texture = MissionTakenIcon;
				color = Color.Gray * 0.8f;
				color2 = Color.Black;
			}
		}
		else
		{
			color = Color.Lerp(Utils.AddativeWhite, Utils.makeColorAddative(themeColor), 0.5f);
			color2 = themeColor;
		}
		bool flag3 = flag || (!flag2 && mission.AgentClaim == null);
		RenderedRectangle.doRectangleOutline(bounds.X, bounds.Y, bounds.Width, bounds.Height, 1, flag3 ? themeColor : Color.Gray);
		Rectangle destinationRectangle = Utils.InsetRectangle(bounds, 1);
		sb.Draw(Utils.gradient, destinationRectangle, color2 * 0.2f);
		int num = 4;
		int num2 = Math.Min(bounds.Width / 6, bounds.Height - 2 * num);
		Rectangle rect = new Rectangle(bounds.X + num, bounds.Y + num, num2, num2);
		rect = Utils.InsetRectangle(rect, 2);
		Rectangle rectangle = new Rectangle(rect.X + rect.Width + num, bounds.Y + num, bounds.Width - rect.Width - num * 6, 40);
		sb.Draw(destinationRectangle: new Rectangle(rectangle.X, rectangle.Y, rectangle.Width + num * 3, rectangle.Height), texture: Utils.white, color: Utils.VeryDarkGray * 0.7f);
		TextItem.doFontLabel(new Vector2(rectangle.X + 2 * num, rectangle.Y), mission.Mission.postingTitle, GuiData.font, flag3 ? themeColor : Color.Gray, rectangle.Width, rectangle.Height, centreVertically: true);
		int num3 = bounds.Width / 5 + num;
		if (num3 < 150)
		{
			num3 = 150;
		}
		Rectangle dest = new Rectangle(rectangle.X + 10, rectangle.Y + rectangle.Height + num, bounds.Width / 5 * 4 - (10 + num3 - num), bounds.Height - (rectangle.Height + num * 3));
		if (flag || mission.AgentClaim == null)
		{
			if (!flag && flag2)
			{
				dest.Width = bounds.Width - (num * 3 + num2);
			}
			string text = Utils.SuperSmartTwimForWidth(mission.Mission.postingBody, dest.Width - num, GuiData.tinyfont);
			ScrollBar.AlwaysDrawUnderBar = true;
			ScrollableTextPanel.UpdateScroll(mission.UITextScrollDown);
			ScrollableTextPanel.Draw(dest, text, sb, (flag || mission.AgentClaim == null) ? Color.White : (Color.LightGray * 0.6f));
			mission.UITextScrollDown = ScrollableTextPanel.GetScrollDown();
			ScrollBar.AlwaysDrawUnderBar = false;
		}
		else
		{
			dest.X = bounds.X;
			dest.Width = bounds.Width;
			Rectangle rectangle2 = new Rectangle(dest.X, dest.Y + ((bounds.Height > 200) ? 20 : 0), dest.Width, 30);
			string text2 = "- " + LocaleTerms.Loc("CLAIMED") + " : ";
			string text3 = text2 + mission.AgentClaim + " - ";
			Vector2 vector = GuiData.font.MeasureString(text3);
			Vector2 vector2 = GuiData.font.MeasureString(text2);
			Vector2 vector3 = GuiData.font.MeasureString(mission.AgentClaim);
			float num4 = Math.Min((float)rectangle2.Width / vector.X, (float)rectangle2.Height / vector.Y);
			float num5 = (float)(rectangle2.X + rectangle2.Width / 2) - vector.X * num4 / 2f;
			float y = (float)(rectangle2.Y + rectangle2.Height / 2) - vector.Y * num4 / 2f;
			sb.DrawString(GuiData.font, text2, new Vector2(num5, y), Color.LightGray, 0f, Vector2.Zero, new Vector2(num4), SpriteEffects.None, 0.4f);
			num5 += vector2.X * num4;
			sb.DrawString(GuiData.font, mission.AgentClaim, new Vector2(num5, y), Color.Lerp(themeColor, Color.Gray, 0.55f), 0f, Vector2.Zero, new Vector2(num4), SpriteEffects.None, 0.4f);
			num5 += vector3.X * num4;
			sb.DrawString(GuiData.font, " -", new Vector2(num5, y), Color.LightGray, 0f, Vector2.Zero, new Vector2(num4), SpriteEffects.None, 0.4f);
			rectangle2.Y += rectangle2.Height;
			Rectangle destinationRectangle3 = new Rectangle(bounds.X + num + num2, rectangle2.Y + 6, bounds.Width - num * 2 - num2, (int)((double)dest.Height * 0.6));
			sb.Draw(Utils.white, destinationRectangle3, Color.Black * 0.65f);
			Rectangle dest2 = new Rectangle(destinationRectangle3.X + num + 10, destinationRectangle3.Y, destinationRectangle3.Width - num2 - 6, destinationRectangle3.Height);
			string text4 = Utils.SuperSmartTwimForWidth(mission.Mission.postingBody, dest2.Width - num, GuiData.tinyfont);
			bool offsetToTopLeft = false;
			if (text4.StartsWith("-- "))
			{
				text4 = "\n                " + text4;
				offsetToTopLeft = true;
			}
			TextItem.doFontLabelToSize(dest2, text4, GuiData.tinyfont, Color.DarkGray * 0.5f, doNotOversize: true, offsetToTopLeft);
			rectangle2.Y += rectangle2.Height + 5;
			rectangle2.Height = 25;
		}
		sb.Draw(texture, rect, color);
		int num6 = Math.Min((bounds.Height - rectangle.Height - num * 6) / 3, 22);
		Vector2 vector4 = Utils.ClipVec2ForTextRendering(new Vector2(dest.X + dest.Width + num, dest.Y));
		if (mission.AgentClaim == null && !flag2)
		{
			if (Button.doButton(391001 + UIButtonOffset, (int)vector4.X, (int)vector4.Y, num3, num6, LocaleTerms.Loc("Claim Contract"), themeColor))
			{
				PlayerAcceptMission(mission);
				SelectedMission = mission;
				State = DHSState.ContractDetailView;
			}
			vector4.Y += num6 + num;
			UIButtonOffset++;
		}
		if (flag)
		{
			if (Button.doButton(391002 + UIButtonOffset, (int)vector4.X, (int)vector4.Y, num3, num6, (num3 > 140) ? LocaleTerms.Loc("View Details") : LocaleTerms.Loc("Details"), Color.White))
			{
				State = DHSState.ContractDetailView;
				ScrollableTextPanel.UpdateScroll(0f);
				SelectedMission = mission;
				AbandonMissionShowConfirmation = false;
				ShouldShowMissionIncompleteMessage = false;
			}
			vector4.Y += num6 + num;
			UIButtonOffset++;
			if (Button.doButton(391003 + UIButtonOffset, (int)vector4.X, (int)vector4.Y, num3, num6, LocaleTerms.Loc("Complete"), themeColor) && !PlayerAttemptCompleteMission(mission))
			{
				State = DHSState.ContractDetailView;
				ShouldShowMissionIncompleteMessage = true;
			}
			vector4.Y += num6 + num;
			UIButtonOffset++;
		}
	}

	private void DrawMissionDetailsPanel(Rectangle bounds, SpriteBatch sb, ClaimableMission mission)
	{
		bool flag = false;
		bool flag2 = PlayerHasClaimedMission();
		Texture2D texture = MissionAvaliableIcon;
		if (mission.AgentClaim != null)
		{
			if (mission.AgentClaim == os.defaultUser.name)
			{
				texture = MissionPlayersIcon;
				flag = true;
			}
			else
			{
				texture = MissionTakenIcon;
			}
		}
		bool flag3 = flag || (!flag2 && mission.AgentClaim == null);
		int num = 4;
		int num2 = Math.Min(bounds.Width / 6, bounds.Height - 2 * num);
		Rectangle destinationRectangle = new Rectangle(bounds.X + num, bounds.Y + num, num2, num2);
		Rectangle destinationRectangle2 = new Rectangle(destinationRectangle.X + destinationRectangle.Width + num, bounds.Y + num, bounds.Width - destinationRectangle.Width - num * 3, 40);
		sb.Draw(Utils.white, destinationRectangle2, Utils.VeryDarkGray * 0.8f);
		TextItem.doFontLabel(new Vector2(destinationRectangle2.X + 2 * num, destinationRectangle2.Y), mission.Mission.email.subject, GuiData.font, flag3 ? themeColor : Color.Gray, destinationRectangle2.Width, destinationRectangle2.Height, centreVertically: true);
		sb.Draw(texture, destinationRectangle, Color.White);
		int num3 = 30;
		Rectangle rectangle = new Rectangle(destinationRectangle.X + destinationRectangle.Width + num, destinationRectangle2.Y + destinationRectangle2.Height + num * 2, bounds.Width - (destinationRectangle.X - bounds.X + destinationRectangle.Width + num * 2), num3);
		DrawHeaderLine(rectangle, LocaleTerms.Loc("Briefing"), sb);
		Rectangle dest = rectangle;
		dest.Y += rectangle.Height + num;
		string text = Utils.SuperSmartTwimForWidth(mission.Mission.email.body, dest.Width, GuiData.tinyfont);
		Vector2 vector = GuiData.tinyfont.MeasureString(text);
		int num4 = MissionTextResponses.Count * 24 + num3 * 4 + num * 4 + mission.Mission.email.attachments.Count * 30 + 10;
		int val = bounds.Height - (num4 + (dest.Y - bounds.Y)) - num * 2;
		int val2 = 70;
		dest.Height = Math.Max(val2, Math.Min(val, Math.Min((int)vector.Y + 2 * num, bounds.Height / 3 * 2)));
		ScrollableTextPanel.Draw(dest, text, sb);
		rectangle.Y = dest.Y + dest.Height + num;
		DrawHeaderLine(rectangle, LocaleTerms.Loc("Attachments"), sb);
		Vector2 dpos = new Vector2(rectangle.X, rectangle.Y + rectangle.Height + num);
		if (mission.Mission.email.attachments.Count == 0)
		{
			Rectangle dest2 = rectangle;
			dest2.Y += dest2.Height;
			TextItem.doFontLabelToSize(dest2, "- " + LocaleTerms.Loc("Empty") + " -", GuiData.smallfont, Color.LightGray, doNotOversize: true);
			dpos.Y += 26f;
		}
		else
		{
			for (int i = 0; i < mission.Mission.email.attachments.Count; i++)
			{
				if (AttachmentRenderer.RenderAttachment(mission.Mission.email.attachments[i], os, dpos, UIButtonOffset++, ButtonPressSound))
				{
					dpos.Y += 22f;
				}
			}
		}
		dpos.Y += num;
		rectangle.Y = (int)dpos.Y;
		if (mission.IsComplete)
		{
			return;
		}
		DrawHeaderLine(rectangle, LocaleTerms.Loc("Tools"), sb);
		if (ShouldShowMissionIncompleteMessage)
		{
			Rectangle dest3 = rectangle;
			dest3.Height -= 4;
			PatternDrawer.draw(dest3, os.timer * 0.0008f, os.lockedColor * 0.3f, os.brightLockedColor * 0.7f, sb, PatternDrawer.thinStripe);
			TextItem.doRightAlignedBackingLabelScaled(dest3, LocaleTerms.Loc("Mission Incomplete"), GuiData.font, Color.Transparent, Color.White);
		}
		Rectangle rectangle2 = rectangle;
		rectangle2.Y += rectangle.Height + num;
		int num5 = (rectangle2.Width - num * 2) / 2;
		if (Button.doButton(639013 + UIButtonOffset++, rectangle2.X, rectangle2.Y, num5, rectangle2.Height, LocaleTerms.Loc("Complete"), themeColor))
		{
			if (!PlayerAttemptCompleteMission(mission))
			{
				ShouldShowMissionIncompleteMessage = true;
			}
			else
			{
				ShouldShowMissionIncompleteMessage = false;
				State = DHSState.Home;
			}
		}
		if (Button.doButton(639414 + UIButtonOffset++, rectangle2.X + num5 + num, rectangle2.Y, num5, rectangle2.Height, AbandonMissionShowConfirmation ? LocaleTerms.Loc("CONFIRM?") : LocaleTerms.Loc("Abandon"), (!AllowContractAbbandon) ? Color.Gray : (AbandonMissionShowConfirmation ? os.brightLockedColor : os.lockedColor)) && AllowContractAbbandon)
		{
			if (!AbandonMissionShowConfirmation)
			{
				AbandonMissionShowConfirmation = true;
			}
			else
			{
				PlayerAbandonedMission(mission);
			}
		}
		if (Settings.forceCompleteEnabled && Button.doButton(649017 + UIButtonOffset++, rectangle2.X + num5 + num, rectangle2.Y + rectangle2.Height + 2, num5, rectangle2.Height, LocaleTerms.Loc("Force Complete"), themeColor))
		{
			PlayerAttemptCompleteMission(mission, ForceComplete: true);
			State = DHSState.Home;
		}
		Rectangle bounds2 = rectangle2;
		bounds2.Y += rectangle2.Height + num;
		bounds2.Height = bounds.Y + bounds.Height - bounds2.Y - num;
		DrawAdditionalDetailsSection(bounds2, sb);
	}

	private void DrawAdditionalDetailsSection(Rectangle bounds, SpriteBatch sb)
	{
		int num = 24;
		Vector2 vector = new Vector2(bounds.X, bounds.Y);
		TextItem.doFontLabel(vector, LocaleTerms.Loc("Additional Details") + " :", GuiData.smallfont, null, Math.Max(200f, (float)bounds.Width - (vector.X - (float)bounds.Width) * 2f));
		vector.Y += num;
		for (int i = 0; i < MissionTextResponses.Count; i++)
		{
			TextItem.doFontLabel(vector + new Vector2(25f, 0f), MissionTextResponses[i], GuiData.tinyfont, null, (float)bounds.Width - (vector.X - (float)bounds.X) * 2f - 20f);
			float num2 = Math.Min(GuiData.tinyfont.MeasureString(MissionTextResponses[i]).X, (float)bounds.Width - (vector.X - (float)bounds.X) * 2f - 20f);
			if (Button.doButton(80000 + i * 100, (int)(vector.X + num2 + 30f), (int)vector.Y, 20, 20, "-", null))
			{
				MissionTextResponses.RemoveAt(i);
			}
			vector.Y += num;
		}
		if (isAddingTextResponse)
		{
			string data = null;
			bool flag = Programs.parseStringFromGetStringCommand(os, out data);
			if (data == null)
			{
				data = "";
			}
			vector.Y += 5f;
			GuiData.spriteBatch.Draw(Utils.white, new Rectangle(bounds.X + 1, (int)vector.Y, bounds.Width - 2 - bounds.Width / 9, 40), os.indentBackgroundColor);
			vector.Y += 10f;
			TextItem.doFontLabel(vector + new Vector2(25f, 0f), data, GuiData.tinyfont, null);
			Vector2 vector2 = GuiData.tinyfont.MeasureString(data);
			vector2.Y = 0f;
			if (os.timer % 1f <= 0.5f)
			{
				GuiData.spriteBatch.Draw(Utils.white, new Rectangle((int)(vector.X + vector2.X + 2f) + 25, (int)vector.Y, 4, 20), Color.White);
			}
			int num3 = bounds.Width - 1 - bounds.Width / 10;
			if (flag || Button.doButton(8000094, bounds.X + num3 - 4, (int)vector.Y - 10, bounds.Width / 9 - 3, 40, LocaleTerms.Loc("Add"), os.highlightColor))
			{
				if (!flag)
				{
					os.terminal.executeLine();
				}
				isAddingTextResponse = false;
				if (!string.IsNullOrWhiteSpace(data))
				{
					MissionTextResponses.Add(data);
				}
				inProgressTextResponse = null;
			}
			else
			{
				inProgressTextResponse = data;
			}
		}
		else if (Button.doButton(8000098, (int)(vector.X + 4f), (int)vector.Y, 140, 24, LocaleTerms.Loc("Add Detail"), null))
		{
			isAddingTextResponse = true;
			os.execute("getString Detail");
			os.terminal.executionPreventionIsInteruptable = true;
		}
	}

	private void DrawHeaderLine(Rectangle dest, string header, SpriteBatch sb)
	{
		Vector2 vector = GuiData.font.MeasureString(header);
		Vector2 vector2 = new Vector2(1f, Math.Min(dest.Height, vector.Y) / vector.Y);
		if (vector.X > (float)dest.Width)
		{
			vector2.X = 1f / (vector.X / (float)dest.Width);
		}
		vector2 = new Vector2(Math.Min(vector2.X, vector2.Y), Math.Min(vector2.X, vector2.Y));
		sb.DrawString(position: new Vector2(dest.X, (float)(dest.Y + dest.Height) - vector.Y * vector2.Y), spriteFont: GuiData.font, text: header, color: themeColor, rotation: 0f, origin: Vector2.Zero, scale: vector2, effects: SpriteEffects.None, layerDepth: 0.4f);
		dest.Y += dest.Height - 1;
		dest.Height = 1;
		sb.Draw(Utils.white, dest, themeColor);
	}
}
