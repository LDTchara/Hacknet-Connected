using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class ExplodingUIElementEffect
{
	private struct ExplosionParticle
	{
		public Vector2 Pos;

		public float angle;

		public float speed;

		public float rotation;

		public float rotationRate;

		public float fade;

		public int textureIndex;

		public float Size;

		public float frictionSlowdownPerSec;

		public float FlickerOffset;
	}

	private List<Texture2D> Textures = new List<Texture2D>();

	private List<ExplosionParticle> Particles = new List<ExplosionParticle>();

	public void Init(ContentManager content)
	{
		Textures.Add(Utils.white);
		Textures.Add(Utils.white);
	}

	public void Explode(int particles, Vector2 AngleRange, Vector2 startPos, float minScale, float maxScale, float minSpeed, float maxSpeed, float minFriction, float maxFriction, float minStartFade, float maxStartFade)
	{
		for (int i = 0; i < particles; i++)
		{
			Particles.Add(new ExplosionParticle
			{
				Pos = startPos,
				angle = AngleRange.X + Utils.randm(AngleRange.Y - AngleRange.X),
				fade = minStartFade + Utils.randm(maxStartFade - minStartFade),
				rotation = Utils.randm((float)Math.PI * 2f),
				rotationRate = Utils.randm(20f) - Utils.randm(20f),
				Size = minScale + Utils.randm(maxScale - minScale),
				speed = minSpeed + Utils.randm(maxSpeed - minSpeed),
				frictionSlowdownPerSec = minFriction + Utils.randm(minFriction - maxFriction),
				textureIndex = Utils.random.Next(Textures.Count),
				FlickerOffset = Utils.randm(50f)
			});
		}
	}

	public void Update(float dt)
	{
		for (int i = 0; i < Particles.Count; i++)
		{
			ExplosionParticle value = Particles[i];
			value.Pos += Utils.PolarToCartesian(value.angle, value.speed * dt);
			value.speed -= value.frictionSlowdownPerSec * dt;
			if (value.speed <= 0f)
			{
				value.speed = 0f;
			}
			value.rotation += value.rotationRate * dt;
			value.rotationRate -= value.frictionSlowdownPerSec * dt * 0.1f;
			if (value.rotationRate <= 0f)
			{
				value.rotationRate = 0f;
			}
			value.fade -= dt;
			if (value.fade <= 0f)
			{
				Particles.RemoveAt(i);
				i--;
			}
			else
			{
				Particles[i] = value;
			}
		}
	}

	public void Render(SpriteBatch sb)
	{
		for (int i = 0; i < Particles.Count; i++)
		{
			ExplosionParticle explosionParticle = Particles[i];
			Texture2D texture2D = Textures[explosionParticle.textureIndex];
			if (texture2D.IsDisposed)
			{
				texture2D = Utils.white;
			}
			Vector2 vector = new Vector2(explosionParticle.Size / (float)texture2D.Width, explosionParticle.Size / (float)texture2D.Width);
			vector = new Vector2(Math.Min(vector.X, vector.Y), Math.Min(vector.X, vector.Y));
			float num = 1f - Math.Min(1f, Math.Max(0f, explosionParticle.fade / 2f));
			FlickeringTextEffect.DrawFlickeringSpriteFull(sb, explosionParticle.Pos, explosionParticle.rotation, vector, texture2D.GetCentreOrigin(), texture2D, explosionParticle.FlickerOffset, explosionParticle.Size, 0f, OS.currentInstance, Utils.AddativeWhite * (explosionParticle.fade / 4f));
		}
	}
}
