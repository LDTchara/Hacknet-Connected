using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class ISPDaemon : Daemon
{
	private struct ExpandingRectangleData
	{
		public float scaleIn;

		public float rate;

		public float thickness;

		public float blackLerp;
	}

	private enum ISPDaemonState
	{
		Welcome,
		About,
		Loading,
		IPEntry,
		EnterIP,
		AdminOnlyError,
		NotFoundError
	}

	private const float MAX_WIDTH = 7f;

	private const float MAX_RATE = 16f;

	private const float EFFECT_TIMER = 30f;

	private const float SEARCH_TIME = 2f;

	private const string ABOUT_MESSAGE_FILE = "ISP_About_Message.txt";

	private List<ExpandingRectangleData> outlineEffectEntries = new List<ExpandingRectangleData>();

	private float lastTimer = 0f;

	private float timeEnteredLoadingScreen = 0f;

	private ISPDaemonState state = ISPDaemonState.Welcome;

	private string ipSearch = null;

	private Computer scannedComputer = null;

	private bool inspectionFlagged = false;

	public ISPDaemon(Computer c, OS os)
		: base(c, LocaleTerms.Loc("ISP Management System"), os)
	{
	}

	public override void initFiles()
	{
		base.initFiles();
		Folder folder = comp.files.root.searchForFolder("home");
		if (folder != null)
		{
			string dataEntry = Utils.readEntireFile("Content/LocPost/ISPAbout.txt");
			FileEntry item = new FileEntry(dataEntry, "ISP_About_Message.txt");
			folder.files.Add(item);
		}
	}

	public override void navigatedTo()
	{
		state = ISPDaemonState.Welcome;
		base.navigatedTo();
	}

	public override string getSaveString()
	{
		return "<ispSystem />";
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		if (state == ISPDaemonState.AdminOnlyError || state == ISPDaemonState.NotFoundError)
		{
			PatternDrawer.draw(bounds, 0.1f, Color.Transparent, os.lockedColor, sb, PatternDrawer.errorTile);
		}
		Rectangle bounds2 = new Rectangle(bounds.X + 40, bounds.Y + 40, bounds.Width - 80, bounds.Height - 80);
		switch (state)
		{
		case ISPDaemonState.Welcome:
			DrawWelcomeScreen(bounds2, sb, bounds);
			break;
		case ISPDaemonState.About:
			DrawAboutScreen(bounds2, sb);
			break;
		case ISPDaemonState.Loading:
			DrawLoadingScreen(bounds2, sb);
			break;
		case ISPDaemonState.IPEntry:
			DrawIPEntryScreen(bounds2, sb);
			break;
		case ISPDaemonState.AdminOnlyError:
			DrawAdminOnlyError(bounds2, sb);
			break;
		case ISPDaemonState.EnterIP:
			DrawEnterIPScreen(bounds2, sb);
			break;
		case ISPDaemonState.NotFoundError:
			DrawNotFoundError(bounds2, sb);
			break;
		}
		drawOutlineEffect(bounds, sb);
	}

	private void DrawIPEntryScreen(Rectangle bounds, SpriteBatch sb)
	{
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 20), LocaleTerms.Loc("IP Entry") + " :: " + scannedComputer.ip, GuiData.font, Color.White, bounds.Width);
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 70), LocaleTerms.Loc("Identified as") + " :\"" + scannedComputer.name + "\"", GuiData.smallfont, Color.White, bounds.Width);
		sb.Draw(os.display.GetComputerImage(scannedComputer), new Vector2(bounds.X + 30, bounds.Y + 100), Color.White);
		int x = bounds.X + 30 + 130;
		int num = bounds.Y + 100;
		if (Button.doButton(3388301, x, num, bounds.Width - 180, 30, LocaleTerms.Loc("Assign New IP"), os.brightUnlockedColor))
		{
			bool flag = false;
			do
			{
				scannedComputer.ip = NetworkMap.generateRandomIP();
				flag = false;
				for (int i = 0; i < os.netMap.nodes.Count; i++)
				{
					if (os.netMap.nodes[i].ip == scannedComputer.ip && os.netMap.nodes[i].idName != scannedComputer.idName)
					{
						flag = true;
					}
				}
			}
			while (flag);
			if (os.thisComputer.idName == scannedComputer.idName)
			{
				os.thisComputerIPReset();
			}
		}
		num += 34;
		if (Button.doButton(3388304, x, num, bounds.Width - 180, 30, LocaleTerms.Loc("Flag for Inspection") + (inspectionFlagged ? (" : " + LocaleTerms.Loc("ACTIVE")) : ""), inspectionFlagged ? Color.Gray : os.highlightColor))
		{
			inspectionFlagged = true;
		}
		num += 34;
		if (Button.doButton(3388308, x, num, bounds.Width - 180, 30, LocaleTerms.Loc("Prioritize Routing"), os.highlightColor))
		{
		}
		drawBackButton(bounds);
	}

	private void DrawLoadingScreen(Rectangle bounds, SpriteBatch sb)
	{
		float num = os.timer - timeEnteredLoadingScreen;
		if (num >= 2f)
		{
			scannedComputer = null;
			for (int i = 0; i < os.netMap.nodes.Count; i++)
			{
				if (ipSearch == os.netMap.nodes[i].ip)
				{
					scannedComputer = os.netMap.nodes[i];
					break;
				}
			}
			if (scannedComputer != null)
			{
				state = ISPDaemonState.IPEntry;
				inspectionFlagged = false;
			}
			else
			{
				state = ISPDaemonState.NotFoundError;
			}
		}
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + bounds.Height / 2 - 35), LocaleTerms.Loc("Scanning for") + " " + ipSearch + " ...", GuiData.font, Color.White, bounds.Width);
		Rectangle destinationRectangle = new Rectangle(bounds.X + 10, bounds.Y + bounds.Height / 2, bounds.Width - 20, 16);
		sb.Draw(Utils.white, destinationRectangle, Color.Gray);
		destinationRectangle.X++;
		destinationRectangle.Y++;
		destinationRectangle.Width -= 2;
		destinationRectangle.Height -= 2;
		sb.Draw(Utils.white, destinationRectangle, Color.Black);
		float num2 = Utils.QuadraticOutCurve(num / 2f);
		destinationRectangle.Width = (int)((float)destinationRectangle.Width * num2);
		sb.Draw(Utils.white, destinationRectangle, os.highlightColor);
	}

	private void DrawEnterIPScreen(Rectangle bounds, SpriteBatch sb)
	{
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		string[] array = os.getStringCache.Split(separator, StringSplitOptions.None);
		string text = null;
		int num = bounds.X + 10;
		int num2 = bounds.Y + 10;
		num2 += (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), LocaleTerms.Loc("Enter IP Address to Scan for") + " :", null).Y + 5;
		if (array.Length > 1)
		{
			text = array[1];
			if (text.Equals(""))
			{
				text = os.terminal.currentLine;
			}
		}
		Rectangle destinationRectangle = new Rectangle(num, num2, bounds.Width - 20, 200);
		sb.Draw(Utils.white, destinationRectangle, os.darkBackgroundColor);
		num2 += 80;
		destinationRectangle.X = num + (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), LocaleTerms.Loc("IP Address") + ": " + text, null).X + 2;
		destinationRectangle.Y = num2;
		destinationRectangle.Width = 7;
		destinationRectangle.Height = 20;
		if (os.timer % 1f < 0.3f)
		{
			sb.Draw(Utils.white, destinationRectangle, os.outlineColor);
		}
		num2 += 122;
		if (array.Length > 2 || Button.doButton(30, num, num2, 300, 22, LocaleTerms.Loc("Scan"), os.highlightColor))
		{
			if (array.Length <= 2)
			{
				os.terminal.executeLine();
			}
			ipSearch = text;
			state = ISPDaemonState.Loading;
			timeEnteredLoadingScreen = os.timer;
			os.getStringCache = "";
		}
		num2 += 26;
		if (Button.doButton(35, num, num2, 300, 22, LocaleTerms.Loc("Cancel"), os.lockedColor))
		{
			os.terminal.executeLine();
			state = ISPDaemonState.Welcome;
			os.getStringCache = "";
		}
	}

	private void drawBackButton(Rectangle bounds, ISPDaemonState stateTo = ISPDaemonState.Welcome)
	{
		if (Button.doButton(92271094, bounds.X + 30, bounds.Y + bounds.Height - 50, 200, 25, LocaleTerms.Loc("Back"), os.lockedColor))
		{
			state = stateTo;
		}
	}

	private void DrawAdminOnlyError(Rectangle bounds, SpriteBatch sb)
	{
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 20), LocaleTerms.Loc("Insufficient Permissions"), GuiData.font, Color.White, bounds.Width);
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 70), LocaleTerms.Loc("IP Search is limited to administrators") + "\n" + LocaleTerms.Loc("of this machine only") + ".\n" + LocaleTerms.Loc("If you require access, contact customer support") + ".", GuiData.smallfont, Color.White, bounds.Width);
		drawBackButton(bounds);
	}

	private void DrawNotFoundError(Rectangle bounds, SpriteBatch sb)
	{
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 20), LocaleTerms.Loc("IP Not Found"), GuiData.font, Color.White, bounds.Width);
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 70), LocaleTerms.Loc("IP Address is not registered with") + "\n" + LocaleTerms.Loc("our servers") + ".\n" + LocaleTerms.Loc("Check address and try again") + ".", GuiData.smallfont, Color.White, bounds.Width);
		drawBackButton(bounds);
	}

	private void DrawAboutScreen(Rectangle bounds, SpriteBatch sb)
	{
		TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 20), LocaleTerms.Loc("About this server"), GuiData.font, Color.White, bounds.Width);
		Folder folder = comp.files.root.searchForFolder("home");
		if (folder != null)
		{
			FileEntry fileEntry = folder.searchForFile("ISP_About_Message.txt");
			if (fileEntry != null)
			{
				TextItem.DrawShadow = false;
				TextItem.doFontLabel(new Vector2(bounds.X + 30, bounds.Y + 70), fileEntry.data, GuiData.smallfont, Color.White, bounds.Width);
			}
		}
		drawBackButton(bounds);
	}

	private void DrawWelcomeScreen(Rectangle bounds, SpriteBatch sb, Rectangle fullBounds)
	{
		TextItem.doFontLabel(new Vector2(bounds.X + 20, bounds.Y + 20), LocaleTerms.Loc("ISP Management System"), GuiData.font, Color.White, bounds.Width);
		int num = 30;
		int width = (int)((float)bounds.Width * 0.8f);
		int num2 = bounds.Y + 90;
		Rectangle destinationRectangle = fullBounds;
		destinationRectangle.Y = num2 - 20;
		destinationRectangle.Height = 22;
		bool flag = comp.adminIP == os.thisComputer.ip;
		sb.Draw(Utils.white, destinationRectangle, flag ? os.unlockedColor : os.lockedColor);
		string text = LocaleTerms.Loc("Valid Administrator Account Detected");
		if (!flag)
		{
			text = LocaleTerms.Loc("Non-Admin Account Active");
		}
		Vector2 vector = GuiData.smallfont.MeasureString(text);
		sb.DrawString(position: new Vector2((float)(destinationRectangle.X + destinationRectangle.Width / 2) - vector.X / 2f, destinationRectangle.Y), spriteFont: GuiData.smallfont, text: text, color: Color.White);
		num2 += 30;
		if (Button.doButton(95371001, bounds.X + 20, num2, width, num, LocaleTerms.Loc("About"), os.highlightColor))
		{
			state = ISPDaemonState.About;
		}
		num2 += num + 10;
		if (Button.doButton(95371004, bounds.X + 20, num2, width, num, LocaleTerms.Loc("Search for IP"), os.highlightColor))
		{
			if (comp.adminIP == os.thisComputer.ip)
			{
				state = ISPDaemonState.EnterIP;
				os.execute("getString IP_Address");
				ipSearch = null;
			}
			else
			{
				state = ISPDaemonState.AdminOnlyError;
			}
		}
		num2 += num + 10;
		if (Button.doButton(95371008, bounds.X + 20, num2, width, num, LocaleTerms.Loc("Exit"), os.highlightColor))
		{
			os.display.command = "connect";
		}
	}

	private void drawOutlineEffect(Rectangle bounds, SpriteBatch sb)
	{
		if (lastTimer == 0f)
		{
			lastTimer = os.timer;
		}
		if (os.timer % 0.5f > 0.1f && lastTimer % 0.5f < 0.1f)
		{
			addNewOutlineEffect();
		}
		for (int i = 0; i < outlineEffectEntries.Count; i++)
		{
			ExpandingRectangleData value = outlineEffectEntries[i];
			value.scaleIn += (os.timer - lastTimer) * value.rate * 1f;
			if (value.scaleIn > 30f)
			{
				outlineEffectEntries.RemoveAt(i);
				i--;
			}
			else
			{
				Rectangle rect = new Rectangle((int)((float)bounds.X + value.scaleIn), (int)((float)bounds.Y + value.scaleIn), (int)((float)bounds.Width - 2f * value.scaleIn), (int)((float)bounds.Height - 2f * value.scaleIn));
				drawRect(rect, sb, (int)value.thickness, 1f - value.scaleIn / 30f, value.blackLerp);
				outlineEffectEntries[i] = value;
			}
		}
		drawRect(bounds, sb, 4, 1f, 0f);
		lastTimer = os.timer;
	}

	private void drawRect(Rectangle rect, SpriteBatch sb, int thickness, float opacity, float blackLerp)
	{
		Rectangle destinationRectangle = rect;
		destinationRectangle.Width = thickness;
		Color color = Color.Lerp(os.highlightColor, Color.Black, blackLerp) * opacity;
		sb.Draw(Utils.white, destinationRectangle, color);
		destinationRectangle.X += rect.Width - thickness;
		sb.Draw(Utils.white, destinationRectangle, color);
		destinationRectangle.X = rect.X + thickness;
		destinationRectangle.Width = rect.Width - 2 * thickness;
		destinationRectangle.Height = thickness;
		sb.Draw(Utils.white, destinationRectangle, color);
		destinationRectangle.Y += rect.Height - thickness;
		sb.Draw(Utils.white, destinationRectangle, color);
	}

	private void addNewOutlineEffect()
	{
		ExpandingRectangleData expandingRectangleData = default(ExpandingRectangleData);
		expandingRectangleData.rate = Utils.randm(1f) * 15f + 1f;
		expandingRectangleData.scaleIn = 1f;
		expandingRectangleData.thickness = 1f + Utils.randm(1f) * Utils.randm(1f) * 6f;
		expandingRectangleData.blackLerp = Utils.randm(0.8f);
		ExpandingRectangleData item = expandingRectangleData;
		outlineEffectEntries.Add(item);
	}
}
