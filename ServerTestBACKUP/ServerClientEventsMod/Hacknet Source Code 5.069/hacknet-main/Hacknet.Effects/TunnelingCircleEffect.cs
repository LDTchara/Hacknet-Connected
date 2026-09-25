using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class TunnelingCircleEffect
{
	private static Texture2D CircleTex;

	public static void Draw(SpriteBatch sb, Vector2 circleCentre, float diamater, float nodeDiamater, int subdivisions, float timer, Color nodeColor, Color lineColor, Color ZeroNodeColor, Rectangle clipBounds)
	{
		if (CircleTex == null)
		{
			CircleTex = Game1.getSingleton().Content.Load<Texture2D>("Circle");
		}
		float num = diamater / 2f;
		for (int i = 0; i < subdivisions; i++)
		{
			float num2 = (float)(Math.PI / (double)subdivisions);
			float num3 = (float)i * num2;
			Vector2 left = circleCentre - Utils.PolarToCartesian(num3, num);
			Vector2 right = circleCentre + Utils.PolarToCartesian(num3, num);
			Utils.ClipLineSegmentsForRect(clipBounds, left, right, out var leftOut, out var rightOut);
			if (Vector2.Distance(leftOut, rightOut) > 0.001f)
			{
				Utils.drawLine(sb, leftOut, rightOut, Vector2.Zero, lineColor, 0.2f);
			}
			float num4 = (float)Math.Sin((double)timer + (double)num3);
			Vector2 vector = circleCentre + Utils.PolarToCartesian(num3, num * num4) - new Vector2(nodeDiamater / 2f);
			Vector2 scale = new Vector2(nodeDiamater / (float)CircleTex.Width);
			Vector2 origin = new Vector2(nodeDiamater / 2f * scale.X);
			Rectangle clipRectForSpritePos = Utils.getClipRectForSpritePos(clipBounds, CircleTex, vector, scale.X);
			if (clipRectForSpritePos.Height > 0 && clipRectForSpritePos.Width > 0)
			{
				sb.Draw(CircleTex, vector, clipRectForSpritePos, (i == 0) ? ZeroNodeColor : nodeColor, 0f, origin, scale, SpriteEffects.None, 0.25f);
			}
		}
	}
}
