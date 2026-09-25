using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public static class WebpageLoadingEffect
{
	public static void DrawLoadingEffect(Rectangle bounds, SpriteBatch sb, object OS_obj, bool drawLoadingText = true)
	{
		OS oS = (OS)OS_obj;
		Rectangle rectangle = new Rectangle(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, 2, 70);
		float num = (float)Math.Abs(Math.Sin(oS.timer * 0.2f) * 100.0);
		Vector2 origin = new Vector2(2f, 10f);
		float num2 = (float)Math.PI * 2f / num;
		float num3 = 0f;
		int num4 = 0;
		for (; num3 < (float)Math.PI * 2f; num3 += num2)
		{
			Rectangle destinationRectangle = rectangle;
			destinationRectangle.Height = Math.Abs((int)((double)(float)rectangle.Height * Math.Sin(2f * oS.timer + (float)num4 * 0.2f))) + 10;
			origin.Y = 1.6f;
			sb.Draw(Utils.white, destinationRectangle, null, oS.highlightColor, num3, origin, SpriteEffects.FlipVertically, 0.6f);
			num4++;
		}
		if (drawLoadingText)
		{
			TextItem.doFontLabelToSize(new Rectangle(bounds.X, bounds.Y + 20, bounds.Width, 30), LocaleTerms.Loc("Loading..."), GuiData.font, Utils.AddativeWhite);
		}
	}
}
