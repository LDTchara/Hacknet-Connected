using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class DepthDotGridEffect
{
	private Texture2D tex;

	public DepthDotGridEffect(ContentManager content)
	{
		tex = content.Load<Texture2D>("Circle");
	}

	public void DrawGrid(Rectangle fullAreaDest, Vector2 xyOffset, SpriteBatch sb, float pixelsInOnRecurse, int recursionSteps, Color dotColor, float dotSeperation, float dotSize, float MaxDepthEffectDistance, float timer, float chaosPercent)
	{
		if (recursionSteps > 0 && dotSeperation > dotSize && dotSize > 0.1f)
		{
			DrawGrid(fullAreaDest, xyOffset, sb, pixelsInOnRecurse, recursionSteps - 1, dotColor * 0.8f, dotSeperation, dotSize * 0.6f, MaxDepthEffectDistance + pixelsInOnRecurse, timer, chaosPercent);
		}
		Vector2 vector = new Vector2((float)fullAreaDest.X * xyOffset.X, (float)fullAreaDest.Y + xyOffset.Y);
		Vector2 vector2 = vector;
		float num = timer + (float)recursionSteps * 0.12f;
		float num2 = 2.5f * (float)recursionSteps;
		Vector2 vector3 = new Vector2((float)Math.Sin(num) * num2, 0f);
		Vector2 vector4 = vector3;
		float num3 = num % 6f;
		if (num3 <= 2f)
		{
			vector3.X = Utils.QuadraticOutCurve(num3 / 2f) * dotSeperation;
			vector3.Y = 0f;
		}
		else if (num3 <= 5f && num3 >= 3f)
		{
			vector3.Y = Utils.QuadraticOutCurve((num3 - 3f) / 2f) * dotSeperation;
			vector3.X = 0f;
		}
		else
		{
			vector3.X = 0f;
			vector3.Y = 0f;
		}
		vector3 += chaosPercent * vector4;
		vector -= new Vector2(dotSeperation * 1f);
		int num4 = 0;
		int num5 = 0;
		while (vector.Y < (float)(fullAreaDest.Y + fullAreaDest.Height))
		{
			while (vector.X - dotSize - MaxDepthEffectDistance < (float)(fullAreaDest.X + fullAreaDest.Width))
			{
				Vector2 vector5 = new Vector2(0f, ((num5 % 2 == 0) ? 1f : (-1f)) * vector3.Y);
				Vector2 vector6 = vector + vector5;
				float num6 = (float)fullAreaDest.Width / 2f;
				float num7 = vector6.X - (float)fullAreaDest.X - num6;
				float num8 = num7 / num6;
				float num9 = (float)fullAreaDest.Height / 2f;
				float num10 = vector6.Y - (float)fullAreaDest.Y - num9;
				float num11 = num10 / num9;
				num8 *= 0.5f;
				num11 *= 0.5f;
				Vector2 vector7 = new Vector2(-1f * num8 * MaxDepthEffectDistance, -1f * num11 * MaxDepthEffectDistance);
				Vector2 position = vector6 + vector7;
				Utils.RenderSpriteIntoClippedRectDest(fullAreaDest, new Rectangle((int)(position.X - dotSize / 2f), (int)(position.Y - dotSize / 2f), (int)dotSize, (int)dotSize), tex, dotColor, sb);
				if ((Utils.random.NextDouble() < 1E-05 || Utils.random.NextDouble() < 3E-05) && fullAreaDest.Contains((int)position.X, (int)position.Y))
				{
					sb.DrawString(GuiData.UITinyfont, num8.ToString(".0") + "/" + num11.ToString(".0"), position, Color.Red);
				}
				vector.X += dotSeperation;
				num5++;
			}
			vector.Y += dotSeperation;
			num4++;
			vector.X = vector2.X + ((num4 % 2 == 0) ? 1f : (-1f)) * vector3.X;
			num5 = 0;
		}
	}
}
