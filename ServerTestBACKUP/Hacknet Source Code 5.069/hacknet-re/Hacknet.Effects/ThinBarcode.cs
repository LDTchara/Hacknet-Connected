using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class ThinBarcode
{
	private List<int> widths = new List<int>();

	private List<int> gaps = new List<int>();

	private List<int> widthsLast = new List<int>();

	private List<int> gapsLast = new List<int>();

	private int height;

	private int oldW;

	public ThinBarcode(int w, int height)
	{
		oldW = w;
		this.height = height;
		regenerate();
		widthsLast = widths;
		gapsLast = gaps;
	}

	public void regenerate()
	{
		int num = oldW;
		widthsLast = widths;
		gapsLast = gaps;
		widths.Clear();
		gaps.Clear();
		int num2 = 0;
		while (num2 < num)
		{
			int num3 = Math.Min(num - num2, Utils.random.Next(1, 20));
			if (num3 > 0)
			{
				int num4 = Utils.random.Next(3, 11);
				widths.Add(num3);
				gaps.Add(num4);
				num2 += num3 + num4;
			}
		}
	}

	public void Draw(SpriteBatch sb, int posX, int posY, Color c)
	{
		Rectangle destinationRectangle = new Rectangle(posX, posY, 0, height);
		for (int i = 0; i < widths.Count; i++)
		{
			destinationRectangle.Width = widths[i];
			sb.Draw(Utils.white, destinationRectangle, c);
			destinationRectangle.X += widths[i];
			destinationRectangle.X += gaps[i];
		}
	}
}
