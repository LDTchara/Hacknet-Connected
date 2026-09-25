using System;
using System.Collections.Generic;
using System.Xml;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class NetworkMap : CoreModule
{
	public static int NODE_SIZE = 26;

	public static float ADMIN_CIRCLE_SCALE = 0.62f;

	public static float PULSE_DECAY = 0.5f;

	public static float PULSE_FREQUENCY = 0.8f;

	public List<Corporation> corporations;

	public List<Computer> nodes;

	public List<int> visibleNodes;

	private Texture2D circle;

	private Texture2D circleOutline;

	private Texture2D adminCircle;

	private Texture2D nodeCircle;

	private Texture2D adminNodeCircle;

	private Texture2D nodeGlow;

	private Texture2D homeNodeCircle;

	private Texture2D targetNodeCircle;

	private Texture2D assetServerNodeOverlay;

	private string label;

	private Vector2 circleOrigin;

	private float rotation = 0f;

	private float pulseFade = 1f;

	private float pulseTimer = PULSE_FREQUENCY;

	public ConnectedNodeEffect nodeEffect;

	public ConnectedNodeEffect adminNodeEffect;

	public bool DimNonConnectedNodes = false;

	public NetmapSortingAlgorithm SortingAlgorithm = NetmapSortingAlgorithm.Scatter;

	public Computer mailServer;

	public Computer academicDatabase;

	public Computer lastAddedNode;

	public NetworkMap(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
	}

	public override void LoadContent()
	{
		label = "Network Map";
		visibleNodes = new List<int>();
		if (OS.WillLoadSave || Settings.IsInExtensionMode)
		{
			nodes = new List<Computer>();
			corporations = new List<Corporation>();
		}
		else if (os.multiplayer)
		{
			nodes = generateNetwork(os);
			corporations = new List<Corporation>();
		}
		else
		{
			nodes = new List<Computer>();
			List<Computer> collection = generateGameNodes();
			nodes.Clear();
			nodes.AddRange(collection);
			if (Settings.isDemoMode)
			{
				nodes.AddRange(generateDemoNodes());
			}
			nodes.Insert(0, generateSPNetwork(os)[0]);
			corporations = generateCorporations();
		}
		nodeEffect = new ConnectedNodeEffect(os);
		adminNodeEffect = new ConnectedNodeEffect(os);
		adminNodeEffect.color = new Color(60, 65, 75, 19);
		circle = TextureBank.load("Circle", os.content);
		nodeCircle = TextureBank.load("NodeCircle", os.content);
		adminNodeCircle = TextureBank.load("AdminNodeCircle", os.content);
		homeNodeCircle = TextureBank.load("HomeNodeCircle", os.content);
		targetNodeCircle = TextureBank.load("TargetNodeCircle", os.content);
		assetServerNodeOverlay = TextureBank.load("AssetServerNodeOverlay", os.content);
		circleOutline = TextureBank.load("CircleOutline", os.content);
		adminCircle = TextureBank.load("AdminCircle", os.content);
		nodeGlow = TextureBank.load("RadialGradient", os.content);
		circleOrigin = new Vector2(circleOutline.Width / 2, circleOutline.Height / 2);
	}

	public override void Update(float t)
	{
		rotation += t / 2f;
		if (pulseFade > 0f)
		{
			pulseFade -= t * PULSE_DECAY;
		}
		else
		{
			pulseTimer -= t;
			if (pulseTimer <= 0f)
			{
				pulseFade = 1f;
				pulseTimer = PULSE_FREQUENCY;
			}
		}
		for (int i = 0; i < nodes.Count; i++)
		{
			if (nodes[i].disabled)
			{
				nodes[i].bootupTick(t);
			}
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		doGui(t);
	}

	public void CleanVisibleListofDuplicates()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < visibleNodes.Count; i++)
		{
			if (!list.Contains(visibleNodes[i]))
			{
				list.Add(visibleNodes[i]);
			}
		}
		visibleNodes = list;
	}

	public string getSaveString()
	{
		string text = $"<NetworkMap sort=\"{SortingAlgorithm}\" >\n";
		text = text + getVisibleNodesString() + "\n";
		text += "<network>\n";
		for (int i = 0; i < nodes.Count; i++)
		{
			text += nodes[i].getSaveString();
		}
		text += "</network>\n";
		return text + "</NetworkMap>";
	}

	public string getVisibleNodesString()
	{
		string text = "<visible>";
		for (int i = 0; i < visibleNodes.Count; i++)
		{
			text = text + visibleNodes[i] + ((i != visibleNodes.Count - 1) ? " " : "");
		}
		return text + "</visible>";
	}

	public void load(XmlReader reader)
	{
		nodes.Clear();
		while (reader.Name != "NetworkMap")
		{
			reader.Read();
		}
		if (reader.MoveToAttribute("sort"))
		{
			string text = reader.ReadContentAsString();
			if (!Enum.TryParse<NetmapSortingAlgorithm>(text, out SortingAlgorithm))
			{
				Console.WriteLine("Error parsing netmap sorting algorithm: " + text);
				Utils.AppendToErrorFile("Error parsing netmap sorting algorithm: " + text);
			}
		}
		while (reader.Name != "visible")
		{
			reader.Read();
		}
		string text2 = reader.ReadElementContentAsString();
		string[] array = text2.Split();
		for (int i = 0; i < array.Length; i++)
		{
			visibleNodes.Add(Convert.ToInt32(array[i]));
		}
		while (reader.Name != "network")
		{
			reader.Read();
		}
		reader.Read();
		while (reader.Name != "network")
		{
			while (reader.Name == "computer" && reader.NodeType != XmlNodeType.EndElement)
			{
				nodes.Add(Computer.load(reader, os));
			}
			while ((!(reader.Name == "computer") || reader.NodeType == XmlNodeType.EndElement) && reader.Name != "network")
			{
				reader.Read();
			}
		}
		for (int i = 0; i < nodes.Count; i++)
		{
			Computer computer = nodes[i];
			for (int j = 0; j < computer.daemons.Count; j++)
			{
				computer.daemons[j].loadInit();
			}
		}
		loadAssignGameNodes();
		Console.WriteLine("Done loading");
	}

	private void loadAssignGameNodes()
	{
		mailServer = Programs.getComputer(os, "jmail");
		academicDatabase = Programs.getComputer(os, "academic");
	}

	public List<Corporation> generateCorporations()
	{
		List<Corporation> list = new List<Corporation>();
		int num = 0;
		for (int i = 0; i < num; i++)
		{
			list.Add(new Corporation(os));
		}
		return list;
	}

	public List<Computer> generateNetwork(OS os)
	{
		List<Computer> list = new List<Computer>();
		List<Computer> list2 = new List<Computer>();
		List<Computer> list3 = new List<Computer>();
		int num = 2;
		float num2 = 0.5f;
		int num3 = 4;
		int num4 = 0;
		float num5 = num;
		bool flag = false;
		float num6 = (bounds.Width - 40) / (num3 * 2 + 3);
		float num7 = bounds.Height - 30;
		float y = 10f;
		Vector2 compLocation = new Vector2(num6, y);
		bool flag2 = false;
		float num8 = 0f;
		while (num4 >= 0 || !flag)
		{
			num8 = (compLocation.Y = num7 / (num5 + 1f));
			for (int i = 0; i < (int)num5; i++)
			{
				Computer computer = new Computer(NameGenerator.generateName(), generateRandomIP(), compLocation, num4, (byte)(Utils.flipCoin() ? 1 : 2), os);
				Utils.random.NextDouble();
				bool flag3 = 1 == 0;
				int index = Math.Min(Math.Max(num4, 0), PortExploits.services.Count - 1);
				computer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[PortExploits.portNums[index]], PortExploits.cracks[PortExploits.portNums[index]]));
				list3.Add(computer);
				list.Add(computer);
				compLocation.Y += num8;
			}
			for (int i = 0; i < list2.Count; i++)
			{
				bool flag4 = false;
				bool flag5 = false;
				bool flag6 = false;
				flag4 = i - 1 >= 0 && i < list2.Count - 1;
				flag5 = i < list3.Count && i < list2.Count;
				flag6 = i + 1 < list3.Count && i + 1 < list2.Count - 1;
				if (flag4)
				{
					list3[i - 1].links.Add(list.IndexOf(list2[i]));
					list2[i - 1].links.Add(list.IndexOf(list3[i]));
				}
				if (flag5)
				{
					list3[i].links.Add(list.IndexOf(list2[i]));
					list2[i].links.Add(list.IndexOf(list3[i]));
				}
				if (flag6)
				{
					list3[i + 1].links.Add(list.IndexOf(list2[i]));
					list2[i + 1].links.Add(list.IndexOf(list3[i]));
				}
			}
			list2.Clear();
			for (int i = 0; i < list3.Count; i++)
			{
				list2.Add(list3[i]);
			}
			list3.Clear();
			compLocation.X += num6;
			if (flag)
			{
				num4--;
				num5 -= num2;
			}
			else
			{
				num4++;
				num5 += num2;
			}
			if (num4 > num3)
			{
				num4--;
				flag = true;
				num5 += num2;
			}
			flag2 = false;
		}
		if (!os.multiplayer)
		{
			list.AddRange(generateGameNodes());
		}
		if (Settings.isDemoMode)
		{
			list.AddRange(generateDemoNodes());
		}
		return list;
	}

	public List<Computer> generateSPNetwork(OS os)
	{
		List<Computer> list = new List<Computer>();
		Computer computer = new Computer(NameGenerator.generateName(), generateRandomIP(), getRandomPosition(), 0, 2, os);
		computer.idName = "firstGeneratedNode";
		Folder folder = computer.files.root.searchForFolder("bin");
		FileEntry item = new FileEntry(Utils.readEntireFile("Content/files/config.txt"), "config.txt");
		folder.files.Add(item);
		ThemeManager.setThemeOnComputer(computer, OSTheme.HackerGreen);
		list.Add(computer);
		if (!os.IsDLCConventionDemo)
		{
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[4], "SecurityTracer.exe"));
		}
		return list;
	}

	public List<Computer> generateGameNodes()
	{
		List<Computer> list = new List<Computer>();
		Computer computer = (Computer)ComputerLoader.loadComputer("Content/Missions/CoreServers/JMailServer.xml");
		computer.location = new Vector2(0.7f, 0.2f);
		mailServer = computer;
		list.Add(computer);
		Computer computer2 = new Computer("boatmail.com", "65.55.72.183", new Vector2(0.6f, 0.9f), 4, 3, os);
		computer2.idName = "boatmail";
		computer2.daemons.Add(new BoatMail(computer2, "Boatmail", os));
		computer2.initDaemons();
		list.Add(computer2);
		Computer computer3 = (Computer)ComputerLoader.loadComputer("Content/Missions/CoreServers/InternationalAcademicDatabase.xml");
		AcademicDatabaseDaemon item = new AcademicDatabaseDaemon(computer3, "Academic Database", os);
		computer3.daemons.Add(item);
		computer3.initDaemons();
		academicDatabase = computer3;
		list.Add(computer3);
		Computer computer4 = (Computer)ComputerLoader.loadComputer("Content/Missions/CoreServers/ContractHubAssetsComp.xml");
		list.Add(computer4);
		Computer ch = (Computer)ComputerLoader.loadComputer("Content/Missions/CoreServers/ContractHubComp.xml");
		os.delayer.Post(ActionDelayer.NextTick(), delegate
		{
			MissionHubServer item2 = new MissionHubServer(ch, "CSEC Contract Database", "CSEC", os);
			ch.daemons.Add(item2);
			ch.initDaemons();
		});
		list.Add(ch);
		computer4.location = ch.location + Corporation.getNearbyNodeOffset(ch.location, 1, 1, this);
		Computer computer5 = new Computer("Cheater's Stash", "1337.1337.1337.1337", getRandomPosition(), 0, 2, os);
		computer5.idName = "haxServer";
		computer5.files.root.files.Add(new FileEntry(PortExploits.crackExeData[PortExploits.portNums[0]], PortExploits.cracks[PortExploits.portNums[0]]));
		computer5.files.root.files.Add(new FileEntry(PortExploits.crackExeData[PortExploits.portNums[1]], PortExploits.cracks[PortExploits.portNums[1]]));
		computer5.files.root.files.Add(new FileEntry(PortExploits.crackExeData[PortExploits.portNums[2]], PortExploits.cracks[PortExploits.portNums[2]]));
		computer5.files.root.files.Add(new FileEntry(PortExploits.crackExeData[PortExploits.portNums[3]], PortExploits.cracks[PortExploits.portNums[3]]));
		computer5.files.root.files.Add(GetProgramForNum(1433));
		computer5.files.root.files.Add(GetProgramForNum(104));
		computer5.files.root.files.Add(GetProgramForNum(9));
		computer5.files.root.files.Add(GetProgramForNum(13));
		computer5.files.root.files.Add(GetProgramForNum(10));
		list.Add(computer5);
		return list;
	}

	private FileEntry GetProgramForNum(int num)
	{
		return new FileEntry(PortExploits.crackExeData[num], PortExploits.cracks[num]);
	}

	public List<Computer> generateDemoNodes()
	{
		List<Computer> list = new List<Computer>();
		Computer computer = new Computer("AvCon Hatland Demo PC", "192.168.1.3", getRandomPosition(), 1, 2, os);
		computer.idName = "avcon1";
		computer.externalCounterpart = new ExternalCounterpart("avcon1", ExternalCounterpart.getIPForServerName("avconServer"));
		list.Add(computer);
		return list;
	}

	public void discoverNode(Computer c)
	{
		if (!visibleNodes.Contains(nodes.IndexOf(c)))
		{
			visibleNodes.Add(nodes.IndexOf(c));
		}
		c.highlightFlashTime = 1f;
		lastAddedNode = c;
	}

	public void discoverNode(string cName)
	{
		for (int i = 0; i < nodes.Count; i++)
		{
			if (nodes[i].idName.Equals(cName))
			{
				discoverNode(nodes[i]);
				break;
			}
		}
	}

	public Vector2 getRandomPosition()
	{
		for (int i = 0; i < 50; i++)
		{
			Vector2 vector = generatePos();
			if (!collides(vector))
			{
				return vector;
			}
		}
		return generatePos();
	}

	private Vector2 generatePos()
	{
		float num = NODE_SIZE;
		return new Vector2((float)Utils.random.NextDouble(), (float)Utils.random.NextDouble());
	}

	public bool collides(Vector2 location, float minSeperation = -1f)
	{
		if (nodes == null)
		{
			return false;
		}
		float num = 0.075f;
		if (minSeperation > 0f)
		{
			num = minSeperation;
		}
		for (int i = 0; i < nodes.Count; i++)
		{
			if (Vector2.Distance(location, nodes[i].location) <= num)
			{
				return true;
			}
		}
		return false;
	}

	public void randomizeNetwork()
	{
		for (int i = 0; i < 10; i++)
		{
			Vector2 compLocation = new Vector2((float)(Utils.random.NextDouble() * (double)bounds.Width), (float)(Utils.random.NextDouble() * (double)bounds.Height));
			nodes.Add(new Computer(NameGenerator.generateName(), generateRandomIP(), compLocation, Utils.random.Next(0, 4), Utils.randomCompType(), os));
		}
		int num = 2;
		for (int i = 0; i < nodes.Count; i++)
		{
			int num2 = num + Utils.random.Next(0, 2);
			for (int j = 0; j < num2; j++)
			{
				nodes[i].links.Add(Utils.random.Next(0, nodes.Count - 1));
			}
		}
	}

	public void doGui(float t)
	{
		int num = -1;
		Color highlightColor = os.highlightColor;
		for (int i = 0; i < nodes.Count; i++)
		{
			Vector2 nodeDrawPos = GetNodeDrawPos(nodes[i], i);
			if (!visibleNodes.Contains(i) || nodes[i].disabled)
			{
				continue;
			}
			for (int j = 0; j < nodes[i].links.Count; j++)
			{
				if (visibleNodes.Contains(nodes[i].links[j]) && !nodes[nodes[i].links[j]].disabled)
				{
					drawLine(GetNodeDrawPos(nodes[i], i), GetNodeDrawPos(nodes[nodes[i].links[j]], nodes[i].links[j]), new Vector2(bounds.X, bounds.Y));
				}
			}
			if (pulseFade > 0f)
			{
				Color color = os.highlightColor * (pulseFade * pulseFade);
				if (DimNonConnectedNodes)
				{
					color = Utils.AddativeRed * 0.5f * (pulseFade * pulseFade);
				}
				spriteBatch.Draw(circleOutline, new Vector2(bounds.X + (int)nodeDrawPos.X + NODE_SIZE / 2, bounds.Y + (int)nodeDrawPos.Y + NODE_SIZE / 2), null, color, rotation, circleOrigin, new Vector2(ADMIN_CIRCLE_SCALE * (2f - 2f * pulseFade)), SpriteEffects.None, 0.5f);
			}
			if (nodes[i].idName == os.homeNodeID && !nodes[i].disabled)
			{
				spriteBatch.Draw(homeNodeCircle, new Vector2(bounds.X + (int)nodeDrawPos.X + NODE_SIZE / 2, bounds.Y + (int)nodeDrawPos.Y + NODE_SIZE / 2), null, (os.connectedComp == null || os.connectedComp != nodes[i]) ? (nodes[i].Equals(os.thisComputer) ? os.thisComputerNode : os.highlightColor) : ((nodes[i].adminIP == os.thisComputer.ip) ? Utils.AddativeWhite : Utils.AddativeWhite), -1f * rotation, circleOrigin, new Vector2(ADMIN_CIRCLE_SCALE), SpriteEffects.None, 0.5f);
			}
			if (nodes[i] == lastAddedNode && !nodes[i].disabled)
			{
				float num2 = ((nodes[i].adminIP == os.thisComputer.ip) ? 1f : 4f);
				spriteBatch.Draw(targetNodeCircle, new Vector2(bounds.X + (int)nodeDrawPos.X + NODE_SIZE / 2, bounds.Y + (int)nodeDrawPos.Y + NODE_SIZE / 2), null, (os.connectedComp == null || os.connectedComp != nodes[i]) ? (nodes[i].Equals(os.thisComputer) ? os.thisComputerNode : ((nodes[i].adminIP == os.thisComputer.ip) ? os.highlightColor : Utils.AddativeWhite)) : ((nodes[i].adminIP == os.thisComputer.ip) ? os.highlightColor : Utils.AddativeWhite), -1f * rotation, circleOrigin, new Vector2(ADMIN_CIRCLE_SCALE) + Vector2.One * (float)(Math.Sin((double)os.timer * 3.0) * (double)num2) / targetNodeCircle.Height, SpriteEffects.None, 0.5f);
			}
			if (nodes[i].adminIP == os.thisComputer.ip && !nodes[i].disabled)
			{
				spriteBatch.Draw(nodes[i].ip.Equals(os.thisComputer.ip) ? adminCircle : nodeGlow, new Vector2(bounds.X + (int)nodeDrawPos.X + NODE_SIZE / 2, bounds.Y + (int)nodeDrawPos.Y + NODE_SIZE / 2), null, nodes[i].Equals(os.thisComputer) ? os.thisComputerNode : os.highlightColor, rotation, circleOrigin, new Vector2(ADMIN_CIRCLE_SCALE), SpriteEffects.None, 0.5f);
			}
		}
		lock (nodes)
		{
			for (int i = 0; i < nodes.Count; i++)
			{
				if (!visibleNodes.Contains(i) || nodes[i].disabled)
				{
					continue;
				}
				if (os.thisComputer.ip == nodes[i].ip)
				{
					highlightColor = os.thisComputerNode;
				}
				else
				{
					highlightColor = ((os.connectedComp != null) ? ((os.connectedComp.ip == nodes[i].ip) ? Color.White : ((nodes[i].adminIP == os.thisComputer.ip && nodes[i].ip == os.opponentLocation) ? Color.DarkRed : ((!os.shellIPs.Contains(nodes[i].ip)) ? os.highlightColor : os.shellColor))) : ((nodes[i].adminIP == os.thisComputer.ip && nodes[i].ip == os.opponentLocation) ? Color.DarkRed : ((!os.shellIPs.Contains(nodes[i].ip)) ? os.highlightColor : os.shellColor)));
					if (nodes[i].highlightFlashTime > 0f)
					{
						nodes[i].highlightFlashTime -= t;
						highlightColor = Color.Lerp(highlightColor, Utils.AddativeWhite, Utils.QuadraticOutCurve(nodes[i].highlightFlashTime));
					}
				}
				Vector2 nodeDrawPos2 = GetNodeDrawPos(nodes[i], i);
				if (DimNonConnectedNodes && os.connectedComp != null && os.connectedComp.ip != nodes[i].ip)
				{
					highlightColor *= 0.3f;
				}
				if (Button.doButton(2000 + i, bounds.X + (int)nodeDrawPos2.X, bounds.Y + (int)nodeDrawPos2.Y, NODE_SIZE, NODE_SIZE, "", highlightColor, (nodes[i].adminIP == os.thisComputer.ip) ? adminNodeCircle : nodeCircle) && os.inputEnabled)
				{
					bool flag = false;
					if (os.terminal.preventingExecution && os.terminal.executionPreventionIsInteruptable)
					{
						os.terminal.executeLine();
						flag = true;
					}
					int nodeindex = i;
					Action action = delegate
					{
						os.runCommand("connect " + nodes[nodeindex].ip);
					};
					if (flag)
					{
						os.delayer.Post(ActionDelayer.NextTick(), action);
					}
					else
					{
						action();
					}
				}
				if (GuiData.hot == 2000 + i)
				{
					num = i;
				}
				if (nodes[i].idName == os.homeAssetServerID && !nodes[i].disabled)
				{
					spriteBatch.Draw(assetServerNodeOverlay, new Vector2(bounds.X + (int)nodeDrawPos2.X + NODE_SIZE / 2, bounds.Y + (int)nodeDrawPos2.Y + NODE_SIZE / 2), null, (num == i) ? GuiData.Default_Lit_Backing_Color : ((os.connectedComp != null && os.connectedComp == nodes[i]) ? Color.Black : (nodes[i].Equals(os.thisComputer) ? os.thisComputerNode : os.highlightColor)), -0.5f * rotation, new Vector2(assetServerNodeOverlay.Width / 2), new Vector2(ADMIN_CIRCLE_SCALE - 0.22f), SpriteEffects.None, 0.5f);
				}
			}
		}
		int nodeIndex = ((os.connectedComp != null) ? nodes.IndexOf(os.connectedComp) : 0);
		if (os.connectedComp != null && !os.connectedComp.Equals(os.thisComputer) && !os.connectedComp.adminIP.Equals(os.thisComputer.ip))
		{
			GuiData.spriteBatch.Draw(nodeGlow, GetNodeDrawPos(os.connectedComp, nodeIndex) - new Vector2(nodeGlow.Width / 2, nodeGlow.Height / 2) + new Vector2(bounds.X, bounds.Y) + new Vector2(NODE_SIZE / 2), os.connectedNodeHighlight);
			if (nodeEffect != null && os.connectedComp != null)
			{
				nodeEffect.draw(spriteBatch, GetNodeDrawPos(os.connectedComp, nodeIndex) + new Vector2(NODE_SIZE / 2) + new Vector2(bounds.X, bounds.Y));
			}
		}
		else if (os.connectedComp != null && !os.connectedComp.Equals(os.thisComputer) && adminNodeEffect != null)
		{
			adminNodeEffect.draw(spriteBatch, GetNodeDrawPos(os.connectedComp, nodeIndex) + new Vector2(NODE_SIZE / 2) + new Vector2(bounds.X, bounds.Y));
		}
		if (num != -1)
		{
			try
			{
				int i = num;
				int num3 = i;
				Vector2 nodeDrawPos3 = GetNodeDrawPos(nodes[num3], num3);
				Vector2 ttpos = default(Vector2);
				ref Vector2 reference = ref ttpos;
				reference = new Vector2(bounds.X + (int)nodeDrawPos3.X + NODE_SIZE, bounds.Y + (int)nodeDrawPos3.Y);
				string text = nodes[num3].getTooltipString();
				Vector2 textSize = GuiData.tinyfont.MeasureString(text);
				OS oS = os;
				oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
				{
					GuiData.spriteBatch.Draw(Utils.white, new Rectangle((int)ttpos.X, (int)ttpos.Y, (int)textSize.X, (int)textSize.Y), os.netmapToolTipBackground);
					TextItem.doFontLabel(ttpos, text, GuiData.tinyfont, os.netmapToolTipColor);
				});
			}
			catch (Exception ex)
			{
				DebugLog.add(ex.ToString());
			}
		}
		if (Settings.debugDrawEnabled)
		{
			for (int i = 0; i < Corporation.TestedPositions.Count; i++)
			{
				Vector2 nodeDrawPos3 = GetNodeDrawPosDebug(Corporation.TestedPositions[i]);
				Vector2 position = new Vector2(bounds.X + (int)nodeDrawPos3.X + NODE_SIZE, bounds.Y + (int)nodeDrawPos3.Y);
				GuiData.spriteBatch.Draw(Utils.white, position, null, Utils.AddativeRed, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0.8f);
			}
		}
	}

	public Vector2 GetNodeDrawPosDebug(Vector2 nodeLocation)
	{
		int num = 3;
		nodeLocation = Utils.Clamp(nodeLocation, 0f, 1f);
		float num2 = (float)bounds.Width - (float)NODE_SIZE * 1f;
		float num3 = (float)bounds.Height - (float)NODE_SIZE * 1f;
		num2 -= (float)(2 * num);
		num3 -= (float)(2 * num);
		return new Vector2(nodeLocation.X * num2 + (float)NODE_SIZE / 4f, nodeLocation.Y * num3 + (float)NODE_SIZE / 4f);
	}

	public Vector2 GetNodeDrawPos(Computer node)
	{
		return GetNodeDrawPos(node, nodes.IndexOf(node));
	}

	public Vector2 GetNodeDrawPos(Computer node, int nodeIndex)
	{
		int num = 3;
		Vector2 vector = Utils.Clamp(node.location, 0f, 1f);
		float num2 = (float)bounds.Width - (float)NODE_SIZE * 1f;
		float num3 = (float)bounds.Height - (float)NODE_SIZE * 1f;
		num2 -= (float)(2 * num);
		num3 -= (float)(2 * num);
		Vector2 nodePosition = NetmapSortingAlgorithms.GetNodePosition(SortingAlgorithm, num2, num3, node, nodeIndex, nodes.Count, visibleNodes.Count, os);
		return nodePosition + new Vector2((float)NODE_SIZE / 4f);
	}

	public static string generateRandomIP()
	{
		return Utils.random.Next(254) + 1 + "." + (Utils.random.Next(254) + 1) + "." + (Utils.random.Next(254) + 1) + "." + (Utils.random.Next(254) + 1);
	}

	public void drawLine(Vector2 origin, Vector2 dest, Vector2 offset)
	{
		Vector2 vector = new Vector2(NODE_SIZE / 2);
		origin += vector;
		dest += vector;
		float y = Vector2.Distance(origin, dest);
		float num = (float)Math.Atan2(dest.Y - origin.Y, dest.X - origin.X);
		num += 4.712389f;
		spriteBatch.Draw(Utils.white, origin + offset, null, os.outlineColor, num, Vector2.Zero, new Vector2(1f, y), SpriteEffects.None, 0.5f);
	}
}
