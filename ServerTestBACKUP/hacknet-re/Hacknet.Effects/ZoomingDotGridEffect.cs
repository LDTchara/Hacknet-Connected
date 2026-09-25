using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public static class ZoomingDotGridEffect
{
	private static Texture2D CircleTex;

	public static void Render(Rectangle dest, SpriteBatch sb, float timer, Color themeColor)
	{
		if (CircleTex == null)
		{
			CircleTex = OS.currentInstance.content.Load<Texture2D>("Circle");
		}
		float num = 12f;
		float num2 = 4f;
		float num3 = 4f;
		float num4 = 3.5f * num3;
		float num5 = (float)Math.Sqrt(Math.Pow(num4, 2.0) * 2.0);
		float num6 = timer % num;
		float num7 = num4 + (num5 - num4) * (num6 / num);
		float num8 = (float)Math.PI / 4f;
		float rotation = num6 / num * num8;
		Vector2 origin = new Vector2(dest.X, dest.Y);
		float num9 = (num6 - (num - num2)) / num2;
		if (!(num - num6 <= num2))
		{
			num9 = 0f;
		}
		Vector2 vector = num6 / num * new Vector2(-1f * num7 / 2.5f, -1f * num7 / 3f);
		vector = Vector2.Zero;
		Vector2 point = new Vector2((float)dest.X - num7 * 0f, (float)dest.Y - num7 * 10f);
		float x = point.X;
		while (point.Y < (float)(dest.Y + dest.Height) + num5 + num3)
		{
			point.X = x;
			while (point.X < (float)(dest.X + dest.Width * 2) + num5 + num3)
			{
				Vector2 vector2 = rotatePointAroundOrigin(point, origin, rotation) + vector;
				Utils.RenderSpriteIntoClippedRectDest(targetBounds: new Rectangle((int)(vector2.X - num3 / 2f), (int)(vector2.Y - num3 / 2f), (int)num3, (int)num3), fullBounds: dest, tex: CircleTex, c: themeColor, sb: sb);
				point.X += num7;
			}
			point.Y += num7;
		}
		if (!(num - num6 <= num2))
		{
			return;
		}
		point = new Vector2((float)dest.X - num7 / 2f, (float)dest.Y - num7 * 10.5f);
		x = point.X;
		while (point.Y < (float)(dest.Y + dest.Height) + num5 + num3)
		{
			point.X = x;
			while (point.X < (float)(dest.X + dest.Width * 2) + num5 + num3)
			{
				Vector2 vector2 = rotatePointAroundOrigin(point, origin, rotation) + vector;
				Utils.RenderSpriteIntoClippedRectDest(targetBounds: new Rectangle((int)(vector2.X - num3 / 2f), (int)(vector2.Y - num3 / 2f), (int)num3, (int)num3), fullBounds: dest, tex: CircleTex, c: themeColor * num9, sb: sb);
				point.X += num7;
			}
			point.Y += num7;
		}
	}

	private static Vector2 rotatePointAroundOrigin(Vector2 point, Vector2 origin, float rotation)
	{
		Vector2 point2 = point - origin;
		Vector2 vector = Utils.RotatePoint(point2, rotation);
		return vector + origin;
	}
}
