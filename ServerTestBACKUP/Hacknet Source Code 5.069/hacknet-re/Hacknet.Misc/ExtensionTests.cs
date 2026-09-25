using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hacknet.Extensions;
using Hacknet.Factions;
using Hacknet.PlatformAPI.Storage;
using Microsoft.Xna.Framework;

namespace Hacknet.Misc;

public static class ExtensionTests
{
	private static bool OS_wasTestingPass = false;

	internal static string RuntimeLoadAdditionalErrors = "";

	public static string TestExtensions(ScreenManager screenMan, out int errorsAdded)
	{
		string text = "";
		int errorsAdded2 = 0;
		text += TestBlankExtension(screenMan, out errorsAdded2);
		object obj = text;
		text = string.Concat(obj, "\r\nComplete - ", errorsAdded2, " errors found");
		errorsAdded = errorsAdded2;
		return text;
	}

	public static string TestExtensionCustomStartSong(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string result = "";
		ExtensionLoader.ActiveExtensionInfo = ExtensionInfo.ReadExtensionInfo("Content/Tests/TestExtension");
		ExtensionInfo activeExtensionInfo = ExtensionLoader.ActiveExtensionInfo;
		if (activeExtensionInfo.IntroStartupSong != null)
		{
			string text = "Content/Music/" + activeExtensionInfo.IntroStartupSong;
			if (File.Exists(text))
			{
				MusicManager.loadAsCurrentSong(text);
			}
		}
		errorsAdded = num;
		return result;
	}

	public static string TestExtensionForRuntime(ScreenManager screenMan, string path, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		OS oS = null;
		RuntimeLoadAdditionalErrors = "";
		try
		{
			oS = (OS)SetupOSForTests(path, screenMan);
			string text2 = TestSuite.TestMission(ExtensionLoader.ActiveExtensionInfo.FolderPath + "/" + ExtensionLoader.ActiveExtensionInfo.StartingMissionPath, oS);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				num++;
				text = text + "\n\nSTART MISSION READ ERRORS:\r\n" + text2;
			}
			int errorsAdded2 = 0;
			text2 = TestAllExtensionMissions(oS, out errorsAdded2);
			text += text2;
			num += errorsAdded2;
			errorsAdded2 = 0;
			string text3 = TestAllExtensionNodesRuntime(oS, out errorsAdded2);
			text += text3;
			num += errorsAdded2;
		}
		catch (Exception ex)
		{
			text = text + "\nLoad Error:\n" + Utils.GenerateReportFromException(ex).Trim();
			num++;
		}
		if (oS != null)
		{
			screenMan.RemoveScreen(oS);
		}
		CompleteExtensiontesting();
		if (!string.IsNullOrWhiteSpace(RuntimeLoadAdditionalErrors))
		{
			num++;
			text = text + "\r\n" + RuntimeLoadAdditionalErrors;
		}
		errorsAdded = num;
		return text;
	}

	private static string TestAllExtensionMissions(OS os, out int errorsAdded)
	{
		int errors = 0;
		string ret = "";
		Utils.ActOnAllFilesRevursivley(ExtensionLoader.ActiveExtensionInfo.FolderPath + "/Missions", delegate(string filename)
		{
			if (filename.EndsWith(".xml") && OS.TestingPassOnly)
			{
				try
				{
					ActiveMission activeMission = (ActiveMission)ComputerLoader.readMission(filename);
					if (activeMission != null)
					{
						RuntimeLoadAdditionalErrors += TestExtensionMission(activeMission, filename, os);
					}
				}
				catch (Exception ex)
				{
					errors++;
					ret = ret + "Error Loading Mission: " + filename;
					ret = ret + "\r\n\r\n" + Utils.GenerateReportFromExceptionCompact(ex) + "\r\n\r\n";
				}
			}
		});
		errorsAdded = errors;
		return ret;
	}

	private static string TestAllExtensionNodesRuntime(OS os, out int errorsAdded)
	{
		int errors = 0;
		string ret = "";
		Utils.ActOnAllFilesRevursivley(ExtensionLoader.ActiveExtensionInfo.FolderPath + "/Nodes", delegate(string filename)
		{
			if (filename.EndsWith(".xml"))
			{
				try
				{
					Computer computer = Computer.loadFromFile(filename);
					if (computer != null)
					{
						ExtensionLoader.CheckAndAssignCoreServer(computer, os);
					}
				}
				catch (Exception ex)
				{
					string format = "COMPUTER LOAD ERROR:\nError loading computer \"{0}\"\nError: {1} - {2}";
					format = string.Format(format, filename, ex.GetType().Name, ex.Message);
					ret = ret + format + "\r\n";
					errors++;
				}
			}
		});
		errorsAdded = errors;
		return ret;
	}

	private static string TestTagisClosed(string tagname, string allData)
	{
		string text = "";
		bool flag = allData.Contains("</" + tagname + ">") || allData.Contains("</" + tagname + " >");
		if (allData.Contains("<" + tagname + ">") && !flag)
		{
			text = text + "Tag " + tagname + " is not properly closed! Check your Slashes.";
			text += "\r\n\r\n";
		}
		return text;
	}

	public static string TestExtensionMission(object mission, string filepath, object os)
	{
		string text = "";
		ActiveMission activeMission = (ActiveMission)mission;
		string text2 = File.ReadAllText(filepath);
		text += TestTagisClosed("missionEnd", text2);
		text += TestTagisClosed("missionStart", text2);
		text += TestTagisClosed("nextMission", text2);
		text += TestTagisClosed("goals", text2);
		text += TestTagisClosed("email", text2);
		text += TestTagisClosed("sender", text2);
		text += TestTagisClosed("subject", text2);
		text += TestTagisClosed("body", text2);
		text += TestTagisClosed("attachments", text2);
		if (!text2.Contains("</attachments>"))
		{
			text += "File does not contain attachments tag at the end of the email! It needs to be there!";
			text += "\r\n";
		}
		int count = Regex.Matches(text2, "<goal ").Count;
		if (activeMission.goals.Count != count && activeMission.goals.Count < count)
		{
			object obj = text;
			text = string.Concat(obj, "File defines some goals that are not being correctly parsed in! (", activeMission.goals.Count, " loaded vs ", count, " in file)");
			text += "\r\nCheck your syntax and tags! Valid Goals:\r\n";
			for (int i = 0; i < activeMission.goals.Count; i++)
			{
				text = text + "\r\n" + activeMission.goals[i].ToString().Replace("Hacknet.Mission.", "");
			}
			text += "\r\n";
		}
		if (string.IsNullOrWhiteSpace(activeMission.startFunctionName) && text2.Contains("<missionStart"))
		{
			text += "File contains missionStart, but it's not being correctly parsed in. It might be out of order in the file.";
			text += "\r\n";
		}
		if (activeMission.startFunctionName != null)
		{
			try
			{
				if (!activeMission.startFunctionName.Contains("addRank"))
				{
					MissionFunctions.runCommand(activeMission.startFunctionValue, activeMission.startFunctionName);
				}
			}
			catch (Exception ex)
			{
				string text3 = text;
				text = text3 + "Error running start function " + activeMission.startFunctionName + "\r\n" + Utils.GenerateReportFromException(ex);
			}
		}
		if (string.IsNullOrWhiteSpace(activeMission.endFunctionName) && text2.Contains("<missionEnd"))
		{
			text += "File contains missionEnd, but it's not being correctly parsed in. It might be out of order in the file.";
			text += "\r\n";
		}
		if (activeMission.endFunctionName != null)
		{
			try
			{
				if (!activeMission.endFunctionName.Contains("addRank"))
				{
					MissionFunctions.runCommand(activeMission.endFunctionValue, activeMission.endFunctionName);
				}
			}
			catch (Exception ex)
			{
				string text3 = text;
				text = text3 + "Error running end function " + activeMission.endFunctionName + "\r\n" + Utils.GenerateReportFromException(ex);
			}
		}
		string text4 = TestSuite.TestMission(ExtensionLoader.ActiveExtensionInfo.FolderPath + "/" + ExtensionLoader.ActiveExtensionInfo.StartingMissionPath, os);
		if (!string.IsNullOrWhiteSpace(text4))
		{
			text += text4;
		}
		if (text.Length > 1)
		{
			return "Mission Errors for " + filepath.Replace("\\", "/") + ":\r\n" + text;
		}
		return "";
	}

	public static string TestExtensionCustomThemeSerialization(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		CustomTheme customTheme = new CustomTheme();
		Color color = (customTheme.defaultTopBarColor = new Color(22, 22, 22, 19));
		string text2 = "customTheme1.xml";
		Utils.writeToFile(customTheme.GetSaveString(), text2);
		CustomTheme customTheme2 = CustomTheme.Deserialize(text2);
		if (customTheme2.defaultTopBarColor != color)
		{
			num++;
			text += "\nSave/Load broken for themes!";
		}
		errorsAdded = num;
		return text;
	}

	public static string TestExtensionCustomThemeFile(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		OS oS = (OS)SetupOSForTests("Content/Tests/TestExtension", screenMan);
		Computer computer = Programs.getComputer(oS, "advExamplePC");
		Folder folder = computer.files.root.searchForFolder("sys");
		FileEntry fileEntry = folder.searchForFile("Custom_x-server.sys");
		if (ThemeManager.getThemeForDataString(fileEntry.data) != OSTheme.Custom || ThemeManager.LastLoadedCustomTheme == null)
		{
			num++;
			text += "Custom theme did not read in from file correctly!";
		}
		oS.threadedSaveExecute();
		screenMan.RemoveScreen(oS);
		oS = null;
		OS.WillLoadSave = true;
		oS = (OS)SetupOSForTests("Content/Tests/TestExtension", screenMan);
		computer = Programs.getComputer(oS, "advExamplePC");
		folder = computer.files.root.searchForFolder("sys");
		fileEntry = folder.searchForFile("Custom_x-server.sys");
		if (ThemeManager.getThemeForDataString(fileEntry.data) != OSTheme.Custom || ThemeManager.LastLoadedCustomTheme == null)
		{
			num++;
			text += "Custom theme did not read in from file correctly after save/load!";
		}
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = num;
		return text;
	}

	public static string TestExtensionCustomFactions(ScreenManager screenMan, out int errorsAdded)
	{
		int errorsAdded2 = 0;
		string text = "";
		OS oS = (OS)SetupOSForTests("Content/Tests/TestExtension", screenMan);
		text += TestExtensionsFactions(oS.allFactions, out errorsAdded2);
		oS.threadedSaveExecute();
		screenMan.RemoveScreen(oS);
		oS = null;
		OS.WillLoadSave = true;
		oS = (OS)SetupOSForTests("Content/Tests/TestExtension", screenMan);
		text += TestExtensionsFactions(oS.allFactions, out errorsAdded2);
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = errorsAdded2;
		return text;
	}

	public static string TestExtensionCustomFactionsActions(ScreenManager screenMan, out int errorsAdded)
	{
		int errorsAdded2 = 0;
		string text = "";
		OS oS = (OS)SetupOSForTests("Content/Tests/TestExtension", screenMan);
		text += TestExtensionsFactions(oS.allFactions, out errorsAdded2);
		Computer computer = Programs.getComputer(oS, "linkNode1");
		Folder folder = computer.files.root.searchForFolder("bin");
		if (folder.searchForFile("FTPBounce.exe") != null)
		{
			errorsAdded2++;
			text += "\nFile somehow already on target system for faction test";
		}
		if (oS.currentFaction != null)
		{
			errorsAdded2++;
			text += "\nFaction does not start as null";
		}
		ComputerLoader.loadMission(Utils.GetFileLoadPrefix() + "Missions/FactionTestMission0.xml");
		if (oS.currentFaction.idName != "autoTestFaction")
		{
			errorsAdded2++;
			text += "\nLoading mission with start function to set player faction does not load faction correctly";
		}
		if (oS.currentFaction.playerValue != 0)
		{
			errorsAdded2++;
			text = text + "\nPlayer faction not expected before mission completion. Expected 0, got " + oS.currentFaction.playerValue;
		}
		oS.currentMission.finish();
		if (oS.currentFaction.playerValue != 5)
		{
			errorsAdded2++;
			text = text + "\nPlayer faction not expected after mission completion. Expected 5, got " + oS.currentFaction.playerValue;
		}
		FileEntry fileEntry = folder.searchForFile("FTPBounce.exe");
		if (fileEntry == null)
		{
			errorsAdded2++;
			text += "\nFile not added correctly in response to faction progression";
		}
		else if (fileEntry.data != PortExploits.crackExeData[21])
		{
			errorsAdded2++;
			text += "\nFile added through faction system data not correctly filtered.";
		}
		oS.threadedSaveExecute();
		screenMan.RemoveScreen(oS);
		oS = null;
		OS.WillLoadSave = true;
		oS = (OS)SetupOSForTests("Content/Tests/TestExtension", screenMan);
		text += TestExtensionsFactions(oS.allFactions, out errorsAdded2);
		if (oS.currentFaction.idName != "autoTestFaction")
		{
			errorsAdded2++;
			text += "\nFaction not set correctly after load";
		}
		CustomFaction customFaction = oS.currentFaction as CustomFaction;
		if (customFaction == null)
		{
			errorsAdded2++;
			text += "\nFaction is not set to the correct type after load";
		}
		if (customFaction.CustomActions.Count != 1)
		{
			errorsAdded2++;
			text += "\nFaction has incorrect number of remaining custom actions after load";
		}
		if (oS.currentFaction.playerValue != 5)
		{
			errorsAdded2++;
			text = text + "\nPlayer faction value not set correctly after load. Expected 5, got " + oS.currentFaction.playerValue;
		}
		oS.currentMission.finish();
		computer = Programs.getComputer(oS, "linkNode1");
		folder = computer.files.root.searchForFolder("bin");
		fileEntry = folder.searchForFile("SecondTestFile.txt");
		if (fileEntry == null)
		{
			errorsAdded2++;
			text += "\nSecond File not added correctly in response to faction progression";
		}
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = errorsAdded2;
		return text;
	}

	private static string TestExtensionsFactions(AllFactions allFactions, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		if (allFactions.factions.Count != 2)
		{
			num++;
			text = text + "\nExpected 2 factions, got " + allFactions.factions.Count;
		}
		CustomFaction customFaction = allFactions.factions["tfac"] as CustomFaction;
		if (customFaction == null)
		{
			num++;
			text = text + "\nFaction 1 not listed as custom faction! Reports self as " + customFaction;
		}
		if (customFaction.name != "Test Faction")
		{
			num++;
			text += "\nFaction name incorrect!";
		}
		if (customFaction.playerValue != 0)
		{
			num++;
			text += "\nFaction starting player value incorrect!";
		}
		if (customFaction.CustomActions.Count != 5)
		{
			num++;
			text = text + "\nIncorrect number of custom faction actions! Expected 5, got " + customFaction.CustomActions.Count;
		}
		errorsAdded = num;
		return text;
	}

	public static string TestExtensionMissionLoading(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		OS oS = (OS)SetupOSForTests("Content/Tests/TestExtension", screenMan);
		string text2 = TestSuite.TestMission(ExtensionLoader.ActiveExtensionInfo.FolderPath + "/" + ExtensionLoader.ActiveExtensionInfo.StartingMissionPath, oS);
		if (!string.IsNullOrWhiteSpace(text2))
		{
			num++;
			text = text + "\n\nMISSION READ ERRORS:\n" + text2;
		}
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = num;
		return text;
	}

	public static string TestExtensionStartNodeVisibility(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		OS oS = (OS)SetupOSForTests("Content/Tests/IntroExtension", screenMan);
		for (int i = 0; i < oS.netMap.nodes.Count; i++)
		{
			string idName = oS.netMap.nodes[i].idName;
			if (oS.netMap.visibleNodes.Contains(i) && !(idName == "playerComp") && !ExtensionLoader.ActiveExtensionInfo.StartingVisibleNodes.Contains(idName) && !ExtensionLoader.ActiveExtensionInfo.StartingVisibleNodes.Contains(oS.netMap.nodes[i].ip))
			{
				num++;
				text = text + "\nNode is discovered but it should not be! Node: " + idName;
			}
		}
		for (int i = 0; i < ExtensionLoader.ActiveExtensionInfo.StartingVisibleNodes.Length; i++)
		{
			string idName = ExtensionLoader.ActiveExtensionInfo.StartingVisibleNodes[i];
			Computer computer = Programs.getComputer(oS, idName);
			if (computer != null && !oS.netMap.visibleNodes.Contains(oS.netMap.nodes.IndexOf(computer)))
			{
				num++;
				text = text + "\nNode " + idName + " should be discovered, but it is not! It's on the starting visible list";
			}
		}
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = num;
		return text;
	}

	public static string TestPopulatedExtension(ScreenManager screenMan, out int errorsAdded)
	{
		int errorsAdded2 = 0;
		string text = "";
		OS oS = (OS)SetupOSForTests("Content/Tests/IntroExtension", screenMan);
		List<Computer> nodes = oS.netMap.nodes;
		text += TestPopulatedExtensionRead(oS, out errorsAdded2);
		oS.threadedSaveExecute();
		screenMan.RemoveScreen(oS);
		oS = null;
		OS.WillLoadSave = true;
		oS = (OS)SetupOSForTests("Content/Tests/IntroExtension", screenMan);
		text += TestPopulatedExtensionRead(oS, out errorsAdded2);
		text += TestSuite.getTestingReportForLoadComparison(oS, nodes, errorsAdded2, out errorsAdded2);
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = errorsAdded2;
		return text;
	}

	private static string TestPopulatedExtensionRead(object os_obj, out int errorsAdded)
	{
		string text = "";
		errorsAdded = 0;
		OS oS = (OS)os_obj;
		Computer computer = Programs.getComputer(oS, "linkNode1");
		Computer computer2 = Programs.getComputer(oS, "linkNode2");
		Computer computer3 = Programs.getComputer(oS, "linkNode3");
		if (!computer.links.Contains(oS.netMap.nodes.IndexOf(computer2)) || !computer2.links.Contains(oS.netMap.nodes.IndexOf(computer3)) || !computer3.links.Contains(oS.netMap.nodes.IndexOf(computer)))
		{
			errorsAdded++;
			text += "\nLinks Error! Link node 1,2 or 3 does not have specified links\n";
		}
		Computer computer4 = Programs.getComputer(oS, "secTestNode");
		if (computer4.adminPass != "sectestpass")
		{
			text += "\nAdmin Password for SecNode does not match!";
			errorsAdded++;
		}
		if (computer4.portsNeededForCrack != 1)
		{
			text = text + "\nSecNode ports needed for crack mismatch. Expected 2, got " + computer4.portsNeededForCrack;
			errorsAdded++;
		}
		if (!Utils.FloatEquals(computer4.traceTime, 300f))
		{
			text = text + "\nSecNode trace time mismatch. Expected 300, got " + computer4.traceTime;
			errorsAdded++;
		}
		if (!Utils.FloatEquals(computer4.proxyOverloadTicks, 2f * Computer.BASE_PROXY_TICKS))
		{
			object obj = text;
			text = string.Concat(obj, "\nSecNode proxy time mismatch. Expected ", 2f * Computer.BASE_PROXY_TICKS, " Got ", computer4.proxyOverloadTicks);
			errorsAdded++;
		}
		if (computer4.admin.ResetsPassword || computer4.admin.IsSuper || !(computer4.admin is FastBasicAdministrator))
		{
			text = text + "\nSecNode administrator error! Expected non resetting Fast admin, got " + computer4.admin.ToString();
			errorsAdded++;
		}
		Firewall firewall = new Firewall(6, "Scypio", 1f);
		if (firewall.getSaveString() != computer4.firewall.getSaveString())
		{
			string text2 = text;
			text = text2 + "\nFireall difference! Expected \n" + firewall.getSaveString() + "\nbut got\n" + computer4.firewall.getSaveString();
			errorsAdded++;
		}
		if (!computer4.ports.Contains(21) || !computer4.ports.Contains(22) || !computer4.ports.Contains(80) || !computer4.ports.Contains(25) || !computer4.ports.Contains(1433))
		{
			errorsAdded++;
			text += "\nSecNodem ports assignment error\n";
		}
		bool flag = false;
		bool flag2 = false;
		for (int i = 0; i < computer4.users.Count; i++)
		{
			if (computer4.users[i].name == "SecTest")
			{
				flag = true;
				if (computer4.users[i].pass != "userpas")
				{
					errorsAdded++;
					text = text + "\nUser account password for user SecTest do not match. Found Pass :" + computer4.users[i].pass + "\n";
				}
			}
			if (computer4.users[i].name == "mailGuy")
			{
				flag2 = true;
				if (computer4.users[i].pass != "mailPass")
				{
					errorsAdded++;
					text = text + "\nUser account password for user MailGuy do not match. Found Pass :" + computer4.users[i].pass + "\n";
				}
			}
		}
		if (!flag2 || !flag)
		{
			errorsAdded++;
			object obj = text;
			text = string.Concat(obj, "\nAccounts missing from SecTest node - found ", computer4.users.Count, " users.");
		}
		return text;
	}

	public static string TestBlankExtension(ScreenManager screenMan, out int errorsAdded)
	{
		int errors = 0;
		string ret = "";
		OS oS;
		try
		{
			oS = (OS)SetupOSForTests("Content/Tests/TestBlankExtension", screenMan);
		}
		catch (Exception ex)
		{
			errorsAdded = 1;
			return "\nError generating blank extension:\n" + Utils.GenerateReportFromException(ex);
		}
		TestOSForBlankSession(oS, out ret, out errors);
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = errors;
		return ret;
	}

	public static string TestAcademicDatabase(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		OS oS = (OS)SetupOSForTests("Content/Tests/IntroExtension", screenMan);
		if (oS.netMap.academicDatabase == null)
		{
			num++;
			text += "\nNo Academic Database Detected where there should be one!";
		}
		oS.threadedSaveExecute();
		screenMan.RemoveScreen(oS);
		oS = null;
		OS.WillLoadSave = true;
		oS = (OS)SetupOSForTests("Content/Tests/IntroExtension", screenMan);
		if (oS.netMap.academicDatabase == null)
		{
			num++;
			text += "\nNo Academic Database Detected after save/load!";
		}
		screenMan.RemoveScreen(oS);
		oS = null;
		OS.WillLoadSave = false;
		oS = (OS)SetupOSForTests("Content/Tests/TestBlankExtension", screenMan);
		if (oS.netMap.academicDatabase != null)
		{
			num++;
			text += "\nAcademic database was created in empty session!!";
		}
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = num;
		return text;
	}

	private static void TestOSForBlankSession(object osobj, out string ret, out int errors)
	{
		OS oS = (OS)osobj;
		ret = "";
		errors = 0;
		if (oS.netMap.nodes.Count != 2)
		{
			object obj = ret;
			ret = string.Concat(obj, "\nError: Expected exactly 2 nodes but found ", oS.netMap.nodes.Count, "\n");
			errors++;
		}
		bool flag = false;
		bool flag2 = false;
		for (int i = 0; i < oS.netMap.nodes.Count; i++)
		{
			if (oS.netMap.nodes[i].idName == "playerComp")
			{
				flag = true;
			}
			if (!(oS.netMap.nodes[i].idName == "jmail"))
			{
				continue;
			}
			if (oS.netMap.nodes[i].getDaemon(typeof(MailServer)) != null)
			{
				flag2 = true;
				if (oS.netMap.mailServer != oS.netMap.nodes[i])
				{
					ret += "\nMail server is not correctly registered For NetMap.MailServer!\n";
					errors++;
				}
				bool flag3 = false;
				for (int j = 0; j < oS.netMap.nodes[i].users.Count; j++)
				{
					if (oS.netMap.nodes[i].users[i].name == oS.defaultUser.name)
					{
						flag3 = true;
					}
				}
				if (!flag3)
				{
					object obj = ret;
					ret = string.Concat(obj, "M\nail server does not contain a User for the player! Has ", oS.netMap.nodes[i].users.Count, " users.");
					errors++;
				}
			}
			else
			{
				ret += "\nMail Server ID found but it contains no MailServer Daemon!";
				errors++;
			}
		}
		if (!flag2 || !flag)
		{
			object obj = ret;
			ret = string.Concat(obj, "\nBlank server should have a player node and a JMail server with an email daemon. Has Player Server = ", flag, ", Has Mail Server = ", flag2, "\n");
			errors++;
		}
		if (oS.allFactions.factions.Count != 0)
		{
			errors++;
			object obj = ret;
			ret = string.Concat(obj, "\nBlank session has factions! : ", oS.allFactions.factions.Count, " found.");
		}
	}

	public static string TestBlankExtensionSaveLoad(ScreenManager screenMan, out int errorsAdded)
	{
		string ret = "";
		int errors = 0;
		OS oS = (OS)SetupOSForTests("Content/Tests/TestBlankExtension", screenMan);
		List<Computer> nodes = oS.netMap.nodes;
		oS.threadedSaveExecute();
		screenMan.RemoveScreen(oS);
		oS = null;
		OS.WillLoadSave = true;
		oS = (OS)SetupOSForTests("Content/Tests/TestBlankExtension", screenMan);
		TestOSForBlankSession(oS, out ret, out errors);
		screenMan.RemoveScreen(oS);
		CompleteExtensiontesting();
		errorsAdded = errors;
		return ret;
	}

	private static object SetupOSForTests(string ActiveExtensionFoldername, ScreenManager screenMan)
	{
		string text = "__ExtensionTest";
		string saveFileNameForUsername = SaveFileManager.GetSaveFileNameForUsername(text);
		SaveFileManager.AddUser(text, "testpass");
		OS_wasTestingPass = OS.TestingPassOnly;
		OS.TestingPassOnly = true;
		Settings.IsInExtensionMode = true;
		ExtensionLoader.ActiveExtensionInfo = ExtensionInfo.ReadExtensionInfo(ActiveExtensionFoldername);
		OS oS = new OS();
		oS.SaveUserAccountName = text;
		oS.SaveGameUserName = saveFileNameForUsername;
		screenMan.AddScreen(oS);
		return oS;
	}

	private static void CompleteExtensiontesting()
	{
		OS.TestingPassOnly = OS_wasTestingPass;
		Settings.IsInExtensionMode = false;
		ExtensionLoader.ActiveExtensionInfo = null;
	}
}
