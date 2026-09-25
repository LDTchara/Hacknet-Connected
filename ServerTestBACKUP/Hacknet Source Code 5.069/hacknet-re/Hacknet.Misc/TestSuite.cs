using System;
using System.Collections.Generic;
using System.IO;
using Hacknet.Extensions;
using Hacknet.Localization;
using Hacknet.PlatformAPI.Storage;

namespace Hacknet.Misc;

public class TestSuite
{
	private static string ActiveObjectID = "";

	private static List<string> TestedMissionNames = new List<string>();

	public static string RunTestSuite(ScreenManager screenMan, bool IsQuickTestMode = false)
	{
		bool soundDisabled = Settings.soundDisabled;
		Settings.soundDisabled = true;
		string activeLocale = Settings.ActiveLocale;
		LocaleActivator.ActivateLocale("en-us", Game1.getSingleton().Content);
		Settings.ActiveLocale = "en-us";
		string result = TestSaveLoadOnFile(screenMan, IsQuickTestMode);
		LocaleActivator.ActivateLocale(activeLocale, Game1.getSingleton().Content);
		Settings.ActiveLocale = activeLocale;
		Settings.soundDisabled = soundDisabled;
		return result;
	}

	public static string TestSaveLoadOnFile(ScreenManager screenMan, bool IsQuicktestMode = false)
	{
		string text = "__hacknettestaccount";
		string pass = "__testingpassword";
		SaveFileManager.AddUser(text, pass);
		string saveFileNameForUsername = SaveFileManager.GetSaveFileNameForUsername(text);
		OS.TestingPassOnly = true;
		string text2 = "";
		OS oS = new OS();
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text;
		screenMan.AddScreen(oS, screenMan.controllingPlayer);
		oS.delayer.RunAllDelayedActions();
		oS.threadedSaveExecute();
		List<Computer> nodes = oS.netMap.nodes;
		screenMan.RemoveScreen(oS);
		OS.WillLoadSave = true;
		oS = new OS();
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text;
		screenMan.AddScreen(oS, screenMan.controllingPlayer);
		oS.delayer.RunAllDelayedActions();
		Game1.getSingleton().IsMouseVisible = true;
		string text3 = "Serialization and Integrity Test Report:\r\n";
		Console.WriteLine(text3);
		text2 += text3;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		int errorCount = 0;
		text2 += getTestingReportForLoadComparison(oS, nodes, errorCount, out errorCount);
		text2 = text2 + "\r\n" + TestMissions(oS);
		int errorsOut = 0;
		text2 += TestGameProgression(oS, out errorsOut);
		errorCount += errorsOut;
		for (int i = 0; i < oS.netMap.nodes.Count; i++)
		{
			Folder root = oS.netMap.nodes[i].files.root;
			DeleteAllFilesRecursivley(root);
		}
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text;
		oS.threadedSaveExecute();
		nodes = oS.netMap.nodes;
		screenMan.RemoveScreen(oS);
		OS.WillLoadSave = true;
		oS = new OS();
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text;
		screenMan.AddScreen(oS, screenMan.controllingPlayer);
		int num = errorCount;
		screenMan.RemoveScreen(oS);
		OS.TestingPassOnly = false;
		SaveFileManager.DeleteUser(text);
		int errorsAdded = 0;
		if (!IsQuicktestMode)
		{
			text2 = text2 + "\r\nLocalization Tests: " + LocalizationTests.TestLocalizations(screenMan, out errorsAdded);
			text2 = text2 + "\r\nDLC Localization Tests: " + DLCLocalizationTests.TestDLCLocalizations(screenMan, out errorsAdded);
		}
		text2 = text2 + "\r\nDLC Tests: " + DLCTests.TestDLCFunctionality(screenMan, out errorsAdded);
		text2 = text2 + "\r\nDLC Extended Tests: " + DLCExtendedTests.TesExtendedFunctionality(screenMan, out errorsAdded);
		text2 = text2 + "\r\nEDU Edition Tests: " + EduEditionTests.TestEDUFunctionality(screenMan, out errorsAdded);
		text2 = text2 + "\r\nMisc Tests: " + TestMiscAndCLRFeatures(screenMan, out errorsAdded);
		string text4 = "\r\nCore Tests: Complete - " + errorCount + " errors found.\r\nTested " + nodes.Count + " generated nodes vs " + oS.netMap.nodes.Count + " loaded nodes";
		text2 += text4;
		Console.WriteLine(text4);
		MusicManager.stop();
		try
		{
			string text5 = "testreport.txt";
			File.Delete(text5);
			Utils.writeToFile(text2, text5);
		}
		catch (Exception)
		{
		}
		return Utils.CleanStringToRenderable(text2);
	}

	public static string TestMiscAndCLRFeatures(ScreenManager screenMan, out int errorsAdded)
	{
		string text = "";
		int num = 0;
		if ((ushort)"".GetHashCode() != 5886)
		{
			num++;
			text += "\nBlank string Hashes to an unexpected value! This will break Header file decryption!\n";
		}
		if ((ushort)"12345asdf".GetHashCode() != 25213)
		{
			num++;
			text += "\nTest string Hashes to an unexpected value! This can break file decryption!\n";
		}
		object obj = text;
		text = string.Concat(obj, "Complete - ", num, " errors found");
		errorsAdded = num;
		return text;
	}

	private static void DeleteAllFilesRecursivley(Folder f)
	{
		f.files.Clear();
		for (int i = 0; i < f.folders.Count; i++)
		{
			DeleteAllFilesRecursivley(f.folders[i]);
		}
	}

	internal static string getTestingReportForLoadComparison(object osobj, List<Computer> oldComps, int currentErrorCount, out int errorCount)
	{
		OS oS = (OS)osobj;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string text = "";
		errorCount = currentErrorCount;
		bool flag = false;
		for (int i = 0; i < oS.netMap.nodes.Count; i++)
		{
			for (int j = 0; j < oldComps.Count; j++)
			{
				if (i != j && oS.netMap.nodes[i].idName == oldComps[j].idName)
				{
					errorCount++;
					object obj = text;
					text = string.Concat(obj, "\nDuplicate Node ID found! ", i, ": ", oS.netMap.nodes[i].name, " and ", j, ": ", oldComps[j].name);
					flag = true;
				}
			}
		}
		if (flag)
		{
			return text + "\nCritical Error encountered - exiting tests early";
		}
		for (int i = 0; i < oldComps.Count; i++)
		{
			Computer computer = oldComps[i];
			Computer computer2 = Programs.getComputer(oS, computer.ip);
			string text2 = null;
			Assert(computer.name, computer2.name);
			text2 = computer2.files.TestEquals(computer.files);
			if (list.Contains(computer2.idName))
			{
				int num = list.IndexOf(computer2.idName);
				object obj = text2;
				text2 = string.Concat(obj, "Duplicate ID Found - \"", computer2.idName, "\" from ", list2[num], "@", num, " current: ", computer2.name, "@", i);
			}
			list.Add(computer2.idName);
			list2.Add(computer2.name);
			if (!Utils.FloatEquals(computer.startingOverloadTicks, computer2.startingOverloadTicks))
			{
				object obj = text2;
				text2 = string.Concat(obj, "Proxy timer difference - \"", computer2.name, "\" from Base:", computer.startingOverloadTicks, " to Current: ", computer2.startingOverloadTicks);
			}
			if ((computer.firewall != null || computer2.firewall != null) && !computer.firewall.Equals(computer2.firewall))
			{
				string text3 = text2;
				text2 = text3 + "Firewall difference - \"" + computer2.name + "\" from Base:" + computer.firewall.ToString() + "\r\n to Current: " + computer2.firewall.ToString() + "\r\n";
			}
			if (computer.icon != computer2.icon)
			{
				string text3 = text2;
				text2 = text3 + "Icon difference - \"" + computer2.name + "\" from Base:" + computer.icon + " to Current: " + computer2.icon;
			}
			if (computer.portsNeededForCrack != computer2.portsNeededForCrack)
			{
				object obj = text2;
				text2 = string.Concat(obj, "Port for crack difference - \"", computer2.name, "\" from Base:", computer.portsNeededForCrack, " to Current: ", computer2.portsNeededForCrack);
			}
			for (int k = 0; k < computer.links.Count; k++)
			{
				if (computer.links[k] != computer2.links[k])
				{
					object obj = text2;
					text2 = string.Concat(obj, "Link difference - \"", computer2.name, "\" @", k, " ", computer.links[k], " vs ", computer2.links[k]);
				}
			}
			if (!Utils.FloatEquals(computer.location.X, computer2.location.X) || !Utils.FloatEquals(computer.location.Y, computer2.location.Y))
			{
				object obj = text2;
				text2 = string.Concat(obj, "Location difference - \"", computer2.name, "\" from Base:", computer.location, " to Current: ", computer2.location);
			}
			if (!Utils.FloatEquals(computer.traceTime, computer2.traceTime))
			{
				object obj = text2;
				text2 = string.Concat(obj, "Trace timer difference - \"", computer2.name, "\" from Base:", computer.traceTime, " to Current: ", computer2.traceTime);
			}
			if (computer.adminPass != computer2.adminPass)
			{
				string text3 = text2;
				text2 = text3 + "Password Difference: expected \"" + computer.adminPass + "\" but got \"" + computer2.adminPass + "\"\r\n";
			}
			if (computer.adminIP != computer2.adminIP)
			{
				string text3 = text2;
				text2 = text3 + "Admin IP Difference: expected \"" + computer.adminIP + "\" but got \"" + computer2.adminIP + "\"\r\n";
			}
			for (int k = 0; k < computer.users.Count; k++)
			{
				if (!computer.users[k].Equals(computer2.users[k]))
				{
					object obj = text2;
					text2 = string.Concat(obj, "User difference - \"", computer2.name, "\" @", k, " ", computer.users[k], " vs ", computer2.users[k]);
				}
			}
			for (int l = 0; l < computer.daemons.Count; l++)
			{
				if (computer.daemons[l].getSaveString() != computer2.daemons[l].getSaveString())
				{
					string text3 = text2;
					text2 = text3 + "Daemon Difference: expected \r\n-----\r\n" + computer.daemons[l].getSaveString() + "\r\n----- but got -----\r\n" + computer2.daemons[l].getSaveString() + "\r\n-----\r\n";
				}
			}
			if (computer.hasProxy != computer2.hasProxy)
			{
				object obj = text2;
				text2 = string.Concat(obj, "Proxy Difference: OldProxy:", computer.hasProxy, " vs NewProxy:", computer2.hasProxy);
			}
			if (computer.proxyActive != computer2.proxyActive)
			{
				object obj = text2;
				text2 = string.Concat(obj, "Proxy Active Difference: OldProxy:", computer.proxyActive, " vs NewProxy:", computer2.proxyActive);
			}
			if (computer.portsNeededForCrack != computer2.portsNeededForCrack)
			{
				object obj = text2;
				text2 = string.Concat(obj, "Ports for crack Difference: Old:", computer.portsNeededForCrack, " vs New:", computer2.portsNeededForCrack);
			}
			if (computer.admin != null && computer2.admin != null && computer.admin.GetType() != computer2.admin.GetType())
			{
				object obj = text2;
				text2 = string.Concat(obj, "SecAdmin Difference: Old:", computer.admin, " vs New:", computer2.admin);
			}
			if (computer.admin != null && computer.admin.IsSuper != computer2.admin.IsSuper)
			{
				object obj = text2;
				text2 = string.Concat(obj, "SecAdmin Super Difference: Old:", computer.admin.IsSuper, " vs New:", computer2.admin.IsSuper);
			}
			if (computer.admin != null && computer.admin.ResetsPassword != computer2.admin.ResetsPassword)
			{
				object obj = text2;
				text2 = string.Concat(obj, "SecAdmin PassReset Difference: Old:", computer.admin.ResetsPassword, " vs New:", computer2.admin.ResetsPassword);
			}
			if (text2 != null)
			{
				string text4 = "\r\nErrors in " + computer.idName + " - \"" + computer.name + "\"\r\n" + text2 + "\r\n";
				text = text + text4 + "\r\n";
				Console.WriteLine(text4);
				errorCount++;
			}
			else
			{
				Console.Write(".");
				text += ".";
			}
		}
		return text;
	}

	public static string TestMissions(object os_obj)
	{
		OS os_obj2 = (OS)os_obj;
		string text = "";
		string retAdditions = "";
		MissionFunctions.ReportErrorInCommand = (Action<string>)Delegate.Combine(MissionFunctions.ReportErrorInCommand, (Action<string>)delegate(string report)
		{
			string text4 = retAdditions;
			retAdditions = text4 + ActiveObjectID + " : " + report + "\r\n";
		});
		TestedMissionNames.Clear();
		try
		{
			string text2 = TestMission("Content/Missions/BitMissionIntro.xml", os_obj2);
			text += text2;
			text2 = TestMission("Content/Missions/BitMission0.xml", os_obj2);
			text += text2;
			string text3 = "Content/Missions/Entropy/StartingSet/";
			DirectoryInfo directoryInfo = new DirectoryInfo(text3);
			FileInfo[] files = directoryInfo.GetFiles("*.xml");
			for (int i = 0; i < files.Length; i++)
			{
				string missionName = text3 + files[i].Name;
				text2 = TestMission(missionName, os_obj2);
				text += text2;
			}
			text2 = TestMission("Content/Missions/Entropy/ThemeHackTransitionMission.xml", os_obj2);
			text += text2;
			text2 = TestMission("Content/Missions/MainHub/Intro/Intro01.xml", os_obj2);
			text += text2;
			text3 = "Content/Missions/MainHub/FirstSet/";
			directoryInfo = new DirectoryInfo(text3);
			files = directoryInfo.GetFiles("*.xml");
			for (int i = 0; i < files.Length; i++)
			{
				string missionName = text3 + files[i].Name;
				text2 = TestMission(missionName, os_obj2);
				text += text2;
			}
			text2 = TestMission("Content/Missions/lelzSec/IntroTestMission.xml", os_obj2);
			text += text2;
			text2 = TestMission("Content/Missions/MainHub/BitSet/Missions/BitHubSet01.xml", os_obj2);
			text += text2;
			text2 = TestMission("Content/Missions/BitPath/BitAdv_Intro.xml", os_obj2);
			text += text2;
			text2 = TestMissionEndFucntion("Content/Missions/MainHub/FirstSet/01HubSet02.xml", "changeSong", os_obj2);
			text += text2;
			text2 = TestMissionEndFucntion("Content/Missions/MainHub/FirstSet/02HubSet05.xml", "addFlags:decypher", os_obj2);
			text += text2;
			text2 = TestMissionEndFucntion("Content/Missions/MainHub/DecypherSet/DECHeadMission02.xml", "addFlags:dechead", os_obj2);
			text += text2;
			MissionFunctions.ReportErrorInCommand = null;
		}
		finally
		{
			MissionFunctions.ReportErrorInCommand = null;
		}
		return text + retAdditions;
	}

	public static string TestMission(string missionName, object os_obj)
	{
		string text = "";
		OS oS = (OS)os_obj;
		if (TestedMissionNames.Contains(missionName))
		{
			return text;
		}
		try
		{
			if (!File.Exists(missionName))
			{
				text = text + "Invalid Mission Path! : " + missionName + "\r\n";
			}
			ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(missionName);
			ActiveObjectID = missionName;
			string text2 = "";
			for (int i = 0; i < activeMission.goals.Count; i++)
			{
				string text3 = activeMission.goals[i].TestCompletable();
				if (text3 != null && text3.Length > 0)
				{
					object obj = text2;
					text2 = string.Concat(obj, missionName, " Goal[", i, "] ", activeMission.goals[i].ToString(), " :: ", text3, "\r\n");
				}
			}
			try
			{
				if (!string.IsNullOrWhiteSpace(activeMission.startFunctionName))
				{
					if (!Utils.CheckStringIsRenderable(activeMission.startFunctionName))
					{
						string text4 = text;
						text = text4 + "Mission " + missionName + " has unrenderable start function " + Utils.CleanStringToRenderable(activeMission.startFunctionName);
					}
					MissionFunctions.runCommand(activeMission.startFunctionValue, activeMission.startFunctionName);
				}
				if (!string.IsNullOrWhiteSpace(activeMission.endFunctionName))
				{
					if (!Utils.CheckStringIsRenderable(activeMission.endFunctionName))
					{
						string text4 = text;
						text = text4 + "Mission " + missionName + " has unrenderable end function " + Utils.CleanStringToRenderable(activeMission.endFunctionName);
					}
					MissionFunctions.runCommand(activeMission.endFunctionValue, activeMission.endFunctionName);
				}
				string text5 = Directory.GetCurrentDirectory() + "/";
				string text6 = Utils.GetFileLoadPrefix();
				if (text6 == "Content/")
				{
					text6 += "Missions/";
				}
				else if (!text6.StartsWith("Extensions"))
				{
					text5 = "";
				}
				string path = text5 + LocalizedFileLoader.GetLocalizedFilepath(text6 + activeMission.nextMission);
				if (!(activeMission.nextMission == "NONE") && !File.Exists(path))
				{
					string text4 = text;
					text = text4 + "\r\nNextMission Tag for mission \"" + missionName + "\" has nonexistent next mission path: " + activeMission.nextMission + "\r\n";
				}
			}
			catch (Exception ex)
			{
				string text4 = text;
				text = text4 + "Error running start or end mission function of mission: " + missionName + "\r\nStart Func: " + activeMission.startFunctionName + "\r\nEnd Func: " + activeMission.endFunctionName;
				text = text + "\r\n" + Utils.GenerateReportFromException(ex) + "\r\n";
			}
			if (text2.Length > 0)
			{
				text = text + text2 + "--------------\r\n";
			}
			TestedMissionNames.Add(missionName);
			string text7 = "Content/Missions/";
			if (Settings.IsInExtensionMode)
			{
				text7 = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
			}
			List<ActiveMission> list = new List<ActiveMission>();
			for (int i = 0; i < oS.branchMissions.Count; i++)
			{
				list.Add(oS.branchMissions[i]);
			}
			if (activeMission.nextMission != null && activeMission.nextMission.ToLower() != "none")
			{
				text += TestMission(text7 + activeMission.nextMission, oS);
			}
			for (int i = 0; i < list.Count; i++)
			{
				string localizedFilepath = LocalizedFileLoader.GetLocalizedFilepath(list[i].reloadGoalsSourceFile);
				if (!TestedMissionNames.Contains(localizedFilepath))
				{
					Console.WriteLine("testing Branch Mission " + localizedFilepath);
					text += TestMission(localizedFilepath, oS);
				}
			}
		}
		catch (Exception ex)
		{
			string text4 = text;
			text = text4 + "Error Loading " + missionName + "\r\n" + ex.ToString();
		}
		return text;
	}

	public static string TestMissionEndFucntion(string missionName, string expectedEndFunction, object os_obj)
	{
		string text = "";
		OS oS = (OS)os_obj;
		if (!File.Exists(missionName))
		{
			text = text + "Invalid Mission Path! : " + missionName + "\r\n";
		}
		ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(missionName);
		if (activeMission.endFunctionName != expectedEndFunction)
		{
			string text2 = text;
			text = text2 + "\r\nUnexpected end function in " + missionName + "\r\nExpected: " + expectedEndFunction + " -- found: " + activeMission.endFunctionName;
		}
		return text;
	}

	public static string TestGameProgression(object os_obj, out int errorsOut)
	{
		string text = "";
		int num = 0;
		OS oS = (OS)os_obj;
		oS.delayer.RunAllDelayedActions();
		string filename = "Content/Missions/BitMissionIntro.xml";
		ComputerLoader.loadMission(filename);
		Folder folder = oS.thisComputer.files.root.searchForFolder("bin");
		folder.files.Clear();
		if (!oS.currentMission.isComplete())
		{
			num++;
			text += "\r\nCouldn't finish first mission...\r\n";
		}
		oS.currentMission.finish();
		Computer computer = Programs.getComputer(oS, "portcrack01");
		FileEntry fileEntry = computer.files.root.searchForFolder("bin").files[0];
		folder.files.Add(new FileEntry(fileEntry.data, "ssh.exe"));
		if (!oS.currentMission.isComplete())
		{
			num++;
			text += "\r\nCouldn't finish sshcrack mission...\r\n";
		}
		oS.currentMission.finish();
		if (oS.currentMission.isComplete())
		{
			num++;
			text += "\r\nMission 3 is completable early!\r\n";
		}
		computer = Programs.getComputer(oS, "bitMission00");
		computer.giveAdmin(oS.thisComputer.ip);
		if (!oS.currentMission.isComplete())
		{
			num++;
			text += "\r\nCouldn't finish BitMission0\r\n";
		}
		oS.currentMission.finish();
		errorsOut = num;
		return text;
	}

	private static void Assert(string first, string second)
	{
		if (first != second)
		{
			throw new InvalidProgramException();
		}
	}

	public static void DoWordCount()
	{
		string[] folders = new string[4] { "Content/DLC/Missions", "Content/DLC/Docs", "Content/DLC/Misc", "Content/DLC/ActionScripts" };
		string[] fileOnlyFolders = new string[1] { "Content/DLC" };
		WordCounter.PerformWordCount(folders, fileOnlyFolders);
	}
}
