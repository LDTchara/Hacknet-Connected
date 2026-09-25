using Microsoft.Xna.Framework;

namespace Hacknet;

internal class SecurityTraceExe : ExeModule
{
	public SecurityTraceExe(Rectangle location, OS _os)
		: base(location, _os)
	{
		name = "SecurityTrace";
		IdentifierName = "Security Tracer";
		ramCost = 150;
		if (os.connectedComp == null)
		{
			os.connectedComp = os.thisComputer;
		}
		os.traceTracker.start(30f);
		os.warningFlash();
	}

	public override void Killed()
	{
		base.Killed();
		if (os.connectedComp == null || !(os.connectedComp.idName == "dGibson"))
		{
			os.traceTracker.stop();
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle contentAreaDest = GetContentAreaDest();
		contentAreaDest.X += 2;
		contentAreaDest.Width -= 4;
		contentAreaDest.Height -= Module.PANEL_HEIGHT + 1;
		contentAreaDest.Y += Module.PANEL_HEIGHT;
		PatternDrawer.draw(contentAreaDest, -1f, Color.Transparent, os.darkBackgroundColor, spriteBatch);
		PatternDrawer.draw(contentAreaDest, 3f, Color.Transparent, os.lockedColor, spriteBatch, PatternDrawer.errorTile);
	}
}
