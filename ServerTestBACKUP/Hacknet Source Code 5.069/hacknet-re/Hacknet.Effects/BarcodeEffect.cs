using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class BarcodeEffect
{
	private const double MAX_WIDTH = 8.0;

	private const double MAX_OFFSET = 2.0;

	private const double MAX_DELAY = 3.0;

	private const float COMPLETE_TIME = 4f;

	private const float COMPLETE_TIME_DELAY = 0.9f;

	private List<float> widths;

	private List<float> offsets;

	private List<float> delays;

	private List<float> complete;

	private float completeTime = 0f;

	public int maxWidth;

	public bool isInverted = false;

	public bool leftRightBias = false;

	public bool LeftRightMaxSizeFalloff = false;

	public BarcodeEffect(int width, bool inverted = false, bool leftRight = false)
	{
		maxWidth = width;
		isInverted = inverted;
		leftRightBias = leftRight;
		reset();
	}

	public void reset()
	{
		widths = new List<float>();
		offsets = new List<float>();
		delays = new List<float>();
		complete = new List<float>();
		float num = 0f;
		bool flag = false;
		while (num < (float)maxWidth)
		{
			if ((double)num + 8.0 >= (double)maxWidth)
			{
				widths.Add((float)maxWidth - num);
				delays.Add(0f);
				complete.Add(0f);
				num = maxWidth;
			}
			else
			{
				float num2 = (float)(Utils.random.NextDouble() * (flag ? 2.0 : 8.0));
				if (flag)
				{
					offsets.Add(num2);
				}
				else
				{
					widths.Add(num2);
					complete.Add(0f);
					if (leftRightBias)
					{
						float num3 = num / (float)maxWidth;
						delays.Add((float)((Utils.random.NextDouble() / 2.0 + (double)(num3 / 2f)) * (3.0 * (double)num3)));
					}
					else
					{
						delays.Add((float)(Utils.random.NextDouble() * 3.0));
					}
				}
				num += num2;
			}
			flag = !flag;
		}
	}

	public void Update(float t)
	{
		bool flag = true;
		for (int i = 0; i < delays.Count; i++)
		{
			if (delays[i] > 0f)
			{
				delays[i] -= t;
				if (delays[i] < 0f)
				{
					complete[i] = (0f - delays[i]) / 4f;
					delays[i] = 0f;
				}
				flag = false;
			}
			else
			{
				complete[i] += t / 4f;
				if (complete[i] > 1f)
				{
					complete[i] = 1f;
				}
				else
				{
					flag = false;
				}
			}
		}
		if (flag)
		{
			completeTime += t;
			if (completeTime > 0.9f)
			{
				reset();
				completeTime = 0f;
			}
		}
	}

	public void Draw(int x, int y, int maxWidth, int maxHeight, SpriteBatch sb, Color? barColor = null)
	{
		Color color = (barColor.HasValue ? barColor.Value : Color.White);
		color *= (float)Math.Pow(1f - completeTime / 0.9f, 3.0);
		Vector2 position = new Vector2(x, y);
		float num = 0f;
		for (int i = 0; i < widths.Count; i++)
		{
			float num2 = maxHeight;
			if (LeftRightMaxSizeFalloff)
			{
				int num3 = Math.Abs(i - widths.Count / 2);
				float num4 = 1f - Utils.QuadraticOutCurve((float)num3 / ((float)widths.Count / 2f));
				num2 = (float)maxHeight * num4;
			}
			position.X = (float)x + num;
			position.Y = y;
			float x2 = widths[i];
			float num5 = 0f;
			num5 = ((!(delays[i] > 0f)) ? ((float)((double)num2 * Math.Pow(complete[i], 0.5))) : 0f);
			if (isInverted)
			{
				position.Y = (float)(y + maxHeight) - num5;
			}
			sb.Draw(Utils.white, position, null, color, 0f, Vector2.Zero, new Vector2(x2, num5), SpriteEffects.None, 0.5f);
			num += widths[i];
			if (offsets.Count > i)
			{
				num += offsets[i];
			}
		}
	}
}
