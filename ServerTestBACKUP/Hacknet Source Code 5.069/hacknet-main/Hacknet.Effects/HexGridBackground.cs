using System;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class HexGridBackground
{
	public enum ColoringAlgorithm
	{
		NegaitiveSinWash,
		CorrectedSinWash,
		OutlinedSinWash
	}

	private Texture2D Hex;

	private Texture2D Outline;

	private float timer = 0f;

	private LCG lcg = new LCG(microsoft: false);

	public bool HasRedFlashyOnes = false;

	public float HexScale = 0.1f;

	public HexGridBackground(ContentManager content)
	{
		Hex = content.Load<Texture2D>("Sprites/Misc/Hexagon");
		Outline = content.Load<Texture2D>("Sprites/Misc/HexOutline");
	}

	public void Update(float dt)
	{
		timer += dt;
	}

	public void Draw(Rectangle dest, SpriteBatch sb, Color first, Color second, ColoringAlgorithm algorithm = ColoringAlgorithm.CorrectedSinWash, float angle = 0f)
	{
		float num = 2f;
		float num2 = dest.X;
		float num3 = dest.Y;
		float num4 = HexScale * (float)Hex.Width;
		float num5 = HexScale * (float)Hex.Height;
		int num6 = 10;
		bool flag = true;
		while (num2 + num4 < (float)(dest.X + dest.Width))
		{
			for (; num3 + num5 < (float)(dest.Y + dest.Height); num3 += num5 + num)
			{
				lcg.reSeed(num6);
				float num7 = lcg.NextFloat();
				float num8 = 0f;
				bool flag2 = false;
				Color value = second;
				if (HasRedFlashyOnes && num7 <= 0.001f)
				{
					value = Utils.AddativeRed * 0.5f;
					flag2 = true;
				}
				num8 = algorithm switch
				{
					ColoringAlgorithm.NegaitiveSinWash => (float)Math.Sin(num7 + timer * lcg.NextFloat()), 
					_ => Math.Abs((float)Math.Sin(num7 + timer * Math.Abs(lcg.NextFloat() * (flag2 ? 1f : 0.3f)))), 
				};
				sb.Draw(Hex, Utils.RotatePoint(new Vector2(num2, num3), angle), null, Color.Lerp(first, value, num8), angle, Vector2.Zero, new Vector2(HexScale), SpriteEffects.None, 0.4f);
				if (algorithm == ColoringAlgorithm.OutlinedSinWash)
				{
					if (flag2)
					{
						value = Utils.AddativeRed;
					}
					sb.Draw(Outline, Utils.RotatePoint(new Vector2(num2, num3), angle), null, Color.Lerp(first, value, num8), angle, Vector2.Zero, new Vector2(HexScale), SpriteEffects.None, 0.4f);
				}
				num6++;
			}
			num2 += num4 - 60f * HexScale + num;
			num3 = dest.Y;
			num6++;
			if (flag)
			{
				num3 -= num5 / 2f;
			}
			flag = !flag;
		}
	}
}
