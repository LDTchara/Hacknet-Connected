using System;
using Microsoft.Xna.Framework;

namespace Hacknet.Effects;

public class PortHackCubeSequence
{
	private float rotTime = 0f;

	private float elapsedTime = 0f;

	private float startup = 0.3f;

	private float spinup = 0f;

	private float runtime = 0f;

	private float spindown = 0f;

	private float idle = 0f;

	public bool HeartFadeSequenceComplete = false;

	public bool ShouldCentralSpinInfinitley = false;

	public void Reset()
	{
		spinup = (startup = (runtime = (idle = (spindown = (rotTime = 0f)))));
		HeartFadeSequenceComplete = false;
	}

	public void DrawSequence(Rectangle dest, float t, float totalTime)
	{
		float num = 0.6f;
		float num2 = 1f;
		float num3 = (ShouldCentralSpinInfinitley ? float.MaxValue : (totalTime - 1.7f));
		float num4 = float.MaxValue;
		float num5 = float.MaxValue;
		elapsedTime += t;
		float val = elapsedTime;
		val = Math.Max(0f, val);
		float num6 = 0f;
		float num7 = 1f;
		float num8 = 1f;
		float num9 = 1f;
		if (startup < num)
		{
			startup += t;
			num7 = 0f;
			num8 = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(startup / num));
		}
		else if (spinup < num2)
		{
			spinup = Math.Min(num2, spinup + t);
			num7 = spinup / num2;
			rotTime += t * num7;
		}
		else if (runtime < num3)
		{
			runtime = Math.Min(num3, runtime + t);
			rotTime += t;
		}
		else if (spindown < num4)
		{
			spindown += t;
			num6 = Math.Min(1f, spindown) / 2.3f / 1f;
			num7 = 0.1f + 0.9f * (1f - num6);
			if (num6 >= 0.4f)
			{
				num7 = 0.2f;
			}
			rotTime += t * num7;
			num9 = 1f - num6;
			num8 = 0.3f + 0.7f * Utils.QuadraticOutCurve(1f - num6);
		}
		else if (idle < num5)
		{
			idle += t;
			num7 = 0.1f;
			rotTime += t * (num7 * 2f);
			num9 = 0f;
			num8 = 0.3f;
		}
		else
		{
			spinup = (startup = (runtime = (idle = (spindown = (rotTime = 0f)))));
		}
		int num10 = 20;
		for (int i = 0; i < num10; i++)
		{
			float num11 = (float)i / (float)num10;
			float num12 = 1f + 0.05f * num7 * ((float)(num10 - i) / (float)num10 * 10f);
			float num13 = num9;
			float val2 = rotTime * num12 - (float)i * 0.1f * num13;
			val2 = Math.Max(0f, val2);
			float num14 = val2 * 1.5f;
			float num15 = num8;
			for (int j = 0; j < i; j++)
			{
				num15 *= num8;
			}
			float num16 = num8 * ((float)(num10 - i) / (float)num10);
			bool flag = num11 <= num7;
			Cube3D.RenderWireframe(new Vector3(dest.X / 2, 0f, 0f), 2.6f + (float)i / 4f * num15, new Vector3(MathHelper.ToRadians(35.4f), MathHelper.ToRadians(45f) + num14, MathHelper.ToRadians(0f)), Color.White);
		}
	}

	public void DrawHeartSequence(Rectangle dest, float t, float totalTime)
	{
		float num = 3f;
		float num2 = 10f;
		float num3 = 2f;
		float num4 = 9f;
		float num5 = float.MaxValue;
		float num6 = 2f;
		float num7 = 21f;
		elapsedTime += t;
		float val = elapsedTime;
		val = Math.Max(0f, val);
		float num8 = 0f;
		float num9 = 1f;
		float num10 = 1f;
		float num11 = 1f;
		float num12 = 0f;
		if (startup < num)
		{
			startup += t;
			num10 = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(startup / 3f));
		}
		else if (spinup < num2)
		{
			spinup = Math.Min(10f, spinup + t);
			num9 = spinup / 10f;
			rotTime += t * num9;
		}
		else if (runtime < num3)
		{
			runtime = Math.Min(2f, runtime + t);
			rotTime += t;
		}
		else if (spindown < num4)
		{
			spindown += t;
			num8 = spindown / 9f;
			num9 = 0.1f + 0.9f * (1f - num8);
			rotTime += t * num9;
			num11 = 1f - num8;
			num10 = 0.3f + 0.7f * Utils.QuadraticOutCurve(1f - num8);
		}
		else if (idle < num5)
		{
			idle += t;
			num9 = 0.1f;
			rotTime += t * num9;
			num11 = 0f;
			num10 = 0.3f;
			if (idle > num6)
			{
				num12 = Utils.QuadraticOutCurve(Math.Min(1f, (idle - num6) / num7));
				HeartFadeSequenceComplete = num12 >= 1f;
			}
		}
		else
		{
			spinup = (startup = (runtime = (idle = (spindown = (rotTime = 0f)))));
		}
		int num13 = 20;
		for (int i = 0; i < num13; i++)
		{
			float num14 = (float)i / (float)num13;
			float num15 = 1f + 0.05f * num9 * ((float)(num13 - i) / (float)num13 * 10f);
			float num16 = num11;
			float val2 = rotTime * num15 - (float)i * 0.1f * num16;
			val2 = Math.Max(0f, val2);
			float num17 = val2 * 1.5f;
			float num18 = num10;
			for (int j = 0; j < i; j++)
			{
				num18 *= num10;
			}
			float num19 = num10 * ((float)(num13 - i) / (float)num13);
			bool flag = num14 <= num9;
			float num20 = 1f;
			Color color = Color.White;
			if (num12 > 0f)
			{
				float num21 = 1f / (float)num13;
				if ((float)i * num21 < num12)
				{
					float num22 = (float)(i + 1) * num21;
					if (num12 > num22)
					{
						num20 = 0f;
						color = Color.Transparent;
					}
					else
					{
						num20 = 1f - num12 % num21 / num21;
						color = Color.Lerp(Utils.AddativeRed, Color.Red, num20) * num20;
					}
				}
			}
			Cube3D.RenderWireframe(new Vector3(dest.X / 2, 0f, 0f), 2.6f + (float)i / 4f * num18, new Vector3(MathHelper.ToRadians(35.4f), MathHelper.ToRadians(45f) + num17, MathHelper.ToRadians(0f)), color);
		}
	}
}
