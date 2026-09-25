using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class ConnectedNodeEffect
{
	private const int NUMBER_OF_SEGMENTS = 7;

	private const float MIN_DISTANCE = 18f;

	private const float MAX_DISTANCE = 30f;

	private OS os;

	private static List<Texture2D> textures;

	public Color color;

	public bool Intense = false;

	public float ScaleFactor = 1f;

	private int[] tex;

	private float[] distance;

	private float[] offset;

	private float[] timescale;

	private Vector2 origin;

	public ConnectedNodeEffect(OS os)
	{
		this.os = os;
		init();
	}

	public ConnectedNodeEffect(OS os, bool intense)
	{
		this.os = os;
		Intense = intense;
		init(intense);
	}

	private void init(bool intesne = false)
	{
		if (textures == null)
		{
			textures = new List<Texture2D>();
			textures.Add(os.content.Load<Texture2D>("rotator"));
			textures.Add(os.content.Load<Texture2D>("rotator2"));
			textures.Add(os.content.Load<Texture2D>("rotator3"));
			textures.Add(os.content.Load<Texture2D>("rotator4"));
			textures.Add(os.content.Load<Texture2D>("rotator5"));
		}
		origin = new Vector2(textures[0].Width / 2, textures[0].Height / 2);
		int num = ((!intesne) ? 1 : 2) * 7;
		tex = new int[num];
		distance = new float[num];
		offset = new float[num];
		timescale = new float[num];
		color = new Color(140, 12, 12, 50);
		reset();
	}

	public void reset()
	{
		float num = (Intense ? 1.5f : 1f);
		for (int i = 0; i < 7; i++)
		{
			tex[i] = Utils.random.Next(textures.Count - 1);
			distance[i] = (float)(Utils.random.NextDouble() * (double)(30f * num - 18f / num) + (double)(18f / num));
			offset[i] = (float)(Utils.random.NextDouble() * (Math.PI * 2.0));
			timescale[i] = (float)Utils.random.NextDouble();
		}
	}

	public void draw(SpriteBatch sb, Vector2 pos)
	{
		for (int i = 0; i < 7; i++)
		{
			Vector2 vector = origin + new Vector2(0f, distance[i]);
			float rotation = (float)((double)(os.timer * (timescale[i] * 0.2f) % 1f) * (Math.PI * 2.0) * (double)((i % 2 == 0) ? 1f : (-1f))) + offset[i];
			Vector2 scale = new Vector2(distance[i] / (30f * (Intense ? 1.5f : 1f)) * 0.7f + 0.3f, 1f);
			scale *= ScaleFactor;
			sb.Draw(textures[tex[i]], pos, null, color, rotation, vector, scale, SpriteEffects.None, 0.5f);
		}
	}
}
