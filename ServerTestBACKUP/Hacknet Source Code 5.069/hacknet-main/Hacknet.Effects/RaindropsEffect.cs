using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class RaindropsEffect
{
	private List<Vector3> Drops = new List<Vector3>();

	private List<Vector3> Circles = new List<Vector3>();

	private List<Vector3> FadeoutLines = new List<Vector3>();

	public float FallRate = 1f;

	public float CircleExpandRate = 0.4f;

	public float LineFadeoutRate = 2f;

	public float MaxVerticalLandingVariane = 0.025f;

	private Texture2D Circle;

	private Texture2D Gradient;

	private Texture2D FlashImage;

	public void Init(ContentManager content)
	{
		Circle = content.Load<Texture2D>("CircleOutlineLarge");
		Gradient = content.Load<Texture2D>("Gradient");
		FlashImage = content.Load<Texture2D>("EffectFiles/ScanlinesTextBackground");
	}

	public void ForceSpawnDrop(Vector3 dropData)
	{
		Drops.Add(dropData);
	}

	public void Update(float dt, float dropsAddedPerSecond)
	{
		if (Utils.randm(1f) < dropsAddedPerSecond * dt)
		{
			Drops.Add(new Vector3(Utils.randm(1f), 0f, Utils.rand(MaxVerticalLandingVariane)));
		}
		for (int i = 0; i < Drops.Count; i++)
		{
			float num = dt * FallRate * (1f + Drops[i].Z / 2f);
			Drops[i] = new Vector3(Drops[i].X, Drops[i].Y + num, Drops[i].Z);
			if (Drops[i].Y >= 1f + Drops[i].Z)
			{
				float x = Drops[i].X;
				float z = Drops[i].Z;
				Drops.RemoveAt(i);
				Circles.Add(new Vector3(x, 0f, z));
				FadeoutLines.Add(new Vector3(x, 0f, z));
				i--;
			}
		}
		for (int i = 0; i < Circles.Count; i++)
		{
			Circles[i] = new Vector3(Circles[i].X, Circles[i].Y + dt * CircleExpandRate, Circles[i].Z);
			if (Circles[i].Y >= 1f)
			{
				Circles.RemoveAt(i);
				i--;
			}
		}
		for (int i = 0; i < FadeoutLines.Count; i++)
		{
			FadeoutLines[i] = new Vector3(FadeoutLines[i].X, FadeoutLines[i].Y + dt * LineFadeoutRate, FadeoutLines[i].Z);
			if (FadeoutLines[i].Y >= 1f)
			{
				FadeoutLines.RemoveAt(i);
				i--;
			}
		}
	}

	public void Render(Rectangle dest, SpriteBatch sb, Color DropColor, float maxCircleRadius, float maxFlashWidth)
	{
		int num = 3;
		int num2 = 4;
		float num3 = 0.3f;
		float num4 = 0.2f;
		Color color = Utils.makeColorAddative(DropColor);
		float num5 = (float)dest.Height * 0.84f;
		for (int i = 0; i < FadeoutLines.Count; i++)
		{
			Vector3 vector = FadeoutLines[i];
			sb.Draw(destinationRectangle: new Rectangle((int)((float)dest.X + vector.X * (float)dest.Width - (float)num / 2f), dest.Y, num, (int)(num5 + vector.Z * (float)dest.Height)), texture: Utils.white, color: DropColor * num4 * (1f - vector.Y));
		}
		for (int i = 0; i < Drops.Count; i++)
		{
			Vector3 vector2 = Drops[i];
			Rectangle destinationRectangle2 = new Rectangle((int)((float)dest.X + vector2.X * (float)dest.Width - (float)num / 2f), dest.Y, num, (int)(vector2.Y * (num5 + vector2.Z * (float)dest.Height)));
			sb.Draw(Utils.white, destinationRectangle2, DropColor * num3);
			Rectangle destinationRectangle3 = new Rectangle(destinationRectangle2.X, destinationRectangle2.Y + destinationRectangle2.Height - num2, destinationRectangle2.Width, num2);
			sb.Draw(Utils.gradient, destinationRectangle3, color);
			sb.Draw(Utils.gradient, destinationRectangle3, Utils.AddativeWhite * 0.5f);
		}
		Vector2 vector3 = new Vector2(4f, 1f);
		for (int i = 0; i < Circles.Count; i++)
		{
			Vector3 vector4 = Circles[i];
			Vector2 origin = new Vector2(Circle.Width / 2, Circle.Height / 2);
			Vector2 scale = vector3 * vector4.Y * (maxCircleRadius / (float)Circle.Width);
			Vector2 vector5 = new Vector2((float)dest.X + vector4.X * (float)dest.Width, num5 + (float)dest.Y + vector4.Z * (float)dest.Height);
			Rectangle? sourceRectangle = Utils.getClipRectForSpritePos(dest, Circle, vector5, scale, origin);
			Vector2 vector6 = new Vector2((float)sourceRectangle.Value.X * scale.X, (float)sourceRectangle.Value.Y * scale.Y);
			Color color2 = Color.Lerp(Utils.AddativeWhite, Color.Transparent, (float)Math.Min(1.0, (double)vector4.Y * 2.0));
			float num6 = 1f;
			float num7 = 0.6f;
			if (vector4.Y >= num7)
			{
				float num8 = 1f / (1f - num7);
				num6 = 1f - (vector4.Y - num7) * num8;
			}
			num6 *= 0.2f;
			Color color3 = DropColor * num6;
			float num9 = 0.55f;
			if (vector4.Y < num9)
			{
				float point = vector4.Y / num9;
				point = Utils.QuadraticOutCurve(point);
				Vector2 vector7 = new Vector2(1f, 3f);
				Vector2 vector8 = new Vector2((float)FlashImage.Width * vector7.X, (float)FlashImage.Height * vector7.Y);
				vector8 = new Vector2(maxFlashWidth / vector8.X, maxFlashWidth / vector8.Y);
				vector8 *= 0.3f + point / 3f * 2f;
				Vector2 centreOrigin = FlashImage.GetCentreOrigin();
				Rectangle clipRectForSpritePos = Utils.getClipRectForSpritePos(dest, FlashImage, vector5, vector8, centreOrigin);
				sb.Draw(position: vector5 + new Vector2((float)clipRectForSpritePos.X * vector8.X, (float)clipRectForSpritePos.Y * vector8.Y), texture: FlashImage, sourceRectangle: clipRectForSpritePos, color: color * (1f - point), rotation: 0f, origin: centreOrigin, scale: vector8, effects: SpriteEffects.None, layerDepth: 0.5f);
				if (point < 0.5f)
				{
					sb.Draw(FlashImage, vector5, null, Utils.AddativeWhite * (1f - point * 2f), 0f, centreOrigin, vector8 * 0.6f, SpriteEffects.None, 0.5f);
				}
			}
			sb.Draw(Circle, vector5 + vector6, sourceRectangle, color3, 0f, origin, scale, SpriteEffects.None, 0.5f);
			sb.Draw(Circle, vector5 + vector6, sourceRectangle, color2, 0f, origin, scale, SpriteEffects.None, 0.5f);
		}
	}
}
