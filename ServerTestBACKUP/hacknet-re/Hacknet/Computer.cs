using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Hacknet.Security;
using Microsoft.Xna.Framework;

namespace Hacknet;

[Serializable]
internal class Computer
{
	public const byte CORPORATE = 1;

	public const byte HOME = 2;

	public const byte SERVER = 3;

	public const byte EMPTY = 4;

	public const byte EOS = 5;

	public static float BASE_BOOT_TIME = (Settings.isConventionDemo ? 15f : 25.5f);

	public static float BASE_REBOOT_TIME = 10.5f;

	public static float BASE_PROXY_TICKS = 30f;

	public static float BASE_TRACE_TIME = 15f;

	public string name;

	public string idName;

	public string ip;

	public Vector2 location;

	public FileSystem files;

	public int securityLevel;

	public float traceTime;

	public int portsNeededForCrack = 0;

	public string adminIP;

	public List<UserDetail> users;

	public List<int> links;

	public List<int> ports;

	public List<byte> portsOpen;

	public bool silent = false;

	public bool disabled = false;

	internal float bootTimer = 0f;

	public bool userLoggedIn = false;

	public UserDetail currentUser;

	public Dictionary<int, int> PortRemapping = null;

	public string icon = null;

	private float timeLastPinged;

	public float highlightFlashTime = 0f;

	public byte type;

	public string adminPass;

	public List<Daemon> daemons;

	public bool AllowsDefaultBootModule = true;

	public bool hasProxy = false;

	public float proxyOverloadTicks = 0f;

	public float startingOverloadTicks = -1f;

	public bool proxyActive = false;

	public ShellExe reportingShell = null;

	public ExternalCounterpart externalCounterpart = null;

	public string attatchedDeviceIDs = null;

	public Firewall firewall = null;

	public bool firewallAnalysisInProgress = false;

	public bool HasTracker = false;

	public Administrator admin = null;

	private OS os;

	public MemoryContents Memory;

	public Computer(string compName, string compIP, Vector2 compLocation, int seclevel, byte compType, OS opSystem)
	{
		name = compName;
		ip = compIP;
		location = compLocation;
		type = compType;
		files = generateRandomFileSystem();
		idName = compName.Replace(" ", "_");
		os = opSystem;
		traceTime = -1f;
		securityLevel = seclevel;
		adminIP = NetworkMap.generateRandomIP();
		users = new List<UserDetail>();
		adminPass = PortExploits.getRandomPassword();
		users.Add(new UserDetail("admin", adminPass, 1));
		ports = new List<int>(seclevel);
		portsOpen = new List<byte>(seclevel);
		openPortsForSecurityLevel(seclevel);
		links = new List<int>();
		daemons = new List<Daemon>();
	}

	public void initDaemons()
	{
		for (int i = 0; i < daemons.Count; i++)
		{
			daemons[i].initFiles();
			if (daemons[i].isListed)
			{
				daemons[i].registerAsDefaultBootDaemon();
			}
		}
	}

	public FileSystem generateRandomFileSystem()
	{
		FileSystem fileSystem = new FileSystem();
		if (type != 5 && type != 4)
		{
			int num = Utils.random.Next(6);
			for (int i = 0; i < num; i++)
			{
				string text = null;
				int num2 = 0;
				do
				{
					text = ((num2 > 10) ? "AA" : "") + generateFolderName(Utils.random.Next(100));
					num2++;
				}
				while (fileSystem.root.folders[0].searchForFolder(text) != null);
				Folder folder = new Folder(text);
				int num3 = Utils.random.Next(3);
				for (int j = 0; j < num3; j++)
				{
					if (Utils.random.NextDouble() > 0.8)
					{
						string text2 = null;
						num2 = 0;
						do
						{
							text2 = generateFileName(Utils.random.Next(300));
							num2++;
							if (num2 > 3)
							{
								text2 = Utils.getRandomChar() + Utils.getRandomChar() + text2;
							}
						}
						while (folder.searchForFile(text2) != null);
						folder.files.Add(new FileEntry(Utils.flipCoin() ? generateFileData(Utils.random.Next(500)) : generateBinaryString(500), text2));
					}
					else
					{
						FileEntry fileEntry = new FileEntry();
						string text3 = fileEntry.name;
						while (folder.searchForFile(fileEntry.name) != null)
						{
							fileEntry.name = Utils.getRandomChar() + Utils.getRandomChar() + text3;
						}
						folder.files.Add(fileEntry);
					}
				}
				fileSystem.root.folders[0].folders.Add(folder);
			}
		}
		else if (type == 5)
		{
			fileSystem.root.folders.Insert(0, EOSComp.GenerateEOSFolder());
		}
		return fileSystem;
	}

	public void openPortsForSecurityLevel(int security)
	{
		portsNeededForCrack = security - 1;
		if (security >= 5)
		{
			portsNeededForCrack--;
			float num = 0f;
			for (int i = 4; i < security; i++)
			{
				num += BASE_PROXY_TICKS / (float)(i - 3);
			}
			addProxy(num);
		}
		switch (security)
		{
		default:
			openPorts(PortExploits.portNums.Count);
			break;
		case 1:
			openPorts(PortExploits.portNums.Count - 1);
			break;
		}
		if (security >= 4)
		{
			traceTime = (float)Math.Max(10 - security, 3) * BASE_TRACE_TIME;
		}
		if (security >= 5)
		{
			firewall = new Firewall(security - 5);
			admin = new BasicAdministrator();
		}
	}

	private void openPorts(int n)
	{
		int num = 4;
		for (int num2 = num - 1; num2 >= 0; num2--)
		{
			ports.Add(PortExploits.portNums[num2]);
			portsOpen.Add(0);
		}
	}

	public void addProxy(float time)
	{
		if (time > 0f)
		{
			hasProxy = true;
			proxyActive = true;
			proxyOverloadTicks = time;
			startingOverloadTicks = proxyOverloadTicks;
		}
	}

	public void addFirewall(int level)
	{
		firewall = new Firewall(level);
	}

	public void addFirewall(int level, string solution)
	{
		firewall = new Firewall(level, solution);
	}

	public void addFirewall(int level, string solution, float additionalTime)
	{
		firewall = new Firewall(level, solution, additionalTime);
	}

	public void addMultiplayerTargetFile()
	{
		files.root.folders[0].files.Add(new FileEntry("#CRITICAL SYSTEM FILE - DO NOT MODIFY#\n\n" + generateBinaryString(2000), "system32.sys"));
	}

	private void sendNetworkMessage(string s)
	{
		if (os.multiplayer && !silent)
		{
			os.sendMessage(s);
		}
		if (externalCounterpart != null)
		{
			externalCounterpart.writeMessage(s);
		}
	}

	private void tryExternalCounterpartDisconnect()
	{
		if (externalCounterpart != null)
		{
			externalCounterpart.disconnect();
		}
	}

	public void hostileActionTaken()
	{
		if (os.connectedComp != null && os.connectedComp.ip.Equals(ip))
		{
			if (traceTime > 0f)
			{
				os.traceTracker.start(traceTime);
			}
			if (os.timer - timeLastPinged > 0.35f)
			{
				SFX.addCircle(getScreenSpacePosition(), os.brightLockedColor, 25f);
				timeLastPinged = os.timer;
			}
		}
	}

	public void bootupTick(float t)
	{
		bootTimer -= t;
		if (bootTimer <= 0f)
		{
			disabled = false;
		}
	}

	public void log(string message)
	{
		if (disabled)
		{
			return;
		}
		if (reportingShell != null)
		{
			reportingShell.reportedTo(message);
		}
		message = "@" + (int)OS.currentElapsedTime + " " + message;
		string text = message;
		if (text.Length > 256)
		{
			text = text.Substring(0, 256);
		}
		bool flag = false;
		string text2 = text.Replace(" ", "_");
		int num = 0;
		Folder folder = files.root.searchForFolder("log");
		do
		{
			flag = false;
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i] != null && folder.files[i].name == text2)
				{
					flag = true;
					num++;
					text2 = (text + "_" + num).Replace(" ", "_");
				}
			}
		}
		while (flag);
		text = text2;
		files.root.searchForFolder("log").files.Insert(0, new FileEntry(message, text));
	}

	public string generateFolderName(int seed)
	{
		return "NewFolder" + seed;
	}

	public string generateFileName(int seed)
	{
		return "Data" + seed;
	}

	public string generateFileData(int seed)
	{
		string text = "";
		for (int i = 0; i < seed; i++)
		{
			text = text + " " + i;
		}
		return text;
	}

	public bool connect(string ipFrom)
	{
		if (disabled)
		{
			return false;
		}
		WhitelistConnectionDaemon whitelistConnectionDaemon = (WhitelistConnectionDaemon)getDaemon(typeof(WhitelistConnectionDaemon));
		if (whitelistConnectionDaemon != null && ipFrom == os.thisComputer.ip && !whitelistConnectionDaemon.IPCanPassWhitelist(ipFrom, isFromRemote: false))
		{
			whitelistConnectionDaemon.DisconnectTarget();
			return false;
		}
		log("Connection: from " + ipFrom);
		sendNetworkMessage("cConnection " + ip + " " + ipFrom);
		if (externalCounterpart != null)
		{
			externalCounterpart.establishConnection();
		}
		userLoggedIn = false;
		return true;
	}

	public void addNewUser(string ipFrom, string name, string pass, byte type)
	{
		addNewUser(ipFrom, new UserDetail(name, pass, type));
	}

	public void addNewUser(string ipFrom, UserDetail usr)
	{
		users.Add(usr);
		if (!silent)
		{
			log("User Account Added: from " + ipFrom + " -Name: " + name);
		}
		sendNetworkMessage("cAddUser #" + ip + "#" + ipFrom + "#" + name + "#" + usr.pass + "#" + usr.type);
		for (int i = 0; i < daemons.Count; i++)
		{
			daemons[i].userAdded(usr.name, usr.pass, usr.type);
		}
	}

	public void crash(string ipFrom)
	{
		if (os.connectedComp != null && os.connectedComp.Equals(this) && !os.connectedComp.Equals(os.thisComputer))
		{
			bool flag = os.connectedComp.silent;
			Computer connectedComp = os.connectedComp;
			connectedComp.silent = true;
			os.connectedComputerCrashed(this);
			connectedComp.silent = flag;
		}
		else if (os.thisComputer.Equals(this))
		{
			os.thisComputerCrashed();
		}
		if (!silent)
		{
			log("CRASH REPORT: Kernel Panic -- Fatal Trap");
		}
		disabled = true;
		bootTimer = BASE_BOOT_TIME;
		tryExternalCounterpartDisconnect();
		PostProcessor.dangerModeEnabled = false;
		sendNetworkMessage("cCrash " + ip + " " + ipFrom);
	}

	public void reboot(string ipFrom)
	{
		if (os.connectedComp != null && os.connectedComp.Equals(this) && !os.connectedComp.Equals(os.thisComputer))
		{
			os.connectedComputerCrashed(this);
		}
		else if (os.thisComputer.Equals(this) || os.thisComputer.Equals(os.connectedComp))
		{
			os.rebootThisComputer();
		}
		if (!silent)
		{
			log("Rebooting system : " + ipFrom);
		}
		disabled = true;
		bootTimer = BASE_REBOOT_TIME;
		tryExternalCounterpartDisconnect();
		sendNetworkMessage("cReboot " + ip + " " + ipFrom);
	}

	public bool canReadFile(string ipFrom, FileEntry f, int index)
	{
		bool flag = false;
		if (ipFrom.Equals(adminIP))
		{
			flag = true;
		}
		if (ipFrom == os.thisComputer.ip && currentUser.name != null && currentUser.type == 0)
		{
			flag = true;
		}
		else
		{
			for (int i = 0; i < users.Count; i++)
			{
				if (ipFrom.Equals(users[i]))
				{
					flag = true;
				}
			}
		}
		if (!flag)
		{
			return false;
		}
		if (f.name[0] != '@')
		{
			log("FileRead: by " + ipFrom + " - file:" + f.name);
			sendNetworkMessage("cFile " + ip + " " + ipFrom + " " + f.name + " " + index);
		}
		return true;
	}

	public bool canCopyFile(string ipFrom, string name)
	{
		if (currentUser.type != 0 && !silent && !ipFrom.Equals(adminIP))
		{
			return false;
		}
		log("FileCopied: by " + ipFrom + " - file:" + name);
		string s = "cCopy " + ip + " " + ipFrom + " " + name;
		sendNetworkMessage(s);
		return true;
	}

	public bool deleteFile(string ipFrom, string name, List<int> folderPath)
	{
		bool flag = false;
		if (currentUser.type == 1 || currentUser.type == 0)
		{
			flag = true;
		}
		if (!flag && !silent && !ipFrom.Equals(adminIP) && !ipFrom.Equals(ip))
		{
			return false;
		}
		if (name == "*")
		{
			Folder folder = files.root;
			if (folderPath.Count > 0)
			{
				folder = Programs.getFolderFromNavigationPath(folderPath, folder, os);
			}
			bool flag2 = true;
			List<string> list = new List<string>();
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i] != null && !string.IsNullOrWhiteSpace(folder.files[i].name))
				{
					list.Add(folder.files[i].name);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				flag2 &= deleteFile(ipFrom, list[i], folderPath);
			}
			return flag2;
		}
		if (name[0] != '@')
		{
			log("FileDeleted: by " + ipFrom + " - file:" + name);
		}
		Folder folder2 = files.root;
		if (folderPath.Count > 0)
		{
			folder2 = Programs.getFolderFromNavigationPath(folderPath, folder2, os);
		}
		string value = name;
		if (os.multiplayer && name[0] == '@')
		{
			value = name.Substring(name.IndexOf('_'));
		}
		for (int i = 0; i < folder2.files.Count; i++)
		{
			string text = folder2.files[i].name;
			bool flag3 = false;
			if (os.multiplayer && name[0] == '@')
			{
				text = text.Substring(text.IndexOf('_'));
				flag3 = text.Equals(value);
			}
			else
			{
				flag3 = text.Equals(name);
			}
			if (flag3)
			{
				folder2.files.RemoveAt(i);
				i--;
			}
		}
		string text2 = "cDelete #" + ip + "#" + ipFrom + "#" + name;
		for (int i = 0; i < folderPath.Count; i++)
		{
			text2 = text2 + "#" + folderPath[i];
		}
		sendNetworkMessage(text2);
		return true;
	}

	public bool moveFile(string ipFrom, string name, string newName, List<int> folderPath, List<int> destFolderPath)
	{
		if (currentUser.type != 0 && !silent && !ipFrom.Equals(adminIP) && !ipFrom.Equals(ip))
		{
			return false;
		}
		Folder root = files.root;
		root = Programs.getFolderFromNavigationPath(folderPath, files.root, os);
		Folder folder = Programs.getFolderFromNavigationPath(destFolderPath, files.root, os);
		if (newName.StartsWith("/"))
		{
			if (destFolderPath.Count == 0 || (folderPath.Count > 0 && destFolderPath.Count == folderPath.Count && destFolderPath[0] == folderPath[0]))
			{
				folder = files.root;
				newName = newName.Substring(1);
				Folder folder2 = folder.searchForFolder(newName);
				if (folder2 != null)
				{
					folder = folder2;
					newName = name;
				}
			}
			else
			{
				newName = newName.Substring(1);
			}
		}
		FileEntry fileEntry = null;
		for (int i = 0; i < root.files.Count; i++)
		{
			if (root.files[i].name == name)
			{
				fileEntry = root.files[i];
				root.files.RemoveAt(i);
				break;
			}
		}
		if (fileEntry == null)
		{
			os.write("File not Found");
			return false;
		}
		if (newName == "" || newName == " ")
		{
			newName = name;
		}
		fileEntry.name = newName;
		string text = fileEntry.name;
		int num = 1;
		while (folder.searchForFile(fileEntry.name) != null)
		{
			fileEntry.name = text + "(" + num + ")";
			num++;
		}
		folder.files.Add(fileEntry);
		string text2 = "cMove #" + ip + "#" + ipFrom + "#" + name + "#" + newName + "#";
		for (int i = 0; i < folderPath.Count; i++)
		{
			text2 = text2 + "%" + folderPath[i];
		}
		text2 += "#";
		for (int i = 0; i < destFolderPath.Count; i++)
		{
			text2 = text2 + "%" + destFolderPath[i];
		}
		sendNetworkMessage(text2);
		log("FileMoved: by " + ipFrom + " - file:" + name + " To: " + newName);
		return true;
	}

	public bool makeFile(string ipFrom, string name, string data, List<int> folderPath, bool isUpload = false)
	{
		if (!isUpload && !silent && !ipFrom.Equals(adminIP) && !ipFrom.Equals(ip))
		{
			return false;
		}
		if (name[0] != '@')
		{
			log("FileCreated: by " + ipFrom + " - file:" + name);
		}
		Folder folder = files.root;
		if (folderPath.Count > 0)
		{
			for (int i = 0; i < folderPath.Count; i++)
			{
				if (folder.folders.Count > folderPath[i])
				{
					folder = folder.folders[folderPath[i]];
				}
			}
		}
		if (isUpload)
		{
			folder.files.Insert(0, new FileEntry(data, name));
		}
		else
		{
			folder.files.Add(new FileEntry(data, name));
		}
		string text = "cMake #" + ip + "#" + ipFrom + "#" + name + "#" + data;
		for (int i = 0; i < folderPath.Count; i++)
		{
			text = text + "#" + folderPath[i];
		}
		sendNetworkMessage(text);
		return true;
	}

	public bool makeFolder(string ipFrom, string name, List<int> folderPath)
	{
		if (!silent && !ipFrom.Equals(adminIP) && !ipFrom.Equals(ip))
		{
			return false;
		}
		if (name[0] != '@')
		{
			log("FolderCreated: by " + ipFrom + " - folder:" + name);
		}
		Folder folder = files.root;
		if (folderPath.Count > 0)
		{
			for (int i = 0; i < folderPath.Count; i++)
			{
				if (folder.folders.Count > folderPath[i])
				{
					folder = folder.folders[folderPath[i]];
				}
			}
		}
		folder.folders.Add(new Folder(name));
		string text = "cMkDir #" + ip + "#" + ipFrom + "#" + name;
		for (int i = 0; i < folderPath.Count; i++)
		{
			text = text + "#" + folderPath[i];
		}
		sendNetworkMessage(text);
		return true;
	}

	public void disconnecting(string ipFrom, bool externalDisconnectToo = true)
	{
		if (!silent)
		{
			log(ipFrom + " Disconnected");
		}
		if (os.multiplayer && !silent)
		{
			sendNetworkMessage("cDisconnect " + ip + " " + ipFrom);
		}
		if (externalDisconnectToo)
		{
			tryExternalCounterpartDisconnect();
		}
	}

	public void giveAdmin(string ipFrom)
	{
		adminIP = ipFrom;
		log(ipFrom + " Became Admin");
		if (os.multiplayer && !silent)
		{
			sendNetworkMessage("cAdmin " + ip + " " + ipFrom);
		}
		UserDetail value = users[0];
		value.known = true;
		users[0] = value;
	}

	public void openPort(int portNum, string ipFrom)
	{
		portNum = GetCodePortNumberFromDisplayPort(portNum);
		int num = -1;
		for (int i = 0; i < ports.Count; i++)
		{
			if (ports[i] == portNum)
			{
				num = i;
				break;
			}
		}
		if (num != -1)
		{
			portsOpen[num] = 1;
		}
		log(ipFrom + " Opened Port#" + portNum);
		if (!silent)
		{
			sendNetworkMessage("cPortOpen " + ip + " " + ipFrom + " " + portNum);
		}
	}

	public void closePort(int portNum, string ipFrom)
	{
		portNum = GetCodePortNumberFromDisplayPort(portNum);
		int num = -1;
		for (int i = 0; i < ports.Count; i++)
		{
			if (ports[i] == portNum)
			{
				num = i;
			}
		}
		bool flag = false;
		if (num != -1)
		{
			flag = portsOpen[num] != 0;
			portsOpen[num] = 0;
		}
		if (flag)
		{
			log(ipFrom + " Closed Port#" + portNum);
		}
		if (!silent)
		{
			sendNetworkMessage("cPortClose " + ip + " " + ipFrom + " " + portNum);
		}
	}

	public bool isPortOpen(int portNum)
	{
		for (int i = 0; i < ports.Count; i++)
		{
			if (ports[i] == portNum)
			{
				return portsOpen[i] > 0;
			}
		}
		return false;
	}

	public void openCDTray(string ipFrom)
	{
		if (os.thisComputer.ip.Equals(ip))
		{
			Programs.cdDrive(open: true);
		}
		sendNetworkMessage("cCDDrive " + ip + " open");
	}

	public void closeCDTray(string ipFrom)
	{
		if (os.thisComputer.ip.Equals(ip))
		{
			Programs.cdDrive(open: false);
		}
		sendNetworkMessage("cCDDrive " + ip + " close");
	}

	public void forkBombClients(string ipFrom)
	{
		sendNetworkMessage("cFBClients " + ip + " " + ipFrom);
		if (os.multiplayer)
		{
			return;
		}
		for (int i = 0; i < os.ActiveHackers.Count; i++)
		{
			if (os.ActiveHackers[i].Value == ip)
			{
				string key = os.ActiveHackers[i].Key;
				Computer computer = Programs.getComputer(os, key);
				computer.crash(ip);
			}
		}
	}

	public virtual int login(string username, string password, byte type = 1)
	{
		if (username.ToLower().Equals("admin") && password.Equals(adminPass))
		{
			giveAdmin(os.thisComputer.ip);
			return 1;
		}
		for (int i = 0; i < users.Count; i++)
		{
			if (users[i].name.Equals(username) && users[i].pass.Equals(password) && (users[i].type == type || type == 1))
			{
				currentUser = users[i];
				return 2;
			}
		}
		return 0;
	}

	public int GetDisplayPortNumberFromCodePort(int codePort)
	{
		if (PortRemapping == null || !PortRemapping.ContainsKey(codePort))
		{
			return codePort;
		}
		return PortRemapping[codePort];
	}

	public int GetCodePortNumberFromDisplayPort(int displayPort)
	{
		if (PortRemapping == null)
		{
			return displayPort;
		}
		foreach (KeyValuePair<int, int> item in PortRemapping)
		{
			if (item.Value == displayPort)
			{
				return item.Key;
			}
		}
		return displayPort;
	}

	public void setAdminPassword(string newPass)
	{
		adminPass = newPass;
		for (int i = 0; i < users.Count; i++)
		{
			if (users[i].name.ToLower().Equals("admin"))
			{
				users[i] = new UserDetail("admin", newPass, 0);
			}
		}
	}

	public string getSaveString()
	{
		string text = "none";
		if (os.netMap.mailServer.Equals(this))
		{
			text = "mail";
		}
		if (os.thisComputer.Equals(this))
		{
			text = "player";
		}
		string text2 = ((attatchedDeviceIDs == null) ? "" : (" devices=\"" + attatchedDeviceIDs + "\""));
		string text3 = (HasTracker ? " tracker=\"true\" " : "");
		string text4 = "<computer name=\"" + name + "\" ip=\"" + ip + "\" type=\"" + type + "\" spec=\"" + text + "\" id=\"" + idName + "\" " + ((icon == null) ? "" : ("icon=\"" + icon + "\"")) + text2 + text3 + " >\n";
		string text5 = text4;
		text4 = text5 + "<location x=\"" + location.X.ToString(CultureInfo.InvariantCulture) + "\" y=\"" + location.Y.ToString(CultureInfo.InvariantCulture) + "\" />";
		object obj = text4;
		text4 = string.Concat(obj, "<security level=\"", securityLevel, "\" traceTime=\"", traceTime.ToString(CultureInfo.InvariantCulture), (startingOverloadTicks > 0f) ? ("\" proxyTime=\"" + (hasProxy ? startingOverloadTicks.ToString(CultureInfo.InvariantCulture) : "-1")) : "", "\" portsToCrack=\"", portsNeededForCrack, "\" adminIP=\"", adminIP, "\" />");
		text4 = text4 + "<admin type=\"" + ((admin == null) ? "none" : ((admin is FastBasicAdministrator) ? "fast" : ((admin is FastProgressOnlyAdministrator) ? "progress" : "basic")));
		text4 = text4 + "\" resetPass=\"" + ((admin != null && admin.ResetsPassword) ? "true" : "false") + "\"";
		text4 = text4 + " isSuper=\"" + ((admin != null && admin.IsSuper) ? "true" : "false") + "\" />";
		string text6 = "";
		for (int i = 0; i < links.Count; i++)
		{
			text6 = text6 + " " + links[i];
		}
		text4 = text4 + "<links>" + text6 + "</links>\n";
		if (firewall != null)
		{
			text4 = text4 + firewall.getSaveString() + "\n";
		}
		string text7 = "";
		for (int i = 0; i < portsOpen.Count; i++)
		{
			text7 = text7 + " " + ports[i];
		}
		text4 = text4 + "<portsOpen>" + text7 + "</portsOpen>\n";
		if (PortRemapping != null)
		{
			text4 += PortRemappingSerializer.GetSaveString(PortRemapping);
		}
		text4 += "<users>\n";
		for (int i = 0; i < users.Count; i++)
		{
			text4 = text4 + users[i].getSaveString() + "\n";
		}
		text4 += "</users>\n";
		if (Memory != null)
		{
			text4 += Memory.GetSaveString();
		}
		text4 += "<daemons>\n";
		for (int i = 0; i < daemons.Count; i++)
		{
			text4 = text4 + daemons[i].getSaveString() + "\n";
		}
		text4 += "</daemons>\n";
		text4 += files.getSaveString();
		return text4 + "</computer>\n";
	}

	public static Computer load(XmlReader reader, OS os)
	{
		while (reader.Name != "computer")
		{
			reader.Read();
		}
		reader.MoveToAttribute("name");
		string compName = reader.ReadContentAsString();
		reader.MoveToAttribute("ip");
		string compIP = reader.ReadContentAsString();
		reader.MoveToAttribute("type");
		byte compType = (byte)reader.ReadContentAsInt();
		reader.MoveToAttribute("spec");
		string text = reader.ReadContentAsString();
		reader.MoveToAttribute("id");
		string text2 = reader.ReadContentAsString();
		string text3 = null;
		if (reader.MoveToAttribute("devices"))
		{
			text3 = reader.ReadContentAsString();
		}
		string text4 = null;
		if (reader.MoveToAttribute("icon"))
		{
			text4 = reader.ReadContentAsString();
		}
		bool hasTracker = false;
		if (reader.MoveToAttribute("tracker"))
		{
			hasTracker = reader.ReadContentAsString().ToLower() == "true";
		}
		while (reader.Name != "location")
		{
			reader.Read();
		}
		reader.MoveToAttribute("x");
		float x = reader.ReadContentAsFloat();
		reader.MoveToAttribute("y");
		float y = reader.ReadContentAsFloat();
		while (reader.Name != "security")
		{
			reader.Read();
		}
		int num = 1;
		reader.MoveToAttribute("level");
		num = reader.ReadContentAsInt();
		float num2 = -1f;
		reader.MoveToAttribute("traceTime");
		num2 = reader.ReadContentAsFloat();
		int num3 = 0;
		reader.MoveToAttribute("portsToCrack");
		num3 = reader.ReadContentAsInt();
		string text5 = "192.167.1.111";
		reader.MoveToAttribute("adminIP");
		text5 = reader.ReadContentAsString();
		float num4 = -1f;
		if (reader.MoveToAttribute("proxyTime"))
		{
			num4 = reader.ReadContentAsFloat();
		}
		while (reader.Name != "admin")
		{
			reader.Read();
		}
		string text6 = "none";
		reader.MoveToAttribute("type");
		text6 = reader.ReadContentAsString();
		bool flag = false;
		reader.MoveToAttribute("resetPass");
		flag = reader.ReadContentAsBoolean();
		bool flag2 = false;
		reader.MoveToAttribute("isSuper");
		flag2 = reader.ReadContentAsBoolean();
		Administrator administrator = null;
		switch (text6)
		{
		case "fast":
			administrator = new FastBasicAdministrator();
			break;
		case "basic":
			administrator = new BasicAdministrator();
			break;
		case "progress":
			administrator = new FastProgressOnlyAdministrator();
			break;
		}
		if (administrator != null)
		{
			administrator.ResetsPassword = flag;
		}
		if (administrator != null)
		{
			administrator.IsSuper = flag2;
		}
		while (reader.Name != "links")
		{
			reader.Read();
		}
		string text7 = reader.ReadElementContentAsString();
		string[] array = text7.Split();
		Firewall firewall = null;
		while (reader.Name != "portsOpen" && reader.Name != "firewall")
		{
			reader.Read();
		}
		if (reader.Name == "firewall")
		{
			firewall = Firewall.load(reader);
		}
		while (reader.Name != "portsOpen")
		{
			reader.Read();
		}
		string text8 = reader.ReadElementContentAsString();
		Computer computer = new Computer(compName, compIP, new Vector2(x, y), num, compType, os);
		computer.firewall = firewall;
		computer.admin = administrator;
		computer.HasTracker = hasTracker;
		if (num4 > 0f)
		{
			computer.addProxy(num4);
		}
		else
		{
			computer.hasProxy = false;
			computer.proxyActive = false;
		}
		while (reader.Name != "users" && reader.Name != "portRemap")
		{
			reader.Read();
		}
		if (reader.Name == "portRemap")
		{
			reader.MoveToContent();
			string input = reader.ReadElementContentAsString();
			computer.PortRemapping = PortRemappingSerializer.Deserialize(input);
		}
		while (reader.Name != "users")
		{
			reader.Read();
		}
		computer.users.Clear();
		while (true)
		{
			bool flag3 = true;
			if (reader.Name == "users" && reader.NodeType == XmlNodeType.EndElement)
			{
				break;
			}
			if (reader.Name == "user")
			{
				UserDetail item = UserDetail.loadUserDetail(reader);
				if (item.name.ToLower() == "admin")
				{
					computer.adminPass = item.pass;
				}
				computer.users.Add(item);
			}
			reader.Read();
		}
		while (reader.Name != "Memory" && reader.Name != "daemons")
		{
			reader.Read();
		}
		if (reader.Name == "Memory")
		{
			MemoryContents memory = MemoryContents.Deserialize(reader);
			computer.Memory = memory;
			reader.Read();
		}
		while (reader.Name != "daemons")
		{
			reader.Read();
		}
		reader.Read();
		while (true)
		{
			bool flag3 = true;
			if (reader.Name == "daemons" && reader.NodeType == XmlNodeType.EndElement)
			{
				break;
			}
			if (reader.Name == "MailServer")
			{
				reader.MoveToAttribute("name");
				string text9 = reader.ReadContentAsString();
				MailServer mailServer = new MailServer(computer, text9, os);
				computer.daemons.Add(mailServer);
				if (reader.MoveToAttribute("color"))
				{
					Color themeColor = Utils.convertStringToColor(reader.ReadContentAsString());
					mailServer.setThemeColor(themeColor);
				}
			}
			else if (reader.Name == "MissionListingServer")
			{
				reader.MoveToAttribute("name");
				string text9 = reader.ReadContentAsString();
				reader.MoveToAttribute("group");
				string group = reader.ReadContentAsString();
				reader.MoveToAttribute("public");
				string text10 = reader.ReadContentAsString();
				bool isPublic = text10.ToLower().Equals("true");
				reader.MoveToAttribute("assign");
				string text11 = reader.ReadContentAsString();
				bool isAssigner = text11.ToLower().Equals("true");
				string text12 = null;
				if (reader.MoveToAttribute("title"))
				{
					text12 = reader.ReadContentAsString();
				}
				string text13 = null;
				string text14 = null;
				string articleFolderPath = null;
				if (reader.MoveToAttribute("icon"))
				{
					text13 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("articles"))
				{
					articleFolderPath = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("color"))
				{
					text14 = reader.ReadContentAsString();
				}
				MissionListingServer missionListingServer = null;
				missionListingServer = ((text13 == null || text14 == null) ? new MissionListingServer(computer, text9, group, os, isPublic, isAssigner) : new MissionListingServer(computer, text9, text13, articleFolderPath, Utils.convertStringToColor(text14), os, isPublic, isAssigner));
				if (text12 != null)
				{
					missionListingServer.listingTitle = text12;
				}
				computer.daemons.Add(missionListingServer);
			}
			else if (reader.Name == "AddEmailServer")
			{
				reader.MoveToAttribute("name");
				string text9 = reader.ReadContentAsString();
				AddEmailDaemon item2 = new AddEmailDaemon(computer, text9, os);
				computer.daemons.Add(item2);
			}
			else if (reader.Name == "MessageBoard")
			{
				reader.MoveToAttribute("name");
				string text15 = reader.ReadContentAsString();
				MessageBoardDaemon messageBoardDaemon = new MessageBoardDaemon(computer, os);
				if (reader.MoveToAttribute("boardName"))
				{
					messageBoardDaemon.BoardName = reader.ReadContentAsString();
				}
				messageBoardDaemon.name = text15;
				computer.daemons.Add(messageBoardDaemon);
			}
			else if (reader.Name == "WebServer")
			{
				reader.MoveToAttribute("name");
				string text9 = reader.ReadContentAsString();
				reader.MoveToAttribute("url");
				string pageFileLocation = reader.ReadContentAsString();
				WebServerDaemon item3 = new WebServerDaemon(computer, text9, os, pageFileLocation);
				computer.daemons.Add(item3);
			}
			else if (reader.Name == "OnlineWebServer")
			{
				reader.MoveToAttribute("name");
				string text9 = reader.ReadContentAsString();
				reader.MoveToAttribute("url");
				string pageFileLocation = reader.ReadContentAsString();
				OnlineWebServerDaemon onlineWebServerDaemon = new OnlineWebServerDaemon(computer, text9, os);
				onlineWebServerDaemon.setURL(pageFileLocation);
				computer.daemons.Add(onlineWebServerDaemon);
			}
			else if (reader.Name == "AcademicDatabse")
			{
				reader.MoveToAttribute("name");
				string text9 = reader.ReadContentAsString();
				AcademicDatabaseDaemon item4 = new AcademicDatabaseDaemon(computer, text9, os);
				computer.daemons.Add(item4);
			}
			else if (reader.Name == "MissionHubServer")
			{
				MissionHubServer item5 = new MissionHubServer(computer, "unknown", "unknown", os);
				computer.daemons.Add(item5);
			}
			else if (reader.Name == "DeathRowDatabase")
			{
				DeathRowDatabaseDaemon item6 = new DeathRowDatabaseDaemon(computer, "Death Row Database", os);
				computer.daemons.Add(item6);
			}
			else if (reader.Name == "MedicalDatabase")
			{
				MedicalDatabaseDaemon item7 = new MedicalDatabaseDaemon(computer, os);
				computer.daemons.Add(item7);
			}
			else if (reader.Name == "HeartMonitor")
			{
				string patientID = "UNKNOWN";
				if (reader.MoveToAttribute("patient"))
				{
					patientID = reader.ReadContentAsString();
				}
				HeartMonitorDaemon heartMonitorDaemon = new HeartMonitorDaemon(computer, os);
				heartMonitorDaemon.PatientID = patientID;
				computer.daemons.Add(heartMonitorDaemon);
			}
			else if (reader.Name.Equals("PointClicker"))
			{
				PointClickerDaemon item8 = new PointClickerDaemon(computer, "Point Clicker!", os);
				computer.daemons.Add(item8);
			}
			else if (reader.Name.Equals("ispSystem"))
			{
				ISPDaemon item9 = new ISPDaemon(computer, os);
				computer.daemons.Add(item9);
			}
			else if (reader.Name.Equals("porthackheart"))
			{
				PorthackHeartDaemon item10 = new PorthackHeartDaemon(computer, os);
				computer.daemons.Add(item10);
			}
			else if (reader.Name.Equals("SongChangerDaemon"))
			{
				SongChangerDaemon item11 = new SongChangerDaemon(computer, os);
				computer.daemons.Add(item11);
			}
			else if (reader.Name == "UploadServerDaemon")
			{
				string foldername;
				string text16;
				string serviceName = (foldername = (text16 = ""));
				if (reader.MoveToAttribute("name"))
				{
					serviceName = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("foldername"))
				{
					foldername = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("color"))
				{
					text16 = reader.ReadContentAsString();
				}
				bool needsAuthentication = false;
				bool hasReturnViewButton = false;
				if (reader.MoveToAttribute("needsAuh"))
				{
					needsAuthentication = reader.ReadContentAsBoolean();
				}
				if (reader.MoveToAttribute("hasReturnViewButton"))
				{
					hasReturnViewButton = reader.ReadContentAsString().ToLower() == "true";
				}
				Color themeColor2 = Color.White;
				if (text16 != "")
				{
					themeColor2 = Utils.convertStringToColor(text16);
				}
				UploadServerDaemon uploadServerDaemon = new UploadServerDaemon(computer, serviceName, themeColor2, os, foldername, needsAuthentication);
				uploadServerDaemon.hasReturnViewButton = hasReturnViewButton;
				computer.daemons.Add(uploadServerDaemon);
			}
			else if (reader.Name == "DHSDaemon")
			{
				DLCHubServer item12 = new DLCHubServer(computer, "unknown", "unknown", os);
				computer.daemons.Add(item12);
			}
			else if (reader.Name == "CustomConnectDisplayDaemon")
			{
				CustomConnectDisplayDaemon item13 = new CustomConnectDisplayDaemon(computer, os);
				computer.daemons.Add(item13);
			}
			else if (reader.Name == "DatabaseDaemon")
			{
				string dataTypeIdentifier;
				string permissions;
				string foldername2;
				string text9 = (dataTypeIdentifier = (permissions = (foldername2 = null)));
				Color? themeColor3 = null;
				string text17 = null;
				string adminResetEmailHostID = null;
				if (reader.MoveToAttribute("Name"))
				{
					text9 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("Permissions"))
				{
					permissions = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("DataType"))
				{
					dataTypeIdentifier = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("Foldername"))
				{
					foldername2 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("Color"))
				{
					string input2 = reader.ReadContentAsString();
					themeColor3 = Utils.convertStringToColor(input2);
				}
				if (reader.MoveToAttribute("AdminEmailAccount"))
				{
					text17 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("AdminEmailHostID"))
				{
					adminResetEmailHostID = reader.ReadContentAsString();
				}
				DatabaseDaemon databaseDaemon = new DatabaseDaemon(computer, os, text9, permissions, dataTypeIdentifier, foldername2, themeColor3);
				if (!string.IsNullOrWhiteSpace(text17))
				{
					databaseDaemon.adminResetEmailHostID = adminResetEmailHostID;
					databaseDaemon.adminResetPassEmailAccount = text17;
				}
				computer.daemons.Add(databaseDaemon);
			}
			else if (reader.Name == "WhitelistAuthenticatorDaemon")
			{
				bool authenticatesItself = true;
				if (reader.MoveToAttribute("SelfAuthenticating"))
				{
					authenticatesItself = reader.ReadContentAsString().ToLower() == "true";
				}
				WhitelistConnectionDaemon whitelistConnectionDaemon = new WhitelistConnectionDaemon(computer, os);
				whitelistConnectionDaemon.AuthenticatesItself = authenticatesItself;
				WhitelistConnectionDaemon item14 = whitelistConnectionDaemon;
				computer.daemons.Add(item14);
			}
			else if (reader.Name == "IRCDaemon")
			{
				IRCDaemon item15 = new IRCDaemon(computer, os, "LOAD ERROR");
				computer.daemons.Add(item15);
			}
			else if (reader.Name == "MarkovTextDaemon")
			{
				string corpusLoadPath;
				string text18 = (corpusLoadPath = null);
				if (reader.MoveToAttribute("Name"))
				{
					text18 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("SourceFilesContentFolder"))
				{
					corpusLoadPath = reader.ReadContentAsString();
				}
				MarkovTextDaemon item16 = new MarkovTextDaemon(computer, os, text18, corpusLoadPath);
				computer.daemons.Add(item16);
			}
			else if (reader.Name.Equals("AircraftDaemon"))
			{
				Vector2 zero = Vector2.Zero;
				Vector2 mapDest = Vector2.One * 0.5f;
				float progress = 0.5f;
				string text18 = "Pacific Charter Flight";
				if (reader.MoveToAttribute("Name"))
				{
					text18 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("OriginX"))
				{
					zero.X = Utils.RobustReadAsFloat(reader);
				}
				if (reader.MoveToAttribute("OriginY"))
				{
					zero.Y = Utils.RobustReadAsFloat(reader);
				}
				if (reader.MoveToAttribute("DestX"))
				{
					mapDest.X = Utils.RobustReadAsFloat(reader);
				}
				if (reader.MoveToAttribute("DestY"))
				{
					mapDest.Y = Utils.RobustReadAsFloat(reader);
				}
				if (reader.MoveToAttribute("Progress"))
				{
					progress = Utils.RobustReadAsFloat(reader);
				}
				AircraftDaemon item17 = new AircraftDaemon(computer, os, text18, zero, mapDest, progress);
				computer.daemons.Add(item17);
			}
			else if (reader.Name.Equals("LogoCustomConnectDisplayDaemon"))
			{
				string logoImageName = null;
				string text12 = null;
				string buttonAlignment = null;
				bool logoShouldClipoverdraw = false;
				if (reader.MoveToAttribute("logo"))
				{
					logoImageName = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("title"))
				{
					text12 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("overdrawLogo"))
				{
					logoShouldClipoverdraw = reader.ReadContentAsString().ToLower() == "true";
				}
				if (reader.MoveToAttribute("buttonAlignment"))
				{
					buttonAlignment = reader.ReadContentAsString();
				}
				LogoCustomConnectDisplayDaemon item18 = new LogoCustomConnectDisplayDaemon(computer, os, logoImageName, text12, logoShouldClipoverdraw, buttonAlignment);
				computer.daemons.Add(item18);
			}
			else if (reader.Name.Equals("LogoDaemon"))
			{
				string text19;
				string logoImagePath = (text19 = null);
				bool showsTitle = true;
				Color textColor = Color.White;
				if (reader.MoveToAttribute("LogoImagePath"))
				{
					logoImagePath = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("TextColor"))
				{
					textColor = Utils.convertStringToColor(reader.ReadContentAsString());
				}
				if (reader.MoveToAttribute("Name"))
				{
					text19 = reader.ReadContentAsString();
				}
				if (reader.MoveToAttribute("ShowsTitle"))
				{
					showsTitle = reader.ReadContentAsString().ToLower() == "true";
				}
				LogoDaemon logoDaemon = new LogoDaemon(computer, os, compName, showsTitle, logoImagePath);
				logoDaemon.TextColor = textColor;
				computer.daemons.Add(logoDaemon);
			}
			else if (reader.Name.Equals("DLCCredits"))
			{
				string text20 = null;
				if (reader.MoveToAttribute("Button"))
				{
					text20 = reader.ReadContentAsString();
				}
				string text12 = null;
				if (reader.MoveToAttribute("Title"))
				{
					text12 = reader.ReadContentAsString();
				}
				DLCCreditsDaemon dLCCreditsDaemon = null;
				dLCCreditsDaemon = ((text20 == null && text12 == null) ? new DLCCreditsDaemon(computer, os) : new DLCCreditsDaemon(computer, os, text12, text20));
				if (reader.MoveToAttribute("Action"))
				{
					dLCCreditsDaemon.ConditionalActionsToLoadOnButtonPress = reader.ReadContentAsString();
				}
				computer.daemons.Add(dLCCreditsDaemon);
			}
			else if (reader.Name.Equals("FastActionHost"))
			{
				FastActionHost item19 = new FastActionHost(computer, os, compName);
				computer.daemons.Add(item19);
			}
			reader.Read();
		}
		computer.files = FileSystem.load(reader);
		computer.traceTime = num2;
		computer.portsNeededForCrack = num3;
		computer.adminIP = text5;
		computer.idName = text2;
		computer.icon = text4;
		computer.attatchedDeviceIDs = text3;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] != "")
			{
				computer.links.Add(Convert.ToInt32(array[i]));
			}
		}
		if (text8.Length > 0)
		{
			ComputerLoader.loadPortsIntoComputer(text8, computer);
		}
		if (text == "mail")
		{
			os.netMap.mailServer = computer;
		}
		else if (text == "player")
		{
			os.thisComputer = computer;
		}
		return computer;
	}

	public string getTooltipString()
	{
		string text = name;
		return text + "\n" + ip;
	}

	public Vector2 getScreenSpacePosition()
	{
		Vector2 nodeDrawPos = os.netMap.GetNodeDrawPos(this);
		return new Vector2((float)os.netMap.bounds.X + nodeDrawPos.X + (float)(NetworkMap.NODE_SIZE / 2), (float)os.netMap.bounds.Y + nodeDrawPos.Y + (float)(NetworkMap.NODE_SIZE / 2));
	}

	public Daemon getDaemon(Type t)
	{
		for (int i = 0; i < daemons.Count; i++)
		{
			if (daemons[i].GetType().Equals(t))
			{
				return daemons[i];
			}
		}
		return null;
	}

	public static string generateBinaryString(int length)
	{
		byte[] array = new byte[length / 8];
		Utils.random.NextBytes(array);
		string text = "";
		for (int i = 0; i < array.Length; i++)
		{
			text += Convert.ToString(array[i], 2);
		}
		return text;
	}

	public static string generateBinaryString(int length, MSRandom rng)
	{
		byte[] array = new byte[length / 8];
		rng.NextBytes(array);
		string text = "";
		for (int i = 0; i < array.Length; i++)
		{
			text += Convert.ToString(array[i], 2);
		}
		return text;
	}

	public static Folder getFolderAtDepth(Computer c, int depth, List<int> path)
	{
		Folder folder = c.files.root;
		if (path.Count > 0)
		{
			for (int i = 0; i < depth; i++)
			{
				if (folder.folders.Count > path[i])
				{
					folder = folder.folders[path[i]];
				}
			}
		}
		return folder;
	}

	public override string ToString()
	{
		return "Comp : " + idName;
	}

	public static Computer loadFromFile(string filename)
	{
		return (Computer)ComputerLoader.loadComputer(filename);
	}

	public Folder getFolderFromPath(string path, bool createFoldersThatDontExist = false)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return files.root;
		}
		List<int> folderPath = getFolderPath(path, createFoldersThatDontExist);
		return getFolderAtDepth(this, folderPath.Count, folderPath);
	}

	public List<int> getFolderPath(string path, bool createFoldersThatDontExist = false)
	{
		List<int> list = new List<int>();
		char[] separator = new char[2] { '/', '\\' };
		string[] array = path.Split(separator);
		Folder folder = files.root;
		for (int i = 0; i < array.Length; i++)
		{
			bool flag = false;
			for (int j = 0; j < folder.folders.Count; j++)
			{
				if (folder.folders[j].name.Equals(array[i]))
				{
					list.Add(j);
					folder = folder.folders[j];
					flag = true;
					break;
				}
			}
			if (!flag && createFoldersThatDontExist)
			{
				folder.folders.Add(new Folder(array[i]));
				int num = folder.folders.Count - 1;
				folder = folder.folders[num];
				list.Add(num);
			}
		}
		return list;
	}

	public bool PlayerHasAdminPermissions()
	{
		if (adminIP == os.thisComputer.ip)
		{
			return true;
		}
		if (currentUser.type == 0 && currentUser.name != null)
		{
			return true;
		}
		return false;
	}
}
