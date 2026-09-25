using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class ClockExe : ExeModule
{
	public ClockExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "Clock";
		ramCost = 60;
		IdentifierName = "Clock";
		targetIP = os.thisComputer.ip;
		AchievementsManager.Unlock("clock_run");
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		DateTime now = DateTime.Now;
		string text = (now.Hour % 12).ToString("00") + " : " + now.Minute.ToString("00") + " : " + now.Second.ToString("00");
		TextItem.doFontLabel(new Vector2(bounds.X + 2, bounds.Y + 12), text, GuiData.titlefont, RamModule.USED_RAM_COLOR, bounds.Width - 34, bounds.Height - 10, centreVertically: true);
		TextItem.doFontLabel(new Vector2(bounds.X + bounds.Width - 28, bounds.Y + bounds.Height - 38), (now.Hour > 12) ? "PM" : "AM", GuiData.titlefont, RamModule.USED_RAM_COLOR, 30f, 26f);
		int num = bounds.Width - 2;
		Rectangle destinationRectangle = new Rectangle(bounds.X + 1, bounds.Y + bounds.Height - 1 - 6, num, 1);
		float num2 = (float)now.Millisecond / 1000f;
		float num3 = 0f;
		if (num2 < 0.5f)
		{
			num3 = 1f - num2 * 2f;
		}
		spriteBatch.Draw(Utils.white, destinationRectangle, os.moduleColorSolidDefault * 0.2f * num3);
		destinationRectangle.Width = (int)((float)num * num2);
		spriteBatch.Draw(Utils.white, destinationRectangle, os.moduleColorSolidDefault * 0.2f);
		float num4 = ((float)now.Second + num2) / 60f;
		destinationRectangle.Width = (int)((float)num * num4);
		destinationRectangle.Y += 2;
		spriteBatch.Draw(Utils.white, destinationRectangle, os.moduleColorStrong);
		float num5 = (float)now.Minute / 60f;
		float num6 = (float)now.Hour / 60f;
		destinationRectangle.Width = (int)((float)num * num5);
		destinationRectangle.Y += 2;
		spriteBatch.Draw(Utils.white, destinationRectangle, os.moduleColorSolid);
	}
}
