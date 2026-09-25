using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class ShiftingGridEffect
{
	private struct ShiftingGridSpot
	{
		public float from;

		public float to;

		public float time;

		public float totalTime;
	}

	private ShiftingGridSpot[,] themeGrid = new ShiftingGridSpot[20, 100];

	public ShiftingGridEffect()
	{
	}

	public ShiftingGridEffect(int patternWidth, int patternHeight)
	{
		themeGrid = new ShiftingGridSpot[patternWidth, patternHeight];
	}

	public void ResetThemeGrid()
	{
		for (int i = 0; i < themeGrid.GetLength(0); i++)
		{
			for (int j = 0; j < themeGrid.GetLength(1); j++)
			{
				ResetGridPoint(j, i);
			}
		}
	}

	public void ResetGridPoint(int x, int y)
	{
		ref ShiftingGridSpot reference = ref themeGrid[y, x];
		reference = new ShiftingGridSpot
		{
			from = themeGrid[y, x].to,
			to = Utils.randm(2f),
			time = 0f,
			totalTime = Utils.randm(3f) + 1.2f
		};
	}

	public void Update(float t)
	{
		for (int i = 0; i < themeGrid.GetLength(0); i++)
		{
			for (int j = 0; j < themeGrid.GetLength(1); j++)
			{
				ShiftingGridSpot shiftingGridSpot = themeGrid[i, j];
				shiftingGridSpot.time += t;
				if (shiftingGridSpot.time >= shiftingGridSpot.totalTime)
				{
					ResetGridPoint(j, i);
				}
				else
				{
					themeGrid[i, j] = shiftingGridSpot;
				}
			}
		}
	}

	public void RenderGrid(Rectangle bounds, SpriteBatch sb, Color c1, Color c2, Color c3, bool centreEffect = false)
	{
		int num = 12;
		if (centreEffect)
		{
			bounds.X -= bounds.Width % num;
		}
		int x = bounds.X + bounds.Width - num - 1;
		Rectangle destinationRectangle = new Rectangle(x, bounds.Y + 1, num, num);
		int num2;
		int num3 = (num2 = 0);
		while (destinationRectangle.Y + 1 < bounds.Y + bounds.Height)
		{
			if (destinationRectangle.Y + num + 1 >= bounds.Y + bounds.Height)
			{
				destinationRectangle.Height = bounds.Y + bounds.Height - (destinationRectangle.Y + 2);
			}
			while (destinationRectangle.X - num > bounds.X + bounds.Width - bounds.Width - 1)
			{
				ShiftingGridSpot shiftingGridSpot = themeGrid[num3 % themeGrid.GetLength(0), num2 % themeGrid.GetLength(1)];
				float num4 = shiftingGridSpot.time / shiftingGridSpot.totalTime;
				float num5 = shiftingGridSpot.from + num4 * (shiftingGridSpot.to - shiftingGridSpot.from);
				Color value = c3;
				Color value2 = c2;
				if (num5 >= 1f)
				{
					num5 -= 1f;
					value = c2;
					value2 = c1;
				}
				Color color = Color.Lerp(value, value2, num5);
				sb.Draw(Utils.white, destinationRectangle, color);
				num3++;
				destinationRectangle.X -= num + 1;
			}
			num2++;
			destinationRectangle.X = x;
			destinationRectangle.Y += num + 1;
		}
	}
}
