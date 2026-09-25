using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Hacknet.Extensions;
using Hacknet.Mission;
using Hacknet.PlatformAPI.Storage;
using Hacknet.Security;
using Microsoft.Xna.Framework;

namespace Hacknet;

public static class ComputerLoader
{
	public static Action MissionPreLoadComplete;

	public static Action postAllLoadedActions;

	private static OS os;

	public static void init(object opsys)
	{
		os = (OS)opsys;
	}

	public static object loadComputer(string filename, bool preventAddingToNetmap = false, bool preventInitDaemons = false)
	{
		filename = LocalizedFileLoader.GetLocalizedFilepath(filename);
		Computer c = null;
		Stream stream = null;
		XmlReader xmlReader = null;
		if (filename.EndsWith("ExampleComputer.xml"))
		{
			string text = File.ReadAllText(filename);
			int num = text.IndexOf("<!--START_LABYRINTHS_ONLY_CONTENT-->");
			string text2 = "<!--END_LABYRINTHS_ONLY_CONTENT-->";
			int num2 = text.IndexOf(text2);
			if (num >= 0 && num2 >= 0)
			{
				text = text.Substring(0, num) + text.Substring(num2 + text2.Length);
			}
			stream = Utils.GenerateStreamFromString(text);
		}
		else
		{
			stream = File.OpenRead(filename);
		}
		xmlReader = XmlReader.Create(stream);
		string idName = "UNKNOWN";
		string text3 = "UNKNOWN";
		string icon = null;
		int seclevel = 0;
		byte compType = 1;
		bool allowsDefaultBootModule = true;
		string compIP = NetworkMap.generateRandomIP();
		while (xmlReader.Name != "Computer")
		{
			xmlReader.Read();
			if (xmlReader.EOF)
			{
				return null;
			}
		}
		if (xmlReader.MoveToAttribute("id"))
		{
			idName = xmlReader.ReadContentAsString();
		}
		if (xmlReader.MoveToAttribute("name"))
		{
			text3 = filter(xmlReader.ReadContentAsString());
		}
		if (xmlReader.MoveToAttribute("security"))
		{
			seclevel = xmlReader.ReadContentAsInt();
		}
		if (xmlReader.MoveToAttribute("type"))
		{
			string text4 = xmlReader.ReadContentAsString();
			if (text4.ToLowerInvariant() == "empty")
			{
				text4 = string.Concat((byte)4);
			}
			compType = Convert.ToByte(text4);
		}
		if (xmlReader.MoveToAttribute("ip"))
		{
			compIP = filter(xmlReader.ReadContentAsString());
		}
		if (xmlReader.MoveToAttribute("icon"))
		{
			icon = xmlReader.ReadContentAsString();
		}
		if (xmlReader.MoveToAttribute("allowsDefaultBootModule"))
		{
			allowsDefaultBootModule = xmlReader.ReadContentAsBoolean();
		}
		c = new Computer(text3, compIP, os.netMap.getRandomPosition(), seclevel, compType, os);
		c.idName = idName;
		c.AllowsDefaultBootModule = allowsDefaultBootModule;
		c.icon = icon;
		if (c.type == 4)
		{
			Folder folder = c.files.root.searchForFolder("home");
			if (folder != null)
			{
				folder.files.Clear();
				folder.folders.Clear();
			}
		}
		while (xmlReader.Name != "Computer")
		{
			if (xmlReader.Name.ToLower().Equals("file"))
			{
				bool flag = true;
				bool flag2 = false;
				string path = ((!xmlReader.MoveToAttribute("path")) ? "home" : xmlReader.ReadContentAsString());
				string s = ((!xmlReader.MoveToAttribute("name")) ? "Data" : xmlReader.ReadContentAsString());
				if (xmlReader.MoveToAttribute("EduSafe"))
				{
					flag = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				if (xmlReader.MoveToAttribute("EduSafeOnly"))
				{
					flag2 = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				s = filter(s);
				xmlReader.MoveToContent();
				string text = xmlReader.ReadElementContentAsString();
				if (text.Equals(""))
				{
					text = Computer.generateBinaryString(500);
				}
				text = filter(text);
				Folder folderFromPath = c.getFolderFromPath(path, createFoldersThatDontExist: true);
				if ((flag || !Settings.EducationSafeBuild) && (Settings.EducationSafeBuild || !flag2))
				{
					if (folderFromPath.searchForFile(s) != null)
					{
						folderFromPath.searchForFile(s).data = text;
					}
					else
					{
						folderFromPath.files.Add(new FileEntry(text, s));
					}
				}
			}
			if (xmlReader.Name.Equals("encryptedFile"))
			{
				bool flag3 = false;
				string path = ((!xmlReader.MoveToAttribute("path")) ? "home" : xmlReader.ReadContentAsString());
				string s = ((!xmlReader.MoveToAttribute("name")) ? "Data" : xmlReader.ReadContentAsString());
				string header = ((!xmlReader.MoveToAttribute("header")) ? "ERROR" : xmlReader.ReadContentAsString());
				string ipLink = ((!xmlReader.MoveToAttribute("ip")) ? "ERROR" : xmlReader.ReadContentAsString());
				string pass = ((!xmlReader.MoveToAttribute("pass")) ? "" : xmlReader.ReadContentAsString());
				string text5 = ((!xmlReader.MoveToAttribute("extension")) ? null : xmlReader.ReadContentAsString());
				if (xmlReader.MoveToAttribute("double"))
				{
					flag3 = xmlReader.ReadContentAsBoolean();
				}
				s = filter(s);
				xmlReader.MoveToContent();
				string text = xmlReader.ReadElementContentAsString();
				if (text.Equals(""))
				{
					text = Computer.generateBinaryString(500);
				}
				text = filter(text);
				if (flag3)
				{
					text = FileEncrypter.EncryptString(text, header, ipLink, pass, text5);
				}
				text = FileEncrypter.EncryptString(text, header, ipLink, pass, flag3 ? "_LAYER2.dec" : text5);
				Folder folderFromPath = c.getFolderFromPath(path, createFoldersThatDontExist: true);
				if (folderFromPath.searchForFile(s) != null)
				{
					folderFromPath.searchForFile(s).data = text;
				}
				else
				{
					folderFromPath.files.Add(new FileEntry(text, s));
				}
			}
			if (xmlReader.Name.Equals("memoryDumpFile"))
			{
				string path = ((!xmlReader.MoveToAttribute("path")) ? "home" : xmlReader.ReadContentAsString());
				string s = ((!xmlReader.MoveToAttribute("name")) ? "Data" : xmlReader.ReadContentAsString());
				xmlReader.MoveToContent();
				MemoryContents memoryContents = MemoryContents.Deserialize(xmlReader);
				Folder folderFromPath = c.getFolderFromPath(path, createFoldersThatDontExist: true);
				if (folderFromPath.searchForFile(s) != null)
				{
					folderFromPath.searchForFile(s).data = memoryContents.GetEncodedFileString();
				}
				else
				{
					folderFromPath.files.Add(new FileEntry(memoryContents.GetEncodedFileString(), s));
				}
				while (!(xmlReader.Name == "memoryDumpFile") || xmlReader.IsStartElement() || xmlReader.EOF)
				{
					xmlReader.Read();
				}
				if (xmlReader.EOF)
				{
					throw new FormatException("Unexpected end of file looking for memoryDumpFile close tag");
				}
				xmlReader.Read();
			}
			if (xmlReader.Name.ToLower().Equals("customthemefile"))
			{
				string path = ((!xmlReader.MoveToAttribute("path")) ? "home" : xmlReader.ReadContentAsString());
				string s = ((!xmlReader.MoveToAttribute("name")) ? "Data.txt" : xmlReader.ReadContentAsString());
				s = filter(s);
				string text;
				if (xmlReader.MoveToAttribute("themePath"))
				{
					text = xmlReader.ReadContentAsString();
					text = ThemeManager.getThemeDataStringForCustomTheme(text);
				}
				else
				{
					text = null;
				}
				if (string.IsNullOrWhiteSpace(text))
				{
					text = "DEFINITION ERROR - Theme generated incorrectly. No Custom theme found at definition path";
				}
				text = filter(text);
				Folder folderFromPath = c.getFolderFromPath(path, createFoldersThatDontExist: true);
				if (folderFromPath.searchForFile(s) != null)
				{
					folderFromPath.searchForFile(s).data = text;
				}
				else
				{
					folderFromPath.files.Add(new FileEntry(text, s));
				}
			}
			else if (xmlReader.Name.Equals("ports"))
			{
				xmlReader.MoveToContent();
				string portsList = xmlReader.ReadElementContentAsString();
				loadPortsIntoComputer(portsList, c);
			}
			else if (xmlReader.Name.Equals("positionNear"))
			{
				string targetNearNodeName = "";
				if (xmlReader.MoveToAttribute("target"))
				{
					targetNearNodeName = xmlReader.ReadContentAsString();
				}
				int position = 0;
				int total = 3;
				if (xmlReader.MoveToAttribute("position"))
				{
					position = xmlReader.ReadContentAsInt();
				}
				position++;
				if (xmlReader.MoveToAttribute("total"))
				{
					total = xmlReader.ReadContentAsInt();
				}
				bool forceUse = false;
				if (xmlReader.MoveToAttribute("force"))
				{
					forceUse = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				float extraDistance = 0f;
				if (xmlReader.MoveToAttribute("extraDistance"))
				{
					extraDistance = xmlReader.ReadContentAsFloat();
					extraDistance = Math.Max(-1f, Math.Min(1f, extraDistance));
				}
				postAllLoadedActions = (Action)Delegate.Combine(postAllLoadedActions, (Action)delegate
				{
					Computer computer3 = Programs.getComputer(os, targetNearNodeName);
					if (computer3 != null)
					{
						c.location = computer3.location + Corporation.getNearbyNodeOffset(computer3.location, position, total, os.netMap, extraDistance, forceUse);
					}
				});
			}
			else if (xmlReader.Name.Equals("proxy"))
			{
				float num3 = 1f;
				if (xmlReader.MoveToAttribute("time"))
				{
					num3 = xmlReader.ReadContentAsFloat();
				}
				if (num3 > 0f)
				{
					c.addProxy(Computer.BASE_PROXY_TICKS * num3);
				}
				else
				{
					c.hasProxy = false;
					c.proxyActive = false;
				}
			}
			else if (xmlReader.Name.Equals("portsForCrack"))
			{
				int num4 = -1;
				if (xmlReader.MoveToAttribute("val"))
				{
					num4 = xmlReader.ReadContentAsInt();
				}
				if (num4 != -1)
				{
					c.portsNeededForCrack = num4 - 1;
				}
			}
			else if (xmlReader.Name.Equals("firewall"))
			{
				int num5 = 1;
				if (xmlReader.MoveToAttribute("level"))
				{
					num5 = xmlReader.ReadContentAsInt();
				}
				if (num5 > 0)
				{
					string text6 = null;
					float additionalTime = 0f;
					if (xmlReader.MoveToAttribute("solution"))
					{
						text6 = xmlReader.ReadContentAsString();
					}
					if (xmlReader.MoveToAttribute("additionalTime"))
					{
						additionalTime = xmlReader.ReadContentAsFloat();
					}
					if (text6 != null)
					{
						c.addFirewall(num5, text6, additionalTime);
					}
					else
					{
						c.addFirewall(num5);
					}
				}
				else
				{
					c.firewall = null;
				}
			}
			else if (xmlReader.Name.Equals("link"))
			{
				string ip_Or_ID_or_Name = "";
				if (xmlReader.MoveToAttribute("target"))
				{
					ip_Or_ID_or_Name = xmlReader.ReadContentAsString();
				}
				Computer computer = Programs.getComputer(os, ip_Or_ID_or_Name);
				if (computer != null)
				{
					c.links.Add(os.netMap.nodes.IndexOf(computer));
				}
			}
			else if (xmlReader.Name.Equals("dlink"))
			{
				string comp = "";
				if (xmlReader.MoveToAttribute("target"))
				{
					comp = xmlReader.ReadContentAsString();
				}
				Computer local = c;
				postAllLoadedActions = (Action)Delegate.Combine(postAllLoadedActions, (Action)delegate
				{
					Computer computer2 = Programs.getComputer(os, comp);
					if (computer2 != null)
					{
						local.links.Add(os.netMap.nodes.IndexOf(computer2));
					}
				});
			}
			else if (xmlReader.Name.Equals("trace"))
			{
				float num3 = 1f;
				if (xmlReader.MoveToAttribute("time"))
				{
					num3 = xmlReader.ReadContentAsFloat();
				}
				c.traceTime = num3;
			}
			else if (xmlReader.Name.Equals("adminPass"))
			{
				string text7 = null;
				if (xmlReader.MoveToAttribute("pass"))
				{
					text7 = filter(xmlReader.ReadContentAsString());
				}
				if (text7 == null)
				{
					text7 = PortExploits.getRandomPassword();
				}
				c.setAdminPassword(text7);
			}
			else if (xmlReader.Name.Equals("admin"))
			{
				string text8 = "basic";
				bool resetsPassword = true;
				bool isSuper = false;
				if (xmlReader.MoveToAttribute("type"))
				{
					text8 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("resetPassword"))
				{
					resetsPassword = xmlReader.ReadContentAsBoolean();
				}
				if (xmlReader.MoveToAttribute("isSuper"))
				{
					isSuper = xmlReader.ReadContentAsBoolean();
				}
				switch (text8)
				{
				default:
					c.admin = new BasicAdministrator();
					break;
				case "fast":
					c.admin = new FastBasicAdministrator();
					break;
				case "progress":
					c.admin = new FastProgressOnlyAdministrator();
					break;
				case "none":
					c.admin = null;
					break;
				}
				if (c.admin != null)
				{
					c.admin.ResetsPassword = resetsPassword;
					c.admin.IsSuper = isSuper;
				}
			}
			else if (xmlReader.Name.Equals("portRemap"))
			{
				xmlReader.MoveToContent();
				string text9 = xmlReader.ReadElementContentAsString();
				try
				{
					c.PortRemapping = PortRemappingSerializer.Deserialize(text9);
				}
				catch (FormatException innerException)
				{
					throw new FormatException("Error in portRemap tag. Check that your list is properly comma separated!\nBroken Data: " + text9, innerException);
				}
			}
			else if (xmlReader.Name.Equals("ExternalCounterpart"))
			{
				string serverName = "";
				string idName2 = "";
				if (xmlReader.MoveToAttribute("id"))
				{
					serverName = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("name"))
				{
					idName2 = xmlReader.ReadContentAsString();
				}
				ExternalCounterpart externalCounterpart = new ExternalCounterpart(idName2, ExternalCounterpart.getIPForServerName(serverName));
				c.externalCounterpart = externalCounterpart;
			}
			else if (xmlReader.Name.Equals("account"))
			{
				byte b = 0;
				string text7;
				string s2 = (text7 = "ERROR");
				if (xmlReader.MoveToAttribute("username"))
				{
					s2 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("password"))
				{
					text7 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("type"))
				{
					string text = xmlReader.ReadContentAsString();
					b = (byte)((!(text.ToLower() == "admin")) ? ((text.ToLower() == "all") ? 1 : ((text.ToLower() == "mail") ? 2 : ((!(text.ToLower() == "missionlist")) ? ((byte)Convert.ToInt32(text)) : 3))) : 0);
				}
				s2 = filter(s2);
				text7 = filter(text7);
				bool flag4 = false;
				for (int i = 0; i < c.users.Count; i++)
				{
					UserDetail value = c.users[i];
					if (value.name.Equals(s2))
					{
						value.pass = text7;
						value.type = b;
						c.users[i] = value;
						if (s2.Equals("admin"))
						{
							c.adminPass = text7;
						}
						flag4 = true;
					}
				}
				if (!flag4)
				{
					UserDetail value = new UserDetail(s2, text7, b);
					c.users.Add(value);
				}
			}
			else if (xmlReader.Name.ToLower().Equals("tracker"))
			{
				c.HasTracker = true;
			}
			else if (xmlReader.Name.Equals("missionListingServer"))
			{
				bool missionAssigner = false;
				bool isPublic = false;
				string serviceName;
				string group = (serviceName = "ERROR");
				if (xmlReader.MoveToAttribute("name"))
				{
					serviceName = filter(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("group"))
				{
					group = filter(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("assigner"))
				{
					missionAssigner = xmlReader.ReadContentAsBoolean();
				}
				if (xmlReader.MoveToAttribute("public"))
				{
					isPublic = xmlReader.ReadContentAsBoolean();
				}
				MissionListingServer missionListingServer = new MissionListingServer(c, serviceName, group, os, isPublic);
				missionListingServer.missionAssigner = missionAssigner;
				c.daemons.Add(missionListingServer);
			}
			else if (xmlReader.Name.Equals("variableMissionListingServer"))
			{
				bool missionAssigner = false;
				bool isPublic = false;
				string serviceName;
				string articleFolderPath;
				string text10;
				string iconPath = (serviceName = (articleFolderPath = (text10 = null)));
				Color themeColor = Color.IndianRed;
				if (xmlReader.MoveToAttribute("name"))
				{
					serviceName = filter(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("iconPath"))
				{
					iconPath = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("articleFolderPath"))
				{
					articleFolderPath = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("public"))
				{
					isPublic = xmlReader.ReadContentAsBoolean();
				}
				if (xmlReader.MoveToAttribute("assigner"))
				{
					missionAssigner = xmlReader.ReadContentAsBoolean();
				}
				if (xmlReader.MoveToAttribute("title"))
				{
					text10 = filter(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("color"))
				{
					string input = xmlReader.ReadContentAsString();
					themeColor = Utils.convertStringToColor(input);
				}
				MissionListingServer missionListingServer = new MissionListingServer(c, serviceName, iconPath, articleFolderPath, themeColor, os, isPublic, missionAssigner);
				missionListingServer.missionAssigner = missionAssigner;
				if (text10 != null)
				{
					missionListingServer.listingTitle = text10;
				}
				c.daemons.Add(missionListingServer);
			}
			else if (xmlReader.Name.Equals("missionHubServer"))
			{
				Color themeColorLine;
				Color themeColorBackground;
				Color themeColor2 = (themeColorLine = (themeColorBackground = Color.PaleTurquoise));
				string serviceName2;
				string text11;
				string group2 = (text11 = (serviceName2 = null));
				bool allowAbandon = true;
				if (xmlReader.MoveToAttribute("groupName"))
				{
					filter(group2 = xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("serviceName"))
				{
					serviceName2 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("missionFolderPath"))
				{
					text11 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("themeColor"))
				{
					themeColor2 = Utils.convertStringToColor(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("lineColor"))
				{
					themeColorLine = Utils.convertStringToColor(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("backgroundColor"))
				{
					themeColorBackground = Utils.convertStringToColor(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("allowAbandon"))
				{
					allowAbandon = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				string text12 = "Content/Missions/";
				if (Settings.IsInExtensionMode)
				{
					text12 = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
				}
				text11 = text11.Replace('\\', '/');
				if (!text11.EndsWith("/"))
				{
					text11 += "/";
				}
				MissionHubServer missionHubServer = new MissionHubServer(c, serviceName2, group2, os);
				missionHubServer.MissionSourceFolderPath = text12 + text11;
				missionHubServer.themeColor = themeColor2;
				missionHubServer.themeColorBackground = themeColorBackground;
				missionHubServer.themeColorLine = themeColorLine;
				missionHubServer.allowAbandon = allowAbandon;
				c.daemons.Add(missionHubServer);
			}
			else if (xmlReader.Name.Equals("mailServer"))
			{
				string serviceName = "Mail Server";
				if (xmlReader.MoveToAttribute("name"))
				{
					serviceName = xmlReader.ReadContentAsString();
				}
				bool shouldGenerateJunkEmails = true;
				if (xmlReader.MoveToAttribute("generateJunk"))
				{
					shouldGenerateJunkEmails = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				MailServer ms = new MailServer(c, serviceName, os);
				ms.shouldGenerateJunkEmails = shouldGenerateJunkEmails;
				if (xmlReader.MoveToAttribute("color"))
				{
					ms.setThemeColor(Utils.convertStringToColor(xmlReader.ReadContentAsString()));
				}
				while (!(xmlReader.Name == "mailServer") || xmlReader.IsStartElement())
				{
					if (xmlReader.Name == "email")
					{
						string sender = "UNKNOWN";
						string text13 = null;
						string subject = "UNKNOWN";
						string text14 = "UNKNOWN";
						if (xmlReader.MoveToAttribute("sender"))
						{
							sender = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("recipient"))
						{
							text13 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("subject"))
						{
							subject = filter(xmlReader.ReadContentAsString());
						}
						xmlReader.MoveToContent();
						text14 = filter(xmlReader.ReadElementContentAsString());
						if (text13 != null)
						{
							string email = MailServer.generateEmail(subject, text14, sender);
							string recp = text13;
							MailServer mailServer = ms;
							mailServer.setupComplete = (Action)Delegate.Combine(mailServer.setupComplete, (Action)delegate
							{
								ms.addMail(email, recp);
							});
						}
					}
					xmlReader.Read();
				}
				c.daemons.Add(ms);
			}
			else if (xmlReader.Name.Equals("addEmailDaemon"))
			{
				AddEmailDaemon item = new AddEmailDaemon(c, "Final Task", os);
				c.daemons.Add(item);
			}
			else if (xmlReader.Name.Equals("deathRowDatabase"))
			{
				DeathRowDatabaseDaemon item2 = new DeathRowDatabaseDaemon(c, "Death Row Database", os);
				c.daemons.Add(item2);
			}
			else if (xmlReader.Name.Equals("academicDatabase"))
			{
				AcademicDatabaseDaemon item3 = new AcademicDatabaseDaemon(c, "International Academic Database", os);
				c.daemons.Add(item3);
			}
			else if (xmlReader.Name.Equals("ispSystem"))
			{
				ISPDaemon item4 = new ISPDaemon(c, os);
				c.daemons.Add(item4);
			}
			else if (xmlReader.Name.Equals("messageBoard"))
			{
				MessageBoardDaemon messageBoardDaemon = new MessageBoardDaemon(c, os);
				string text15 = "Anonymous";
				if (xmlReader.MoveToAttribute("name"))
				{
					text15 = xmlReader.ReadContentAsString();
				}
				messageBoardDaemon.name = text15;
				messageBoardDaemon.BoardName = text15;
				while (!(xmlReader.Name == "messageBoard") || xmlReader.IsStartElement())
				{
					if (xmlReader.Name == "thread")
					{
						string text16 = "UNKNOWN";
						xmlReader.MoveToContent();
						text16 = xmlReader.ReadElementContentAsString();
						string text17 = "Content/Missions/";
						if (text16.StartsWith(text17))
						{
							text16 = text16.Substring(text17.Length);
						}
						if (Settings.IsInExtensionMode)
						{
							text17 = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
						}
						if (text16 != null)
						{
							messageBoardDaemon.AddThread(Utils.readEntireFile(text17 + text16));
						}
					}
					xmlReader.Read();
				}
				c.daemons.Add(messageBoardDaemon);
			}
			else if (xmlReader.Name.Equals("addAvconDemoEndDaemon"))
			{
				AvconDemoEndDaemon item5 = new AvconDemoEndDaemon(c, "Demo End", os);
				c.daemons.Add(item5);
			}
			else if (xmlReader.Name.Equals("addWebServer"))
			{
				string serviceName3 = "Web Server";
				string pageFileLocation = null;
				if (xmlReader.MoveToAttribute("name"))
				{
					serviceName3 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("url"))
				{
					pageFileLocation = xmlReader.ReadContentAsString();
				}
				WebServerDaemon webServerDaemon = new WebServerDaemon(c, serviceName3, os, pageFileLocation);
				webServerDaemon.registerAsDefaultBootDaemon();
				c.daemons.Add(webServerDaemon);
			}
			else if (xmlReader.Name.Equals("addOnlineWebServer"))
			{
				string serviceName3 = "Web Server";
				string pageFileLocation = null;
				if (xmlReader.MoveToAttribute("name"))
				{
					serviceName3 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("url"))
				{
					pageFileLocation = xmlReader.ReadContentAsString();
				}
				OnlineWebServerDaemon onlineWebServerDaemon = new OnlineWebServerDaemon(c, serviceName3, os);
				if (pageFileLocation != null)
				{
					onlineWebServerDaemon.setURL(pageFileLocation);
				}
				onlineWebServerDaemon.registerAsDefaultBootDaemon();
				c.daemons.Add(onlineWebServerDaemon);
			}
			else if (xmlReader.Name.Equals("uploadServerDaemon"))
			{
				string serviceName4 = "File Upload Server";
				string foldername = null;
				string input2 = "0,94,38";
				bool needsAuthentication = false;
				bool hasReturnViewButton = false;
				if (xmlReader.MoveToAttribute("name"))
				{
					serviceName4 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("folder"))
				{
					foldername = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("color"))
				{
					input2 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("needsAuth"))
				{
					needsAuthentication = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				if (xmlReader.MoveToAttribute("hasReturnViewButton"))
				{
					hasReturnViewButton = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				Color themeColor3 = Utils.convertStringToColor(input2);
				UploadServerDaemon uploadServerDaemon = new UploadServerDaemon(c, serviceName4, themeColor3, os, foldername, needsAuthentication);
				uploadServerDaemon.hasReturnViewButton = hasReturnViewButton;
				uploadServerDaemon.registerAsDefaultBootDaemon();
				c.daemons.Add(uploadServerDaemon);
			}
			else if (xmlReader.Name.Equals("MedicalDatabase"))
			{
				MedicalDatabaseDaemon item6 = new MedicalDatabaseDaemon(c, os);
				c.daemons.Add(item6);
			}
			else if (xmlReader.Name.Equals("HeartMonitor"))
			{
				string patientID = "UNKNOWN";
				if (xmlReader.MoveToAttribute("patient"))
				{
					patientID = xmlReader.ReadContentAsString();
				}
				HeartMonitorDaemon heartMonitorDaemon = new HeartMonitorDaemon(c, os);
				heartMonitorDaemon.PatientID = patientID;
				c.daemons.Add(heartMonitorDaemon);
			}
			else if (xmlReader.Name.Equals("PointClicker"))
			{
				PointClickerDaemon item7 = new PointClickerDaemon(c, "Point Clicker!", os);
				c.daemons.Add(item7);
			}
			else if (xmlReader.Name.Equals("PorthackHeart"))
			{
				PorthackHeartDaemon item8 = new PorthackHeartDaemon(c, os);
				c.daemons.Add(item8);
			}
			else if (xmlReader.Name.Equals("SongChangerDaemon"))
			{
				SongChangerDaemon item9 = new SongChangerDaemon(c, os);
				c.daemons.Add(item9);
			}
			else if (xmlReader.Name.Equals("DHSDaemon"))
			{
				DLCCheck(xmlReader.Name);
				string group3 = "UNKNOWN";
				bool addsFactionPointForMissionCompleteion = true;
				bool autoClearMissionsOnSingleComplete = true;
				bool allowContractAbbandon = false;
				Color themeColor2 = new Color(38, 201, 155);
				if (xmlReader.MoveToAttribute("groupName"))
				{
					group3 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("addsFactionPointOnMissionComplete"))
				{
					addsFactionPointForMissionCompleteion = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				if (xmlReader.MoveToAttribute("autoClearMissionsOnPlayerComplete"))
				{
					autoClearMissionsOnSingleComplete = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				if (xmlReader.MoveToAttribute("themeColor"))
				{
					themeColor2 = Utils.convertStringToColor(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("allowContractAbbandon"))
				{
					allowContractAbbandon = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				DLCHubServer dLCHubServer = new DLCHubServer(c, "DHS", group3, os);
				dLCHubServer.AddsFactionPointForMissionCompleteion = addsFactionPointForMissionCompleteion;
				dLCHubServer.AutoClearMissionsOnSingleComplete = autoClearMissionsOnSingleComplete;
				dLCHubServer.AllowContractAbbandon = allowContractAbbandon;
				dLCHubServer.themeColor = themeColor2;
				while (!(xmlReader.Name == "DHSDaemon") || xmlReader.IsStartElement())
				{
					if (xmlReader.Name.ToLower() == "user" || xmlReader.Name.ToLower() == "agent")
					{
						string text18 = null;
						if (xmlReader.MoveToAttribute("name"))
						{
							text18 = filter(xmlReader.ReadContentAsString());
						}
						string agentPassword = "password";
						if (xmlReader.MoveToAttribute("pass"))
						{
							agentPassword = filter(xmlReader.ReadContentAsString());
						}
						Color color = Color.LightGreen;
						if (xmlReader.MoveToAttribute("color"))
						{
							color = Utils.convertStringToColor(xmlReader.ReadContentAsString());
						}
						if (!string.IsNullOrWhiteSpace(text18))
						{
							dLCHubServer.AddAgent(filter(text18), agentPassword, color);
						}
					}
					xmlReader.Read();
				}
				c.daemons.Add(dLCHubServer);
			}
			else if (xmlReader.Name.Equals("CustomConnectDisplayDaemon"))
			{
				CustomConnectDisplayDaemon item10 = new CustomConnectDisplayDaemon(c, os);
				c.daemons.Add(item10);
			}
			else if (xmlReader.Name.Equals("DatabaseDaemon"))
			{
				DLCCheck(xmlReader.Name);
				string text19 = null;
				string foldername2 = null;
				Color? themeColor4 = null;
				if (xmlReader.MoveToAttribute("DataType"))
				{
					text19 = xmlReader.ReadContentAsString();
				}
				DatabaseDaemon.DatabasePermissions permissions = DatabaseDaemon.DatabasePermissions.Public;
				if (xmlReader.MoveToAttribute("Permissions"))
				{
					permissions = DatabaseDaemon.GetDatabasePermissionsFromString(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("Foldername"))
				{
					foldername2 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("Color"))
				{
					string input3 = xmlReader.ReadContentAsString();
					themeColor4 = Utils.convertStringToColor(input3);
				}
				string text20 = null;
				string adminResetEmailHostID = null;
				if (xmlReader.MoveToAttribute("AdminEmailAccount"))
				{
					text20 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("AdminEmailHostID"))
				{
					adminResetEmailHostID = xmlReader.ReadContentAsString();
				}
				string serviceName3 = "Database";
				if (xmlReader.MoveToAttribute("Name"))
				{
					serviceName3 = xmlReader.ReadContentAsString();
				}
				List<object> list = null;
				xmlReader.MoveToElement();
				if (!xmlReader.IsEmptyElement)
				{
					Type typeForName = ObjectSerializer.GetTypeForName(text19);
					if (typeForName != null)
					{
						list = new List<object>();
						while (!(xmlReader.Name == "DatabaseDaemon") || xmlReader.IsStartElement())
						{
							if (xmlReader.Name != null && xmlReader.Name == ObjectSerializer.GetTagNameForType(typeForName))
							{
								object item11 = ObjectSerializer.DeserializeObject(xmlReader, typeForName);
								list.Add(item11);
							}
							xmlReader.Read();
						}
					}
				}
				DatabaseDaemon databaseDaemon = new DatabaseDaemon(c, os, serviceName3, permissions, text19, foldername2, themeColor4);
				if (!string.IsNullOrWhiteSpace(text20))
				{
					databaseDaemon.adminResetEmailHostID = adminResetEmailHostID;
					databaseDaemon.adminResetPassEmailAccount = text20;
				}
				databaseDaemon.Dataset = list;
				c.daemons.Add(databaseDaemon);
			}
			else if (xmlReader.Name.Equals("WhitelistAuthenticatorDaemon"))
			{
				string remoteSourceIP = null;
				if (xmlReader.MoveToAttribute("Remote"))
				{
					remoteSourceIP = xmlReader.ReadContentAsString();
				}
				bool authenticatesItself = true;
				if (xmlReader.MoveToAttribute("SelfAuthenticating"))
				{
					authenticatesItself = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				WhitelistConnectionDaemon whitelistConnectionDaemon = new WhitelistConnectionDaemon(c, os);
				whitelistConnectionDaemon.RemoteSourceIP = remoteSourceIP;
				whitelistConnectionDaemon.AuthenticatesItself = authenticatesItself;
				WhitelistConnectionDaemon item12 = whitelistConnectionDaemon;
				c.daemons.Add(item12);
			}
			else if (xmlReader.Name.Equals("MarkovTextDaemon"))
			{
				string corpusLoadPath;
				string name = (corpusLoadPath = null);
				if (xmlReader.MoveToAttribute("Name"))
				{
					name = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("SourceFilesContentFolder"))
				{
					corpusLoadPath = xmlReader.ReadContentAsString();
				}
				MarkovTextDaemon item13 = new MarkovTextDaemon(c, os, name, corpusLoadPath);
				c.daemons.Add(item13);
			}
			else if (xmlReader.Name.Equals("IRCDaemon"))
			{
				Color themeColor2 = new Color(184, 2, 141);
				if (xmlReader.MoveToAttribute("themeColor"))
				{
					themeColor2 = Utils.convertStringToColor(xmlReader.ReadContentAsString());
				}
				string name2 = "IRC Server";
				if (xmlReader.MoveToAttribute("name"))
				{
					name2 = xmlReader.ReadContentAsString();
				}
				bool requiresLogin = false;
				if (xmlReader.MoveToAttribute("needsLogin"))
				{
					requiresLogin = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				IRCDaemon iRCDaemon = new IRCDaemon(c, os, name2);
				iRCDaemon.ThemeColor = themeColor2;
				iRCDaemon.RequiresLogin = requiresLogin;
				while (!(xmlReader.Name == "IRCDaemon") || xmlReader.IsStartElement())
				{
					if (xmlReader.Name.ToLower() == "user" || xmlReader.Name.ToLower() == "agent")
					{
						string text21 = null;
						if (xmlReader.MoveToAttribute("name"))
						{
							text21 = filter(xmlReader.ReadContentAsString());
						}
						Color value2 = Color.LightGreen;
						if (xmlReader.MoveToAttribute("color"))
						{
							value2 = Utils.convertStringToColor(xmlReader.ReadContentAsString());
						}
						if (!string.IsNullOrWhiteSpace(text21))
						{
							iRCDaemon.UserColors.Add(filter(text21), value2);
						}
					}
					if (xmlReader.Name == "post" && xmlReader.IsStartElement())
					{
						string text21 = null;
						if (xmlReader.MoveToAttribute("user"))
						{
							text21 = filter(xmlReader.ReadContentAsString());
						}
						string text22 = null;
						xmlReader.MoveToElement();
						text22 = xmlReader.ReadElementContentAsString();
						if (!string.IsNullOrWhiteSpace(text22) && !string.IsNullOrWhiteSpace(text21))
						{
							iRCDaemon.StartingMessages.Add(new KeyValuePair<string, string>(text21, filter(text22)));
						}
					}
					xmlReader.Read();
				}
				c.daemons.Add(iRCDaemon);
			}
			else if (xmlReader.Name.Equals("AircraftDaemon"))
			{
				DLCCheck(xmlReader.Name);
				Vector2 zero = Vector2.Zero;
				Vector2 mapDest = Vector2.One * 0.5f;
				float progress = 0.5f;
				string name = null;
				if (xmlReader.MoveToAttribute("Name"))
				{
					name = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("OriginX"))
				{
					zero.X = xmlReader.ReadContentAsFloat();
				}
				if (xmlReader.MoveToAttribute("OriginY"))
				{
					zero.Y = xmlReader.ReadContentAsFloat();
				}
				if (xmlReader.MoveToAttribute("DestX"))
				{
					mapDest.X = xmlReader.ReadContentAsFloat();
				}
				if (xmlReader.MoveToAttribute("DestY"))
				{
					mapDest.Y = xmlReader.ReadContentAsFloat();
				}
				if (xmlReader.MoveToAttribute("Progress"))
				{
					progress = xmlReader.ReadContentAsFloat();
				}
				AircraftDaemon item14 = new AircraftDaemon(c, os, name, zero, mapDest, progress);
				c.daemons.Add(item14);
			}
			else if (xmlReader.Name.Equals("LogoCustomConnectDisplayDaemon"))
			{
				string logoImageName = null;
				string titleImageName = null;
				string buttonAlignment = null;
				bool logoShouldClipoverdraw = false;
				if (xmlReader.MoveToAttribute("logo"))
				{
					logoImageName = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("title"))
				{
					titleImageName = filter(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("overdrawLogo"))
				{
					logoShouldClipoverdraw = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				if (xmlReader.MoveToAttribute("buttonAlignment"))
				{
					buttonAlignment = xmlReader.ReadContentAsString();
				}
				LogoCustomConnectDisplayDaemon item15 = new LogoCustomConnectDisplayDaemon(c, os, logoImageName, titleImageName, logoShouldClipoverdraw, buttonAlignment);
				c.daemons.Add(item15);
			}
			else if (xmlReader.Name.Equals("LogoDaemon"))
			{
				string text23;
				string logoImagePath = (text23 = null);
				bool showsTitle = true;
				Color textColor = Color.White;
				if (xmlReader.MoveToAttribute("LogoImagePath"))
				{
					logoImagePath = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("TextColor"))
				{
					textColor = Utils.convertStringToColor(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("Name"))
				{
					text23 = filter(xmlReader.ReadContentAsString());
				}
				if (xmlReader.MoveToAttribute("ShowsTitle"))
				{
					showsTitle = xmlReader.ReadContentAsString().ToLower() == "true";
				}
				string text14 = null;
				if (xmlReader.IsStartElement())
				{
					text14 = xmlReader.ReadElementContentAsString();
				}
				LogoDaemon logoDaemon = new LogoDaemon(c, os, text3, showsTitle, logoImagePath);
				logoDaemon.TextColor = textColor;
				logoDaemon.BodyText = text14;
				c.daemons.Add(logoDaemon);
			}
			else if (xmlReader.Name.Equals("DLCCredits") || xmlReader.Name.Equals("CreditsDaemon"))
			{
				string text24 = null;
				if (xmlReader.MoveToAttribute("ButtonText"))
				{
					text24 = filter(xmlReader.ReadContentAsString());
				}
				string titleImageName = null;
				if (xmlReader.MoveToAttribute("Title"))
				{
					titleImageName = filter(xmlReader.ReadContentAsString());
				}
				DLCCreditsDaemon dLCCreditsDaemon = null;
				dLCCreditsDaemon = ((text24 == null && titleImageName == null) ? new DLCCreditsDaemon(c, os) : new DLCCreditsDaemon(c, os, titleImageName, text24));
				if (xmlReader.MoveToAttribute("ConditionalActionSetToRunOnButtonPressPath"))
				{
					dLCCreditsDaemon.ConditionalActionsToLoadOnButtonPress = xmlReader.ReadContentAsString();
				}
				c.daemons.Add(dLCCreditsDaemon);
			}
			else if (xmlReader.Name.Equals("FastActionHost"))
			{
				FastActionHost item16 = new FastActionHost(c, os, text3);
				c.daemons.Add(item16);
			}
			else if (xmlReader.Name.Equals("eosDevice"))
			{
				EOSComp.AddEOSComp(xmlReader, c, os);
			}
			else if (xmlReader.Name.Equals("Memory"))
			{
				MemoryContents memoryContents = MemoryContents.Deserialize(xmlReader);
				c.Memory = memoryContents;
			}
			xmlReader.Read();
		}
		xmlReader.Close();
		if (!preventInitDaemons)
		{
			c.initDaemons();
		}
		if (!preventAddingToNetmap)
		{
			os.netMap.nodes.Add(c);
		}
		return c;
	}

	private static void DLCCheck(string name)
	{
		if (!DLC1SessionUpgrader.HasDLC1Installed)
		{
			throw new NotSupportedException("LABYRINTHS DLC REQUIRED.\nThe tag " + name + " requires Hacknet Labyrinths to be installed.");
		}
	}

	public static void loadPortsIntoComputer(string portsList, object computer_obj)
	{
		Computer computer = (Computer)computer_obj;
		char[] separator = new char[2] { ' ', ',' };
		string[] array = portsList.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		computer.ports.Clear();
		computer.portsOpen.Clear();
		for (int i = 0; i < array.Length; i++)
		{
			int num = -1;
			try
			{
				num = Convert.ToInt32(array[i]);
				if (PortExploits.portNums.Contains(num))
				{
					computer.ports.Add(num);
					computer.portsOpen.Add(0);
					continue;
				}
			}
			catch (OverflowException)
			{
			}
			catch (FormatException)
			{
			}
			int num2 = -1;
			foreach (KeyValuePair<int, string> crack in PortExploits.cracks)
			{
				if (crack.Value.ToLower().Equals(array[i].ToLower()))
				{
					num2 = crack.Key;
				}
			}
			if (num2 != -1)
			{
				computer.ports.Add(num2);
				computer.portsOpen.Add(0);
			}
		}
	}

	public static object readMission(string filename)
	{
		filename = LocalizedFileLoader.GetLocalizedFilepath(filename);
		Stream input = File.OpenRead(filename);
		XmlReader xmlReader = XmlReader.Create(input);
		List<MisisonGoal> list = new List<MisisonGoal>();
		int val = 0;
		string text = "";
		string text2 = "";
		int val2 = 0;
		bool activeCheck = false;
		bool shouldIgnoreSenderVerification = false;
		while (xmlReader.Name != "mission")
		{
			xmlReader.Read();
			if (xmlReader.EOF)
			{
				return null;
			}
		}
		if (xmlReader.MoveToAttribute("activeCheck"))
		{
			activeCheck = xmlReader.ReadContentAsBoolean();
		}
		if (xmlReader.MoveToAttribute("shouldIgnoreSenderVerification"))
		{
			shouldIgnoreSenderVerification = xmlReader.ReadContentAsBoolean();
		}
		while (xmlReader.Name != "goals" && xmlReader.Name != "generationKeys")
		{
			xmlReader.Read();
			if (xmlReader.EOF)
			{
				throw new FormatException("<goals> tag was not found where it was expected. It's either missing or out of order.");
			}
		}
		if (xmlReader.Name == "generationKeys")
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			while (xmlReader.MoveToNextAttribute())
			{
				string name = xmlReader.Name;
				string value = xmlReader.Value;
				dictionary.Add(name, value);
			}
			xmlReader.MoveToContent();
			string text3 = xmlReader.ReadElementContentAsString();
			if ((text3 != null) & (text3.Length >= 1))
			{
				dictionary.Add("Data", text3);
			}
			MissionGenerator.setMissionGenerationKeys(dictionary);
			while (xmlReader.Name != "goals")
			{
				xmlReader.Read();
			}
		}
		xmlReader.Read();
		if (MissionPreLoadComplete != null)
		{
			MissionPreLoadComplete();
		}
		while (xmlReader.Name != "goals")
		{
			if (xmlReader.Name.Equals("goal"))
			{
				string text4 = "UNKNOWN";
				if (xmlReader.MoveToAttribute("type"))
				{
					text4 = xmlReader.ReadContentAsString();
				}
				try
				{
					if (text4.ToLower().Equals("filedeletion"))
					{
						string filename2;
						string path;
						string text5 = (filename2 = (path = ""));
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("file"))
						{
							filename2 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("path"))
						{
							path = filter(xmlReader.ReadContentAsString());
						}
						Computer computer = findComp(text5);
						FileDeletionMission item = new FileDeletionMission(path, filename2, (computer != null) ? computer.ip : text5, os);
						list.Add(item);
					}
					if (text4.ToLower().Equals("clearfolder"))
					{
						string path;
						string text5 = (path = "");
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("path"))
						{
							path = filter(xmlReader.ReadContentAsString());
						}
						Computer computer = findComp(text5);
						FileDeleteAllMission item2 = new FileDeleteAllMission(path, computer.ip, os);
						list.Add(item2);
					}
					else if (text4.ToLower().Equals("filedownload"))
					{
						string filename2;
						string path;
						string text5 = (filename2 = (path = ""));
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("file"))
						{
							filename2 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("path"))
						{
							path = filter(xmlReader.ReadContentAsString());
						}
						FileDownloadMission item3 = new FileDownloadMission(path, filename2, text5, os);
						list.Add(item3);
					}
					else if (text4.ToLower().Equals("filechange"))
					{
						string filename2;
						string path;
						string targetKeyword;
						string text5 = (filename2 = (path = (targetKeyword = "")));
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("file"))
						{
							filename2 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("path"))
						{
							path = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("keyword"))
						{
							targetKeyword = filter(xmlReader.ReadContentAsString());
						}
						bool isRemoval = false;
						if (xmlReader.MoveToAttribute("removal"))
						{
							isRemoval = xmlReader.ReadContentAsBoolean();
						}
						bool caseSensitive = false;
						if (xmlReader.MoveToAttribute("caseSensitive"))
						{
							caseSensitive = xmlReader.ReadContentAsString().ToLower() == "true";
						}
						Computer computer = findComp(text5);
						if (computer != null)
						{
							FileChangeMission fileChangeMission = new FileChangeMission(path, filename2, computer.ip, targetKeyword, os, isRemoval);
							fileChangeMission.caseSensitive = caseSensitive;
							list.Add(fileChangeMission);
						}
					}
					else if (text4.ToLower().Equals("getadmin"))
					{
						string text5 = "";
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						GetAdminMission item4 = new GetAdminMission(findComp(text5).ip, os);
						list.Add(item4);
					}
					else if (text4.ToLower().Equals("getstring"))
					{
						string text5 = "";
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						GetStringMission item5 = new GetStringMission(text5);
						list.Add(item5);
					}
					else if (text4.ToLower().Equals("delay"))
					{
						float time = 1f;
						if (xmlReader.MoveToAttribute("time"))
						{
							time = xmlReader.ReadContentAsFloat();
						}
						DelayMission item6 = new DelayMission(time);
						list.Add(item6);
					}
					else if (text4.ToLower().Equals("hasflag"))
					{
						string text5 = "";
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						CheckFlagSetMission item7 = new CheckFlagSetMission(text5, os);
						list.Add(item7);
					}
					if (text4.ToLower().Equals("fileupload"))
					{
						bool needsDecrypt = false;
						string filename2;
						string path;
						string computerToUploadToIP;
						string destToUploadToPath;
						string decryptPass;
						string text5 = (filename2 = (path = (computerToUploadToIP = (destToUploadToPath = (decryptPass = "")))));
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("file"))
						{
							filename2 = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("path"))
						{
							path = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("destTarget"))
						{
							computerToUploadToIP = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("destPath"))
						{
							destToUploadToPath = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("decryptPass"))
						{
							decryptPass = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("decrypt"))
						{
							needsDecrypt = xmlReader.ReadContentAsBoolean();
						}
						FileUploadMission item8 = new FileUploadMission(path, filename2, text5, computerToUploadToIP, destToUploadToPath, os, needsDecrypt, decryptPass);
						list.Add(item8);
					}
					else if (text4.ToLower().Equals("adddegree"))
					{
						string uniName;
						string degreeName;
						string targetName = (uniName = (degreeName = ""));
						float desiredGPA = -1f;
						if (xmlReader.MoveToAttribute("owner"))
						{
							targetName = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("degree"))
						{
							degreeName = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("uni"))
						{
							uniName = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("gpa"))
						{
							desiredGPA = xmlReader.ReadContentAsFloat();
						}
						AddDegreeMission item9 = new AddDegreeMission(targetName, degreeName, uniName, desiredGPA, os);
						list.Add(item9);
					}
					else if (text4.ToLower().Equals("wipedegrees"))
					{
						string targetName = "";
						if (xmlReader.MoveToAttribute("owner"))
						{
							targetName = filter(xmlReader.ReadContentAsString());
						}
						WipeDegreesMission item10 = new WipeDegreesMission(targetName, os);
						list.Add(item10);
					}
					else if (text4.ToLower().Equals("removeDeathRowRecord".ToLower()))
					{
						string firstName = "UNKNOWN";
						string lastName = "UNKNOWN";
						if (xmlReader.MoveToAttribute("name"))
						{
							string text6 = filter(xmlReader.ReadContentAsString());
							string[] array = text6.Split(' ');
							firstName = array[0];
							lastName = array[1];
						}
						if (xmlReader.MoveToAttribute("fname"))
						{
							firstName = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("lname"))
						{
							lastName = filter(xmlReader.ReadContentAsString());
						}
						DeathRowRecordRemovalMission item11 = new DeathRowRecordRemovalMission(firstName, lastName, os);
						list.Add(item11);
					}
					else if (text4.ToLower().Equals("modifyDeathRowRecord".ToLower()))
					{
						string firstName = "UNKNOWN";
						string lastName = "UNKNOWN";
						string text7 = null;
						if (xmlReader.MoveToAttribute("name"))
						{
							string text6 = filter(xmlReader.ReadContentAsString());
							string[] array = text6.Split(' ');
							firstName = array[0];
							lastName = array[1];
						}
						if (xmlReader.MoveToAttribute("fname"))
						{
							firstName = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("lname"))
						{
							lastName = filter(xmlReader.ReadContentAsString());
						}
						xmlReader.MoveToContent();
						text7 = xmlReader.ReadElementContentAsString();
						DeathRowRecordModifyMission item12 = new DeathRowRecordModifyMission(firstName, lastName, text7, os);
						list.Add(item12);
					}
					else if (text4.ToLower().Equals("sendemail"))
					{
						string proposedEmailSubject;
						string mailRecipient = (proposedEmailSubject = "");
						string mailServerID = "jmail";
						if (xmlReader.MoveToAttribute("mailServer"))
						{
							mailServerID = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("recipient"))
						{
							mailRecipient = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("subject"))
						{
							proposedEmailSubject = filter(xmlReader.ReadContentAsString());
						}
						SendEmailMission item13 = new SendEmailMission(mailServerID, mailRecipient, proposedEmailSubject, os);
						list.Add(item13);
					}
					else if (text4.ToLower().Equals("databaseentrychange"))
					{
						string operation;
						string fieldName;
						string targetValue;
						string recordName;
						string computerIP = (operation = (fieldName = (targetValue = (recordName = null))));
						if (xmlReader.MoveToAttribute("comp"))
						{
							computerIP = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("operation"))
						{
							operation = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("fieldName"))
						{
							fieldName = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("targetValue"))
						{
							targetValue = filter(xmlReader.ReadContentAsString());
						}
						if (xmlReader.MoveToAttribute("recordName"))
						{
							recordName = filter(xmlReader.ReadContentAsString());
						}
						DatabaseEntryChangeMission item14 = new DatabaseEntryChangeMission(computerIP, os, operation, fieldName, targetValue, recordName);
						list.Add(item14);
					}
					else if (text4.ToLower().Equals("getadminpasswordstring"))
					{
						string text5 = "";
						if (xmlReader.MoveToAttribute("target"))
						{
							text5 = filter(xmlReader.ReadContentAsString());
						}
						GetAdminPasswordStringMission item15 = new GetAdminPasswordStringMission(findComp(text5).ip, os);
						list.Add(item15);
					}
				}
				catch (Exception ex)
				{
					if (ex is NullReferenceException)
					{
						throw new FormatException("Error loading mission Goal \"" + text4 + "\"\r\nNullReferenceException - this means something referenced by an ID (probably a computer) or a filename (missions/scripts etc) was not found.");
					}
					throw new FormatException("Error loading mission Goal \"" + text4 + "\"", ex);
				}
			}
			xmlReader.Read();
		}
		while (xmlReader.Name != "nextMission" && xmlReader.Name != "missionEnd" && xmlReader.Name != "missionStart" && !xmlReader.EOF)
		{
			xmlReader.Read();
		}
		if (xmlReader.EOF)
		{
			throw new FormatException("Unexpected end of file looking for nextMission tag in computer.\nYour tags might be out of order!\n");
		}
		if (xmlReader.Name.Equals("missionStart"))
		{
			int num = 1;
			bool flag = (Settings.IsInExtensionMode ? true : false);
			if (xmlReader.MoveToAttribute("val"))
			{
				num = xmlReader.ReadContentAsInt();
			}
			if (xmlReader.MoveToAttribute("suppress"))
			{
				flag = xmlReader.ReadContentAsBoolean();
			}
			xmlReader.MoveToContent();
			string text8 = xmlReader.ReadElementContentAsString();
			if (flag)
			{
				text2 = text8;
				val2 = num;
			}
			else
			{
				try
				{
					MissionFunctions.runCommand(num, text8);
				}
				catch (Exception ex)
				{
					Utils.AppendToErrorFile("Mission Start function exception!\r\n" + Utils.GenerateReportFromException(ex));
				}
			}
			xmlReader.Read();
		}
		while (xmlReader.NodeType != XmlNodeType.Element)
		{
			xmlReader.Read();
		}
		if (xmlReader.Name.Equals("missionEnd"))
		{
			int num = 1;
			if (xmlReader.MoveToAttribute("val"))
			{
				num = xmlReader.ReadContentAsInt();
			}
			xmlReader.MoveToContent();
			text = xmlReader.ReadElementContentAsString();
			val = num;
		}
		bool willSendEmail = true;
		while (!xmlReader.Name.Equals("nextMission"))
		{
			if (xmlReader.EOF)
			{
				throw new FormatException("Could not find \"nextMission\" tag in mission file! This tag needs to exist!");
			}
			xmlReader.Read();
		}
		if (xmlReader.MoveToAttribute("IsSilent"))
		{
			string text8 = xmlReader.ReadContentAsString().ToLower();
			willSendEmail = text8.Equals("false");
		}
		string text9 = "NONE";
		xmlReader.MoveToContent();
		text9 = xmlReader.ReadElementContentAsString();
		if (os.branchMissions != null)
		{
			os.branchMissions.Clear();
		}
		while (xmlReader.Name != "posting" && xmlReader.Name != "email")
		{
			if (xmlReader.Name.Equals("branchMissions"))
			{
				xmlReader.Read();
				List<ActiveMission> list2 = new List<ActiveMission>();
				while (!xmlReader.Name.Equals("branchMissions") || xmlReader.IsStartElement())
				{
					if (xmlReader.Name == "branch")
					{
						xmlReader.MoveToContent();
						string text10 = xmlReader.ReadElementContentAsString();
						string text11 = "Content/Missions/";
						if (Settings.IsInExtensionMode)
						{
							text11 = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
						}
						list2.Add((ActiveMission)readMission(text11 + text10));
					}
					xmlReader.Read();
				}
				os.branchMissions = list2;
			}
			xmlReader.Read();
			if (xmlReader.EOF)
			{
				throw new FormatException("email tag not found where it was expected! You may have tags out of order.");
			}
		}
		int difficulty;
		int requiredRank = (difficulty = 0);
		string text12;
		string client;
		string target;
		string postingBody = (text12 = (client = (target = "UNKNOWN")));
		string text13 = null;
		while (xmlReader.Name != "posting" && xmlReader.Name != "email")
		{
			if (xmlReader.EOF)
			{
				throw new FormatException("email tag not found where it was expected! You may have tags out of order.");
			}
			xmlReader.Read();
		}
		if (xmlReader.Name.Equals("posting"))
		{
			if (xmlReader.MoveToAttribute("title"))
			{
				text12 = xmlReader.ReadContentAsString();
			}
			if (xmlReader.MoveToAttribute("reqs"))
			{
				text13 = xmlReader.ReadContentAsString();
			}
			if (xmlReader.MoveToAttribute("requiredRank"))
			{
				requiredRank = xmlReader.ReadContentAsInt();
			}
			if (xmlReader.MoveToAttribute("difficulty"))
			{
				difficulty = xmlReader.ReadContentAsInt();
			}
			if (xmlReader.MoveToAttribute("client"))
			{
				client = xmlReader.ReadContentAsString();
			}
			if (xmlReader.MoveToAttribute("target"))
			{
				target = xmlReader.ReadContentAsString();
			}
			xmlReader.MoveToContent();
			postingBody = xmlReader.ReadElementContentAsString();
			text12 = filter(text12);
			postingBody = filter(postingBody);
		}
		string text14 = "UNKNOWN";
		string text15 = "UNKNOWN";
		string text16 = "UNKNOWN";
		while (xmlReader.Name != "email")
		{
			if (xmlReader.EOF)
			{
				throw new FormatException("email tag was not found!");
			}
			xmlReader.Read();
		}
		while (xmlReader.Name != "sender")
		{
			if (xmlReader.EOF)
			{
				throw new FormatException("sender tag was not found!");
			}
			xmlReader.Read();
		}
		text15 = xmlReader.ReadElementContentAsString();
		while (xmlReader.Name != "subject")
		{
			if (xmlReader.EOF)
			{
				throw new FormatException("subject tag was not found!");
			}
			xmlReader.Read();
		}
		text14 = xmlReader.ReadElementContentAsString();
		while (xmlReader.Name != "body")
		{
			if (xmlReader.EOF)
			{
				throw new FormatException("body tag was not found!");
			}
			xmlReader.Read();
		}
		text16 = xmlReader.ReadElementContentAsString();
		text16.Trim();
		text16 = filter(text16);
		text14 = filter(text14);
		text15 = filter(text15);
		while (xmlReader.Name != "attachments")
		{
			if (xmlReader.EOF)
			{
				throw new FormatException("attachments tag was not found! A mission must have an attachments tag even if it contains nothing.");
			}
			xmlReader.Read();
		}
		xmlReader.Read();
		List<string> attachments = new List<string>();
		while (xmlReader.Name != "attachments")
		{
			if (xmlReader.Name.Equals("link"))
			{
				string compname = "";
				if (xmlReader.MoveToAttribute("comp"))
				{
					compname = filter(xmlReader.ReadContentAsString());
				}
				Computer c = null;
				for (int i = 0; i < os.netMap.nodes.Count; i++)
				{
					if (os.netMap.nodes[i].idName.Equals(compname))
					{
						c = os.netMap.nodes[i];
					}
				}
				if (c != null)
				{
					attachments.Add("link#%#" + c.name + "#%#" + c.ip);
				}
				else
				{
					postAllLoadedActions = (Action)Delegate.Combine(postAllLoadedActions, (Action)delegate
					{
						for (int j = 0; j < os.netMap.nodes.Count; j++)
						{
							if (os.netMap.nodes[j].idName.Equals(compname))
							{
								c = os.netMap.nodes[j];
							}
						}
						if (c != null)
						{
							attachments.Add("link#%#" + c.name + "#%#" + c.ip);
						}
					});
				}
			}
			if (xmlReader.Name.Equals("account"))
			{
				string value2 = "";
				if (xmlReader.MoveToAttribute("comp"))
				{
					value2 = filter(xmlReader.ReadContentAsString());
				}
				Computer computer2 = null;
				for (int i = 0; i < os.netMap.nodes.Count; i++)
				{
					if (os.netMap.nodes[i].idName.Equals(value2))
					{
						computer2 = os.netMap.nodes[i];
					}
				}
				string s;
				string s2 = (s = "UNKNOWN");
				if (xmlReader.MoveToAttribute("user"))
				{
					s2 = xmlReader.ReadContentAsString();
				}
				if (xmlReader.MoveToAttribute("pass"))
				{
					s = xmlReader.ReadContentAsString();
				}
				s2 = filter(s2);
				s = filter(s);
				if (computer2 != null)
				{
					attachments.Add("account#%#" + computer2.name + "#%#" + computer2.ip + "#%#" + s2 + "#%#" + s);
				}
			}
			if (xmlReader.Name.Equals("note"))
			{
				string text17 = "Data";
				if (xmlReader.MoveToAttribute("title"))
				{
					text17 = filter(xmlReader.ReadContentAsString());
				}
				string text18 = "ERROR LOADING NOTE";
				xmlReader.MoveToContent();
				text18 = xmlReader.ReadElementContentAsString();
				text18 = filter(text18);
				attachments.Add("note#%#" + text17 + "#%#" + text18);
			}
			xmlReader.Read();
			if (xmlReader.EOF)
			{
				throw new FormatException("attachments tag not found where it was expected! You may have tags out of order.");
			}
		}
		MailServer.EMailData email = new MailServer.EMailData(text15, text16, text14, attachments);
		ActiveMission activeMission = new ActiveMission(list, text9, email);
		activeMission.activeCheck = activeCheck;
		activeMission.ShouldIgnoreSenderVerification = shouldIgnoreSenderVerification;
		activeMission.postingBody = postingBody;
		activeMission.postingTitle = text12;
		activeMission.requiredRank = requiredRank;
		activeMission.difficulty = difficulty;
		activeMission.client = client;
		activeMission.target = target;
		activeMission.reloadGoalsSourceFile = filename;
		if (text13 != null)
		{
			activeMission.postingAcceptFlagRequirements = text13.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
		}
		activeMission.willSendEmail = willSendEmail;
		if (!text.Equals(""))
		{
			activeMission.addEndFunction(val, text);
		}
		if (!text2.Equals(""))
		{
			activeMission.addStartFunction(val2, text2);
		}
		xmlReader.Close();
		return activeMission;
	}

	public static void loadMission(string filename, bool PreventEmail = false)
	{
		ActiveMission activeMission = (ActiveMission)readMission(filename);
		os.currentMission = activeMission;
		activeMission.sendEmail(os);
	}

	public static string filter(string s)
	{
		string text = LocalizedFileLoader.FilterStringForLocalization(MissionGenerationParser.parse(s.Replace("#BINARY#", Computer.generateBinaryString(2000)).Replace("#BINARYSMALL#", Computer.generateBinaryString(800)).Replace("#PLAYERNAME#", os.defaultUser.name)
			.Replace("#PLAYER_IP#", os.thisComputer.ip)
			.Replace("#PLAYER_ACCOUNT_PASSWORD#", SaveFileManager.LastLoggedInUser.Password)
			.Replace("#RANDOM_IP#", NetworkMap.generateRandomIP())
			.Replace("#SSH_CRACK#", PortExploits.crackExeData[22])
			.Replace("#FTP_CRACK#", PortExploits.crackExeData[21])
			.Replace("#WEB_CRACK#", PortExploits.crackExeData[80])
			.Replace("#DECYPHER_PROGRAM#", PortExploits.crackExeData[9])
			.Replace("#DECHEAD_PROGRAM#", PortExploits.crackExeData[10])
			.Replace("#CLOCK_PROGRAM#", PortExploits.crackExeData[11])
			.Replace("#MEDICAL_PROGRAM#", PortExploits.crackExeData[104])
			.Replace("#SMTP_CRACK#", PortExploits.crackExeData[25])
			.Replace("#SQL_CRACK#", PortExploits.crackExeData[1433])
			.Replace("#SECURITYTRACER_PROGRAM#", PortExploits.crackExeData[4])
			.Replace("#HACKNET_EXE#", PortExploits.crackExeData[15])
			.Replace("#HEXCLOCK_EXE#", PortExploits.crackExeData[16])
			.Replace("#SEQUENCER_EXE#", PortExploits.crackExeData[17])
			.Replace("#THEMECHANGER_EXE#", PortExploits.crackExeData[14])
			.Replace("#EOS_SCANNER_EXE#", PortExploits.crackExeData[13])
			.Replace("#TRACEKILL_EXE#", PortExploits.crackExeData[12])
			.Replace("#GREEN_THEME#", ThemeManager.getThemeDataString(OSTheme.HackerGreen))
			.Replace("#WHITE_THEME#", ThemeManager.getThemeDataString(OSTheme.HacknetWhite))
			.Replace("#YELLOW_THEME#", ThemeManager.getThemeDataString(OSTheme.HacknetYellow))
			.Replace("#TEAL_THEME#", ThemeManager.getThemeDataString(OSTheme.HacknetTeal))
			.Replace("#BASE_THEME#", ThemeManager.getThemeDataString(OSTheme.HacknetBlue))
			.Replace("#PURPLE_THEME#", ThemeManager.getThemeDataString(OSTheme.HacknetPurple))
			.Replace("#MINT_THEME#", ThemeManager.getThemeDataString(OSTheme.HacknetMint))
			.Replace("#PACEMAKER_FW_WORKING#", PortExploits.ValidPacemakerFirmware)
			.Replace("#PACEMAKER_FW_DANGER#", PortExploits.DangerousPacemakerFirmware)
			.Replace("#RTSP_EXE#", PortExploits.crackExeData[554])
			.Replace("#EXT_SEQUENCER_EXE#", PortExploits.crackExeData[40])
			.Replace("#SHELL_OPENER_EXE#", PortExploits.crackExeData[41])
			.Replace("#FTP_FAST_EXE#", PortExploits.crackExeData[211])
			.Replace("#EXTENSION_FOLDER_PATH#", (ExtensionLoader.ActiveExtensionInfo != null) ? ExtensionLoader.ActiveExtensionInfo.GetFullFolderPath().Replace("/Content", "/ Content") : "ERROR GETTING PATH")
			.Replace("#PLAYERLOCATION#", "UNKNOWN")
			.Replace("\t", "    ")));
		if (DLC1SessionUpgrader.HasDLC1Installed)
		{
			text = text.Replace("#TORRENT_EXE#", PortExploits.crackExeData[6881]).Replace("#SSL_EXE#", PortExploits.crackExeData[443]).Replace("#KAGUYA_EXE#", PortExploits.crackExeData[31])
				.Replace("#SIGNAL_SCRAMBLER_EXE#", PortExploits.crackExeData[32])
				.Replace("#MEM_FORENSICS_EXE#", PortExploits.crackExeData[33])
				.Replace("#MEM_DUMP_GENERATOR#", PortExploits.crackExeData[34])
				.Replace("#PACIFIC_EXE#", PortExploits.crackExeData[192])
				.Replace("#NETMAP_ORGANIZER_EXE#", PortExploits.crackExeData[35])
				.Replace("#SHELL_CONTROLLER_EXE#", PortExploits.crackExeData[36])
				.Replace("#NOTES_DUMPER_EXE#", PortExploits.crackExeData[37])
				.Replace("#CLOCK_V2_EXE#", PortExploits.crackExeData[38])
				.Replace("#DLC_MUSIC_EXE#", PortExploits.crackExeData[39])
				.Replace("#GIBSON_IP#", os.GibsonIP);
		}
		return text;
	}

	private static Computer findComp(string target)
	{
		for (int i = 0; i < os.netMap.nodes.Count; i++)
		{
			if (os.netMap.nodes[i].idName.Equals(target))
			{
				return os.netMap.nodes[i];
			}
		}
		return null;
	}

	public static object findComputer(string target)
	{
		return findComp(target);
	}
}
