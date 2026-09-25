using System;
using Microsoft.Xna.Framework;

namespace Hacknet.Misc;

public class HSLColor
{
	public float Hue;

	public float Saturation;

	public float Luminosity;

	public HSLColor(float H, float S, float L)
	{
		Hue = H;
		Saturation = S;
		Luminosity = L;
	}

	public static HSLColor FromRGB(Color Clr)
	{
		return FromRGB(Clr.R, Clr.G, Clr.B);
	}

	public static HSLColor FromRGB(byte R, byte G, byte B)
	{
		float num = (float)(int)R / 255f;
		float num2 = (float)(int)G / 255f;
		float num3 = (float)(int)B / 255f;
		float num4 = Math.Min(Math.Min(num, num2), num3);
		float num5 = Math.Max(Math.Max(num, num2), num3);
		float num6 = num5 - num4;
		float h = 0f;
		float s = 0f;
		float num7 = (num5 + num4) / 2f;
		if (num6 != 0f)
		{
			s = ((!(num7 < 0.5f)) ? (num6 / (2f - num5 - num4)) : (num6 / (num5 + num4)));
			if (num == num5)
			{
				h = (num2 - num3) / num6;
			}
			else if (num2 == num5)
			{
				h = 2f + (num3 - num) / num6;
			}
			else if (num3 == num5)
			{
				h = 4f + (num - num2) / num6;
			}
		}
		return new HSLColor(h, s, num7);
	}

	private float Hue_2_RGB(float v1, float v2, float vH)
	{
		if (vH < 0f)
		{
			vH += 1f;
		}
		if (vH > 1f)
		{
			vH -= 1f;
		}
		if (6f * vH < 1f)
		{
			return v1 + (v2 - v1) * 6f * vH;
		}
		if (2f * vH < 1f)
		{
			return v2;
		}
		if (3f * vH < 2f)
		{
			return v1 + (v2 - v1) * (0f - vH) * 6f;
		}
		return v1;
	}

	public Color ToRGB()
	{
		byte r;
		byte g;
		byte b;
		if (Saturation == 0f)
		{
			r = (byte)Math.Round((double)Luminosity * 255.0);
			g = (byte)Math.Round((double)Luminosity * 255.0);
			b = (byte)Math.Round((double)Luminosity * 255.0);
		}
		else
		{
			double num = (double)Hue / 6.0;
			double num2 = ((!((double)Luminosity < 0.5)) ? ((double)(Luminosity + Saturation - Luminosity * Saturation)) : ((double)Luminosity * (1.0 + (double)Saturation)));
			double t = 2.0 * (double)Luminosity - num2;
			double c = num + 1.0 / 3.0;
			double c2 = num;
			double c3 = num - 1.0 / 3.0;
			c = ColorCalc(c, t, num2);
			c2 = ColorCalc(c2, t, num2);
			c3 = ColorCalc(c3, t, num2);
			r = (byte)Math.Round(c * 255.0);
			g = (byte)Math.Round(c2 * 255.0);
			b = (byte)Math.Round(c3 * 255.0);
		}
		Color result = default(Color);
		result.R = r;
		result.G = g;
		result.B = b;
		result.A = byte.MaxValue;
		return result;
	}

	private static double ColorCalc(double c, double t1, double t2)
	{
		if (c < 0.0)
		{
			c += 1.0;
		}
		if (c > 1.0)
		{
			c -= 1.0;
		}
		if (6.0 * c < 1.0)
		{
			return t1 + (t2 - t1) * 6.0 * c;
		}
		if (2.0 * c < 1.0)
		{
			return t2;
		}
		if (3.0 * c < 2.0)
		{
			return t1 + (t2 - t1) * (2.0 / 3.0 - c) * 6.0;
		}
		return t1;
	}
}
