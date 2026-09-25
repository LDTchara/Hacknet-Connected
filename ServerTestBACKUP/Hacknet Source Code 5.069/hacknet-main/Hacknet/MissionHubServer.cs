using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hacknet.Effects;
using Hacknet.Factions;
using Hacknet.Gui;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MissionHubServer : AuthenticatingDaemon
{
	private enum HubState
	{
		Welcome,
		Menu,
		Login,
		Listing,
		ContractPreview,
		UserList,
		CancelContract
	}

	public const string ROOT_FOLDERNAME = "ContractHub";

	public const string CONFIG_FILENAME = "settings.sys";

	public const string CRITICAL_FILE_FILENAME = "net64.sys";

	public const double BUTTON_TRANSITION_OFFSET = 40.0;

	public const float TRANSITION_TIME = 0.3f;

	public const double TRANSITION_ELEMENT_INCREASE = 0.1;

	public Color themeColor = Color.PaleTurquoise;

	public Color themeColorBackground = new Color(10, 15, 25, 200);

	public Color themeColorLine = new Color(20, 25, 45, 200);

	private Folder root;

	private Folder missionsFolder;

	private Folder usersFolder;

	private Folder listingsFolder;

	private Folder listingArchivesFolder;

	private string groupName = "UNKNOWN";

	private int contractRegistryNumber = 256;

	private Dictionary<string, ActiveMission> listingMissions = new Dictionary<string, ActiveMission>();

	private Texture2D decorationPanel;

	private Texture2D decorationPanelSide;

	private Texture2D lockIcon;

	public string MissionSourceFolderPath = "Content/Missions/MainHub/FirstSet/";

	private BarcodeEffect barcode;

	private ThinBarcode thinBarcodeTop;

	private ThinBarcode thinBarcodeBot;

	public bool allowAbandon = true;

	private HubState state = HubState.Welcome;

	private string activeUserName = "UNKNOWN USER";

	private DateTime activeUserLoginTime = DateTime.Now;

	private float screenTransition = 0f;

	private int selectedElementIndex = 0;

	private int userListPageNumber = 0;

	private int missionListPageNumber = 0;

	private int missionListDisplayed = 1;

	private float timeSpentInLoading = 0f;

	public MissionHubServer(Computer c, string serviceName, string group, OS _os)
		: base(c, serviceName, _os)
	{
		groupName = group;
		decorationPanel = TextureBank.load("Sprites/HubDecoration", os.content);
		decorationPanelSide = TextureBank.load("Sprites/HubDecorationSide", os.content);
		lockIcon = TextureBank.load("Lock", os.content);
	}

	public override void initFiles()
	{
		root = new Folder("ContractHub");
		missionsFolder = new Folder("Contracts");
		listingsFolder = new Folder("Listings");
		listingArchivesFolder = new Folder("Archives");
		missionsFolder.folders.Add(listingsFolder);
		missionsFolder.folders.Add(listingArchivesFolder);
		usersFolder = new Folder("Users");
		root.folders.Add(missionsFolder);
		root.folders.Add(usersFolder);
		root.files.Add(generateConfigFile());
		root.files.Add(new FileEntry(Computer.generateBinaryString(1024), "net64.sys"));
		populateUserList();
		os.delayer.Post(ActionDelayer.NextTick(), delegate
		{
			loadInitialContracts();
		});
		comp.files.root.folders.Add(root);
	}

	private void populateUserList()
	{
		initializeUsers();
	}

	private void loadInitialContracts()
	{
		int num = 0;
		DirectoryInfo directoryInfo = new DirectoryInfo(MissionSourceFolderPath);
		FileInfo[] files = directoryInfo.GetFiles("*.xml");
		for (int i = 0; i < files.Length; i++)
		{
			string text = MissionSourceFolderPath + files[i].Name;
			try
			{
				addMission((ActiveMission)ComputerLoader.readMission(text));
			}
			catch (Exception innerException)
			{
				FormatException ex = new FormatException("Error Loading Mission: " + text, innerException);
				throw ex;
			}
			num++;
		}
	}

	public void AddMissionToListings(string missionFilename, int desiredIndex = -1)
	{
		addMission((ActiveMission)ComputerLoader.readMission(missionFilename), insertAtTop: true, preventRegistryNumberChange: false, desiredIndex);
	}

	public void RemoveMissionFromListings(string missionFilename)
	{
		List<ActiveMission> branchMissions = os.branchMissions;
		ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(Utils.GetFileLoadPrefix() + missionFilename);
		string text = null;
		foreach (KeyValuePair<string, ActiveMission> listingMission in listingMissions)
		{
			if (listingMission.Value.reloadGoalsSourceFile == activeMission.reloadGoalsSourceFile)
			{
				text = listingMission.Key;
				break;
			}
		}
		if (text != null)
		{
			listingMissions.Remove(text);
			for (int i = 0; i < listingsFolder.files.Count; i++)
			{
				if (listingsFolder.files[i].name.Contains("#" + text))
				{
					listingsFolder.files.RemoveAt(i);
					i--;
				}
			}
		}
		os.branchMissions = branchMissions;
	}

	private void addMission(ActiveMission mission, bool insertAtTop = false, bool preventRegistryNumberChange = false, int desiredInsertionIndex = -1)
	{
		if (insertAtTop && desiredInsertionIndex <= -1)
		{
			desiredInsertionIndex = 0;
		}
		contractRegistryNumber += Utils.getRandomByte() + 1;
		listingMissions.Add(string.Concat(contractRegistryNumber), mission);
		FileEntry item = new FileEntry(MissionSerializer.generateMissionFile(mission, contractRegistryNumber), "Contract#" + contractRegistryNumber);
		if (desiredInsertionIndex < listingsFolder.files.Count && (insertAtTop || desiredInsertionIndex >= 0))
		{
			listingsFolder.files.Insert(desiredInsertionIndex, item);
		}
		else
		{
			listingsFolder.files.Add(item);
		}
	}

	private FileEntry generateConfigFile()
	{
		string text = "//Contract Hub Setup File\n";
		text = text + "ThemeColor = " + Utils.convertColorToParseableString(themeColor) + "\n";
		text = text + "ServiceName = " + name + "\n";
		text = text + "GroupName = " + groupName + "\n";
		object obj = text;
		text = string.Concat(obj, "ContractListingIndexes = ", contractRegistryNumber, "\n");
		text = text + "LineColor = " + Utils.convertColorToParseableString(themeColorLine) + "\n";
		text = text + "BackColor = " + Utils.convertColorToParseableString(themeColorBackground) + "\n";
		obj = text;
		text = string.Concat(obj, "EnableABN = ", allowAbandon, "\n");
		text += "\n";
		return new FileEntry(text, "settings.sys");
	}

	private void loadFromConfigFileData(string config)
	{
		string[] array = config.Split(Utils.newlineDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].StartsWith("//"))
			{
				continue;
			}
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
			else if (text.StartsWith("ContractListingIndexes"))
			{
				try
				{
					contractRegistryNumber = Convert.ToInt32(getDataFromConfigLine(text));
				}
				catch (FormatException)
				{
					contractRegistryNumber = 0;
				}
				catch (OverflowException)
				{
					contractRegistryNumber = 0;
				}
			}
			if (text.StartsWith("LineColor"))
			{
				themeColorLine = Utils.convertStringToColor(getDataFromConfigLine(text));
			}
			if (text.StartsWith("BackColor"))
			{
				themeColorBackground = Utils.convertStringToColor(getDataFromConfigLine(text));
			}
			if (text.StartsWith("EnableABN"))
			{
				allowAbandon = getDataFromConfigLine(text).ToLower() == "true";
			}
		}
	}

	private string getDataFromConfigLine(string line, string sentinel = "= ")
	{
		return line.Substring(line.IndexOf(sentinel) + 2);
	}

	public override void loadInit()
	{
		base.loadInit();
		root = comp.files.root.searchForFolder("ContractHub");
		missionsFolder = root.searchForFolder("Contracts");
		listingsFolder = missionsFolder.searchForFolder("Listings");
		listingArchivesFolder = missionsFolder.searchForFolder("Archives");
		usersFolder = root.searchForFolder("Users");
		FileEntry fileEntry = root.searchForFile("settings.sys");
		if (fileEntry != null)
		{
			loadFromConfigFileData(fileEntry.data);
		}
		loadListingMissionsFromFiles();
	}

	private void loadListingMissionsFromFiles()
	{
		for (int i = 0; i < listingsFolder.files.Count; i++)
		{
			string data = listingsFolder.files[i].data;
			string iDStringForContractFile = getIDStringForContractFile(listingsFolder.files[i]);
			try
			{
				listingMissions.Add(iDStringForContractFile, (ActiveMission)MissionSerializer.restoreMissionFromFile(data, out contractRegistryNumber));
			}
			catch (FormatException)
			{
			}
		}
	}

	public override string getSaveString()
	{
		return "<MissionHubServer />";
	}

	private void initializeUsers()
	{
		List<int> list = new List<int>();
		int num = 0;
		string text = "";
		string text2 = "";
		if (People.hubAgents == null)
		{
			People.init();
		}
		for (int i = 0; i < People.hubAgents.Count; i++)
		{
			num = 0;
			do
			{
				num = Utils.random.Next(9999);
			}
			while (list.Contains(num));
			text = "USER: " + num + "\n";
			text = text + "Handle: " + People.hubAgents[i].handle + "\n";
			text = text + "Date Joined : " + (DateTime.Now - TimeSpan.FromDays(Utils.random.NextDouble() * 200.0)).ToString().Replace('/', '-').Replace(' ', '_') + "\n";
			text = text + "Status : " + generateUserState(People.hubAgents[i].handle) + "\n";
			text = text + "Rank : " + generateUserRank(People.hubAgents[i].handle) + "\n";
			text2 = People.hubAgents[i].handle + "#" + num;
			FileEntry item = new FileEntry(text, text2);
			usersFolder.files.Add(item);
		}
		num = 0;
		do
		{
			num = Utils.random.Next(9999);
		}
		while (list.Contains(num));
		text = "USER: " + num + "\n";
		text += "Handle: Bit\n";
		text = text + "Date Joined : " + (DateTime.Now - TimeSpan.FromDays(411.0)).ToString().Replace('/', '-').Replace(' ', '_') + "\n";
		text = text + "Status : " + generateUserState("Bit") + "\n";
		text = text + "Rank : " + generateUserRank("Bit") + "\n";
		text2 = "Bit#" + num;
		FileEntry item2 = new FileEntry(text, text2);
		usersFolder.files.Insert(0, item2);
	}

	public void addUser(UserDetail newUser)
	{
		int num = Utils.random.Next(9999);
		string text = "USER: " + num + "\n";
		text = text + "Handle: " + newUser.name + "\n";
		text = text + "Date Joined : " + DateTime.Now.ToString().Replace('/', '-').Replace(' ', '_') + "\n";
		text += "Status : Active\n";
		object obj = text;
		text = string.Concat(obj, "Rank : ", 0, "\n");
		string nameEntry = newUser.name + "#" + num;
		FileEntry item = new FileEntry(text, nameEntry);
		usersFolder.files.Add(item);
	}

	private string generateUserRank(string username)
	{
		if (username != null && username == "Bit")
		{
			return "2116";
		}
		return string.Concat((int)(Utils.random.NextDouble() * 2500.0));
	}

	private string generateUserState(string username)
	{
		if (username != null && username == "Bit")
		{
			return "UNKNOWN";
		}
		if (username.GetHashCode() < 1073741823)
		{
			return "Active";
		}
		return "Passive";
	}

	public int GetNumberOfAvaliableMissions()
	{
		return listingMissions.Count;
	}

	private void CheckForGameStateIssuesAndFix()
	{
		if (Settings.IsInExtensionMode)
		{
			return;
		}
		if (os.currentMission == null)
		{
			if (os.Flags.HasFlag("bitPathStarted") && !os.Flags.HasFlag("Victory"))
			{
				bool flag = false;
				foreach (KeyValuePair<string, ActiveMission> listingMission in listingMissions)
				{
					if (listingMission.Value.reloadGoalsSourceFile.Contains("BitPath/BitAdv_Recovery.xml"))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					AddMissionToListings("Content/Missions/BitPath/BitAdv_Recovery.xml");
				}
			}
			if (DLC1SessionUpgrader.HasDLC1Installed && !os.Flags.HasFlag("dlc_complete"))
			{
				bool flag = false;
				foreach (KeyValuePair<string, ActiveMission> listingMission2 in listingMissions)
				{
					if (listingMission2.Value.reloadGoalsSourceFile.Contains("CSEC_DLCConnectorIntro.xml"))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					AddMissionToListings("Content/DLC/Missions/BaseGameConnectors/Missions/CSEC_DLCConnectorIntro.xml");
				}
			}
		}
		if (GetNumberOfAvaliableMissions() == 0 && os.currentMission == null && !os.Flags.HasFlag("bitPathStarted"))
		{
			foreach (KeyValuePair<string, Faction> faction in os.allFactions.factions)
			{
				if (faction.Value.idName == "hub" && faction.Value is HubFaction hubFaction)
				{
					hubFaction.ForceStartBitMissions(os);
				}
			}
		}
		int num = 0;
		int num2 = 0;
		string text = null;
		foreach (KeyValuePair<string, ActiveMission> listingMission3 in listingMissions)
		{
			bool flag2 = true;
			if (listingMission3.Value.reloadGoalsSourceFile.Contains("DLC"))
			{
				flag2 = false;
			}
			if (listingMission3.Value.postingAcceptFlagRequirements != null)
			{
				for (int i = 0; i < listingMission3.Value.postingAcceptFlagRequirements.Length; i++)
				{
					if (!os.Flags.HasFlag(listingMission3.Value.postingAcceptFlagRequirements[i]))
					{
						flag2 = false;
						if (text == null)
						{
							text = listingMission3.Key;
						}
					}
				}
			}
			if (flag2)
			{
				num++;
			}
			else
			{
				num2++;
			}
		}
		if (num > 0 || num2 <= 0 || os.currentMission != null || os.Flags.HasFlag("bitPathStarted"))
		{
			return;
		}
		foreach (KeyValuePair<string, Faction> faction2 in os.allFactions.factions)
		{
			if (faction2.Value.idName == "hub" && faction2.Value is HubFaction hubFaction2)
			{
				hubFaction2.ForceStartBitMissions(os);
			}
		}
	}

	private string getIDStringForContractFile(FileEntry file)
	{
		int num = file.name.IndexOf('#');
		if (num > -1)
		{
			num++;
			return file.name.Substring(file.name.IndexOf('#') + 1);
		}
		return "";
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		screenTransition = 1f;
		state = HubState.Welcome;
		missionListPageNumber = 0;
		if (thinBarcodeTop != null)
		{
			thinBarcodeTop.regenerate();
		}
		if (thinBarcodeBot != null)
		{
			thinBarcodeBot.regenerate();
		}
		CheckForGameStateIssuesAndFix();
	}

	public override void loginGoBack()
	{
		base.loginGoBack();
		state = HubState.Welcome;
		screenTransition = 1f;
	}

	public override void userLoggedIn()
	{
		base.userLoggedIn();
		activeUserName = user.name;
		state = HubState.Menu;
		screenTransition = 1f;
		activeUserLoginTime = DateTime.Now;
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		updateScreenTransition();
		switch (state)
		{
		case HubState.Welcome:
			doBarcodeEffect(bounds, sb);
			drawWelcomeScreen(bounds, sb);
			break;
		case HubState.Menu:
			doMenuScreen(bounds, sb);
			doLoggedInScreenDetailing(bounds, sb);
			break;
		case HubState.CancelContract:
			doCancelContractScreen(bounds, sb);
			doLoggedInScreenDetailing(bounds, sb);
			break;
		case HubState.Login:
			doBarcodeEffect(bounds, sb);
			doLoginDisplay(bounds, sb);
			break;
		case HubState.Listing:
			doListingScreen(bounds, sb);
			break;
		case HubState.ContractPreview:
			doLoggedInScreenDetailing(bounds, sb);
			doContractPreviewScreen(bounds, sb);
			break;
		case HubState.UserList:
			doLoggedInScreenDetailing(bounds, sb);
			doUserListScreen(bounds, sb);
			break;
		}
	}

	private void updateScreenTransition()
	{
		screenTransition -= (float)os.lastGameTime.ElapsedGameTime.TotalSeconds / 0.3f;
		screenTransition = Math.Max(screenTransition, 0f);
	}

	private int getTransitionOffset(int position)
	{
		return (int)(Math.Pow(Math.Min((double)screenTransition + (double)position * 0.1, 1.0), 1.0) * 40.0 * (double)screenTransition);
	}

	private void doMenuScreen(Rectangle bounds, SpriteBatch sb)
	{
		Rectangle rectangle = new Rectangle(bounds.X + 10, bounds.Y + bounds.Height / 3 + 10, bounds.Width / 2, 40);
		if (Button.doButton(101010, rectangle.X + getTransitionOffset(0), rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Contract Listing"), themeColor))
		{
			state = HubState.Listing;
			screenTransition = 1f;
		}
		rectangle.Y += rectangle.Height + 5;
		if (Button.doButton(101015, rectangle.X + getTransitionOffset(1), rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("User List"), themeColor))
		{
			state = HubState.UserList;
			screenTransition = 1f;
		}
		rectangle.Y += rectangle.Height + 5;
		if (allowAbandon)
		{
			if (Button.doButton(101017, rectangle.X + getTransitionOffset(1), rectangle.Y, rectangle.Width, rectangle.Height / 2, LocaleTerms.Loc("Abort Current Contract"), (os.currentMission == null) ? Color.Black : themeColor) && os.currentMission != null)
			{
				state = HubState.CancelContract;
				screenTransition = 1f;
			}
			rectangle.Y += rectangle.Height / 2 + 5;
		}
		if (Button.doButton(102015, rectangle.X + getTransitionOffset(3), rectangle.Y, rectangle.Width, rectangle.Height / 2, LocaleTerms.Loc("Exit"), os.lockedColor))
		{
			os.display.command = "connect";
		}
		rectangle.Y += rectangle.Height + 5;
	}

	private void doCancelContractScreen(Rectangle bounds, SpriteBatch sb)
	{
		Rectangle rectangle = new Rectangle(bounds.X + 10, bounds.Y + bounds.Height / 3 + 10, bounds.Width / 2, 40);
		TextItem.doFontLabel(new Vector2(rectangle.X, rectangle.Y), LocaleTerms.Loc("Are you sure you with to abandon your current contract?") + "\n" + LocaleTerms.Loc("This cannot be reversed."), GuiData.font, Color.White, bounds.Width - 30, rectangle.Height);
		rectangle.Y += rectangle.Height + 4;
		if (Button.doButton(142011, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Abandon Contract"), Color.Red) && os.currentMission != null)
		{
			os.currentMission = null;
			os.currentFaction.contractAbbandoned(os);
			screenTransition = 0f;
			state = HubState.Menu;
			CheckForGameStateIssuesAndFix();
		}
		rectangle.Y += rectangle.Height + 10;
		if (Button.doButton(142015, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Back"), themeColor))
		{
			screenTransition = 1f;
			state = HubState.Menu;
		}
	}

	private void doListingScreen(Rectangle bounds, SpriteBatch sb)
	{
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		Rectangle rectangle = doListingScreenBackground(bounds, sb);
		Rectangle rectangle2 = new Rectangle(bounds.X + 10, rectangle.Y, bounds.Width / 2, 30);
		int num = 36;
		for (int i = missionListPageNumber * missionListDisplayed; i < listingsFolder.files.Count; i++)
		{
			if (rectangle2.Y + num >= rectangle.Y + rectangle.Height)
			{
				break;
			}
			string iDStringForContractFile = getIDStringForContractFile(listingsFolder.files[i]);
			if (listingMissions.ContainsKey(iDStringForContractFile))
			{
				ActiveMission activeMission = listingMissions[iDStringForContractFile];
				if (activeMission == null)
				{
					Console.WriteLine("Mission: " + iDStringForContractFile + " is null! There is an error in one of your mission files preventing draw");
					continue;
				}
				drawMissionEntry(new Rectangle(bounds.X + 1, rectangle2.Y, bounds.Width - 2, num), sb, activeMission, i);
				rectangle2.Y += num;
			}
		}
		int num2 = 0;
		rectangle2 = new Rectangle(bounds.X + 10, rectangle.Y, bounds.Width / 2, 30);
		while (rectangle2.Y + num < rectangle.Y + rectangle.Height)
		{
			rectangle2.Y += num;
			num2++;
		}
		missionListDisplayed = num2;
		TextItem.DrawShadow = drawShadow;
	}

	private void drawMissionEntry(Rectangle bounds, SpriteBatch sb, ActiveMission mission, int index)
	{
		bool flag = false;
		if (mission.postingAcceptFlagRequirements != null)
		{
			for (int i = 0; i < mission.postingAcceptFlagRequirements.Length; i++)
			{
				if (!os.Flags.HasFlag(mission.postingAcceptFlagRequirements[i]))
				{
					flag = true;
				}
			}
		}
		if (os.currentFaction != null && os.currentFaction.playerValue < mission.requiredRank)
		{
			flag = true;
		}
		int num = index * 139284 + 984275 + index;
		bool outlineOnly = Button.outlineOnly;
		bool drawingOutline = Button.drawingOutline;
		Button.outlineOnly = true;
		Button.drawingOutline = false;
		if (GuiData.active == num)
		{
			sb.Draw(Utils.white, bounds, Color.Black);
		}
		else if (GuiData.hot == num)
		{
			sb.Draw(Utils.white, bounds, themeColor * 0.12f);
		}
		else
		{
			Color color = ((index % 2 == 0) ? themeColorLine : themeColorBackground);
			if (flag)
			{
				color = Color.Lerp(color, Color.Gray, 0.25f);
			}
			sb.Draw(Utils.white, bounds, color);
		}
		if (mission.postingTitle.StartsWith("#"))
		{
			PatternDrawer.draw(bounds, 1f, Color.Transparent, Color.Black * 0.6f, sb);
		}
		if (flag)
		{
			Rectangle destinationRectangle = bounds;
			destinationRectangle.Height -= 6;
			destinationRectangle.Y += 3;
			destinationRectangle.X += bounds.Width - bounds.Height - 6;
			destinationRectangle.Width = destinationRectangle.Height;
			sb.Draw(lockIcon, destinationRectangle, Color.White * 0.2f);
		}
		if (!flag && Button.doButton(num, bounds.X, bounds.Y, bounds.Width, bounds.Height, "", Color.Transparent))
		{
			selectedElementIndex = index;
			state = HubState.ContractPreview;
			screenTransition = 1f;
		}
		string text = mission.postingTitle.Replace("#", "") ?? "";
		TextItem.doFontLabel(new Vector2(bounds.X + 1 + getTransitionOffset(index), bounds.Y + 3), text, GuiData.smallfont, Color.White, bounds.Width);
		string text2 = "Target: " + mission.target + " -- Client: " + mission.client + " -- Key: " + mission.generationKeys;
		TextItem.doFontLabel(new Vector2(bounds.X + 1, bounds.Y + bounds.Height - 16), text2, GuiData.detailfont, Color.White * 0.3f, bounds.Width, 13f);
		bounds.Y += bounds.Height - 1;
		bounds.Height = 1;
		sb.Draw(Utils.white, bounds, themeColor * 0.2f);
		Button.outlineOnly = outlineOnly;
		Button.drawingOutline = drawingOutline;
	}

	private Rectangle doListingScreenBackground(Rectangle bounds, SpriteBatch sb)
	{
		Rectangle destinationRectangle = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
		sb.Draw(Utils.white, destinationRectangle, themeColorBackground);
		doLoggedInScreenDetailing(bounds, sb);
		destinationRectangle.Height = 5;
		destinationRectangle.Width = 0;
		destinationRectangle.Y += 12;
		if (thinBarcodeTop == null)
		{
			thinBarcodeTop = new ThinBarcode(bounds.Width - 4, 5);
		}
		if (thinBarcodeBot == null)
		{
			thinBarcodeBot = new ThinBarcode(bounds.Width - 4, 5);
		}
		thinBarcodeTop.Draw(sb, destinationRectangle.X, destinationRectangle.Y, themeColor);
		TextItem.doFontLabel(new Vector2(bounds.X + 20, destinationRectangle.Y + 10), "MISSION LISTING", GuiData.titlefont, themeColor, bounds.Width - 40, 35f);
		destinationRectangle.Y += 45;
		destinationRectangle.X = bounds.X + 1;
		thinBarcodeBot.Draw(sb, destinationRectangle.X, destinationRectangle.Y, themeColor);
		destinationRectangle.Y += 10;
		destinationRectangle.X = bounds.X + 1;
		destinationRectangle.Width = decorationPanel.Width;
		destinationRectangle.Height = decorationPanel.Height;
		sb.Draw(decorationPanel, destinationRectangle, themeColor);
		destinationRectangle.X += destinationRectangle.Width;
		destinationRectangle.Width = bounds.Width - 2 - destinationRectangle.Width;
		sb.Draw(decorationPanelSide, destinationRectangle, themeColor);
		Vector2 pos = new Vector2(bounds.X + 6, destinationRectangle.Y + decorationPanel.Height / 2 - 8);
		float num = 0.3f;
		int num2 = (int)((float)decorationPanel.Height * num);
		float num3 = ((float)decorationPanel.Height - (float)num2) / 2f;
		Rectangle destinationRectangle2 = new Rectangle(bounds.X + 1, (int)((float)destinationRectangle.Y + num3), decorationPanel.Width, num2);
		Rectangle value = new Rectangle(0, (int)(((float)decorationPanel.Height - (float)num2) / 2f), decorationPanel.Width, num2);
		bool outlineOnly = Button.outlineOnly;
		bool drawingOutline = Button.drawingOutline;
		Button.outlineOnly = true;
		Button.drawingOutline = false;
		int num4 = 974748322;
		Color color = ((GuiData.active == num4) ? Color.Black : ((GuiData.hot == num4) ? themeColorLine : themeColorBackground));
		sb.Draw(decorationPanel, destinationRectangle2, value, color, 0f, Vector2.Zero, SpriteEffects.None, 0.6f);
		if (Button.doButton(num4, bounds.X + 1, destinationRectangle.Y + decorationPanel.Height / 6 + 4, decorationPanel.Width - 30, (int)((float)decorationPanel.Height - (float)(decorationPanel.Height / 6) * 3.2f), LocaleTerms.Loc("Back"), Color.Transparent))
		{
			screenTransition = 1f;
			state = HubState.Menu;
		}
		Button.outlineOnly = outlineOnly;
		Button.drawingOutline = drawingOutline;
		pos.X += decorationPanel.Width - 10;
		pos.Y += 4f;
		string text = LocaleTerms.Loc(groupName + " secure contract listing panel : Verified Connection : Token last verified") + " " + DateTime.Now.ToString();
		TextItem.doFontLabel(pos, text, GuiData.detailfont, themeColor, (float)destinationRectangle.Width - 10f, 14f);
		pos.Y += 12f;
		if (missionListPageNumber > 0 && Button.doButton(188278101, (int)pos.X, (int)pos.Y, 45, 20, "<", null))
		{
			missionListPageNumber--;
			screenTransition = 1f;
		}
		destinationRectangle.X += 50;
		int num5 = listingsFolder.files.Count / missionListDisplayed + 1;
		int num6 = missionListPageNumber + 1;
		string text2 = num6 + "/" + num5;
		float num7 = 50f - GuiData.smallfont.MeasureString(text2).X / 2f;
		sb.DrawString(GuiData.smallfont, text2, new Vector2((float)destinationRectangle.X + num7, (int)pos.Y + 1), Color.White);
		destinationRectangle.X += 100;
		if (missionListPageNumber < num5 - 1 && Button.doButton(188278102, destinationRectangle.X, (int)pos.Y, 45, 20, ">", null))
		{
			missionListPageNumber++;
			screenTransition = 1f;
		}
		destinationRectangle.Y += decorationPanel.Height + 4;
		destinationRectangle.Width = bounds.Width - 2;
		destinationRectangle.X = bounds.X + 1;
		destinationRectangle.Height = 7;
		sb.Draw(Utils.white, destinationRectangle, themeColor);
		destinationRectangle.Y += destinationRectangle.Height;
		return new Rectangle(bounds.X, destinationRectangle.Y, bounds.Width, bounds.Height - bounds.Height / 12 - (destinationRectangle.Y - bounds.Y));
	}

	private void doContractPreviewScreen(Rectangle bounds, SpriteBatch sb)
	{
		string iDStringForContractFile = getIDStringForContractFile(listingsFolder.files[selectedElementIndex]);
		if (!listingMissions.ContainsKey(iDStringForContractFile))
		{
			return;
		}
		ActiveMission activeMission = listingMissions[iDStringForContractFile];
		Vector2 vector = new Vector2(bounds.X + 20, bounds.Y + 20);
		TextItem.doFontLabel(vector + new Vector2(getTransitionOffset(0), 0f), "CONTRACT:" + iDStringForContractFile, GuiData.titlefont, null, bounds.Width / 2, 40f);
		vector.Y += 40f;
		TextItem.doFontLabel(vector + new Vector2(getTransitionOffset(1), 0f), activeMission.postingTitle.Replace("#", ""), GuiData.font, null, bounds.Width - 30);
		vector.Y += 30f;
		string text = DisplayModule.cleanSplitForWidth(activeMission.postingBody, bounds.Width - 110);
		StringBuilder stringBuilder = new StringBuilder();
		int num = (int)(((float)bounds.Width - 20f) / GuiData.smallfont.MeasureString("-").X / 2f);
		for (int i = 1; i < num - 5; i++)
		{
			stringBuilder.Append("-");
		}
		text = text.Replace("###", stringBuilder.ToString());
		if (LocaleActivator.ActiveLocaleIsCJK())
		{
			text = Utils.SuperSmartTwimForWidth(activeMission.postingBody, bounds.Width - 110, GuiData.smallfont);
			vector.Y += 20f;
		}
		TextItem.doFontLabel(vector + new Vector2(getTransitionOffset(2), 0f), text, GuiData.smallfont, null, bounds.Width - 20);
		int num2 = Math.Max(135, bounds.Height / 6);
		if (Button.doButton(2171618, bounds.X + 20 + getTransitionOffset(3), bounds.Y + bounds.Height - num2, bounds.Width / 5, 30, LocaleTerms.Loc("Back"), null))
		{
			state = HubState.Listing;
			screenTransition = 1f;
		}
		if (os.currentMission == null)
		{
			if (Button.doButton(2171615, bounds.X + 20 + getTransitionOffset(4), bounds.Y + bounds.Height - num2 - 40, bounds.Width / 5, 30, LocaleTerms.Loc("Accept"), os.highlightColor))
			{
				acceptMission(activeMission, selectedElementIndex, iDStringForContractFile);
				state = HubState.Listing;
				screenTransition = 1f;
			}
		}
		else
		{
			TextItem.doFontLabelToSize(new Rectangle(bounds.X + 20 + getTransitionOffset(4), bounds.Y + bounds.Height - num2 - 40, bounds.Width / 2, 30), LocaleTerms.Loc("Abort current contract to accept new ones."), GuiData.smallfont, Color.White);
		}
	}

	private void doUserListScreen(Rectangle bounds, SpriteBatch sb)
	{
		if (Button.doButton(101801, bounds.X + 2, bounds.Y + 20, bounds.Width / 4, 22, LocaleTerms.Loc("Back"), themeColor))
		{
			state = HubState.Menu;
		}
		Rectangle rectangle = new Rectangle(bounds.X + 30, bounds.Y + 50, bounds.Width / 2, 30);
		int num = 18 + rectangle.Height - 8;
		int num2 = bounds.Height - 90;
		int num3 = num2 / num;
		int num4 = num3 * userListPageNumber;
		int num5 = 0;
		for (int i = num3 * userListPageNumber; i < usersFolder.files.Count && i < num4 + num3; i++)
		{
			string[] array = usersFolder.files[i].data.Split(Utils.newlineDelim);
			string text2;
			string arg;
			string text;
			string text3 = (text2 = (arg = (text = "")));
			for (int j = 0; j < array.Length; j++)
			{
				if (array[j].StartsWith("USER"))
				{
					text3 = getDataFromConfigLine(array[j], ": ");
				}
				if (array[j].StartsWith("Rank"))
				{
					text = getDataFromConfigLine(array[j], ": ");
				}
				if (array[j].StartsWith("Handle"))
				{
					text2 = getDataFromConfigLine(array[j], ": ");
				}
				if (array[j].StartsWith("Date"))
				{
					arg = getDataFromConfigLine(array[j], ": ");
				}
			}
			Rectangle destinationRectangle = new Rectangle(rectangle.X + (bounds.Width - 60) - 10, rectangle.Y + 2, rectangle.Height + 1, bounds.Width - 60);
			GuiData.spriteBatch.Draw(Utils.gradient, destinationRectangle, null, themeColorLine, (float)Math.PI / 2f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0.8f);
			TextItem.doSmallLabel(new Vector2(rectangle.X, rectangle.Y), "#" + text3 + " - \"" + text2 + "\" Rank:" + text, null);
			rectangle.Y += 18;
			TextItem.doFontLabel(new Vector2(rectangle.X, rectangle.Y), string.Format(LocaleTerms.Loc("Joined {0}"), arg), GuiData.detailfont, null);
			rectangle.Y += rectangle.Height - 10;
			num5 = i;
		}
		rectangle.Y += 16;
		if (userListPageNumber > 0 && Button.doButton(101005, rectangle.X, rectangle.Y, rectangle.Width / 2 - 20, 15, LocaleTerms.Loc("Previous Page"), null))
		{
			userListPageNumber--;
		}
		TextItem.doTinyLabel(new Vector2(rectangle.X + rectangle.Width / 2 - 8, rectangle.Y), string.Concat(userListPageNumber), null);
		if (usersFolder.files.Count > num5 + 1 && Button.doButton(101010, rectangle.X + rectangle.Width / 2 + 10, rectangle.Y, rectangle.Width / 2 - 10, 15, LocaleTerms.Loc("Next Page"), null))
		{
			userListPageNumber++;
		}
	}

	private void acceptMission(ActiveMission mission, int index, string id)
	{
		os.currentMission = mission;
		ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(mission.reloadGoalsSourceFile);
		mission.sendEmail(os);
		mission.ActivateSuppressedStartFunctionIfPresent();
		FileEntry fileEntry = listingsFolder.files[index];
		listingsFolder.files.RemoveAt(index);
		string text = "Contract Archive:\nAccepted : " + DateTime.Now.ToString() + "\nUser : " + activeUserName + "\nActive Since : " + activeUserLoginTime.ToString();
		listingArchivesFolder.files.Add(new FileEntry(text + "\n\n" + fileEntry.data, "Contract#" + id + "Archive"));
		listingMissions.Remove(id);
	}

	private void drawWelcomeScreen(Rectangle bounds, SpriteBatch sb)
	{
		Rectangle rectangle = new Rectangle(bounds.X + 10, bounds.Y + bounds.Height / 3 - 10, bounds.Width / 2, 30);
		string text = $"{groupName} Contract Hub";
		text = text.ToUpper();
		TextItem.doFontLabel(new Vector2(rectangle.X, rectangle.Y), text, GuiData.titlefont, null, (float)bounds.Width / 0.6f, 50f);
		rectangle.Y += 50;
		if (Button.doButton(11005, rectangle.X + getTransitionOffset(0), rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Login"), themeColor))
		{
			startLogin();
			state = HubState.Login;
		}
		rectangle.Y += rectangle.Height + 5;
		if (Button.doButton(12010, rectangle.X + getTransitionOffset(1), rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Exit"), themeColor))
		{
			os.display.command = "connect";
		}
	}

	private void doBarcodeEffect(Rectangle bounds, SpriteBatch sb)
	{
		if (barcode == null || barcode.maxWidth != bounds.Width - 2 || barcode.leftRightBias)
		{
			barcode = new BarcodeEffect(bounds.Width - 2, inverted: true);
		}
		barcode.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		barcode.Draw(bounds.X + 1, bounds.Y + 5 * (bounds.Height / 6) - 1, bounds.Width - 2, bounds.Height / 6, sb, themeColor);
		barcode.isInverted = false;
		barcode.Draw(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height / 6, sb, themeColor);
		barcode.isInverted = true;
	}

	private void doLoggedInScreenDetailing(Rectangle bounds, SpriteBatch sb)
	{
		string text = LocaleTerms.Loc("Authenticated User:") + " " + activeUserName + " -- Token Active Since: " + activeUserLoginTime.ToString();
		TextItem.doFontLabel(new Vector2(bounds.X + 1, bounds.Y + 1), text, GuiData.detailfont, null, bounds.Width - 2);
		doBaseBarcodeEffect(bounds, sb);
	}

	private void doBaseBarcodeEffect(Rectangle bounds, SpriteBatch sb)
	{
		if (barcode == null || barcode.maxWidth != bounds.Width - 2 || !barcode.leftRightBias)
		{
			barcode = new BarcodeEffect(bounds.Width - 2, inverted: true, leftRight: true);
		}
		barcode.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		int y = bounds.Y + 11 * (bounds.Height / 12) - 1;
		barcode.Draw(bounds.X + 1, y, bounds.Width - 2, bounds.Height / 12, sb, themeColor);
	}
}
