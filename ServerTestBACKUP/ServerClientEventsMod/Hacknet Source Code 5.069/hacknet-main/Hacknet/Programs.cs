using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Hacknet.Extensions;
using Hacknet.Gui;
using Microsoft.Xna.Framework.Input;
using SDL2;

namespace Hacknet;

internal static class Programs
{
	private static readonly object ConnectionLockObject = new object();

	public static void doDots(int num, int msDelay, OS os)
	{
		for (int i = 0; i < num; i++)
		{
			os.writeSingle(".");
			Thread.Sleep(Utils.DebugGoFast() ? 1 : msDelay);
		}
	}

	public static void typeOut(string s, OS os, int delay = 50)
	{
		os.write(string.Concat(s[0]));
		Thread.Sleep(delay);
		for (int i = 1; i < s.Length; i++)
		{
			os.writeSingle(string.Concat(s[i]));
			Thread.Sleep(Utils.DebugGoFast() ? 1 : delay);
		}
	}

	public static void firstTimeInit(string[] args, OS os, bool callWasRecursed = false)
	{
		bool flag = Settings.initShowsTutorial;
		bool flag2 = false;
		if (Settings.IsInExtensionMode && !ExtensionLoader.ActiveExtensionInfo.StartsWithTutorial)
		{
			flag = false;
			if (!OS.WillLoadSave)
			{
				flag2 = true;
			}
		}
		if (flag)
		{
			os.display.visible = false;
			os.ram.visible = false;
			os.netMap.visible = false;
			os.terminal.visible = true;
			os.mailicon.isEnabled = false;
			if (os.hubServerAlertsIcon != null)
			{
				os.hubServerAlertsIcon.IsEnabled = false;
			}
		}
		int num = (Settings.isConventionDemo ? 80 : 200);
		int num2 = (Settings.isConventionDemo ? 150 : 300);
		if (Settings.debugCommandsEnabled && GuiData.getKeyboadState().IsKeyDown(Keys.LeftAlt))
		{
			num = (num2 = 1);
		}
		typeOut("Initializing .", os);
		doDots(7, num + 100, os);
		typeOut("Loading modules.", os);
		doDots(5, num, os);
		os.writeSingle("Complete");
		if (!Utils.DebugGoFast())
		{
			Thread.Sleep(num2);
		}
		typeOut("Loading nodes.", os);
		doDots(5, num, os);
		os.writeSingle("Complete");
		if (!Utils.DebugGoFast())
		{
			Thread.Sleep(num2);
		}
		typeOut("Reticulating splines.", os);
		doDots(5, num - 50, os);
		os.writeSingle("Complete");
		if (!Utils.DebugGoFast())
		{
			Thread.Sleep(num2);
		}
		if (os.crashModule.BootLoadErrors.Length > 0)
		{
			typeOut("\n------ " + LocaleTerms.Loc("BOOT ERRORS DETECTED") + " ------", os);
			Thread.Sleep(200);
			string[] array = os.crashModule.BootLoadErrors.Split(Utils.newlineDelim, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				typeOut(array[i], os);
				Thread.Sleep(100);
			}
			typeOut("---------------------------------\n", os);
			Thread.Sleep(200);
		}
		typeOut("\n--Initialization Complete--\n", os);
		GuiData.getFilteredKeys();
		os.inputEnabled = true;
		if (!Utils.DebugGoFast())
		{
			Thread.Sleep(num2 + 100);
		}
		if (!flag)
		{
			typeOut(LocaleTerms.Loc("For A Command List, type \"help\""), os);
			if (!Utils.DebugGoFast())
			{
				Thread.Sleep(num2 + 100);
			}
		}
		os.write("");
		if (!Utils.DebugGoFast())
		{
			Thread.Sleep(num2);
		}
		os.write("");
		if (!Utils.DebugGoFast())
		{
			Thread.Sleep(num2);
		}
		os.write("");
		if (!Utils.DebugGoFast())
		{
			Thread.Sleep(num2);
		}
		os.write("\n");
		if (flag)
		{
			os.write(LocaleTerms.Loc("Launching Tutorial..."));
			os.launchExecutable("Tutorial.exe", PortExploits.crackExeData[1], -1);
			Settings.initShowsTutorial = false;
			AdvancedTutorial advancedTutorial = null;
			for (int i = 0; i < os.exes.Count; i++)
			{
				advancedTutorial = os.exes[i] as AdvancedTutorial;
				if (advancedTutorial != null)
				{
					break;
				}
			}
			if (advancedTutorial != null)
			{
				advancedTutorial.CanActivateFirstStep = false;
			}
			int num3 = 100;
			for (int i = 0; i < num3; i++)
			{
				double num4 = (double)i / (double)num3;
				if (Utils.random.NextDouble() < num4)
				{
					os.ram.visible = true;
					os.netMap.visible = false;
					os.terminal.visible = false;
				}
				else
				{
					os.ram.visible = false;
					os.netMap.visible = false;
					os.terminal.visible = true;
				}
				Thread.Sleep(16);
			}
			os.ram.visible = true;
			os.netMap.visible = false;
			os.terminal.visible = false;
			if (advancedTutorial != null)
			{
				advancedTutorial.CanActivateFirstStep = true;
			}
		}
		else
		{
			os.runCommand("connect " + os.thisComputer.ip);
			if (flag2 && !os.Flags.HasFlag("ExtensionFirstBootComplete"))
			{
				ExtensionLoader.SendStartingEmailForActiveExtensionNextFrame(os);
				float num5 = 2.2f;
				int num3 = (int)(60f * num5);
				for (int i = 0; i < num3; i++)
				{
					double num4 = (double)i / (double)num3;
					os.ram.visible = Utils.random.NextDouble() < num4;
					os.netMap.visible = Utils.random.NextDouble() < num4;
					os.display.visible = Utils.random.NextDouble() < num4;
					Thread.Sleep(16);
				}
				os.terminal.visible = true;
				os.display.visible = true;
				os.netMap.visible = true;
				os.ram.visible = true;
				os.terminal.visible = true;
				os.display.inputLocked = false;
				os.netMap.inputLocked = false;
				os.ram.inputLocked = false;
				os.Flags.AddFlag("ExtensionFirstBootComplete");
			}
		}
		Thread.Sleep(500);
		if (callWasRecursed)
		{
			os.ram.visible = true;
			os.ram.inputLocked = false;
			os.display.visible = true;
			os.display.inputLocked = false;
			os.netMap.visible = true;
			os.netMap.inputLocked = false;
		}
		else if (flag)
		{
			os.ram.visible = true;
			os.ram.inputLocked = false;
			os.display.visible = true;
			os.display.inputLocked = false;
			os.netMap.visible = true;
			os.netMap.inputLocked = false;
		}
		else if (!os.ram.visible && !Settings.IsInExtensionMode)
		{
			firstTimeInit(args, os, callWasRecursed: true);
		}
	}

	public static void connect(string[] args, OS os)
	{
		lock (ConnectionLockObject)
		{
			os.navigationPath.Clear();
			if (os.connectedComp != null)
			{
				os.connectedComp.disconnecting(os.thisComputer.ip, externalDisconnectToo: false);
			}
			os.terminal.prompt = "> ";
			os.write("Disconnected \n");
			bool lockTaken = false;
			ConnectedNodeEffect obj = default(ConnectedNodeEffect);
			try
			{
				Monitor.Enter(obj = os.netMap.nodeEffect, ref lockTaken);
				os.netMap.nodeEffect.reset();
			}
			finally
			{
				if (lockTaken)
				{
					Monitor.Exit(obj);
				}
			}
			bool lockTaken2 = false;
			try
			{
				Monitor.Enter(obj = os.netMap.adminNodeEffect, ref lockTaken2);
				os.netMap.adminNodeEffect.reset();
			}
			finally
			{
				if (lockTaken2)
				{
					Monitor.Exit(obj);
				}
			}
			os.display.command = args[0];
			os.display.commandArgs = args;
			os.display.typeChanged();
			bool flag = false;
			if (args.Length < 2)
			{
				os.write("Usage: connect [WHERE TO CONNECT TO]");
				return;
			}
			os.write("Scanning For " + args[1]);
			for (int i = 0; i < os.netMap.nodes.Count; i++)
			{
				if (!os.netMap.nodes[i].ip.Equals(args[1]) && !os.netMap.nodes[i].name.Equals(args[1]))
				{
					continue;
				}
				if (os.netMap.nodes[i].connect(os.thisComputer.ip))
				{
					os.connectedComp = os.netMap.nodes[i];
					os.connectedIP = os.netMap.nodes[i].ip;
					os.write("Connection Established ::");
					os.write("Connected To " + os.netMap.nodes[i].name + "@" + os.netMap.nodes[i].ip);
					os.terminal.prompt = os.netMap.nodes[i].ip + "@> ";
					if (!os.netMap.visibleNodes.Contains(i))
					{
						os.netMap.visibleNodes.Add(i);
					}
					Folder folder = os.netMap.nodes[i].files.root.searchForFolder("sys");
					FileEntry fileEntry = folder.searchForFile(ComputerTypeInfo.getDefaultBootDaemonFilename(os.netMap.nodes[i]));
					if (fileEntry == null)
					{
						return;
					}
					string data = fileEntry.data;
					for (int j = 0; j < os.netMap.nodes[i].daemons.Count; j++)
					{
						if (os.netMap.nodes[i].daemons[j].name == data)
						{
							os.netMap.nodes[i].daemons[j].navigatedTo();
							os.display.command = os.netMap.nodes[i].daemons[j].name;
						}
					}
					return;
				}
				os.write("External Computer Refused Connection");
				flag = true;
			}
			if (!flag)
			{
				os.write("Failed to Connect:\nCould Not Find Computer at " + args[1]);
				os.display.command = "dc";
			}
			os.connectedComp = null;
			os.connectedIP = "";
		}
	}

	public static void disconnect(string[] args, OS os)
	{
		os.navigationPath.Clear();
		if (os.connectedComp != null)
		{
			os.connectedComp.disconnecting(os.thisComputer.ip);
		}
		os.connectedComp = null;
		os.connectedIP = "";
		os.terminal.prompt = "> ";
		os.write("Disconnected \n");
		bool lockTaken = false;
		ConnectedNodeEffect obj = default(ConnectedNodeEffect);
		try
		{
			Monitor.Enter(obj = os.netMap.nodeEffect, ref lockTaken);
			os.netMap.nodeEffect.reset();
		}
		finally
		{
			if (lockTaken)
			{
				Monitor.Exit(obj);
			}
		}
		bool lockTaken2 = false;
		try
		{
			Monitor.Enter(obj = os.netMap.adminNodeEffect, ref lockTaken2);
			os.netMap.adminNodeEffect.reset();
		}
		finally
		{
			if (lockTaken2)
			{
				Monitor.Exit(obj);
			}
		}
	}

	public static void getString(string[] args, OS os)
	{
		string prompt = os.terminal.prompt;
		os.getStringCache = "terminalString#$#$#$$#$&$#$#$#$#";
		os.terminal.preventingExecution = true;
		os.terminal.prompt = args[1] + " :";
		os.terminal.usingTabExecution = true;
		string text = "";
		int num = os.terminal.commandsRun();
		while (os.terminal.commandsRun() == num)
		{
			Thread.Sleep(16);
		}
		text = os.terminal.getLastRunCommand();
		os.getStringCache = "loginData#$#$#$$#$&$#$#$#$#" + text;
		os.terminal.prompt = prompt;
		os.terminal.preventingExecution = false;
		os.terminal.usingTabExecution = false;
		os.getStringCache += "#$#$#$$#$&$#$#$#$#done";
	}

	public static bool parseStringFromGetStringCommand(OS os, out string data)
	{
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		string[] array = os.getStringCache.Split(separator, StringSplitOptions.None);
		string text = null;
		if (array.Length > 1)
		{
			text = array[1];
			if (text.Equals(""))
			{
				text = os.terminal.currentLine;
			}
		}
		data = text;
		return array.Length > 2;
	}

	public static void login(string[] args, OS os)
	{
		string prompt = os.terminal.prompt;
		os.display.command = "login";
		os.displayCache = "loginData#$#$#$$#$&$#$#$#$#";
		os.terminal.preventingExecution = true;
		os.terminal.prompt = "Username :";
		os.terminal.usingTabExecution = true;
		string text = "";
		int num = os.terminal.commandsRun();
		while (os.terminal.commandsRun() == num)
		{
			Thread.Sleep(4);
		}
		text = os.terminal.getLastRunCommand();
		os.displayCache = "loginData#$#$#$$#$&$#$#$#$#" + text + "#$#$#$$#$&$#$#$#$#";
		os.terminal.prompt = "Password :";
		TextBox.MaskingText = true;
		num = os.terminal.commandsRun();
		while (os.terminal.commandsRun() == num)
		{
			Thread.Sleep(16);
		}
		string lastRunCommand = os.terminal.getLastRunCommand();
		os.displayCache += lastRunCommand;
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		int num2 = computer.login(text, lastRunCommand, 1);
		os.terminal.prompt = prompt;
		os.terminal.preventingExecution = false;
		os.terminal.usingTabExecution = false;
		TextBox.MaskingText = false;
		switch (num2)
		{
		case 2:
			os.write("User " + text + " Login Successful");
			os.connectedComp.userLoggedIn = true;
			break;
		case 1:
			os.write("Admin Login Successful");
			break;
		default:
			os.write("Login Failed");
			break;
		}
		os.displayCache = os.displayCache + "#$#$#$$#$&$#$#$#$#" + num2 + "#$#$#$$#$&$#$#$#$#";
	}

	public static void ls(string[] args, OS os)
	{
		if (os.hasConnectionPermission(admin: false))
		{
			Folder folder = getCurrentFolder(os);
			if (args.Length >= 2)
			{
				folder = getFolderAtPath(args[1], os);
			}
			int num = 0;
			for (int i = 0; i < folder.folders.Count; i++)
			{
				os.write(":" + folder.folders[i].name);
				num++;
			}
			for (int i = 0; i < folder.files.Count; i++)
			{
				os.write(folder.files[i].name ?? "");
				num++;
			}
			if (num == 0)
			{
				os.write("--Folder Empty --");
			}
		}
		else
		{
			os.write("Insufficient Privileges to Perform Operation");
		}
	}

	public static void cat(string[] args, OS os)
	{
		if (os.hasConnectionPermission(admin: true))
		{
			os.displayCache = "";
			Folder currentFolder = getCurrentFolder(os);
			if (args.Length < 2)
			{
				os.write("Usage: cat [FILENAME]");
				return;
			}
			for (int i = 0; i < currentFolder.files.Count; i++)
			{
				if (!currentFolder.files[i].name.Equals(args[1]))
				{
					continue;
				}
				bool flag = false;
				if (os.connectedComp != null)
				{
					if (os.connectedComp.canReadFile(os.thisComputer.ip, currentFolder.files[i], i))
					{
						flag = true;
					}
				}
				else
				{
					flag = os.thisComputer.canReadFile(os.thisComputer.ip, currentFolder.files[i], i);
				}
				if (flag)
				{
					string text = LocalizedFileLoader.SafeFilterString(currentFolder.files[i].data);
					os.write(currentFolder.files[i].name + " : " + (double)currentFolder.files[i].size / 1000.0 + "kb\n" + text + "\n");
					os.displayCache = currentFolder.files[i].data;
					os.display.LastDisplayedFileFolder = currentFolder;
					os.display.LastDisplayedFileSourceIP = ((os.connectedComp != null) ? os.connectedComp.ip : os.thisComputer.ip);
				}
				return;
			}
			os.displayCache = "File Not Found";
			os.validCommand = false;
			os.write("File Not Found\n");
		}
		else
		{
			os.displayCache = "Insufficient Privileges";
			os.validCommand = false;
			os.write("Insufficient Privileges to Perform Operation");
		}
	}

	public static void ps(string[] args, OS os)
	{
		if (os.exes.Count > 0)
		{
			os.write("UID   :  PID  :  NAME");
			Thread.Sleep(100);
			for (int i = 0; i < os.exes.Count; i++)
			{
				os.write("root  :  " + os.exes[i].PID + "     : " + os.exes[i].IdentifierName);
				Thread.Sleep(100);
			}
			os.write("");
		}
		else
		{
			os.write("No Running Processes");
		}
	}

	public static void kill(string[] args, OS os)
	{
		try
		{
			int num = Convert.ToInt32(args[1].Replace("[", "").Replace("]", "").Replace("\"", ""));
			int num2 = -1;
			for (int i = 0; i < os.exes.Count; i++)
			{
				if (os.exes[i].PID == num)
				{
					num2 = i;
				}
			}
			if (num2 < 0 || num2 >= os.exes.Count)
			{
				os.write(LocaleTerms.Loc("Invalid PID"));
				return;
			}
			os.write("Process " + num + "[" + os.exes[num2].IdentifierName + "] Ended");
			os.exes[num2].Killed();
			os.exes.RemoveAt(num2);
		}
		catch
		{
			os.write(LocaleTerms.Loc("Error: Invalid PID or Input Format"));
		}
	}

	public static void scp(string[] args, OS os)
	{
		if (os.connectedComp == null)
		{
			os.write("Must be Connected to a Non-Local Host\n");
			return;
		}
		if (args.Length < 2)
		{
			os.write("Not Enough Arguments");
			return;
		}
		string text = "bin";
		string text2 = text;
		bool flag = false;
		if (args.Length > 2)
		{
			text2 = args[2];
			if (!args[2].Contains('.') || args[2].Contains('/') || args[2].Contains('\\'))
			{
				text = args[2];
				flag = true;
			}
		}
		Folder currentFolder = getCurrentFolder(os);
		bool flag2 = false;
		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
		for (int i = 0; i < currentFolder.files.Count; i++)
		{
			if (args[1] == "*")
			{
				flag2 = true;
				list.Add(new KeyValuePair<string, int>(currentFolder.files[i].name, currentFolder.files[i].size));
			}
			else if (currentFolder.files[i].name.ToLower().Equals(args[1].ToLower()))
			{
				flag2 = true;
				list.Add(new KeyValuePair<string, int>(currentFolder.files[i].name, currentFolder.files[i].size));
				break;
			}
		}
		if (!flag2)
		{
			os.write("File \"" + args[1] + "\" Does Not Exist\n");
			return;
		}
		for (int j = 0; j < list.Count; j++)
		{
			if (j > 0)
			{
				Thread.Sleep(250);
			}
			int index = currentFolder.files.IndexOf(currentFolder.searchForFile(list[j].Key));
			int value = list[j].Value;
			if (!flag)
			{
				string text3 = currentFolder.files[index].name.ToLower();
				if (text3.EndsWith(".exe"))
				{
					text = "bin";
				}
				else if (text3.EndsWith(".sys"))
				{
					text = "sys";
				}
				else if (text3.StartsWith("@"))
				{
					text = "home/dl_logs";
					Folder folder = os.thisComputer.files.root.searchForFolder("home");
					if (folder.searchForFolder("dl_logs") == null)
					{
						folder.folders.Add(new Folder("dl_logs"));
					}
				}
				else
				{
					text = "home";
				}
			}
			string text4 = currentFolder.files[index].name;
			string[] array = text2.Split(Utils.directorySplitterDelim, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length > 0 && array[array.Length - 1].Contains("."))
			{
				text4 = array[array.Length - 1];
				StringBuilder stringBuilder = new StringBuilder();
				int num = 0;
				for (int i = 0; i < array.Length - 1; i++)
				{
					stringBuilder.Append(array[i]);
					stringBuilder.Append('/');
					num++;
				}
				if (num > 0)
				{
					text = stringBuilder.ToString();
					if (text.Length > 0)
					{
						text = text.Substring(0, text.Length - 1);
					}
				}
			}
			string likelyFilename = null;
			Folder folder2 = getFolderAtPathAsFarAsPossible(text, os, os.thisComputer.files.root, out likelyFilename);
			if (likelyFilename != null)
			{
				text4 = likelyFilename;
			}
			if (likelyFilename == text && folder2 == os.thisComputer.files.root)
			{
				folder2 = getCurrentFolder(os);
			}
			Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
			if (computer.canCopyFile(os.thisComputer.ip, list[j].Key))
			{
				os.write("Copying Remote File " + list[j].Key + "\nto Local Folder /" + text);
				os.write(".");
				for (int i = 0; i < Math.Min(value / 500, 20); i++)
				{
					os.writeSingle(".");
					OS.operationProgress = (float)i / (float)(value / 1000);
					Thread.Sleep(200);
				}
				string text5 = text4;
				int num2 = 0;
				bool flag3 = false;
				do
				{
					flag3 = true;
					for (int i = 0; i < folder2.files.Count; i++)
					{
						if (folder2.files[i].name == text5)
						{
							num2++;
							text5 = text4 + "(" + num2 + ")";
							flag3 = false;
							break;
						}
					}
				}
				while (!flag3);
				try
				{
					folder2.files.Add(new FileEntry(currentFolder.files[index].data, text5));
				}
				catch (ArgumentOutOfRangeException)
				{
					os.write("SCP ERROR: File not found.");
					break;
				}
				os.write("Transfer Complete\n");
			}
			else
			{
				os.write("Insufficient Privileges to Perform Operation : This File Requires Admin Access\n");
			}
		}
	}

	public static void upload(string[] args, OS os)
	{
		if (os.connectedComp == null || os.connectedComp == os.thisComputer)
		{
			os.write("Must be Connected to a Non-Local Host\n");
			os.write("Connect to a REMOTE host and run upload with a LOCAL filepath\n");
			return;
		}
		if (args.Length < 2)
		{
			os.write("Not Enough Arguments");
			return;
		}
		if (!os.hasConnectionPermission(admin: false))
		{
			os.write("Insufficient user permissions to upload");
			return;
		}
		Folder root = os.thisComputer.files.root;
		int num = args[1].LastIndexOf('/');
		if (num <= 0)
		{
			return;
		}
		string text = args[1].Substring(0, num);
		root = getFolderAtPath(text, os, os.thisComputer.files.root);
		if (root == null)
		{
			os.write("Local Folder " + text + " not found.");
			return;
		}
		string text2 = args[1].Substring(num + 1);
		FileEntry fileEntry = root.searchForFile(text2);
		if (fileEntry == null)
		{
			os.write("File " + text2 + " not found at specified filepath.");
			return;
		}
		Folder currentFolder = getCurrentFolder(os);
		List<int> list = new List<int>();
		list.AddRange(os.navigationPath);
		os.write("Uploading Local File " + text2 + "\nto Remote Folder /" + currentFolder.name);
		int num2 = fileEntry.size;
		if (num2 > 150)
		{
			num2 = (int)(((float)num2 - 150f) * 0.2f + 150f);
		}
		for (int i = 0; i < num2 / 300; i++)
		{
			os.writeSingle(".");
			OS.operationProgress = (float)i / (float)(num2 / 1000);
			Thread.Sleep(200);
		}
		os.connectedComp.makeFile(os.thisComputer.ip, fileEntry.name, fileEntry.data, list, isUpload: true);
		os.write("Transfer Complete\n");
	}

	public static void replace2(string[] args, OS os)
	{
		Folder currentFolder = getCurrentFolder(os);
		FileEntry fileEntry = null;
		int num = 2;
		string[] array = Utils.SplitToTokens(args);
		if (array.Length < 3)
		{
			os.write("Not Enough Arguments\n");
			os.write("Usage: replace targetFile.txt \"Target String\" \"Replacement String\"");
			return;
		}
		if (array.Length <= 3)
		{
			if (os.display.command.StartsWith("cat"))
			{
				string text = os.display.commandArgs[1];
				fileEntry = currentFolder.searchForFile(text);
				if (fileEntry != null)
				{
					os.write("Assuming active flag file \"" + text + "\" For editing");
					num = 1;
				}
			}
			if (fileEntry == null)
			{
				os.write("Not Enough Arguments\n");
			}
		}
		if (fileEntry == null)
		{
			for (int i = 0; i < currentFolder.files.Count; i++)
			{
				if (currentFolder.files[i].name.Equals(args[1]))
				{
					fileEntry = currentFolder.files[i];
				}
			}
			if (fileEntry == null)
			{
				os.write("File " + args[1] + " not found.");
				if (os.display.command.StartsWith("cat"))
				{
					string text = os.display.commandArgs[1];
					fileEntry = currentFolder.searchForFile(text);
					if (fileEntry != null)
					{
						os.write("Assuming active file \"" + text + "\" for editing");
					}
				}
				return;
			}
		}
		string oldValue = array[num].Replace("\"", "");
		string newValue = array[num + 1].Replace("\"", "");
		string data = fileEntry.data;
		fileEntry.data = fileEntry.data.Replace(oldValue, newValue);
		if (fileEntry.data.Length > 20000)
		{
			fileEntry.data = data;
			os.write("REPLACE ERROR: Replacement will cause file to be too long.");
		}
		if (os.display.command.ToLower().Equals("cat"))
		{
			os.displayCache = fileEntry.data;
		}
	}

	public static void replace(string[] args, OS os)
	{
		Folder currentFolder = getCurrentFolder(os);
		FileEntry fileEntry = null;
		int num = 2;
		if (args.Length <= 3)
		{
			if (os.display.command.StartsWith("cat"))
			{
				string text = os.display.commandArgs[1];
				fileEntry = currentFolder.searchForFile(text);
				if (fileEntry != null)
				{
					os.write("Assuming active flag file \"" + text + "\" For editing");
					num = 1;
				}
			}
			if (fileEntry == null)
			{
				os.write("Not Enough Arguments\n");
			}
		}
		if (fileEntry == null)
		{
			for (int i = 0; i < currentFolder.files.Count; i++)
			{
				if (currentFolder.files[i].name.Equals(args[1]))
				{
					fileEntry = currentFolder.files[i];
				}
			}
			if (fileEntry == null)
			{
				os.write("File " + args[1] + " not found.");
				if (os.display.command.StartsWith("cat"))
				{
					string text = os.display.commandArgs[1];
					fileEntry = currentFolder.searchForFile(text);
					if (fileEntry != null)
					{
						os.write("Assuming active file \"" + text + "\" for editing");
					}
				}
				return;
			}
		}
		string text2 = "";
		for (int i = num; i < args.Length; i++)
		{
			text2 = text2 + args[i] + " ";
		}
		text2 = text2.Trim();
		if (text2[0] != '"')
		{
			return;
		}
		int num2 = text2.IndexOf('"', 1);
		if (num2 >= 1)
		{
			num2--;
			int length = num2;
			string oldValue = text2.Substring(1, length);
			int startIndex = num2 + 2;
			startIndex = text2.IndexOf(" \"", startIndex);
			if (startIndex > num2)
			{
				int num3 = text2.LastIndexOf('"');
				if (num3 > startIndex)
				{
					startIndex += 2;
					length = num3 - startIndex;
					string newValue = text2.Substring(startIndex, length);
					fileEntry.data = fileEntry.data.Replace(oldValue, newValue);
					if (os.display.command.ToLower().Equals("cat"))
					{
						os.displayCache = fileEntry.data;
					}
					return;
				}
			}
		}
		os.write("Format Error: Target and Replacement strings not found.");
		os.write("Usage: replace targetFile.txt \"Target String\" \"Replacement String\"");
	}

	public static void rm(string[] args, OS os)
	{
		List<FileEntry> list = new List<FileEntry>();
		if (args.Length <= 1)
		{
			os.write("Not Enough Arguments\n");
			return;
		}
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		int num = args[1].LastIndexOf('/');
		string text = args[1];
		string text2 = null;
		if (num > 0 && num < args[1].Length - 1)
		{
			text = args[1].Substring(num + 1);
			text2 = args[1].Substring(0, num);
		}
		Folder folder = getCurrentFolder(os);
		if (text2 != null)
		{
			folder = getFolderAtPath(text2, os, folder, returnsNullOnNoFind: true);
			if (folder == null)
			{
				os.write("Folder " + text2 + " Not found!");
				return;
			}
		}
		if (text.Equals("*"))
		{
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (!list.Contains(folder.files[i]))
				{
					list.Add(folder.files[i]);
				}
			}
		}
		else
		{
			bool flag = false;
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i].name.Equals(text))
				{
					list.Add(folder.files[i]);
					flag = true;
					break;
				}
				if (flag)
				{
					break;
				}
			}
		}
		if (list.Count == 0)
		{
			os.write("File " + text + " not found!");
			return;
		}
		List<int> list2 = new List<int>();
		for (int i = 0; i < os.navigationPath.Count; i++)
		{
			list2.Add(os.navigationPath[i]);
		}
		if (text2 != null)
		{
			list2.AddRange(getNavigationPathAtPath(text2, os, getCurrentFolder(os)));
		}
		for (int i = 0; i < list.Count; i++)
		{
			os.write(LocaleTerms.Loc("Deleting") + " " + list[i].name + ".");
			for (int j = 0; j < Math.Min(Math.Max(list[i].size / 1000, 3), 26); j++)
			{
				Thread.Sleep(200);
				os.writeSingle(".");
			}
			if (!computer.deleteFile(os.thisComputer.ip, list[i].name, list2))
			{
				os.writeSingle(LocaleTerms.Loc("Error - Insufficient Privileges"));
			}
			else
			{
				os.writeSingle(LocaleTerms.Loc("Done"));
			}
		}
		os.write("");
	}

	public static void rm2(string[] args, OS os)
	{
		List<FileEntry> list = new List<FileEntry>();
		if (args.Length <= 1)
		{
			os.write("Not Enough Arguments\n");
			return;
		}
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		bool flag = false;
		string text = args[1];
		if (args[1].ToLower() == "-rf" && args.Length >= 3)
		{
			text = args[2];
			flag = true;
		}
		else
		{
			text = args[1];
		}
		int num = text.LastIndexOf('/');
		string text2 = text;
		string text3 = null;
		if (num > 0 && num < text.Length - 1)
		{
			text2 = text.Substring(num + 1);
			text3 = text.Substring(0, num);
		}
		Folder folder = getCurrentFolder(os);
		if (text3 != null)
		{
			folder = getFolderAtPath(text3, os, folder, returnsNullOnNoFind: true);
			if (folder == null)
			{
				os.write("Folder " + text3 + " Not found!");
				return;
			}
		}
		if (text2.Equals("*") || text2.Equals("*.*"))
		{
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (!list.Contains(folder.files[i]))
				{
					list.Add(folder.files[i]);
				}
			}
		}
		else
		{
			bool flag2 = false;
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i].name.Equals(text2))
				{
					list.Add(folder.files[i]);
					flag2 = true;
					break;
				}
				if (flag2)
				{
					break;
				}
			}
		}
		if (list.Count == 0)
		{
			os.write("File " + text2 + " not found!");
			return;
		}
		List<int> list2 = new List<int>();
		for (int i = 0; i < os.navigationPath.Count; i++)
		{
			list2.Add(os.navigationPath[i]);
		}
		if (text3 != null)
		{
			list2.AddRange(getNavigationPathAtPath(text3, os, getCurrentFolder(os)));
		}
		for (int i = 0; i < list.Count; i++)
		{
			os.write(LocaleTerms.Loc("Deleting") + " " + list[i].name + ".");
			for (int j = 0; j < Math.Min(Math.Max(list[i].size / 1000, 3), 26); j++)
			{
				Thread.Sleep(200);
				os.writeSingle(".");
			}
			if (!computer.deleteFile(os.thisComputer.ip, list[i].name, list2))
			{
				os.writeSingle(LocaleTerms.Loc("Error - Insufficient Privileges"));
			}
			else
			{
				os.writeSingle(LocaleTerms.Loc("Done"));
			}
			if (os.HasExitedAndEnded)
			{
				return;
			}
		}
		os.write("");
	}

	public static void mv(string[] args, OS os)
	{
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		Folder currentFolder = getCurrentFolder(os);
		List<string> list = new List<string>(args);
		for (int i = 0; i < list.Count; i++)
		{
			if (string.IsNullOrWhiteSpace(list[i]))
			{
				list.RemoveAt(i);
				i--;
			}
		}
		List<int> list2 = new List<int>();
		for (int i = 0; i < os.navigationPath.Count; i++)
		{
			list2.Add(os.navigationPath[i]);
		}
		if (list.Count < 3)
		{
			os.write(LocaleTerms.Loc("Not Enough Arguments. Usage: mv [FILE] [DESTINATION]"));
			return;
		}
		char[] array = new char[2] { '\\', '/' };
		string text = list[1];
		string text2 = list[2];
		string text3 = "";
		string path = "";
		int num = text2.LastIndexOf('/');
		if (num > 0)
		{
			text3 = text2.Substring(num + 1);
			path = text2.Substring(0, num + 1);
			if (text3.Length <= 0)
			{
				text3 = text;
			}
		}
		else
		{
			text3 = text2;
		}
		List<int> navigationPathAtPath = getNavigationPathAtPath(path, os);
		navigationPathAtPath.InsertRange(0, list2);
		computer.moveFile(os.thisComputer.ip, text, text3, list2, navigationPathAtPath);
	}

	public static void analyze(string[] args, OS os)
	{
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		if (computer.firewall != null)
		{
			computer.firewall.writeAnalyzePass(os, computer);
			computer.hostileActionTaken();
		}
		else
		{
			os.write("No Firewall Detected");
		}
	}

	public static void solve(string[] args, OS os)
	{
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		if (computer.firewall != null)
		{
			if (args.Length >= 2)
			{
				string attempt = args[1];
				os.write("");
				doDots(30, 60, os);
				if (computer.firewall.attemptSolve(attempt, os))
				{
					os.write("SOLVE " + LocaleTerms.Loc("SUCCESSFUL") + " - Syndicated UDP Traffic Enabled");
				}
				else
				{
					os.write("SOLVE " + LocaleTerms.Loc("FAILED") + " - Incorrect bypass sequence");
				}
			}
			else
			{
				os.write(LocaleTerms.Loc("ERROR: No Solution provided"));
			}
		}
		else
		{
			os.write("No Firewall Detected");
		}
	}

	public static void clear(string[] args, OS os)
	{
		os.terminal.reset();
	}

	public static void execute(string[] args, OS os)
	{
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		Folder folder = os.thisComputer.files.root.folders[2];
		os.write("Available Executables:\n");
		os.write("PortHack");
		os.write("ForkBomb");
		os.write("Shell");
		os.write("Tutorial");
		for (int i = 0; i < folder.files.Count; i++)
		{
			for (int j = 0; j < PortExploits.exeNums.Count; j++)
			{
				if (folder.files[i].data == PortExploits.crackExeData[PortExploits.exeNums[j]] || folder.files[i].data == PortExploits.crackExeDataLocalRNG[PortExploits.exeNums[j]])
				{
					os.write(folder.files[i].name.Replace(".exe", ""));
					break;
				}
			}
		}
		os.write(" ");
	}

	public static void scan(string[] args, OS os)
	{
		if (args.Length > 1)
		{
			Computer computer = null;
			for (int i = 0; i < os.netMap.nodes.Count; i++)
			{
				if (os.netMap.nodes[i].ip.Equals(args[1]) || os.netMap.nodes[i].name.Equals(args[1]))
				{
					computer = os.netMap.nodes[i];
					break;
				}
			}
			if (computer != null)
			{
				os.netMap.discoverNode(computer);
				os.write("Found Terminal : " + computer.name + "@" + computer.ip);
			}
			return;
		}
		Computer computer2 = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		if (os.hasConnectionPermission(admin: true))
		{
			os.write("Scanning...");
			for (int i = 0; i < computer2.links.Count; i++)
			{
				if (!os.netMap.visibleNodes.Contains(computer2.links[i]))
				{
					os.netMap.visibleNodes.Add(computer2.links[i]);
				}
				os.netMap.nodes[computer2.links[i]].highlightFlashTime = 1f;
				os.write("Found Terminal : " + os.netMap.nodes[computer2.links[i]].name + "@" + os.netMap.nodes[computer2.links[i]].ip);
				os.netMap.lastAddedNode = os.netMap.nodes[computer2.links[i]];
				Thread.Sleep(400);
			}
			os.write("Scan Complete\n");
		}
		else
		{
			os.write("Scanning Requires Admin Access\n");
		}
	}

	public static void fastHack(string[] args, OS os)
	{
		if (OS.DEBUG_COMMANDS && os.connectedComp != null)
		{
			os.connectedComp.adminIP = os.thisComputer.ip;
		}
	}

	public static void revealAll(string[] args, OS os)
	{
		for (int i = 0; i < os.netMap.nodes.Count; i++)
		{
			os.netMap.visibleNodes.Add(i);
		}
	}

	public static void cd(string[] args, OS os)
	{
		if (os.hasConnectionPermission(admin: false))
		{
			if (args.Length >= 2)
			{
				Folder currentFolder = getCurrentFolder(os);
				if (args[1].Equals("/"))
				{
					os.navigationPath.Clear();
				}
				if (args[1].Equals(".."))
				{
					if (os.navigationPath.Count > 0)
					{
						os.navigationPath.RemoveAt(os.navigationPath.Count - 1);
					}
					else
					{
						os.display.command = "connect";
						os.validCommand = false;
					}
				}
				else
				{
					if (args[1].StartsWith("/"))
					{
						Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
						currentFolder = computer.files.root;
						os.navigationPath.Clear();
					}
					List<int> navigationPathAtPath = getNavigationPathAtPath(args[1], os);
					for (int i = 0; i < navigationPathAtPath.Count; i++)
					{
						if (navigationPathAtPath[i] == -1)
						{
							os.navigationPath.RemoveAt(os.navigationPath.Count - 1);
						}
						else
						{
							os.navigationPath.Add(navigationPathAtPath[i]);
						}
					}
				}
			}
			else
			{
				os.write("Usage: cd [WHERE TO GO or .. TO GO BACK]");
			}
			string text = "";
			if (os.connectedComp != null)
			{
				text = os.connectedComp.ip + "/";
			}
			for (int i = 0; i < os.navigationPath.Count; i++)
			{
				text = text + getFolderAtDepth(os, i + 1).name + "/";
			}
			text += "> ";
			os.terminal.prompt = text;
		}
		else
		{
			os.write("Insufficient Privileges to Perform Operation");
		}
	}

	public static void probe(string[] args, OS os)
	{
		string text = "";
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		text = computer.ip;
		os.write("Probing " + text + "...\n");
		for (int i = 0; i < 10; i++)
		{
			Thread.Sleep(80);
			os.writeSingle(".");
		}
		os.write("\nProbe Complete - Open ports:\n");
		os.write("---------------------------------");
		for (int i = 0; i < computer.ports.Count; i++)
		{
			os.write("Port#: " + computer.GetDisplayPortNumberFromCodePort(computer.ports[i]) + "  -  " + PortExploits.services[computer.ports[i]] + ((computer.portsOpen[i] > 0) ? " : OPEN" : ""));
			Thread.Sleep(120);
		}
		os.write("---------------------------------");
		os.write("Open Ports Required for Crack : " + Math.Max(computer.portsNeededForCrack + 1, 0));
		if (computer.hasProxy)
		{
			os.write("Proxy Detected : " + (computer.proxyActive ? "ACTIVE" : "INACTIVE"));
		}
		if (computer.firewall != null)
		{
			os.write("Firewall Detected : " + (computer.firewall.solved ? "SOLVED" : "ACTIVE"));
		}
	}

	public static void reboot(string[] args, OS os)
	{
		if (os.hasConnectionPermission(admin: true))
		{
			bool flag = os.connectedComp == null || os.connectedComp == os.thisComputer;
			bool flag2 = false;
			if (args.Length > 1 && args[1].ToLower() == "-i")
			{
				flag2 = true;
			}
			Computer computer = os.connectedComp;
			if (computer == null)
			{
				computer = os.thisComputer;
			}
			if (!flag)
			{
				os.write("Rebooting Connected System : " + computer.name);
			}
			if (!flag2)
			{
				int num = 5;
				while (num > 0)
				{
					os.write("System Reboot in ............" + num);
					num--;
					Thread.Sleep(1000);
					if (os.HasExitedAndEnded)
					{
						return;
					}
				}
			}
			if (!os.HasExitedAndEnded)
			{
				if (computer == null || computer == os.thisComputer)
				{
					os.rebootThisComputer();
				}
				else
				{
					computer.reboot(os.thisComputer.ip);
				}
			}
		}
		else
		{
			os.write("Rebooting requires Admin access");
		}
	}

	public static void addNote(string[] args, OS os)
	{
		string text = "";
		for (int i = 1; i < args.Length; i++)
		{
			text = text + args[i] + " ";
		}
		text = text.Trim().Replace("\\n", "\n");
		NotesExe.AddNoteToOS(text, os);
	}

	public static void opCDTray(string[] args, OS os, bool isOpen)
	{
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		if (os.hasConnectionPermission(admin: true) || computer.ip.Equals(os.thisComputer.ip))
		{
			if (isOpen)
			{
				computer.openCDTray(computer.ip);
			}
			else
			{
				computer.closeCDTray(computer.ip);
			}
		}
		else
		{
			os.write("Insufficient Privileges to Perform Operation");
		}
	}

	[DllImport("winmm.dll")]
	private static extern long mciSendString(string a, StringBuilder b, int c, IntPtr d);

	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void system([MarshalAs(UnmanagedType.LPStr)] string cmd);

	public static void cdDrive(bool open)
	{
		string text = SDL.SDL_GetPlatform();
		if (text.Equals("Linux"))
		{
			system("eject" + (open ? "" : " -t"));
			return;
		}
		if (text.Equals("Windows"))
		{
			mciSendString("set cdaudio door " + (open ? "open" : "closed"), null, 0, IntPtr.Zero);
			return;
		}
		if (text.Equals("Mac OS X"))
		{
			Console.WriteLine("CD drive interop unsupported on OSX!");
			return;
		}
		throw new NotSupportedException("Unhandled SDL2 platform!");
	}

	public static void sudo(OS os, Action action)
	{
		string adminIP = os.connectedComp.adminIP;
		os.connectedComp.adminIP = os.thisComputer.ip;
		action?.Invoke();
		os.connectedComp.adminIP = adminIP;
	}

	public static Folder getCurrentFolder(OS os)
	{
		return getFolderAtDepth(os, os.navigationPath.Count);
	}

	public static Folder getFolderAtDepth(OS os, int depth)
	{
		Folder folder = ((os.connectedComp != null) ? os.connectedComp.files.root : os.thisComputer.files.root);
		if (os.navigationPath.Count > 0)
		{
			try
			{
				for (int i = 0; i < depth; i++)
				{
					if (folder.folders.Count > os.navigationPath[i])
					{
						folder = folder.folders[os.navigationPath[i]];
					}
				}
			}
			catch
			{
			}
		}
		return folder;
	}

	public static bool computerExists(OS os, string ip)
	{
		for (int i = 0; i < os.netMap.nodes.Count; i++)
		{
			if (os.netMap.nodes[i].ip.Equals(ip) || os.netMap.nodes[i].name.Equals(ip))
			{
				return true;
			}
		}
		return false;
	}

	public static Computer getComputer(OS os, string ip_Or_ID_or_Name)
	{
		for (int i = 0; i < os.netMap.nodes.Count; i++)
		{
			if (os.netMap.nodes[i].ip.Equals(ip_Or_ID_or_Name) || os.netMap.nodes[i].idName.Equals(ip_Or_ID_or_Name) || os.netMap.nodes[i].name.Equals(ip_Or_ID_or_Name))
			{
				return os.netMap.nodes[i];
			}
		}
		return null;
	}

	public static Folder getFolderAtPath(string path, OS os, Folder rootFolder = null, bool returnsNullOnNoFind = false)
	{
		Folder folder = ((rootFolder != null) ? rootFolder : getCurrentFolder(os));
		char[] separator = new char[2] { '/', '\\' };
		string[] array = path.Split(separator);
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == "" || array[i] == " ")
			{
				continue;
			}
			if (array[i] == "..")
			{
				num++;
				folder = getFolderAtDepth(os, os.navigationPath.Count - num);
				continue;
			}
			bool flag = false;
			for (int j = 0; j < folder.folders.Count; j++)
			{
				if (folder.folders[j].name == array[i])
				{
					folder = folder.folders[j];
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				os.write("Invalid Path");
				if (!returnsNullOnNoFind)
				{
					return getCurrentFolder(os);
				}
				return null;
			}
		}
		return folder;
	}

	public static Folder getFolderAtPathAsFarAsPossible(string path, OS os, Folder rootFolder)
	{
		Folder folder = ((rootFolder != null) ? rootFolder : getCurrentFolder(os));
		char[] separator = new char[2] { '/', '\\' };
		string[] array = path.Split(separator);
		int num = 0;
		if (path == "/")
		{
			Computer computer = os.connectedComp;
			if (computer == null)
			{
				computer = os.thisComputer;
			}
			return computer.files.root;
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == "" || array[i] == " ")
			{
				continue;
			}
			if (array[i] == "..")
			{
				num++;
				folder = getFolderAtDepth(os, os.navigationPath.Count - num);
				continue;
			}
			for (int j = 0; j < folder.folders.Count; j++)
			{
				if (folder.folders[j].name == array[i])
				{
					folder = folder.folders[j];
					break;
				}
			}
		}
		return folder;
	}

	public static Folder getFolderAtPathAsFarAsPossible(string path, OS os, Folder rootFolder, out string likelyFilename)
	{
		Folder folder = ((rootFolder != null) ? rootFolder : getCurrentFolder(os));
		char[] separator = new char[2] { '/', '\\' };
		string[] array = path.Split(separator);
		int num = 0;
		likelyFilename = null;
		if (path == "/")
		{
			Computer computer = os.connectedComp;
			if (computer == null)
			{
				computer = os.thisComputer;
			}
			return computer.files.root;
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == "" || array[i] == " ")
			{
				continue;
			}
			if (array[i] == "..")
			{
				num++;
				folder = getFolderAtDepth(os, os.navigationPath.Count - num);
				continue;
			}
			bool flag = false;
			for (int j = 0; j < folder.folders.Count; j++)
			{
				if (folder.folders[j].name == array[i])
				{
					folder = folder.folders[j];
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				likelyFilename = array[i];
			}
		}
		return folder;
	}

	public static List<int> getNavigationPathAtPath(string path, OS os, Folder currentFolder = null)
	{
		List<int> list = new List<int>();
		Folder folder = ((currentFolder != null) ? currentFolder : getCurrentFolder(os));
		char[] separator = new char[2] { '/', '\\' };
		string[] array = path.Split(separator);
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == "" || array[i] == " ")
			{
				continue;
			}
			if (array[i] == "..")
			{
				list.Add(-1);
				num++;
				folder = getFolderAtDepth(os, os.navigationPath.Count - num);
				continue;
			}
			bool flag = false;
			for (int j = 0; j < folder.folders.Count; j++)
			{
				if (folder.folders[j].name == array[i])
				{
					folder = folder.folders[j];
					flag = true;
					list.Add(j);
					break;
				}
			}
			if (!flag)
			{
				os.write("Invalid Path");
				list.Clear();
				return list;
			}
		}
		return list;
	}

	public static Folder getFolderFromNavigationPath(List<int> path, Folder startFolder, OS os)
	{
		Folder folder = startFolder;
		Folder folder2 = startFolder;
		for (int i = 0; i < path.Count; i++)
		{
			if (path[i] <= -1)
			{
				folder = folder2;
				continue;
			}
			if (folder.folders.Count > path[i])
			{
				folder2 = folder;
				folder = folder.folders[path[i]];
				continue;
			}
			os.write("Invalid Path");
			return folder;
		}
		return folder;
	}
}
