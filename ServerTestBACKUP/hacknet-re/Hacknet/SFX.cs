using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public static class SFX
{
	private struct RadialLineData
	{
		public Vector2 destination;

		public float angle;

		public float distance;

		public Color color;

		public float width;

		public float MovementPerSecond;

		public float Acceleration;

		public float FadeDistance;

		public float SizeForSpeedMultiplier;

		public bool SnapMode;
	}

	private static List<Vector2> circlePos = new List<Vector2>();

	private static List<float> circleRadius = new List<float>();

	private static List<float> circleExpand = new List<float>();

	private static List<Color> circleColor = new List<Color>();

	private static List<RadialLineData> RadialLines = new List<RadialLineData>();

	private static Texture2D circleTex;

	private static Vector2 circleOrigin;

	public static void init(ContentManager content)
	{
		circleTex = content.Load<Texture2D>("CircleOutlineLarge");
		circleOrigin = new Vector2(circleTex.Width / 2, circleTex.Height / 2);
	}

	public static void AddRadialLine(Vector2 pos, float incomingAngle, float startDistance, float startSpeed, float acceleration, float fadeDistance, float speedSizeMultiplier, Color color, float width = 1f, bool snapMode = false)
	{
		RadialLines.Add(new RadialLineData
		{
			destination = pos,
			angle = incomingAngle,
			color = color,
			distance = startDistance,
			Acceleration = acceleration,
			FadeDistance = fadeDistance,
			SizeForSpeedMultiplier = speedSizeMultiplier,
			SnapMode = snapMode,
			width = width
		});
	}

	public static void addCircle(Vector2 pos, Color color, float radius)
	{
		circlePos.Add(pos);
		circleColor.Add(color);
		circleExpand.Add(0f);
		circleRadius.Add(radius / 256f);
	}

	public static void Update(float t)
	{
		for (int i = 0; i < circlePos.Count; i++)
		{
			circleExpand[i] += t;
			if (circleExpand[i] >= 3f)
			{
				circlePos.RemoveAt(i);
				circleColor.RemoveAt(i);
				circleExpand.RemoveAt(i);
				circleRadius.RemoveAt(i);
				i--;
			}
		}
		for (int i = 0; i < RadialLines.Count; i++)
		{
			RadialLineData value = RadialLines[i];
			value.distance -= value.MovementPerSecond * t;
			value.MovementPerSecond += value.Acceleration * t;
			if (value.distance <= 0f)
			{
				RadialLines.RemoveAt(i);
				i--;
			}
			else
			{
				RadialLines[i] = value;
			}
		}
	}

	public static void Draw(SpriteBatch sb)
	{
		try
		{
			for (int i = 0; i < circlePos.Count; i++)
			{
				float num = circleExpand[i] / 3f;
				num = ((!(num < 0.2f)) ? (1f - num) : (num / 0.2f));
				sb.Draw(circleTex, circlePos[i], null, circleColor[i] * num, 0f, circleOrigin, Vector2.One * circleRadius[i] * circleExpand[i], SpriteEffects.None, 0.2f);
			}
			for (int i = 0; i < RadialLines.Count; i++)
			{
				RadialLineData radialLineData = RadialLines[i];
				float val = radialLineData.MovementPerSecond * radialLineData.SizeForSpeedMultiplier;
				Vector2 vector = Utils.PolarToCartesian(radialLineData.angle, radialLineData.distance);
				if (radialLineData.SnapMode)
				{
					val = Math.Min(val, vector.Length());
				}
				else
				{
					val = Math.Min(val, radialLineData.distance);
					if (val < 0f)
					{
						val = 0f;
					}
				}
				Vector2 vector2 = vector + Utils.PolarToCartesian(radialLineData.angle, val);
				float num2 = 1f;
				if (radialLineData.distance > radialLineData.FadeDistance)
				{
					num2 = 1f - (radialLineData.distance - radialLineData.FadeDistance) / radialLineData.FadeDistance;
				}
				Utils.drawLineAlt(sb, radialLineData.destination + vector, radialLineData.destination + vector2, Vector2.Zero, radialLineData.color * num2, 0.5f, radialLineData.width, Utils.gradientLeftRight);
			}
		}
		catch (IndexOutOfRangeException)
		{
		}
		catch (ArgumentOutOfRangeException)
		{
		}
	}
}
