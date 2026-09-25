using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.UIUtils;

internal class WaveformRenderer
{
	private double[] left;

	private double[] right;

	private int SecondBlockSize = 100;

	public WaveformRenderer(string filename)
	{
		AudioUtils.openWav(filename, out left, out right);
	}

	public void RenderWaveform(double time, double totalTime, SpriteBatch sb, Rectangle bounds)
	{
		time %= totalTime;
		SecondBlockSize = (int)((double)left.Length / totalTime);
		int num = SecondBlockSize / 100;
		int num2 = (int)(time * (double)SecondBlockSize);
		int num3 = Math.Min(left.Length - 1, num2 + num);
		int num4 = num3 - num2;
		double num5 = (double)bounds.Width / (double)num4;
		int num6 = 0;
		for (int i = 0; i < num4; i++)
		{
			double num7 = left[num2 + i];
			if (num7 == 0.0)
			{
				num6++;
			}
			double num8 = num7 * (double)bounds.Height;
			sb.Draw(destinationRectangle: new Rectangle((int)((double)bounds.X + (double)i * num5), bounds.Y + bounds.Height / 2 - (int)(num8 / 2.0), (int)num5, (int)num8), texture: Utils.white, color: Color.White * 0.4f);
		}
	}
}
