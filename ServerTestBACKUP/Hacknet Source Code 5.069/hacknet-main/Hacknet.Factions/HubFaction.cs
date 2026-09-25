using System;
using System.Collections.Generic;

namespace Hacknet.Factions;

internal class HubFaction : Faction
{
	public HubFaction(string _name, int _neededValue)
		: base(_name, _neededValue)
	{
		PlayerLosesValueOnAbandon = false;
	}

	public override void addValue(int value, object os)
	{
		int oldValue = playerValue;
		base.addValue(value, os);
		if (valuePassedPoint(oldValue, 1) && !((OS)os).Flags.HasFlag("themeChangerAdded"))
		{
			Computer computer = Programs.getComputer((OS)os, "mainHubAssets");
			Folder folder = computer.files.root.searchForFolder("bin");
			Folder folder2 = new Folder("ThemeChanger");
			folder2.files.Add(new FileEntry(PortExploits.crackExeData[14], "ThemeChanger.exe"));
			string dataEntry = Utils.readEntireFile("Content/LocPost/ThemeChangerReadme.txt");
			folder2.files.Add(new FileEntry(dataEntry, "info.txt"));
			folder.folders.Add(folder2);
			((OS)os).delayer.Post(ActionDelayer.Wait(1.0), delegate
			{
				SendAssetAddedNotification(os);
				((OS)os).Flags.AddFlag("themeChangerAdded");
				((OS)os).saveGame();
			});
		}
		Computer computer2;
		if (valuePassedPoint(oldValue, 4))
		{
			((OS)os).delayer.Post(ActionDelayer.Wait(2.0), delegate
			{
				Computer computer6 = Programs.getComputer((OS)os, "mainHub");
				MissionHubServer missionHubServer3 = (MissionHubServer)computer6.getDaemon(typeof(MissionHubServer));
				missionHubServer3.AddMissionToListings("Content/Missions/MainHub/BitSet/Missions/BitHubSet01.xml");
				((OS)os).saveGame();
			});
		}
		else if (playerValue >= 7 && ((OS)os).Flags.HasFlag("decypher") && ((OS)os).Flags.HasFlag("dechead") && !((OS)os).Flags.HasFlag("csecRankingS2Pass"))
		{
			SendNotification(os, "Project Junebug");
			((OS)os).Flags.AddFlag("csecRankingS2Pass");
			((OS)os).saveGame();
		}
		else if (playerValue >= 10 && !((OS)os).Flags.HasFlag("bitPathStarted"))
		{
			computer2 = Programs.getComputer((OS)os, "mainHub");
			MissionHubServer missionHubServer = (MissionHubServer)computer2.getDaemon(typeof(MissionHubServer));
			if (missionHubServer != null && missionHubServer.GetNumberOfAvaliableMissions() > 0)
			{
				return;
			}
			ForceStartBitMissions(os);
		}
		if (playerValue < 2 || !((OS)os).Flags.HasFlag("dlc_complete") || !DLC1SessionUpgrader.HasDLC1Installed || ((OS)os).Flags.HasFlag("dlc_post_missionadded"))
		{
			return;
		}
		bool flag = false;
		string flagStartingWith = ((OS)os).Flags.GetFlagStartingWith("dlc_csec_end_facval");
		if (flagStartingWith != null)
		{
			try
			{
				string value2 = flagStartingWith.Substring(flagStartingWith.IndexOf(":") + 1);
				int num = Convert.ToInt32(value2);
				if (playerValue - num > 1)
				{
					flag = true;
				}
			}
			catch (Exception ex)
			{
				Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
			}
		}
		else
		{
			flag = true;
		}
		if (!flag || !((OS)os).Flags.HasFlag("DLC_PlaneSaveResponseTriggered"))
		{
			return;
		}
		((OS)os).Flags.AddFlag("dlc_post_missionadded");
		computer2 = Programs.getComputer((OS)os, "mainHub");
		MissionHubServer missionHubServer2 = (MissionHubServer)computer2.getDaemon(typeof(MissionHubServer));
		Computer computer3 = Programs.getComputer((OS)os, "dHidden");
		Folder folder3 = computer3.files.root.searchForFolder("home");
		FileEntry item = folder3.searchForFile("PA_0022_Incident.dec");
		Computer computer4 = Programs.getComputer((OS)os, "mainHubAssets");
		computer4.files.root.searchForFolder("home").files.Add(item);
		missionHubServer2.AddMissionToListings("Content/DLC/Missions/Injections/Missions/CSEC_Injection_Mission.xml");
		Computer computer5 = Programs.getComputer((OS)os, "dAttackHome");
		Folder folder4 = computer5.files.root.searchForFolder("home").searchForFolder("uni");
		if (folder4.files.Count == 0)
		{
			MailServer mailServer = ((OS)os).netMap.mailServer.getDaemon(typeof(MailServer)) as MailServer;
			string text = Utils.readEntireFile("Content/DLC/Docs/StrikerLateEmail.txt");
			int num2 = text.IndexOf("\n");
			string subject = text.Substring(0, num2).Trim();
			string body = text.Substring(num2, text.Length - num2).Trim();
			List<string> list = new List<string>();
			if (Settings.ActiveLocale == "en-us")
			{
				list.Add("note#%#Important Extra Information#%#Fuck you");
			}
			mailServer.addMail(MailServer.generateEmail(subject, body, "StrikeR", list), ((OS)os).defaultUser.name);
		}
		((OS)os).saveGame();
	}

	public void ForceStartBitMissions(object os)
	{
		((OS)os).Flags.AddFlag("bitPathStarted");
		((OS)os).delayer.Post(ActionDelayer.Wait(1.6), delegate
		{
			ComputerLoader.loadMission("Content/Missions/BitPath/BitAdv_Intro.xml");
		});
		Computer computer = Programs.getComputer((OS)os, "mainHubAssets");
		Folder folder = computer.files.root.searchForFolder("bin");
		Folder folder2 = new Folder("Misc");
		folder2.files.Add(new FileEntry(PortExploits.crackExeData[9], "Decypher.exe"));
		folder2.files.Add(new FileEntry(PortExploits.crackExeData[10], "DECHead.exe"));
		folder2.files.Add(new FileEntry(PortExploits.crackExeData[104], "KBT_PortTest.exe"));
		folder2.files.Add(new FileEntry("Kellis BioTech medical port cycler - target 104-103.", "kbt_readme.txt"));
		folder.folders.Add(folder2);
		SendNotification(os, LocaleTerms.Loc("Agent") + ",\n" + LocaleTerms.Loc("Additional resources have been added to the CSEC members asset pool, for your free use.") + " " + LocaleTerms.Loc("Find them in the misc folder on the asset server.") + "\n\n" + LocaleTerms.Loc("Thankyou") + ",\n -" + name, name + " " + LocaleTerms.Loc("Admins :: Asset Uploads"));
	}

	private void SendNotification(object osIn, string body, string subject)
	{
		OS oS = (OS)osIn;
		string sender = name + " ReplyBot";
		string mail = MailServer.generateEmail(subject, body, sender);
		MailServer mailServer = (MailServer)oS.netMap.mailServer.getDaemon(typeof(MailServer));
		mailServer.addMail(mail, oS.defaultUser.name);
	}

	private void SendNotification(object osIn, string contractName)
	{
		OS oS = (OS)osIn;
		string subject = name + " " + LocaleTerms.Loc("Admins :: Flagged for Critical Contract");
		string body = string.Format(Utils.readEntireFile("Content/LocPost/CSEC_JunebugEmail.txt"), name);
		string sender = name + " ReplyBot";
		string mail = MailServer.generateEmail(subject, body, sender);
		MailServer mailServer = (MailServer)oS.netMap.mailServer.getDaemon(typeof(MailServer));
		mailServer.addMail(mail, oS.defaultUser.name);
	}

	private void SendAssetAddedNotification(object osIn)
	{
		OS oS = (OS)osIn;
		string subject = name + " " + LocaleTerms.Loc("Admins :: New asset added");
		string body = string.Format(Utils.readEntireFile("Content/LocPost/CSEC_ThemechangerEmail.txt"), name);
		string sender = name + " ReplyBot";
		Computer computer = Programs.getComputer(oS, "mainHubAssets");
		string text = "link#%#" + computer.name + "#%#" + computer.ip;
		string mail = MailServer.generateEmail(subject, body, sender, new List<string>(new string[1] { text }));
		MailServer mailServer = (MailServer)oS.netMap.mailServer.getDaemon(typeof(MailServer));
		mailServer.addMail(mail, oS.defaultUser.name);
	}
}
