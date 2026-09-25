using System.Linq;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class NetmapOrganizerExe : ExeModule, MainDisplayOverrideEXE
{
	private bool AllowChaos = false;

	public bool DisplayOverrideIsActive { get; set; }

	public NetmapOrganizerExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		needsProxyAccess = false;
		name = "NetmapOrganizer";
		ramCost = 300;
		IdentifierName = "NetmapOrganizer";
		DisplayOverrideIsActive = false;
		for (int i = 1; i < p.Length; i++)
		{
			if (p[i].ToLower().StartsWith("-c"))
			{
				AllowChaos = true;
			}
		}
	}

	public override void Update(float t)
	{
		base.Update(t);
	}

	public override void Completed()
	{
		base.Completed();
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle contentAreaDest = GetContentAreaDest();
		ZoomingDotGridEffect.Render(contentAreaDest, spriteBatch, os.timer, os.highlightColor * 0.4f);
		int x = contentAreaDest.X + 10;
		int width = contentAreaDest.Width - 20;
		int num = contentAreaDest.Y + 50;
		if (!isExiting)
		{
			if (Button.doButton(10777001 + PID, x, num, width, 20, LocaleTerms.Loc("Scatter"), (os.netMap.SortingAlgorithm == NetmapSortingAlgorithm.Scatter) ? Color.White : os.highlightColor))
			{
				os.netMap.SortingAlgorithm = NetmapSortingAlgorithm.Scatter;
			}
			num += 25;
			if (Button.doButton(10777003 + PID, x, num, width, 20, LocaleTerms.Loc("Grid"), (os.netMap.SortingAlgorithm == NetmapSortingAlgorithm.Grid) ? Color.White : os.highlightColor))
			{
				os.netMap.SortingAlgorithm = NetmapSortingAlgorithm.Grid;
			}
			num += 25;
			if (Button.doButton(10777005 + PID, x, num, width, 20, LocaleTerms.Loc("Scan Sequence Grid"), (os.netMap.SortingAlgorithm == NetmapSortingAlgorithm.LockGrid) ? Color.White : os.highlightColor))
			{
				os.netMap.SortingAlgorithm = NetmapSortingAlgorithm.LockGrid;
				os.netMap.visibleNodes = os.netMap.visibleNodes.Distinct().ToList();
			}
			num += 25;
			if (AllowChaos && Button.doButton(10777019 + PID, x, num, width, 20, LocaleTerms.Loc("CHAOS"), (os.netMap.SortingAlgorithm == NetmapSortingAlgorithm.Chaos) ? Color.White : os.highlightColor))
			{
				os.netMap.SortingAlgorithm = NetmapSortingAlgorithm.Chaos;
			}
			num += 25;
			if (Button.doButton(10777088 + PID, x, contentAreaDest.Y + contentAreaDest.Height - 24, width, 20, LocaleTerms.Loc("Exit"), os.lockedColor))
			{
				isExiting = true;
			}
		}
	}

	public void RenderMainDisplay(Rectangle dest, SpriteBatch sb)
	{
	}
}
