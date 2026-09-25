using System;
using Hacknet.Extensions;
using Hacknet.Localization;
using Microsoft.Xna.Framework;

namespace Hacknet;

public static class ProgramRunner
{
	public static bool ExecuteProgram(object os_object, string[] arguments)
	{
		OS os = (OS)os_object;
		string[] array = arguments;
		bool flag = true;
		if (array[0].ToLower().Equals("connect"))
		{
			Programs.connect(array, os);
			flag = false;
		}
		else if (array[0].Equals("disconnect") || array[0].Equals("dc"))
		{
			Programs.disconnect(array, os);
		}
		else if (array[0].Equals("ls") || array[0].Equals("dir"))
		{
			Programs.ls(array, os);
		}
		else if (array[0].Equals("cd"))
		{
			Programs.cd(array, os);
		}
		else if (array[0].Equals("cd.."))
		{
			array = new string[2] { "cd", ".." };
			Programs.cd(array, os);
		}
		else if (array[0].Equals("cat") || array[0].Equals("more") || array[0].Equals("less"))
		{
			Programs.cat(array, os);
		}
		else if (array[0].Equals("exe"))
		{
			Programs.execute(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("probe") || array[0].Equals("nmap"))
		{
			Programs.probe(array, os);
		}
		else if (array[0].Equals("scp"))
		{
			Programs.scp(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("scan"))
		{
			Programs.scan(array, os);
			flag = false;
		}
		else if (array[0].Equals("rm") || array[0].Equals("del"))
		{
			Programs.rm(array, os);
			flag = false;
		}
		else if (array[0].Equals("mv"))
		{
			Programs.mv(array, os);
			flag = false;
		}
		else if (array[0].Equals("ps"))
		{
			Programs.ps(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("kill") || array[0].Equals("pkill"))
		{
			Programs.kill(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("reboot"))
		{
			Programs.reboot(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("opencdtray"))
		{
			Programs.opCDTray(array, os, isOpen: true);
			flag = false;
		}
		else if (array[0].ToLower().Equals("closecdtray"))
		{
			Programs.opCDTray(array, os, isOpen: false);
			flag = false;
		}
		else if (array[0].Equals("replace"))
		{
			Programs.replace2(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("analyze"))
		{
			Programs.analyze(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("solve"))
		{
			Programs.solve(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("clear"))
		{
			Programs.clear(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("upload") || array[0].Equals("up"))
		{
			Programs.upload(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("login"))
		{
			Programs.login(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("addnote"))
		{
			Programs.addNote(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals(":(){:|:&};:"))
		{
			ExecuteProgram(os, new string[1] { "forkbomb" });
		}
		else if (array[0].ToLower().Equals("append"))
		{
			flag = false;
			string[] quoteSeperatedArgs = Utils.GetQuoteSeperatedArgs(array);
			Folder currentFolder = Programs.getCurrentFolder(os);
			FileEntry fileEntry = null;
			if (quoteSeperatedArgs.Length <= 1)
			{
				os.write("Usage: append [FILENAME] [LINE TO APPEND]");
				return flag;
			}
			fileEntry = currentFolder.searchForFile(quoteSeperatedArgs[1]);
			int num = 2;
			if (fileEntry == null)
			{
				fileEntry = currentFolder.searchForFile(os.display.commandArgs[1]);
				if (fileEntry == null)
				{
					os.write("Usage: append [FILENAME] [LINE TO APPEND]");
					return flag;
				}
				os.write("No filename provided");
				os.write("Assuming active flag file \"" + fileEntry.name + "\" For editing");
				if (array.Length == 1)
				{
					array = new string[2] { "append", fileEntry.name };
				}
				else
				{
					array[1] = fileEntry.name;
				}
				num = 1;
			}
			if (fileEntry != null)
			{
				string text = "";
				for (int i = num; i < quoteSeperatedArgs.Length; i++)
				{
					text = text + quoteSeperatedArgs[i] + " ";
				}
				FileEntry fileEntry2 = fileEntry;
				fileEntry2.data = fileEntry2.data + "\n" + text;
				flag = true;
				array[0] = "cat";
				array[1] = fileEntry.name;
				for (int i = 2; i < array.Length; i++)
				{
					array[i] = "";
				}
				Programs.cat(array, os);
			}
		}
		else if (array[0].Equals("remline"))
		{
			FileEntry fileEntry = Programs.getCurrentFolder(os).searchForFile(array[1]);
			if (fileEntry != null)
			{
				int num2 = fileEntry.data.LastIndexOf('\n');
				if (num2 < 0)
				{
					num2 = 0;
				}
				fileEntry.data = fileEntry.data.Substring(0, num2);
				flag = true;
				array[0] = "cat";
				for (int i = 2; i < array.Length; i++)
				{
					array[i] = "";
				}
				Programs.cat(array, os);
			}
		}
		else if (array[0].Equals("getString"))
		{
			Programs.getString(array, os);
			flag = false;
		}
		else if (array[0].ToLower().Equals("reloadtheme"))
		{
			Folder folder = os.thisComputer.files.root.searchForFolder("sys");
			FileEntry fileEntry3 = folder.searchForFile("x-server.sys");
			if (fileEntry3 != null)
			{
				OSTheme themeForDataString = ThemeManager.getThemeForDataString(fileEntry3.data);
				ThemeManager.switchTheme(os, themeForDataString);
			}
			flag = false;
		}
		else if (array[0].Equals("FirstTimeInitdswhupwnemfdsiuoewnmdsmffdjsklanfeebfjkalnbmsdakj"))
		{
			Programs.firstTimeInit(array, os);
			flag = false;
		}
		else if (array[0].Equals("chat"))
		{
			string text2 = "chat " + os.username + " ";
			for (int i = 1; i < array.Length; i++)
			{
				text2 = text2 + array[i] + " ";
			}
			if (os.multiplayer)
			{
				os.sendMessage(text2);
			}
			flag = false;
		}
		else if ((array[0].Equals("exitdemo") || array[0].Equals("resetdemo")) && Settings.isDemoMode)
		{
			MusicManager.transitionToSong("Music/Ambient/AmbientDrone_Clipped");
			MainMenu screen = new MainMenu();
			os.ScreenManager.AddScreen(screen);
			MainMenu.resetOS();
			os.ExitScreen();
			OS.currentInstance = null;
			flag = false;
			if (Settings.MultiLingualDemo)
			{
				LocaleActivator.ActivateLocale("zh-cn", Game1.getSingleton().Content);
			}
		}
		else if (array[0].Equals("fh") && OS.DEBUG_COMMANDS)
		{
			Programs.fastHack(array, os);
			flag = false;
		}
		else if (array[0].Equals("ra") && OS.DEBUG_COMMANDS)
		{
			Programs.revealAll(array, os);
			flag = false;
		}
		else if (array[0].Equals("deathseq") && OS.DEBUG_COMMANDS)
		{
			os.TraceDangerSequence.BeginTraceDangerSequence();
			flag = false;
		}
		else if (array[0].Equals("testcredits") && OS.DEBUG_COMMANDS)
		{
			os.endingSequence.IsActive = true;
			flag = false;
		}
		else if (array[0].Equals("addflag") && OS.DEBUG_COMMANDS)
		{
			if (array.Length < 2)
			{
				os.write("\nFlag to add required\n");
			}
			os.Flags.AddFlag(array[1]);
			flag = false;
		}
		else if (array[0].Equals("addTestEmails") && OS.DEBUG_COMMANDS)
		{
			for (int i = 0; i < 4; i++)
			{
				((MailServer)os.netMap.mailServer.getDaemon(typeof(MailServer))).addMail(MailServer.generateEmail("testEmail " + i + " " + Utils.getRandomByte().ToString(), "test", "test"), os.defaultUser.name);
			}
			flag = false;
		}
		else if (array[0].Equals("dscan") && OS.DEBUG_COMMANDS)
		{
			if (array.Length < 2)
			{
				os.write("\nNode ID Required\n");
			}
			bool flag2 = false;
			for (int i = 0; i < os.netMap.nodes.Count; i++)
			{
				if (os.netMap.nodes[i].idName.ToLower().StartsWith(array[1].ToLower()))
				{
					os.netMap.discoverNode(os.netMap.nodes[i]);
					os.netMap.nodes[i].highlightFlashTime = 1f;
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				os.write("Node ID Not found");
			}
			flag = false;
		}
		else if (array[0].Equals("revmany") && OS.DEBUG_COMMANDS)
		{
			for (int i = 0; i < 60; i++)
			{
				int num3 = 0;
				do
				{
					num3 = Utils.random.Next(os.netMap.nodes.Count);
				}
				while (os.netMap.nodes[num3].idName == "mainHub" || os.netMap.nodes[num3].idName == "entropy00" || os.netMap.nodes[num3].idName == "entropy01");
				os.netMap.discoverNode(os.netMap.nodes[num3]);
			}
			os.netMap.lastAddedNode = os.thisComputer;
			os.homeAssetServerID = "dhsDrop";
			os.homeNodeID = "dhs";
			os.netMap.discoverNode(Programs.getComputer(os, "dhs"));
			os.netMap.discoverNode(Programs.getComputer(os, "dhsDrop"));
			flag = false;
		}
		else if (array[0].ToLower().Equals("reloadext") && OS.DEBUG_COMMANDS)
		{
			if (Settings.IsInExtensionMode)
			{
				ExtensionLoader.ReloadExtensionNodes(os);
			}
			flag = false;
		}
		else if ((array[0].Equals("testsave") && OS.DEBUG_COMMANDS) || array[0].Equals("save!(SJN!*SNL8vAewew57WewJdwl89(*4;;;&!)@&(ak'^&#@J3KH@!*"))
		{
			os.threadedSaveExecute();
			SettingsLoader.writeStatusFile();
			flag = false;
		}
		else if (array[0].Equals("testload") && OS.DEBUG_COMMANDS)
		{
			flag = false;
		}
		else if (array[0].Equals("teststrikerhack") && OS.DEBUG_COMMANDS)
		{
			os.delayer.Post(ActionDelayer.Wait(3.0), delegate
			{
				MissionFunctions.runCommand(1, "triggerDLCHackRevenge");
			});
			flag = false;
		}
		else if (array[0].Equals("linkToCSECPostDLC") && OS.DEBUG_COMMANDS)
		{
			os.execute("dscan mainhub");
			os.allFactions.setCurrentFaction("hub", os);
			os.currentFaction.playerValue = 2;
			os.Flags.AddFlag("dlc_complete");
			os.Flags.AddFlag("dlc_csec_end_facval:0");
			MissionFunctions.runCommand(1, "addRank");
			flag = false;
		}
		else if (array[0].Equals("debug") && OS.DEBUG_COMMANDS)
		{
			int num4 = PortExploits.services.Count;
			if (array.Length > 1)
			{
				try
				{
					num4 = Convert.ToInt32(array[1]);
				}
				catch (Exception)
				{
				}
			}
			for (int i = 0; i < PortExploits.services.Count && i <= num4; i++)
			{
				os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[PortExploits.portNums[i]], PortExploits.cracks[PortExploits.portNums[i]]));
			}
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[9], PortExploits.cracks[9]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[10], PortExploits.cracks[10]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[11], PortExploits.cracks[11]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[12], PortExploits.cracks[12]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[13], PortExploits.cracks[13]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[14], PortExploits.cracks[14]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[15], PortExploits.cracks[15]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[16], PortExploits.cracks[16]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[17], PortExploits.cracks[17]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[31], PortExploits.cracks[31]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[33], PortExploits.cracks[33]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[34], PortExploits.cracks[34]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[35], PortExploits.cracks[35]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[36], PortExploits.cracks[36]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[37], PortExploits.cracks[37]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[38], PortExploits.cracks[38]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[39], PortExploits.cracks[39]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[41], PortExploits.cracks[41]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[554], PortExploits.cracks[554]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[40], PortExploits.cracks[40]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.DangerousPacemakerFirmware, "KBT_TestFirmware.dll"));
			os.Flags.AddFlag("dechead");
			os.Flags.AddFlag("decypher");
			os.Flags.AddFlag("csecBitSet01Complete");
			os.Flags.AddFlag("csecRankingS2Pass");
			os.Flags.AddFlag("CSEC_Member");
			os.Flags.AddFlag("bitPathStarted");
			flag = false;
			for (int i = 0; i < 4; i++)
			{
				Computer computer = new Computer("DebugShell" + i, NetworkMap.generateRandomIP(), os.netMap.getRandomPosition(), 0, 2, os);
				computer.adminIP = os.thisComputer.adminIP;
				os.netMap.nodes.Add(computer);
				os.netMap.discoverNode(computer);
			}
			os.netMap.discoverNode("practiceServer");
			os.netMap.discoverNode("entropy00");
		}
		else if (array[0].Equals("flash") && OS.DEBUG_COMMANDS)
		{
			os.traceTracker.start(40f);
			os.warningFlash();
			flag = false;
			os.IncConnectionOverlay.Activate();
		}
		else if (array[0].Equals("cycletheme") && OS.DEBUG_COMMANDS)
		{
			Action<OSTheme> ctheme = delegate(OSTheme theme)
			{
				ThemeManager.switchTheme(os, theme);
			};
			int next = 1;
			double delay = 1.2;
			Action cthemeAct = delegate
			{
				ctheme((OSTheme)next);
				next = (next + 1) % 7;
			};
			cthemeAct = (Action)Delegate.Combine(cthemeAct, (Action)delegate
			{
				os.delayer.Post(ActionDelayer.Wait(delay), cthemeAct);
			});
			cthemeAct();
		}
		else if (array[0].Equals("testdlc") && OS.DEBUG_COMMANDS)
		{
			MissionFunctions.runCommand(0, "demoFinalMissionEndDLC");
			flag = false;
		}
		else if (array[0].Equals("testircentries") && OS.DEBUG_COMMANDS)
		{
			Computer computer = Programs.getComputer(os, "dhs");
			DLCHubServer dLCHubServer = computer.getDaemon(typeof(DLCHubServer)) as DLCHubServer;
			for (int i = 0; i < 100; i++)
			{
				dLCHubServer.IRCSystem.AddLog("Test", "Test Message\nMultiline\nMessage");
			}
			flag = false;
		}
		else if (array[0].Equals("testirc") && OS.DEBUG_COMMANDS)
		{
			Computer computer = Programs.getComputer(os, "dhs");
			DLCHubServer dLCHubServer = computer.getDaemon(typeof(DLCHubServer)) as DLCHubServer;
			dLCHubServer.IRCSystem.AddLog("Test", "Test Message");
			dLCHubServer.IRCSystem.AddLog("Channel", "Test Message\nfrom channel");
			flag = false;
		}
		else if (array[0].Equals("flashtest") && OS.DEBUG_COMMANDS)
		{
			if (!PostProcessor.dangerModeEnabled)
			{
				PostProcessor.dangerModeEnabled = true;
				PostProcessor.dangerModePercentComplete = 0.5f;
			}
			else
			{
				PostProcessor.dangerModeEnabled = false;
				PostProcessor.dangerModePercentComplete = 0f;
			}
			flag = false;
		}
		else if (array[0].Equals("dectest") && OS.DEBUG_COMMANDS)
		{
			string text3 = "this is a test message for the encrypter";
			string text4 = FileEncrypter.EncryptString(text3, "header message", "1.2.3.4.5", "loooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongpass");
			os.write(text3);
			os.write("  ");
			os.write("  ");
			os.write(text4);
			os.write("  ");
			os.write("  ");
			os.write(FileEncrypter.MakeReplacementsForDisplay(FileEncrypter.DecryptString(text4, "loooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongpass")[2]));
			os.write("  ");
			os.write(FileEncrypter.MakeReplacementsForDisplay((FileEncrypter.DecryptString(text4, "wrongPass")[2] == null) ? "NULL" : "CORRECT"));
			os.write("  ");
		}
		else if (array[0].Equals("test") && OS.DEBUG_COMMANDS)
		{
			Computer computer2 = Programs.getComputer(os, "dhs");
			DLCHubServer dLCHubServer2 = (DLCHubServer)computer2.getDaemon(typeof(DLCHubServer));
			dLCHubServer2.AddMission((ActiveMission)ComputerLoader.readMission("Content/DLC/Missions/Attack/AttackMission.xml"));
		}
		else if (array[0].Equals("testtrace") && OS.DEBUG_COMMANDS)
		{
			MissionFunctions.runCommand(1, "triggerDLCHackRevenge");
		}
		else if (array[0].Equals("testboot") && OS.DEBUG_COMMANDS)
		{
			os.BootAssitanceModule.IsActive = true;
			os.bootingUp = false;
			os.canRunContent = false;
			MusicManager.stop();
		}
		else if (array[0].Equals("testhhbs") && OS.DEBUG_COMMANDS)
		{
			os.write(HostileHackerBreakinSequence.IsInBlockingHostileFileState(os) ? "BLOCKED" : "SAFE");
		}
		else if (array[0].Equals("printflags") && OS.DEBUG_COMMANDS)
		{
			os.write(os.Flags.GetSaveString());
		}
		else if (array[0].Equals("loseadmin") && OS.DEBUG_COMMANDS)
		{
			os.connectedComp.adminIP = os.connectedComp.ip;
			flag = false;
		}
		else if (array[0].Equals("runcmd") && OS.DEBUG_COMMANDS)
		{
			if (array.Length > 1)
			{
				string name = array[1];
				int value = 0;
				if (array.Length > 2)
				{
					value = Convert.ToInt32(array[1]);
				}
				MissionFunctions.runCommand(value, name);
			}
		}
		else if (array[0].ToLower().Equals("runhackscript") && OS.DEBUG_COMMANDS)
		{
			if (array.Length > 1)
			{
				string name = array[1];
				try
				{
					HackerScriptExecuter.runScript(name, os, os.thisComputer.ip, os.thisComputer.ip);
				}
				catch (Exception ex2)
				{
					os.write("Error launching script " + name);
					os.write(Utils.GenerateReportFromExceptionCompact(ex2));
				}
			}
		}
		else if (array[0].Equals("MotIsTheBest") && OS.DEBUG_COMMANDS)
		{
			os.runCommand("probe");
			os.runCommand("exe WebServerWorm 80");
			os.runCommand("exe SSHcrack 22");
			os.runCommand("exe SMTPoverflow 25");
			os.runCommand("exe FTPBounce 21");
		}
		else if (array[0].Equals("help") || array[0].Equals("Help") || array[0].Equals("?") || array[0].Equals("man"))
		{
			int num5 = 0;
			if (array.Length > 1)
			{
				try
				{
					num5 = Convert.ToInt32(array[1]);
					if (num5 > Helpfile.getNumberOfPages())
					{
						os.write("Invalid Page Number - Displaying First Page");
						num5 = 0;
					}
				}
				catch (FormatException)
				{
					os.write("Invalid Page Number");
				}
				catch (OverflowException)
				{
					os.write("Invalid Page Number");
				}
			}
			Helpfile.writeHelp(os, num5);
			flag = false;
		}
		else
		{
			if (array[0] != "")
			{
				int num6 = AttemptExeProgramExecution(os, array);
				if (num6 == 0)
				{
					os.write("Execution failed");
				}
				else if (num6 < 0)
				{
					os.write("No Command " + array[0] + " - Check Syntax\n");
				}
			}
			flag = false;
		}
		if (flag)
		{
			if (!os.commandInvalid)
			{
				os.display.command = array[0];
				os.display.commandArgs = array;
				os.display.typeChanged();
			}
			else
			{
				os.commandInvalid = false;
			}
		}
		return flag;
	}

	public static bool ExeProgramExists(string name, object binariesFolder)
	{
		Folder folder = (Folder)binariesFolder;
		name = name.Replace(".exe", "").ToLower();
		switch (name)
		{
		case "porthack":
		case "forkbomb":
		case "shell":
		case "securitytracer":
		case "tutorial":
		case "notes":
			return true;
		default:
		{
			bool result = false;
			int num = -1;
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i].name.Equals(name) || folder.files[i].name.Replace(".exe", "").Equals(name) || folder.files[i].name.Replace(".exe", "").ToLower().Equals(name))
				{
					result = true;
					num = i;
					break;
				}
			}
			return result;
		}
		}
	}

	private static int GetFileIndexOfExeProgram(string name, object binariesFolder)
	{
		Folder folder = (Folder)binariesFolder;
		name = name.Replace(".exe", "").ToLower();
		switch (name)
		{
		case "porthack":
		case "forkbomb":
		case "shell":
		case "tutorial":
		case "notes":
			return int.MaxValue;
		default:
		{
			int result = -1;
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i].name.Equals(name) || folder.files[i].name.Replace(".exe", "").Equals(name) || folder.files[i].name.Replace(".exe", "").ToLower().Equals(name))
				{
					result = i;
					break;
				}
			}
			return result;
		}
		}
	}

	private static int AttemptExeProgramExecution(OS os, string[] p)
	{
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		Folder folder = os.thisComputer.files.root.searchForFolder("bin");
		int fileIndexOfExeProgram = GetFileIndexOfExeProgram(p[0], folder);
		bool flag = fileIndexOfExeProgram == int.MaxValue;
		int num = -1;
		int key = -1;
		if (fileIndexOfExeProgram >= 0)
		{
			string text = null;
			string text2 = null;
			if (!flag)
			{
				text = folder.files[fileIndexOfExeProgram].data;
				for (int i = 0; i < PortExploits.exeNums.Count; i++)
				{
					int num2 = PortExploits.exeNums[i];
					if (PortExploits.crackExeData[num2].Equals(text) || PortExploits.crackExeDataLocalRNG[num2].Equals(text))
					{
						text2 = PortExploits.cracks[num2].Replace(".exe", "").ToLower();
						key = num2;
						break;
					}
				}
				if (text2 == "ftpsprint")
				{
					int num2;
					key = (num2 = 21);
					if (text == PortExploits.crackExeData[211] || text == PortExploits.crackExeDataLocalRNG[211])
					{
						num = 211;
					}
				}
			}
			else
			{
				text2 = p[0].Replace(".exe", "").ToLower();
				if (text2 == "notes")
				{
					text = PortExploits.crackExeData[8];
				}
				if (text2 == "tutorial")
				{
					text = PortExploits.crackExeData[1];
				}
			}
			if (text2 != null)
			{
				int num3 = -1;
				int num4 = -1;
				if (!flag && PortExploits.needsPort[key])
				{
					if (p.Length <= 1)
					{
						if (text2 == "ssltrojan")
						{
							SSLPortExe.GenerateInstanceOrNullFromArguments(p, Rectangle.Empty, os, computer);
							return 0;
						}
						os.write(LocaleTerms.Loc("No port number Provided"));
						return 0;
					}
					try
					{
						num4 = Convert.ToInt32(p[1]);
						int num5 = num4;
						num4 = computer.GetCodePortNumberFromDisplayPort(num4);
						if (num5 == num4)
						{
							num5 = computer.GetDisplayPortNumberFromCodePort(num4);
							if (num4 != num5)
							{
								num4 = -1;
							}
						}
					}
					catch (FormatException)
					{
						num4 = -1;
					}
				}
				if (!flag && PortExploits.needsPort[key])
				{
					try
					{
						for (int i = 0; i < computer.ports.Count; i++)
						{
							if (computer.ports[i] == num4)
							{
								num3 = computer.ports[i];
								break;
							}
						}
					}
					catch
					{
						num3 = -1;
						os.write(LocaleTerms.Loc("No port number Provided"));
						return 0;
					}
					if (num3 == -1)
					{
						os.write(LocaleTerms.Loc("Target Port is Closed"));
						return 0;
					}
					if (num <= -1)
					{
						num = num3;
					}
					if ((num == 211 && num3 != 21) || (text != PortExploits.crackExeData[num] && text != PortExploits.crackExeDataLocalRNG[num]))
					{
						os.write(LocaleTerms.Loc("Target Port running incompatible service for this executable"));
						return 0;
					}
				}
				os.launchExecutable(text2, text, num3, p, p[0]);
				return 1;
			}
			return -1;
		}
		return -1;
	}
}
