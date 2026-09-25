using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class PointGatherEffect
{
	public struct StarPoint
	{
		public Vector2 Pos;

		public Vector2 Velocity;

		public float size;

		public bool AttractedToOthers;

		public float drawnSize;
	}

	public List<StarPoint> Points = new List<StarPoint>();

	public float timeRemainingWithoutAttract = 0f;

	public float MaxSpeed = 1.6f;

	public float Friction = 0.001f;

	public float NoAttractPhaseFriction = 1.7f;

	public float GravityConstant = 0.001f;

	public float absorbDistance = 0.005f;

	public float attractionToCentreMass = 1f;

	public float GlowScaleMod = 1f;

	public float LineLengthPercentage = 1f;

	public bool AllowDoubleLines = false;

	private float starSize = -1f;

	public Texture2D CircleTex;

	public Texture2D ScanlinesBackground;

	public Texture2D Star;

	public Texture2D LineTexture;

	public Color NodeColor = Utils.AddativeWhite * 0.65f;

	public void Init(ContentManager content)
	{
		CircleTex = content.Load<Texture2D>("Circle");
		ScanlinesBackground = content.Load<Texture2D>("EffectFiles/ScanlinesTextBackground");
		Star = content.Load<Texture2D>("EffectFiles/PointClicker/Star");
		LineTexture = Utils.white;
	}

	public void Explode(int entities)
	{
		for (int i = 0; i < entities; i++)
		{
			float num = ((Utils.randm(1f) < 0.3f) ? 2f : 1f);
			Points.Add(new StarPoint
			{
				Pos = new Vector2(0.5f, 0.5f),
				Velocity = new Vector2((float)(-1.0 * (double)MaxSpeed + Utils.random.NextDouble() * (double)MaxSpeed * 2.0), (float)(-1.0 * (double)MaxSpeed + Utils.random.NextDouble() * (double)MaxSpeed * 2.0)),
				size = num,
				drawnSize = num,
				AttractedToOthers = (Utils.randm(1f) < 0.7f)
			});
		}
		timeRemainingWithoutAttract = 1f;
	}

	private StarPoint ProcessAttractionBetweenPoints(StarPoint pA, StarPoint pB, float dt, bool constantForce = false)
	{
		Vector2 vector = (constantForce ? (pA.Pos + pA.Velocity * 0.4f) : pA.Pos);
		float num = Vector2.Distance(pA.Pos, pB.Pos);
		float num2 = (float)Math.Pow(num, 2.0);
		if (constantForce)
		{
			num2 = 0.2f;
		}
		float num3 = GravityConstant * (pA.size * pB.size) / num2;
		num3 *= pA.size / pB.size;
		Vector2 vector2 = vector - pB.Pos;
		vector2.Normalize();
		vector2 *= num3;
		pB.Velocity += vector2 * dt;
		return pB;
	}

	public void FlashComplete()
	{
		starSize = 6f;
	}

	public void Update(float dt)
	{
		float num = NoAttractPhaseFriction;
		timeRemainingWithoutAttract -= dt;
		if (timeRemainingWithoutAttract <= 0f)
		{
			if (Points.Count > 1)
			{
				num = Friction;
			}
			for (int i = 0; i < Points.Count; i++)
			{
				for (int j = 0; j < Points.Count; j++)
				{
					if (i != j)
					{
						StarPoint pA = Points[i];
						StarPoint pB = Points[j];
						if (pB.AttractedToOthers)
						{
							Points[j] = ProcessAttractionBetweenPoints(pA, pB, dt);
						}
					}
				}
			}
		}
		StarPoint starPoint = default(StarPoint);
		starPoint.Pos = new Vector2(0.5f);
		starPoint.size = attractionToCentreMass;
		starPoint.Velocity = Vector2.Zero;
		StarPoint pA2 = starPoint;
		for (int k = 0; k < Points.Count; k++)
		{
			StarPoint starPoint2 = Points[k];
			if (starPoint2.drawnSize < starPoint2.size)
			{
				starPoint2.drawnSize = Math.Min(starPoint2.size, starPoint2.drawnSize + dt * 15f);
			}
			starPoint2.Pos += starPoint2.Velocity * dt;
			if (starPoint2.Pos.X > 1f || starPoint2.Pos.Y > 1f || starPoint2.Pos.X < 0f || starPoint2.Pos.Y < 0f)
			{
				starPoint2.Pos.X = Math.Max(0f, Math.Min(starPoint2.Pos.X, 1f));
				starPoint2.Pos.Y = Math.Max(0f, Math.Min(starPoint2.Pos.Y, 1f));
				starPoint2.Velocity *= -0.5f;
			}
			starPoint2.Velocity -= starPoint2.Velocity * (num * dt);
			Points[k] = starPoint2;
			if (Points.Count > 1)
			{
				Points[k] = ProcessAttractionBetweenPoints(pA2, starPoint2, dt, constantForce: true);
			}
		}
		ResolveMergesThisFrame();
		if (starSize > 0f)
		{
			starSize -= dt * 3f;
		}
	}

	private int ResolveMergesThisFrame()
	{
		if (timeRemainingWithoutAttract <= 0f)
		{
			for (int i = 0; i < Points.Count; i++)
			{
				for (int j = 0; j < Points.Count; j++)
				{
					if (i == j)
					{
						continue;
					}
					StarPoint starPoint = Points[i];
					StarPoint starPoint2 = Points[j];
					float num = Vector2.Distance(starPoint.Pos, starPoint2.Pos);
					bool flag = 1 == 0;
					if (num < absorbDistance * (1f + 0.2f * (starPoint.size + starPoint2.size)))
					{
						float num2 = starPoint.size / starPoint2.size;
						float drawnSize = ((starPoint.drawnSize > starPoint2.drawnSize) ? starPoint.drawnSize : starPoint2.drawnSize);
						StarPoint starPoint3 = default(StarPoint);
						starPoint3.Pos = ((starPoint.size > starPoint2.size) ? starPoint.Pos : starPoint2.Pos);
						starPoint3.Velocity = Vector2.Lerp(Vector2.Zero, starPoint.Velocity + starPoint2.Velocity, 0.3f);
						starPoint3.size = starPoint.size + starPoint2.size;
						starPoint3.drawnSize = drawnSize;
						StarPoint item = starPoint3;
						Points.RemoveAt(i);
						if (j > i)
						{
							Points.RemoveAt(j - 1);
						}
						else
						{
							Points.RemoveAt(j);
						}
						Points.Add(item);
						int num3 = 1;
						return num3 + ResolveMergesThisFrame();
					}
				}
			}
		}
		return 0;
	}

	public void Render(Rectangle dest, SpriteBatch sb)
	{
		Vector2 vector = new Vector2(dest.Width, dest.Height);
		Vector2 vector2 = new Vector2(dest.X, dest.Y);
		for (int i = 0; i < Points.Count; i++)
		{
			Vector2 vector3 = Points[i].Pos * vector + vector2;
			float drawnSize = Points[i].drawnSize;
			int num = (AllowDoubleLines ? Points.Count : (Points.Count / 2));
			for (int j = 0; j < num; j++)
			{
				if (j != i)
				{
					Vector2 vector4 = Points[j].Pos * vector + vector2;
					float drawnSize2 = Points[j].drawnSize;
					if (drawnSize2 + drawnSize > 20f)
					{
						Utils.drawLineAlt(sb, vector3, vector4, Vector2.Zero, OS.currentInstance.highlightColor * 0.6f, 0.7f, LineLengthPercentage, LineTexture);
					}
				}
			}
			drawnSize /= (float)CircleTex.Width;
			float num2 = Utils.SmoothStep(0f, 20f, Points[i].drawnSize);
			float num3 = 0.5f - num2;
			float num4 = num2 * 0.5f;
			num2 *= GlowScaleMod;
			Vector2 vector5 = new Vector2(num4 * 1f * num2, num4 * 3f * num2);
			Vector2 vector6 = new Vector2((float)ScanlinesBackground.Width / 2f, (float)ScanlinesBackground.Height / 2f);
			Vector2 origin = new Vector2((float)CircleTex.Width / 2f);
			Rectangle clipRectForSpritePos = Utils.getClipRectForSpritePos(dest, ScanlinesBackground, vector3 - vector6 * vector5, vector5);
			sb.Draw(ScanlinesBackground, vector3 + new Vector2((float)clipRectForSpritePos.X * vector5.X, (float)clipRectForSpritePos.Y * vector5.Y), clipRectForSpritePos, Utils.AddativeWhite * 0.05f, 0f, vector6, vector5, SpriteEffects.None, 0.7f);
			float num5 = (float)CircleTex.Width * drawnSize;
			if (num5 > (float)dest.Height)
			{
				drawnSize = (float)dest.Height / (float)CircleTex.Width;
			}
			sb.Draw(CircleTex, vector3, null, NodeColor, 0f, origin, new Vector2(drawnSize), SpriteEffects.None, 0.7f);
			if (starSize > 0f && Points[i].drawnSize > 30f)
			{
				Color color = Color.Lerp(Utils.makeColorAddative(OS.currentInstance.highlightColor), OS.currentInstance.highlightColor, 0.5f);
				sb.Draw(Star, vector3, null, color * (starSize / 4f), OS.currentInstance.timer * 0f, new Vector2(Star.Width / 2, Star.Height / 2), new Vector2(starSize * 3.2f, starSize * 1.2f), SpriteEffects.None, 0.8f);
				color = Utils.makeColorAddative(OS.currentInstance.highlightColor);
				sb.Draw(Star, vector3, null, color * (starSize / 4f), OS.currentInstance.timer * 0f, new Vector2(Star.Width / 2, Star.Height / 2), new Vector2(starSize * 2.7f, starSize * 1f), SpriteEffects.None, 0.8f);
			}
		}
	}
}
