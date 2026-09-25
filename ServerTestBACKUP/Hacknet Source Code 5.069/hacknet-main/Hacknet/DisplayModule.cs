using System;
using System.Collections.Generic;
using System.Text;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.Localization;
using Hacknet.Modules.Helpers;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class DisplayModule : CoreModule
{
	private const int MAX_DISPLAY_STRING_LENGTH = 6000;

	public string command = "";

	public string[] commandArgs;

	public Folder LastDisplayedFileFolder = null;

	public string LastDisplayedFileSourceIP = null;

	private int x;

	private int y;

	private Rectangle tmpRect;

	private List<Texture2D> computers;

	private Dictionary<string, Texture2D> compAltIcons;

	private Texture2D defaultComputer;

	private Texture2D lockSprite;

	private Texture2D openLockSprite;

	private Texture2D fancyCornerSprite;

	private Texture2D fancyPanelSprite;

	private DisplayModuleLSHelper lsModuleHelper = new DisplayModuleLSHelper();

	private ScrollableTextRegion catTextRegion;

	private Vector2 scroll;

	private Vector2 catScroll;

	private int errorCount = 0;

	private bool hasSentErrorEmail = false;

	private string invioableSecurityCacheString = null;

	private float invioabilityCharChangeTimer = 0f;

	private string loginDetailsCache = null;

	private bool lockLoginDisplayCache = false;

	public DisplayModule(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		catTextRegion = new ScrollableTextRegion(GuiData.spriteBatch.GraphicsDevice);
	}

	public override void LoadContent()
	{
		computers = new List<Texture2D>();
		computers.Add(TextureBank.load("Sprites/CompLogos/Sec0Computer", os.content));
		computers.Add(TextureBank.load("Sprites/CompLogos/Sec1Computer", os.content));
		computers.Add(TextureBank.load("Sprites/CompLogos/Computer", os.content));
		computers.Add(TextureBank.load("Sprites/CompLogos/OldServer", os.content));
		computers.Add(TextureBank.load("Sprites/CompLogos/Sec2Computer", os.content));
		computers.Add(TextureBank.load("Sprites/CompLogos/Sec2Computer", os.content));
		compAltIcons = new Dictionary<string, Texture2D>();
		compAltIcons.Add("laptop", TextureBank.load("Sprites/CompLogos/Laptop", os.content));
		compAltIcons.Add("chip", TextureBank.load("Sprites/CompLogos/Chip", os.content));
		compAltIcons.Add("kellis", TextureBank.load("Sprites/CompLogos/KellisCompIcon", os.content));
		compAltIcons.Add("tablet", TextureBank.load("Sprites/CompLogos/Tablet", os.content));
		compAltIcons.Add("ePhone", TextureBank.load("Sprites/CompLogos/Phone1", os.content));
		compAltIcons.Add("ePhone2", TextureBank.load("Sprites/CompLogos/Phone2", os.content));
		if (DLC1SessionUpgrader.HasDLC1Installed)
		{
			compAltIcons.Add("Psylance", TextureBank.load("DLC/Sprites/Psylance", os.content));
			compAltIcons.Add("PacificAir", TextureBank.load("DLC/Sprites/PacificAir", os.content));
			compAltIcons.Add("Alchemist", TextureBank.load("DLC/Sprites/AlchemistsIcon", os.content));
			compAltIcons.Add("DLCLaptop", TextureBank.load("DLC/Icons/Laptop", os.content));
			compAltIcons.Add("DLCPC1", TextureBank.load("DLC/Icons/PC1", os.content));
			compAltIcons.Add("DLCPC2", TextureBank.load("DLC/Icons/PC2", os.content));
			compAltIcons.Add("DLCServer", TextureBank.load("DLC/Icons/Server", os.content));
		}
		defaultComputer = TextureBank.load("Sprites/CompLogos/Computer", os.content);
		lockSprite = TextureBank.load("Lock", os.content);
		openLockSprite = TextureBank.load("OpenLock", os.content);
		fancyCornerSprite = TextureBank.load("Corner", os.content);
		fancyPanelSprite = TextureBank.load("Panel", os.content);
		tmpRect = default(Rectangle);
		scroll = Vector2.Zero;
		catScroll = Vector2.Zero;
	}

	public override void Update(float t)
	{
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		try
		{
			doCommandModule();
			errorCount = 0;
		}
		catch (Exception ex)
		{
			string text = Utils.GenerateReportFromException(ex);
			Console.WriteLine(text);
			if (OS.DEBUG_COMMANDS)
			{
				os.write("ERROR RENDERING DAEMON: " + command);
				os.write(Utils.GenerateReportFromExceptionCompact(ex));
			}
			errorCount++;
			if (errorCount >= 3)
			{
				if (!hasSentErrorEmail)
				{
					Utils.SendThreadedErrorReport(ex, LocaleTerms.Loc("Display module Crash: ") + command, DebugLog.GetDump());
					hasSentErrorEmail = true;
				}
				command = "connect";
				commandArgs = new string[1];
				commandArgs[0] = "connect";
				errorCount = 0;
			}
			else
			{
				Utils.AppendToErrorFile(text);
			}
		}
	}

	public void typeChanged()
	{
		scroll = Vector2.Zero;
		catScroll = Vector2.Zero;
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
	}

	private void doCommandModule()
	{
		x = bounds.X + 5;
		y = bounds.Y + 5;
		spriteBatch.Draw(Utils.white, bounds, os.displayModuleExtraLayerBackingColor);
		if (!command.Equals("login"))
		{
			lockLoginDisplayCache = false;
			loginDetailsCache = null;
		}
		for (int i = 0; i < os.exes.Count; i++)
		{
			if (os.exes[i] is MainDisplayOverrideEXE { DisplayOverrideIsActive: not false } mainDisplayOverrideEXE && !os.exes[i].isExiting)
			{
				mainDisplayOverrideEXE.RenderMainDisplay(bounds, spriteBatch);
				return;
			}
		}
		if (os.connectedComp != null && os.connectedComp.getDaemon(typeof(PorthackHeartDaemon)) is PorthackHeartDaemon)
		{
			doDaemonDisplay();
		}
		else if (command.Equals("ls") || command.Equals("dir") || command.Equals("cd"))
		{
			doLsDisplay();
		}
		else if (command.Equals("connect"))
		{
			doConnectDisplay();
			invioableSecurityCacheString = null;
		}
		else if (command.Equals("cat") || command.Equals("less"))
		{
			doCatDisplay();
		}
		else if (command.ToLower().Equals("probe") || command.Equals("nmap"))
		{
			doProbeDisplay();
		}
		else if (command.Equals("dc") || command.Equals("disconnect") || command.Equals("crash"))
		{
			doDisconnectDisplay();
		}
		else if (command.Equals("login"))
		{
			doLoginDisplay();
		}
		else if (command.Equals("connectiondenied"))
		{
			doDisconnectForcedDisplay();
		}
		else
		{
			doDaemonDisplay();
		}
	}

	private void doDaemonDisplay()
	{
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		for (int i = 0; i < computer.daemons.Count; i++)
		{
			if (computer.daemons[i].name.Equals(command) || computer.daemons[i] is PorthackHeartDaemon)
			{
				computer.daemons[i].draw(bounds, spriteBatch);
				break;
			}
		}
	}

	private void doLoginDisplay()
	{
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		if (!lockLoginDisplayCache)
		{
			loginDetailsCache = os.displayCache;
		}
		string[] array = loginDetailsCache.Split(separator, StringSplitOptions.None);
		string text = "";
		string text2 = "";
		int num = -1;
		int num2 = 0;
		if (array[0].Equals("loginData"))
		{
			text = ((!(array[1] != "")) ? os.terminal.currentLine : array[1]);
			if (array.Length > 2)
			{
				num2 = 1;
				text2 = array[2];
				if (text2.Equals(""))
				{
					for (int i = 0; i < os.terminal.currentLine.Length; i++)
					{
						text2 += "*";
					}
				}
				else
				{
					string text3 = "";
					for (int i = 0; i < text2.Length; i++)
					{
						text3 += "*";
					}
					text2 = text3;
				}
			}
			if (array.Length > 3)
			{
				num2 = 2;
				num = Convert.ToInt32(array[3]);
			}
		}
		doConnectHeader();
		Rectangle destinationRectangle = GuiData.tmpRect;
		destinationRectangle.X = bounds.X + 2;
		destinationRectangle.Y = y;
		destinationRectangle.Height = 200;
		destinationRectangle.Width = bounds.Width - 4;
		spriteBatch.Draw(Utils.white, destinationRectangle, (num == 0) ? os.lockedColor : os.indentBackgroundColor);
		if (num == 0)
		{
		}
		destinationRectangle.Height = 22;
		y += 30;
		Vector2 vector = TextItem.doMeasuredLabel(new Vector2(x, y), LocaleTerms.Loc("Login "), Color.White);
		switch (num)
		{
		case 0:
			x += (int)vector.X;
			TextItem.doLabel(new Vector2(x, y), LocaleTerms.Loc("Failed"), os.brightLockedColor);
			x -= (int)vector.X;
			lockLoginDisplayCache = true;
			break;
		default:
			x += (int)vector.X;
			TextItem.doLabel(new Vector2(x, y), LocaleTerms.Loc("Successful"), os.brightUnlockedColor);
			x -= (int)vector.X;
			lockLoginDisplayCache = true;
			break;
		case -1:
			break;
		}
		y += 60;
		if (num2 == 0)
		{
			destinationRectangle.Y = y;
			spriteBatch.Draw(Utils.white, destinationRectangle, os.subtleTextColor);
		}
		int num3 = 100;
		string text4 = LocaleTerms.Loc("username :");
		string text5 = LocaleTerms.Loc("password :");
		num3 = (int)Math.Max(Math.Max(GuiData.smallfont.MeasureString(text4).X + 4f, GuiData.smallfont.MeasureString(text5).X + 4f), num3);
		spriteBatch.DrawString(GuiData.smallfont, text4, new Vector2(x, y), Color.White);
		x += num3;
		spriteBatch.DrawString(GuiData.smallfont, text, new Vector2(x, y), Color.White);
		x -= num3;
		y += 30;
		if (num2 == 1)
		{
			destinationRectangle.Y = y;
			spriteBatch.Draw(Utils.white, destinationRectangle, os.subtleTextColor);
		}
		spriteBatch.DrawString(GuiData.smallfont, text5, new Vector2(x, y), Color.White);
		x += num3;
		spriteBatch.DrawString(GuiData.smallfont, text2, new Vector2(x, y), Color.White);
		y += 30;
		x -= num3;
		if (num != -1)
		{
			int num4 = Math.Min(bounds.Width / 3, 180);
			if (Button.doButton(12345, x, y, (num > 0) ? 140 : num4, 30, (num > 0) ? LocaleTerms.Loc("Complete") : LocaleTerms.Loc("Back"), os.indentBackgroundColor))
			{
				command = "connect";
			}
			if (num <= 0 && Button.doButton(123456, x + num4 + 5, y, num4, 30, LocaleTerms.Loc("Retry"), os.indentBackgroundColor))
			{
				lockLoginDisplayCache = false;
				loginDetailsCache = null;
				os.runCommand("login");
			}
			return;
		}
		y += 65;
		int num5 = x;
		int num6 = y;
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		for (int i = 0; i < computer.users.Count; i++)
		{
			if (computer.users[i].known && Daemon.validUser(computer.users[i].type))
			{
				num5 = x + 320;
				if (Button.doButton(123457 + i, x, y, 300, 25, "Login - User: " + computer.users[i].name + " Pass: " + computer.users[i].pass, os.darkBackgroundColor))
				{
					forceLogin(computer.users[i].name, computer.users[i].pass);
				}
				y += 27;
			}
		}
		if (Button.doButton(2111844, num5, num6, 100, 25, LocaleTerms.Loc("Cancel"), os.lockedColor))
		{
			forceLogin("", "");
			command = "connect";
		}
	}

	public void forceLogin(string username, string pass)
	{
		string prompt = os.terminal.prompt;
		os.terminal.currentLine = username;
		os.terminal.executeLine();
		while (os.terminal.prompt.Equals(prompt))
		{
		}
		os.terminal.currentLine = pass;
		os.terminal.executeLine();
	}

	public Texture2D GetComputerImage(Computer comp)
	{
		if (comp.icon == null || !compAltIcons.ContainsKey(comp.icon))
		{
			return (comp.securityLevel >= computers.Count) ? defaultComputer : computers[comp.securityLevel];
		}
		return compAltIcons[comp.icon];
	}

	private void doConnectHeader()
	{
		Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
		x += 20;
		y += 5;
		spriteBatch.Draw(GetComputerImage(computer), new Vector2(x, y), Color.White);
		spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("Connected to") + " ", new Vector2(x + 160, y), Color.White);
		y += 40;
		string text2 = ((os.connectedComp == null) ? os.thisComputer.name : os.connectedComp.name);
		TextItem.doFontLabel(new Vector2(x + 160, y), text2, GuiData.font, Color.White, (float)bounds.Width - 190f, 60f);
		y += 33;
		text2 = ((os.connectedComp == null) ? os.thisComputer.ip : os.connectedComp.ip);
		float num = (LocaleActivator.ActiveLocaleIsCJK() ? 4f : 0f);
		spriteBatch.DrawString(GuiData.smallfont, "@  " + text2, new Vector2(x + 160, (float)y + num), Color.White);
		y += 60;
		if (os.hasConnectionPermission(admin: true))
		{
			y -= 20;
			Rectangle empty = Rectangle.Empty;
			empty.X = bounds.X + 1;
			empty.Y = y;
			empty.Width = bounds.Width - 2;
			empty.Height = 20;
			spriteBatch.Draw(Utils.white, empty, os.highlightColor);
			string text = LocaleTerms.Loc("You are the Administrator of this System");
			Vector2 vector = GuiData.UISmallfont.MeasureString(text);
			Vector2 pos = default(Vector2);
			ref Vector2 reference = ref pos;
			reference = new Vector2((float)(empty.X + empty.Width / 2) - vector.X / 2f, empty.Y);
			if (LocaleActivator.ActiveLocaleIsCJK())
			{
				pos.Y -= 2f;
			}
			OS oS = os;
			oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
			{
				spriteBatch.DrawString(GuiData.UISmallfont, text, pos, Color.Black);
			});
			if (bounds.Height > 500)
			{
				y += 40;
			}
			else
			{
				y += 12;
			}
		}
		if (computer.portsNeededForCrack > 100)
		{
			y += 10;
			Rectangle rectangle = new Rectangle(bounds.X + 2, y, bounds.Width - 4, 20);
			string text3 = "INVIOLABILITY DETECTED";
			Vector2 vector2 = GuiData.titlefont.MeasureString(text3);
			float num2 = vector2.X / vector2.Y;
			int num3 = 10;
			int num4 = (int)((float)rectangle.Height * num2);
			vector2.Y /= vector2.Y / 20f;
			int num5 = (int)((float)(rectangle.Width - num4) / 2f);
			Rectangle destinationRectangle = new Rectangle(rectangle.X, rectangle.Y, num5 - num3, rectangle.Height);
			Rectangle dest = new Rectangle(destinationRectangle.X + destinationRectangle.Width + num3, destinationRectangle.Y, num4, rectangle.Height);
			destinationRectangle.Y += 4;
			destinationRectangle.Height -= 8;
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Red);
			FlickeringTextEffect.DrawLinedFlickeringText(dest, text3, 3f, 0.1f, GuiData.titlefont, os, Utils.AddativeWhite);
			destinationRectangle.X = rectangle.X + rectangle.Width - (destinationRectangle.Width + num3) + num3;
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Red);
			y += 40;
		}
		else if (os.hasConnectionPermission(admin: true))
		{
			y += 40;
		}
	}

	private void doConnectDisplay()
	{
		doConnectHeader();
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		for (int i = 0; i < computer.daemons.Count; i++)
		{
			if (computer.daemons[i] is CustomConnectDisplayOverride)
			{
				Rectangle rectangle = new Rectangle(bounds.X + 1, y, bounds.Width - 2, bounds.Height - (y - bounds.Y) - 1);
				computer.daemons[i].draw(rectangle, spriteBatch);
				return;
			}
		}
		int num = computer.daemons.Count + 6;
		int num2 = 40;
		int num3 = bounds.Height - (y - bounds.Y) - 20;
		num3 -= num * 5;
		if ((double)num3 / (double)num < (double)num2)
		{
			num2 = (int)((double)num3 / (double)num);
		}
		for (int i = 0; i < computer.daemons.Count; i++)
		{
			if (Button.doButton(29000 + i, x, y, 300, num2, computer.daemons[i].name, os.highlightColor))
			{
				command = computer.daemons[i].name;
				computer.daemons[i].navigatedTo();
			}
			y += num2 + 5;
		}
		if (Button.doButton(300000, x, y, 300, num2, LocaleTerms.Loc("Login"), os.hasConnectionPermission(admin: true) ? os.subtleTextColor : os.highlightColor))
		{
			os.runCommand("login");
			os.terminal.clearCurrentLine();
		}
		y += num2 + 5;
		if (Button.doButton(300002, x, y, 300, num2, LocaleTerms.Loc("Probe System"), os.highlightColor))
		{
			os.runCommand("probe");
		}
		y += num2 + 5;
		if (Button.doButton(300003, x, y, 300, num2, LocaleTerms.Loc("View Filesystem"), os.hasConnectionPermission(admin: false) ? os.highlightColor : os.subtleTextColor))
		{
			os.runCommand("ls");
		}
		y += num2 + 5;
		if (Button.doButton(300006, x, y, 300, num2, LocaleTerms.Loc("View Logs"), os.hasConnectionPermission(admin: false) ? os.highlightColor : os.subtleTextColor))
		{
			os.runCommand("cd log");
		}
		y += num2 + 5;
		if (Button.doButton(300009, x, y, 300, num2, LocaleTerms.Loc("Scan Network"), os.hasConnectionPermission(admin: true) ? os.highlightColor : os.subtleTextColor))
		{
			os.runCommand("scan");
		}
		y = bounds.Y + bounds.Height - 30;
		if (Button.doButton(300012, x, y, 300, 20, LocaleTerms.Loc("Disconnect"), os.lockedColor))
		{
			os.runCommand("dc");
		}
	}

	private void doDisconnectDisplay()
	{
		tmpRect.X = bounds.X + 2;
		tmpRect.Width = bounds.Width - 4;
		tmpRect.Y = bounds.Y + bounds.Height / 6 * 2;
		tmpRect.Height = bounds.Height / 3;
		spriteBatch.Draw(Utils.white, tmpRect, os.indentBackgroundColor);
		Vector2 vector = GuiData.font.MeasureString(LocaleTerms.Loc("Disconnected"));
		Vector2 position = new Vector2((float)(tmpRect.X + bounds.Width / 2) - vector.X / 2f, bounds.Y + bounds.Height / 2 - 10);
		spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("Disconnected"), position, os.subtleTextColor);
	}

	private void doDisconnectForcedDisplay()
	{
		tmpRect.X = bounds.X + 2;
		tmpRect.Width = bounds.Width - 4;
		tmpRect.Y = bounds.Y + bounds.Height / 6 * 2;
		tmpRect.Height = bounds.Height / 3;
		Rectangle rect = tmpRect;
		double num = Math.Abs(Math.Sin(os.timer));
		int num2 = (int)(num * 40.0);
		rect = Utils.InsetRectangle(rect, -1 * num2);
		rect.X = tmpRect.X;
		rect.Width = tmpRect.Width;
		spriteBatch.Draw(Utils.white, rect, Utils.AddativeRed * (float)(1.0 - num));
		spriteBatch.Draw(Utils.white, tmpRect, os.indentBackgroundColor);
		Vector2 vector = GuiData.font.MeasureString(LocaleTerms.Loc("Connection Denied by Remote Server"));
		Vector2 position = new Vector2((float)(tmpRect.X + bounds.Width / 2) - vector.X / 2f, bounds.Y + bounds.Height / 2 - 10);
		spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("Connection Denied by Remote Server"), position, os.brightLockedColor);
	}

	private void doCatDisplay()
	{
		if (os.hasConnectionPermission(admin: false))
		{
			if (Button.doButton(299999, bounds.X + (bounds.Width - 41), bounds.Y + 12, 27, 29, "<-", null))
			{
				os.runCommand("ls");
			}
			Rectangle rectangle = GuiData.tmpRect;
			rectangle.Width = bounds.Width;
			rectangle.X = bounds.X;
			rectangle.Y = bounds.Y + 1;
			rectangle.Height = bounds.Height - 2;
			if (os.connectedComp != null && os.connectedComp.ip != LastDisplayedFileSourceIP && LastDisplayedFileSourceIP != os.thisComputer.ip)
			{
				command = "dc";
				return;
			}
			string text = "";
			for (int i = 1; i < commandArgs.Length; i++)
			{
				text = text + commandArgs[i] + " ";
			}
			text = LocalizedFileLoader.SafeFilterString(text);
			if (LastDisplayedFileFolder.searchForFile(text.Trim()) == null)
			{
				OS oS = os;
				oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
				{
					Rectangle rectangle2 = new Rectangle(bounds.X + 1, bounds.Y + bounds.Height / 2 - 70, bounds.Width - 2, 140);
					spriteBatch.Draw(Utils.white, rectangle2, os.lockedColor);
					TextItem.doCenteredFontLabel(rectangle2, "File Not Found", GuiData.font, Color.White);
				});
				catScroll = Vector2.Zero;
			}
			else
			{
				TextItem.doFontLabel(new Vector2(x, y + 3), text, GuiData.font, Color.White, bounds.Width - 70);
				int num = 55;
				Rectangle dest = new Rectangle(rectangle.X + 4, rectangle.Y + num, rectangle.Width - 6, rectangle.Height - num - 2);
				string displayCache = os.displayCache;
				y += 70;
				displayCache = LocalizedFileLoader.SafeFilterString(displayCache);
				string text2 = Utils.SuperSmartTwimForWidth(displayCache, bounds.Width - 40, GuiData.tinyfont);
				catTextRegion.Draw(dest, text2, spriteBatch);
			}
		}
		else
		{
			command = "connect";
		}
	}

	private void doProbeDisplay()
	{
		Rectangle empty = Rectangle.Empty;
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		if (computer.proxyActive)
		{
			empty = bounds;
			empty.X++;
			empty.Y++;
			empty.Width -= 2;
			empty.Height -= 2;
			PatternDrawer.draw(empty, 0.8f, Color.Transparent, os.superLightWhite, os.ScreenManager.SpriteBatch);
		}
		if (Button.doButton(299999, bounds.X + (bounds.Width - 50), bounds.Y + y, 27, 27, "<-", null))
		{
			command = "connect";
		}
		spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("Open Ports"), new Vector2(x, y), Color.White);
		y += 40;
		spriteBatch.DrawString(GuiData.smallfont, computer.name + " @" + computer.ip, new Vector2(x, y), Color.White);
		y += 30;
		int num = Math.Max(computer.portsNeededForCrack + 1, 0);
		string text = string.Concat(num);
		bool flag = num > 100;
		if (flag)
		{
			if (invioableSecurityCacheString == null)
			{
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < text.Length; i++)
				{
					stringBuilder.Append(Utils.getRandomChar());
				}
				invioableSecurityCacheString = stringBuilder.ToString();
			}
			else
			{
				invioabilityCharChangeTimer -= (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
				if (invioabilityCharChangeTimer <= 0f)
				{
					StringBuilder stringBuilder2 = new StringBuilder(invioableSecurityCacheString);
					stringBuilder2[Utils.random.Next(stringBuilder2.Length)] = ((Utils.random.NextDouble() > 0.3) ? Utils.getRandomNumberChar() : Utils.getRandomChar());
					invioableSecurityCacheString = stringBuilder2.ToString();
					invioabilityCharChangeTimer = 0.025f;
				}
			}
			text = invioableSecurityCacheString;
		}
		spriteBatch.DrawString(GuiData.smallfont, LocalizedFileLoader.SafeFilterString(LocaleTerms.Loc("Open Ports Required for Crack:")) + " " + text, new Vector2(x, y), flag ? Color.Lerp(Color.Red, os.brightLockedColor, Utils.randm(0.5f) + 0.5f) : os.highlightColor);
		y += 40;
		if (flag)
		{
			empty.X = bounds.X + 2;
			empty.Y = y;
			empty.Width = bounds.Width - 4;
			empty.Height = 110;
			DrawInvioabilityEffect(empty);
			y += empty.Height + 10;
		}
		if (computer.hasProxy)
		{
			empty.X = x;
			empty.Y = y;
			empty.Width = bounds.Width - 10;
			empty.Height = 40;
			PatternDrawer.draw(empty, 1f, computer.proxyActive ? os.topBarColor : Color.Lerp(os.unlockedColor, Color.Black, 0.2f), computer.proxyActive ? (os.shellColor * 0.3f) : os.unlockedColor, os.ScreenManager.SpriteBatch);
			if (computer.proxyActive)
			{
				empty.Width = (int)((float)empty.Width * (1f - computer.proxyOverloadTicks / computer.startingOverloadTicks));
				spriteBatch.Draw(Utils.white, empty, Color.Black * 0.5f);
			}
			spriteBatch.DrawString(GuiData.smallfont, computer.proxyActive ? LocaleTerms.Loc("Proxy Detected") : LocaleTerms.Loc("Proxy Bypassed"), new Vector2(x + 4, y + 2), Color.Black);
			spriteBatch.DrawString(GuiData.smallfont, computer.proxyActive ? LocaleTerms.Loc("Proxy Detected") : LocaleTerms.Loc("Proxy Bypassed"), new Vector2(x + 3, y + 1), computer.proxyActive ? Color.White : os.highlightColor);
			y += 60;
		}
		if (computer.firewall != null)
		{
			empty.X = x;
			empty.Y = y;
			empty.Width = bounds.Width - 10;
			empty.Height = 40;
			bool flag2 = !computer.firewall.solved;
			PatternDrawer.draw(empty, 1f, flag2 ? os.topBarColor : Color.Lerp(os.unlockedColor, Color.Black, 0.2f), flag2 ? (os.shellColor * 0.3f) : os.unlockedColor, os.ScreenManager.SpriteBatch);
			spriteBatch.DrawString(GuiData.smallfont, flag2 ? LocaleTerms.Loc("Firewall Detected") : LocaleTerms.Loc("Firewall Solved"), new Vector2(x + 4, y + 2), Color.Black);
			spriteBatch.DrawString(GuiData.smallfont, flag2 ? LocaleTerms.Loc("Firewall Detected") : LocaleTerms.Loc("Firewall Solved"), new Vector2(x + 3, y + 1), flag2 ? Color.White : os.highlightColor);
			y += 60;
		}
		Vector2 zero = Vector2.Zero;
		empty.X = x + 1;
		empty.Width = 420;
		empty.Height = 41;
		Vector2 position = new Vector2(empty.X + empty.Width - 36, empty.Y + 7);
		x += 10;
		for (int i = 0; i < computer.ports.Count; i++)
		{
			empty.Y = y + 4;
			position.Y = empty.Y + 4;
			spriteBatch.Draw(Utils.white, empty, (computer.portsOpen[i] > 0) ? os.unlockedColor : os.lockedColor);
			spriteBatch.Draw((computer.portsOpen[i] > 0) ? openLockSprite : lockSprite, position, Color.White);
			string text2 = "Port#: " + computer.GetDisplayPortNumberFromCodePort(computer.ports[i]);
			zero = GuiData.font.MeasureString(text2);
			spriteBatch.DrawString(GuiData.font, text2, new Vector2(x, y + 3), Color.White);
			string text3 = " - " + PortExploits.services[computer.ports[i]];
			Vector2 vector = GuiData.smallfont.MeasureString(text3);
			float num2 = (float)empty.Width - zero.X - 50f;
			float scale = Math.Min(1f, num2 / vector.X);
			spriteBatch.DrawString(GuiData.smallfont, text3, new Vector2((float)x + zero.X, y + 4), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.8f);
			y += 45;
		}
	}

	private void DrawInvioabilityEffect(Rectangle dest)
	{
		Color color = Color.Lerp(os.lockedColor, os.brightLockedColor, Utils.randm(0.5f) + 0.5f);
		color.A = 0;
		Rectangle rectangle = new Rectangle(dest.X + fancyCornerSprite.Width, dest.Y, dest.Width - fancyCornerSprite.Width * 2, fancyPanelSprite.Height);
		spriteBatch.Draw(fancyPanelSprite, rectangle, color);
		spriteBatch.Draw(fancyCornerSprite, new Vector2(dest.X, dest.Y), null, color, 0f, Vector2.Zero, 1f, SpriteEffects.FlipHorizontally, 0.4f);
		spriteBatch.Draw(fancyCornerSprite, new Vector2(rectangle.X + rectangle.Width, dest.Y), color);
		int num = fancyCornerSprite.Width - 38;
		Rectangle dest2 = new Rectangle(dest.X + num, dest.Y + fancyCornerSprite.Height / 2, dest.Width - num * 2, dest.Height - fancyCornerSprite.Height);
		Color color2 = Color.Lerp(Utils.AddativeWhite, os.brightLockedColor, Utils.randm(0.5f) + 0.5f);
		FlickeringTextEffect.DrawLinedFlickeringText(dest2, "INVIOLABILITY ERROR", 4f, 0.26f, GuiData.titlefont, os, Utils.AddativeWhite);
		Rectangle destinationRectangle = rectangle;
		destinationRectangle.Y = dest.Y + dest.Height - fancyPanelSprite.Height;
		spriteBatch.Draw(fancyPanelSprite, destinationRectangle, null, color, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0.5f);
		float num2 = fancyCornerSprite.Height - fancyPanelSprite.Height;
		spriteBatch.Draw(fancyCornerSprite, new Vector2(dest.X, (float)destinationRectangle.Y - num2), null, color, 0f, Vector2.Zero, 1f, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0.4f);
		spriteBatch.Draw(fancyCornerSprite, new Vector2(rectangle.X + rectangle.Width, (float)destinationRectangle.Y - num2), null, color, 0f, Vector2.Zero, 1f, SpriteEffects.FlipVertically, 0.5f);
	}

	private void doLsDisplay()
	{
		if (os.hasConnectionPermission(admin: false))
		{
			x = 5;
			y = 5;
			int num = bounds.Width - 25;
			int num2 = 20;
			string text = ((os.connectedComp == null) ? os.thisComputer.name : (os.connectedComp.name + " " + LocaleTerms.Loc("File System")));
			TextItem.doFontLabel(new Vector2(bounds.X + x, bounds.Y + y), text, GuiData.font, Color.White, (float)bounds.Width - 46f, 60f);
			if (Button.doButton(299999, bounds.X + (bounds.Width - 41), bounds.Y + 12, 27, 29, "<-", null))
			{
				if (os.navigationPath.Count > 0)
				{
					os.runCommand("cd ..");
				}
				else
				{
					os.display.command = "connect";
				}
			}
			y += 50;
			Rectangle dest = GuiData.tmpRect;
			dest.Width = bounds.Width;
			dest.X = bounds.X;
			dest.Y = bounds.Y + 55;
			dest.Height = bounds.Height - 57;
			lsModuleHelper.DrawUI(dest, os);
		}
		else
		{
			command = "connect";
		}
	}

	private void doFolderGui(int width, int height, int indexOffset, Folder f, int recItteration)
	{
		int i;
		for (i = 0; i < f.folders.Count; i++)
		{
			if (Button.doButton(300000 + i + indexOffset, x, y, width, height, "/" + f.folders[i].name, null))
			{
				int num = 0;
				for (int j = 0; j < os.navigationPath.Count - recItteration; j++)
				{
					Action action = delegate
					{
						os.runCommand("cd ..");
					};
					if (num > 0)
					{
						os.delayer.Post(ActionDelayer.Wait((double)num * 1.0), action);
					}
					else
					{
						action();
					}
					num++;
				}
				Action action2 = delegate
				{
					os.runCommand("cd " + f.folders[i].name);
				};
				if (num > 0)
				{
					os.delayer.Post(ActionDelayer.Wait((double)num * 1.0), action2);
				}
				else
				{
					action2();
				}
			}
			y += height + 2;
			x += 30;
			if (os.navigationPath.Count - 1 >= recItteration && os.navigationPath[recItteration] == i)
			{
				doFolderGui(width - 30, height, indexOffset + 10000 * (i + 1), f.folders[i], recItteration + 1);
			}
			x -= 30;
		}
		for (int k = 0; k < f.files.Count; k++)
		{
			if (Button.doButton(400000 + k + indexOffset / 2 + (k + 1) * indexOffset, x, y, width, height, f.files[k].name, null))
			{
				for (int j = 0; j < os.navigationPath.Count - recItteration; j++)
				{
					os.runCommand("cd ..");
				}
				os.runCommand("cat " + f.files[k].name);
			}
			y += height + 2;
		}
		if (f.folders.Count == 0 && f.files.Count == 0)
		{
			TextItem.doFontLabel(new Vector2(x, y), "-" + LocaleTerms.Loc("Empty") + "-", GuiData.tinyfont, null, width, height);
			y += height + 2;
		}
	}

	public static string splitForWidth(string s, int width)
	{
		string text = "";
		int num = 0;
		foreach (char c in s)
		{
			text += c;
			num = ((c != '\n') ? (num + 1) : 0);
			if (num > width / 6 && (c == ' ' || (float)num > (float)width / 5.2f))
			{
				text += '\n';
				num = 0;
			}
		}
		return text;
	}

	public static string splitForWidth(string s, int width, bool correct)
	{
		string text = "";
		int num = 0;
		width /= 8;
		foreach (char c in s)
		{
			text += c;
			num = ((c != '\n') ? (num + 1) : 0);
			if (num >= width && (c == ' ' || (float)num > (float)width * 0.9f))
			{
				text += '\n';
				num = 0;
			}
		}
		return text;
	}

	public static string cleanSplitForWidth(string s, int width)
	{
		int num = 10;
		width /= num;
		string text = "";
		char[] separator = new char[1] { ' ' };
		string[] array = s.Split(separator);
		int num2 = 0;
		int num3 = 0;
		if (array.Length == 1)
		{
			return splitForWidth(array[0], width * 8, correct: true);
		}
		while (num2 < array.Length)
		{
			num3 = 0;
			while (num3 < width && num2 < array.Length)
			{
				text = text + array[num2] + " ";
				num3 += array[num2].Length;
				int num4 = array[num2].IndexOf('\n');
				if (num4 >= 0)
				{
					num3 = array[num2].Length - (num4 + 1);
				}
				num2++;
			}
			text += '\n';
		}
		return text;
	}
}
