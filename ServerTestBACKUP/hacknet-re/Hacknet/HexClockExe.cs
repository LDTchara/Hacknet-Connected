using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class HexClockExe : ExeModule
{
	private OSTheme theme;

	private bool stopUIChange = false;

	public HexClockExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "HexClock";
		ramCost = 55;
		IdentifierName = "HexClock";
		targetIP = os.thisComputer.ip;
		if (p.Length > 1 && (p[1].ToLower().StartsWith("-s") || p[1].ToLower().StartsWith("-n")))
		{
			stopUIChange = true;
		}
		else
		{
			os.write("HexClock Running. Use -s or -n for restricted mode\n ");
		}
		theme = ThemeManager.currentTheme;
	}

	public override void Killed()
	{
		base.Killed();
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		DateTime now = DateTime.Now;
		string text = "#" + now.Hour.ToString("00") + now.Minute.ToString("00") + now.Second.ToString("00");
		Color color = Utils.ColorFromHexString(text);
		if (!stopUIChange)
		{
			AutoUpdateTheme(color);
		}
		Rectangle contentAreaDest = GetContentAreaDest();
		spriteBatch.Draw(Utils.white, contentAreaDest, color);
		int num = (int)((float)bounds.Width * 0.8f);
		int num2 = 30;
		Rectangle dest = new Rectangle(contentAreaDest.X + (contentAreaDest.Width / 2 - num / 2), contentAreaDest.Y + (contentAreaDest.Height / 2 - num2 / 2), num, num2);
		TextItem.doFontLabelToSize(dest, text, GuiData.font, Utils.AddativeWhite);
	}

	private void AutoUpdateTheme(Color c)
	{
		Color c2 = c;
		Utils.RGB2HSL(c, out var h, out var s, out var l);
		s = Math.Min(0.7, s);
		l = Math.Max(0.2, l);
		c = Utils.HSL2RGB(h, s, l);
		l = Math.Max(0.35, l);
		double num = h - 0.5;
		if (num < 0.0)
		{
			num += 1.0;
		}
		Color color = Utils.HSL2RGB(num, s, l);
		color = Utils.GetComplimentaryColor(c2);
		os.defaultHighlightColor = color;
		os.defaultTopBarColor = new Color(0, 0, 0, 60);
		os.highlightColor = os.defaultHighlightColor;
		os.highlightColor = os.defaultHighlightColor;
		os.moduleColorSolid = Color.Lerp(c, Utils.AddativeWhite, 0.5f);
		os.moduleColorSolidDefault = os.moduleColorSolid;
		os.moduleColorStrong = c;
		os.moduleColorStrong.A = 80;
		os.topBarColor = os.defaultTopBarColor;
		os.exeModuleTopBar = new Color(32, 22, 40, 80);
		os.exeModuleTitleText = new Color(91, 132, 207, 0);
		os.netmapToolTipColor = new Color(213, 245, 255, 0);
		os.netmapToolTipBackground = new Color(0, 0, 0, 70);
		os.displayModuleExtraLayerBackingColor = new Color(0, 0, 0, 60);
		os.thisComputerNode = new Color(95, 220, 83);
		os.scanlinesColor = new Color(255, 255, 255, 15);
	}
}
