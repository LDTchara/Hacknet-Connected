using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class MovingBarsEffect
{
	private struct BarLine
	{
		public float Current;

		public float Next;

		public float TimeRemaining;

		public float TotalTimeThisStep;
	}

	private List<BarLine> Lines = new List<BarLine>();

	public float MinLineChangeTime = 0.2f;

	public float MaxLineChangeTime = 2f;

	public bool IsInverted = false;

	public void Update(float dt)
	{
		for (int i = 0; i < Lines.Count; i++)
		{
			BarLine value = Lines[i];
			value.TimeRemaining -= dt;
			if (value.TimeRemaining <= 0f)
			{
				value.Current = value.Next;
				value.Next = Utils.randm(1f);
				value.TotalTimeThisStep = MinLineChangeTime + Utils.randm(MaxLineChangeTime - MinLineChangeTime);
				value.TimeRemaining = value.TotalTimeThisStep;
			}
			Lines[i] = value;
		}
	}

	public void Draw(SpriteBatch sb, Rectangle bounds, float minHeight, float lineWidth, float lineSeperation, Color drawColor)
	{
		int num = 0;
		float num2;
		for (num2 = 0f; num2 + lineWidth < (float)bounds.Width; num2 += lineWidth + lineSeperation)
		{
			num++;
		}
		bool flag = false;
		while (Lines.Count - 1 < num)
		{
			Lines.Add(new BarLine
			{
				TimeRemaining = -1f
			});
			flag = true;
		}
		if (flag)
		{
			Update(0f);
		}
		num2 = bounds.X;
		for (int i = 0; i < num; i++)
		{
			BarLine barLine = Lines[i];
			float point = 1f - barLine.TimeRemaining / barLine.TotalTimeThisStep;
			point = Utils.QuadraticOutCurve(point);
			float num3 = barLine.Current + (barLine.Next - barLine.Current) * point;
			float num4 = (float)bounds.Height - minHeight;
			int num5 = (int)(minHeight + num4 * num3);
			Rectangle destinationRectangle = new Rectangle((int)num2, bounds.Y + bounds.Height - num5, (int)lineWidth, num5);
			if (IsInverted)
			{
				destinationRectangle.Y = bounds.Y;
			}
			sb.Draw(Utils.white, destinationRectangle, drawColor);
			num2 += lineWidth + lineSeperation;
		}
	}
}
