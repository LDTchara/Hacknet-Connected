using System;
using System.Collections.Generic;
using System.IO;
using Hacknet.Extensions;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MissionListingServer : AuthenticatingDaemon
{
	private const int NEED_LOGIN = 0;

	private const int BOARD = 1;

	private const int MESSAGE = 2;

	private const int LOGIN = 3;

	private string groupName;

	public string listingTitle;

	private Color themeColor;

	private Texture2D topBar;

	private Texture2D corner;

	private Texture2D logo;

	private Rectangle panelRect;

	private Rectangle logoRect;

	private Folder root;

	private Folder missionFolder;

	private Folder closedMissionsFolder;

	private List<int> rootPath;

	private List<int> missionFolderPath;

	private FileEntry sysFile;

	private int targetIndex;

	private ScrollableTextRegion TextRegion;

	public bool missionAssigner = false;

	public bool isPublic;

	private int state;

	private bool NeedsCustomFolderLoad = false;

	private string CustomFolderLoadPath = null;

	private string IconReloadPath = null;

	private string ArticleFolderPath = null;

	private bool HasCustomColor = false;

	private List<ActiveMission> missions;

	private List<List<ActiveMission>> branchMissions = new List<List<ActiveMission>>();

	public MissionListingServer(Computer c, string serviceName, string group, OS _os, bool _isPublic = false, bool _isAssigner = false)
		: base(c, serviceName, _os)
	{
		groupName = group;
		topBar = os.content.Load<Texture2D>("Panel");
		corner = os.content.Load<Texture2D>("Corner");
		if (group.Equals("Entropy"))
		{
			themeColor = new Color(3, 102, 49);
			logo = os.content.Load<Texture2D>("EntropyLogo");
		}
		else if (group.Equals("NetEdu"))
		{
			themeColor = new Color(119, 104, 160);
			logo = os.content.Load<Texture2D>("Sprites/Academic_Logo");
		}
		else if (group.Equals("Kellis Biotech"))
		{
			themeColor = new Color(106, 176, 255);
			logo = os.content.Load<Texture2D>("Sprites/KellisLogo");
		}
		else
		{
			themeColor = new Color(204, 163, 27);
			logo = os.content.Load<Texture2D>("SlashbotLogo");
		}
		logoRect = new Rectangle(0, 0, 64, 64);
		isPublic = _isPublic;
		missionAssigner = _isAssigner;
		if (isPublic)
		{
			state = 1;
		}
		else
		{
			state = 0;
		}
		missions = new List<ActiveMission>();
		branchMissions = new List<List<ActiveMission>>();
		if (isPublic)
		{
			listingTitle = string.Format(LocaleTerms.Loc("{0} News"), group);
		}
		else
		{
			listingTitle = LocaleTerms.Loc("Available Contracts");
		}
	}

	public MissionListingServer(Computer c, string serviceName, string iconPath, string articleFolderPath, Color themeColor, OS _os, bool _isPublic = false, bool _isAssigner = false)
		: base(c, serviceName, _os)
	{
		groupName = serviceName;
		topBar = os.content.Load<Texture2D>("Panel");
		corner = os.content.Load<Texture2D>("Corner");
		this.themeColor = themeColor;
		string text = "Content/";
		if (Settings.IsInExtensionMode)
		{
			text = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
		}
		try
		{
			using FileStream stream = File.OpenRead(text + iconPath);
			logo = Texture2D.FromStream(GuiData.spriteBatch.GraphicsDevice, stream);
		}
		catch (Exception)
		{
			logo = os.content.Load<Texture2D>("Sprites/Academic_Logo");
		}
		NeedsCustomFolderLoad = true;
		CustomFolderLoadPath = text + articleFolderPath;
		CustomFolderLoadPath = CustomFolderLoadPath.Replace('\\', '/');
		if (!CustomFolderLoadPath.EndsWith("/"))
		{
			CustomFolderLoadPath += "/";
		}
		logoRect = new Rectangle(0, 0, 64, 64);
		isPublic = _isPublic;
		missionAssigner = _isAssigner;
		if (isPublic)
		{
			state = 1;
		}
		else
		{
			state = 0;
		}
		missions = new List<ActiveMission>();
		branchMissions = new List<List<ActiveMission>>();
		if (isPublic)
		{
			listingTitle = serviceName;
		}
		else
		{
			listingTitle = LocaleTerms.Loc("Available Contracts");
		}
		HasCustomColor = true;
		IconReloadPath = iconPath;
		ArticleFolderPath = articleFolderPath;
	}

	public override void initFiles()
	{
		base.initFiles();
		initFilesystem();
		addListingsForGroup();
	}

	public override void loadInit()
	{
		base.loadInit();
		root = comp.files.root.searchForFolder("MsgBoard");
		missionFolder = root.searchForFolder("listings");
		closedMissionsFolder = root.searchForFolder("closed");
		if (closedMissionsFolder == null)
		{
			closedMissionsFolder = new Folder("closed");
			root.folders.Add(closedMissionsFolder);
		}
		if (!missionAssigner)
		{
			for (int i = 0; i < missionFolder.files.Count; i++)
			{
				try
				{
					string postingTitle = missionFolder.files[i].name.Replace("_", " ");
					string data = missionFolder.files[i].data;
					ActiveMission activeMission = new ActiveMission(null, null, default(MailServer.EMailData));
					activeMission.postingTitle = postingTitle;
					activeMission.postingBody = data;
					missions.Add(activeMission);
				}
				catch (Exception)
				{
					int num = 0;
					num++;
				}
			}
			return;
		}
		List<ActiveMission> list = os.branchMissions;
		for (int i = 0; i < missionFolder.files.Count; i++)
		{
			try
			{
				string data2 = missionFolder.files[i].data;
				int contractRegistryNumber = 0;
				os.branchMissions.Clear();
				ActiveMission activeMission = (ActiveMission)MissionSerializer.restoreMissionFromFile(data2, out contractRegistryNumber);
				List<ActiveMission> list2 = new List<ActiveMission>();
				list2.AddRange(os.branchMissions.ToArray());
				branchMissions.Add(list2);
				missions.Add(activeMission);
			}
			catch (Exception)
			{
				int num = 0;
				num++;
			}
		}
		os.branchMissions = list;
	}

	public override string getSaveString()
	{
		string text = "";
		if (HasCustomColor)
		{
			text = $"icon=\"{IconReloadPath}\" color=\"{Utils.convertColorToParseableString(themeColor)}\" articles=\"{ArticleFolderPath}\" ";
		}
		return "<MissionListingServer name=\"" + name + "\" group=\"" + groupName + "\" public=\"" + isPublic + "\" assign=\"" + missionAssigner + "\" title=\"" + listingTitle + "\" " + text + "/>";
	}

	public void addMisison(ActiveMission m, bool injectToTop = false)
	{
		string dataEntry = m.postingBody;
		if (missionAssigner)
		{
			dataEntry = MissionSerializer.generateMissionFile(m, 0, groupName);
		}
		FileEntry item = new FileEntry(dataEntry, m.postingTitle);
		if (injectToTop)
		{
			missionFolder.files.Insert(0, item);
			missions.Insert(0, m);
			branchMissions.Insert(0, os.branchMissions);
		}
		else
		{
			missionFolder.files.Add(item);
			missions.Add(m);
			branchMissions.Add(os.branchMissions);
		}
	}

	public void removeMission(string missionPath)
	{
		for (int i = 0; i < missions.Count; i++)
		{
			if (missions[i].reloadGoalsSourceFile == LocalizedFileLoader.GetLocalizedFilepath(missionPath))
			{
				removeMission(i);
				i--;
			}
		}
	}

	public void removeMission(int index)
	{
		bool flag = false;
		for (int i = 0; i < missionFolder.files.Count; i++)
		{
			if (missionFolder.files[i].name.Equals(missions[index].postingTitle.Replace(" ", "_")))
			{
				FileEntry item = missionFolder.files[i];
				missionFolder.files.RemoveAt(i);
				closedMissionsFolder.files.Add(item);
				flag = true;
				break;
			}
		}
		if (!flag)
		{
		}
		missions.RemoveAt(index);
	}

	public void initFilesystem()
	{
		rootPath = new List<int>();
		missionFolderPath = new List<int>();
		root = new Folder("MsgBoard");
		rootPath.Add(comp.files.root.folders.IndexOf(root));
		missionFolderPath.Add(comp.files.root.folders.IndexOf(root));
		missionFolder = new Folder("listings");
		missionFolderPath.Add(0);
		root.folders.Add(missionFolder);
		closedMissionsFolder = new Folder("closed");
		root.folders.Add(closedMissionsFolder);
		sysFile = new FileEntry(Computer.generateBinaryString(1024), "config.sys");
		root.files.Add(sysFile);
		string dataEntry = Utils.readEntireFile("Content/LocPost/ListingServerCautionFile.txt");
		FileEntry item = new FileEntry(dataEntry, "Config_CAUTION.txt");
		root.files.Add(item);
		comp.files.root.folders.Add(root);
	}

	public void addListingsForGroup()
	{
		List<ActiveMission> list = os.branchMissions;
		os.branchMissions = null;
		bool flag = false;
		if (!NeedsCustomFolderLoad && groupName.ToLower().Equals("entropy"))
		{
			NeedsCustomFolderLoad = true;
			CustomFolderLoadPath = "Content/Missions/Entropy/StartingSet/";
			flag = true;
		}
		if (NeedsCustomFolderLoad)
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(CustomFolderLoadPath);
			FileInfo[] files = directoryInfo.GetFiles("*.xml");
			for (int i = 0; i < files.Length; i++)
			{
				string filename = CustomFolderLoadPath + files[i].Name;
				os.branchMissions = new List<ActiveMission>();
				addMisison((ActiveMission)ComputerLoader.readMission(filename));
			}
			if (flag)
			{
				for (int i = 0; i < 2; i++)
				{
					os.branchMissions = new List<ActiveMission>();
					addMisison((ActiveMission)MissionGenerator.generate(2));
				}
			}
		}
		else if (groupName.ToLower().Equals("netedu"))
		{
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Misc/EducationArticles/Education4.0.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Misc/EducationArticles/Education2.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Misc/EducationArticles/Education3.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Misc/EducationArticles/Education1.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Misc/EducationArticles/Education4.1.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Misc/EducationArticles/Education5.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Misc/EducationArticles/Education6.xml"));
		}
		else if (groupName.ToLower().Equals("slashbot"))
		{
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Entropy/NewsArticles/EntropyNews1.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Entropy/NewsArticles/EntropyNews3.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Entropy/NewsArticles/EntropyNews2.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Entropy/NewsArticles/EntropyNews4.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/Entropy/NewsArticles/EntropyNews5.xml"));
		}
		else if (groupName.ToLower().Equals("kellis biotech"))
		{
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/MainHub/PacemakerSet/HardwareArticles/Hardware1.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/MainHub/PacemakerSet/HardwareArticles/Hardware2.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/MainHub/PacemakerSet/HardwareArticles/Hardware3.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/MainHub/PacemakerSet/HardwareArticles/Hardware4.xml"));
			addMisison((ActiveMission)ComputerLoader.readMission("Content/Missions/MainHub/PacemakerSet/HardwareArticles/Hardware5.xml"));
		}
		os.branchMissions = list;
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		if (isPublic)
		{
			state = 1;
		}
		else
		{
			state = 0;
		}
		ProgressionSaveFixHacks();
	}

	private void ProgressionSaveFixHacks()
	{
		if (groupName.Equals("Entropy") && missions.Count == 0 && os.currentFaction.idName == "entropy" && os.currentMission == null && !os.Flags.HasFlag("ThemeHackTransitionAssetsAdded"))
		{
			ComputerLoader.loadMission("Content/Missions/Entropy/ThemeHackTransitionMission.xml");
			os.saveGame();
		}
	}

	public override void loginGoBack()
	{
		base.loginGoBack();
		state = 0;
	}

	public override void userLoggedIn()
	{
		base.userLoggedIn();
		if (!user.name.Equals(""))
		{
			state = 1;
		}
		else
		{
			state = 0;
		}
	}

	public bool hasSysfile()
	{
		for (int i = 0; i < root.files.Count; i++)
		{
			if (root.files[i].name.Equals("config.sys"))
			{
				return true;
			}
		}
		return false;
	}

	public bool hasListingFile(string name)
	{
		name = name.Replace(" ", "_");
		for (int i = 0; i < missionFolder.files.Count; i++)
		{
			if (missionFolder.files[i].name == name)
			{
				return true;
			}
		}
		return false;
	}

	public void drawTopBar(Rectangle bounds, SpriteBatch sb)
	{
		panelRect = new Rectangle(bounds.X, bounds.Y, bounds.Width - corner.Width, topBar.Height);
		sb.Draw(topBar, panelRect, themeColor);
		sb.Draw(corner, new Vector2(bounds.X + bounds.Width - corner.Width, bounds.Y), themeColor);
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		PatternDrawer.draw(new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), 0.28f, Color.Transparent, themeColor * 0.1f, sb, PatternDrawer.thinStripe);
		drawTopBar(bounds, sb);
		if (!hasSysfile())
		{
			if (Button.doButton(800003, bounds.X + 10, bounds.Y + topBar.Height + 10, 300, 30, LocaleTerms.Loc("Exit"), themeColor))
			{
				os.display.command = "connect";
			}
			PatternDrawer.draw(new Rectangle(bounds.X + 1, bounds.Y + 1 + 64, bounds.Width - 2, bounds.Height - 2 - 64), 1f, Color.Transparent, os.lockedColor, sb, PatternDrawer.errorTile);
			int num = bounds.X + 20;
			int num2 = bounds.Y + bounds.Height / 2;
			num2 -= 20;
			TextItem.doLabel(new Vector2(num, num2), LocaleTerms.Loc("CRITICAL ERROR"), null);
			num2 += 40;
			TextItem.doSmallLabel(new Vector2(num, num2), "ERROR #4040408 - NULL_SYSFILE\nUnhandled Exception - IOException@L 2217 :R 28\nSystem Files Corrupted and/or Destroyed\nContact the System Administrator", null);
			return;
		}
		switch (state)
		{
		case 0:
		{
			if (Button.doButton(800003, bounds.X + 10, bounds.Y + topBar.Height + 10, 300, 30, LocaleTerms.Loc("Exit"), themeColor))
			{
				os.display.command = "connect";
			}
			sb.Draw(logo, new Rectangle(bounds.X + 30, bounds.Y + 115, 128, 128), Color.White);
			string text2 = (string.IsNullOrWhiteSpace(listingTitle) ? (string.Format(LocaleTerms.Loc("{0} Group"), groupName) + "\n" + LocaleTerms.Loc("Message Board")) : listingTitle);
			TextItem.doFontLabel(new Vector2(bounds.X + 40 + 128, bounds.Y + 115), text2, GuiData.font, null, bounds.Width - 40, 60f);
			if (Button.doButton(800004, bounds.X + 30, bounds.Y + bounds.Height / 2, 300, 40, LocaleTerms.Loc("Login"), themeColor))
			{
				startLogin();
				state = 3;
			}
			break;
		}
		case 3:
			doLoginDisplay(bounds, sb);
			break;
		case 1:
		{
			if (Button.doButton(800003, bounds.X + 10, bounds.Y + topBar.Height + 10, 300, 30, LocaleTerms.Loc("Exit"), themeColor))
			{
				os.display.command = "connect";
			}
			int num = bounds.X + 10;
			int num2 = bounds.Y + topBar.Height + 50;
			logoRect.X = num;
			logoRect.Y = num2;
			sb.Draw(logo, logoRect, Color.White);
			num += logoRect.Width + 5;
			TextItem.doLabel(new Vector2(num, num2), listingTitle, null);
			num2 += 40;
			for (int i = 0; i < missions.Count; i++)
			{
				if (hasListingFile(missions[i].postingTitle))
				{
					Rectangle rect = new Rectangle(num, num2, (int)((float)bounds.Width * 0.8f), 30);
					rect = Utils.InsetRectangle(rect, 1);
					rect.X += 12;
					rect.Width -= 12;
					if (missions[i].postingTitle.StartsWith("#"))
					{
						PatternDrawer.draw(rect, 1f, Color.Black * 1f, Color.DarkRed * 0.3f, sb, PatternDrawer.warningStripe);
					}
					if (Button.doButton(87654 + i, num, num2, (int)((float)bounds.Width * 0.8f), 30, missions[i].postingTitle, null))
					{
						state = 2;
						targetIndex = i;
					}
					num2 += 35;
				}
			}
			break;
		}
		case 2:
		{
			if (Button.doButton(800003, bounds.X + 10, bounds.Y + topBar.Height + 10, 300, 30, LocaleTerms.Loc("Back"), themeColor))
			{
				state = 1;
			}
			int num3 = 60;
			int num4 = 84;
			Rectangle destinationRectangle = new Rectangle(bounds.X + 30, bounds.Y + topBar.Height + num3, num4, num4);
			sb.Draw(logo, destinationRectangle, themeColor);
			num3 += 30;
			TextItem.doFontLabel(new Vector2(bounds.X + 34 + num4, bounds.Y + topBar.Height + num3), missions[targetIndex].postingTitle, GuiData.font, null, bounds.Width - (36 + num4 + 6), 40f);
			num3 += 40;
			Rectangle dest = new Rectangle(destinationRectangle.X + destinationRectangle.Width + 2, bounds.Y + topBar.Height + num3 - 8, bounds.Width - (destinationRectangle.X - bounds.X + destinationRectangle.Width + 10), PatternDrawer.warningStripe.Height / 2);
			PatternDrawer.draw(dest, 1f, Color.Transparent, themeColor, sb, PatternDrawer.warningStripe);
			num3 += 36;
			string text = Utils.SuperSmartTwimForWidth(missions[targetIndex].postingBody, bounds.Width - 60, GuiData.tinyfont);
			if (TextRegion == null)
			{
				TextRegion = new ScrollableTextRegion(sb.GraphicsDevice);
			}
			TextRegion.Draw(new Rectangle(bounds.X + 30, bounds.Y + topBar.Height + num3, bounds.Width - 50, bounds.Height - num3 - topBar.Height - 10), text, sb);
			bool flag = os.currentFaction != null && os.currentFaction.idName.ToLower() == groupName.ToLower();
			if (missionAssigner && os.currentMission == null && flag && Button.doButton(800005, bounds.X + bounds.Width / 2 - 10, bounds.Y + bounds.Height - 35, bounds.Width / 2, 30, LocaleTerms.Loc("Accept"), os.highlightColor))
			{
				os.currentMission = missions[targetIndex];
				ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(missions[targetIndex].reloadGoalsSourceFile);
				missions[targetIndex].sendEmail(os);
				missions[targetIndex].ActivateSuppressedStartFunctionIfPresent();
				removeMission(targetIndex);
				state = 1;
			}
			else if (missionAssigner && os.currentMission != null)
			{
				if (os.currentMission.wasAutoGenerated && Button.doButton(8000105, bounds.X + 6, bounds.Y + bounds.Height - 29, 210, 25, LocaleTerms.Loc("Abandon Current Contract"), os.lockedColor))
				{
					os.currentMission = null;
					os.currentFaction.contractAbbandoned(os);
				}
				TextItem.doFontLabel(new Vector2(bounds.X + 10, bounds.Y + bounds.Height - 52), LocaleTerms.Loc("Mission Unavailable") + " : " + (flag ? LocaleTerms.Loc("Complete Existing Contracts") : (LocaleTerms.Loc("User ID Assigned to Different Faction") + " ")), GuiData.smallfont, null, bounds.Width - 20, 30f);
			}
			else if (missionAssigner && !flag)
			{
				TextItem.doFontLabel(new Vector2(bounds.X + 10, bounds.Y + bounds.Height - 52), LocaleTerms.Loc("Mission Unavailable") + " : " + LocaleTerms.Loc("User ID Assigned to Different Faction") + " ", GuiData.smallfont, null, bounds.Width - 20, 30f);
			}
			break;
		}
		}
	}
}
