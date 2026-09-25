using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class NodeBounceEffect
{
	public float TimeBetweenBounces = 0.07f;

	public float NodeHitDelay = 0.2f;

	private List<Vector2> locations = new List<Vector2>();

	private float timeToNextBounce = 0f;

	private float delayTillNextBounce = 0f;

	public int maxNodes = 200;

	public NodeBounceEffect()
	{
		locations.Add(new Vector2(Utils.rand(), Utils.rand()));
		locations.Add(new Vector2(Utils.rand(), Utils.rand()));
	}

	public void Update(float t, Action<Vector2> nodeHitAction = null)
	{
		timeToNextBounce -= t;
		if (!(timeToNextBounce <= 0f))
		{
			return;
		}
		if (delayTillNextBounce <= 0f)
		{
			nodeHitAction?.Invoke(locations[locations.Count - 1]);
			locations.Add(new Vector2(Utils.rand(), Utils.rand()));
			timeToNextBounce = TimeBetweenBounces;
			delayTillNextBounce = NodeHitDelay;
			while (locations.Count > maxNodes)
			{
				locations.RemoveAt(0);
			}
		}
		else
		{
			delayTillNextBounce -= t;
			timeToNextBounce = 0f;
		}
	}

	public void Draw(SpriteBatch spriteBatch, Rectangle bounds, Color lineColor, Color nodeColor)
	{
		Vector2 vector = locations[0];
		Vector2 vector2 = new Vector2((float)bounds.X + 2f, (float)bounds.Y + 26f);
		Vector2 vector3 = new Vector2((float)bounds.Width - 4f, (float)bounds.Height - 30f);
		if (!(vector3.X > 0f) || !(vector3.Y > 0f))
		{
			return;
		}
		for (int i = 1; i < locations.Count; i++)
		{
			Vector2 vector4 = locations[i];
			if (i == locations.Count - 1)
			{
				vector4 = Vector2.Lerp(vector, vector4, 1f - timeToNextBounce / TimeBetweenBounces);
			}
			Utils.drawLine(spriteBatch, vector2 + vector * vector3, vector2 + vector4 * vector3, Vector2.Zero, lineColor * ((float)i / (float)locations.Count), 0.4f);
			vector = locations[i];
		}
		for (int i = 1; i < locations.Count; i++)
		{
			spriteBatch.Draw(Utils.white, locations[i] * vector3 + vector2, nodeColor);
		}
	}
}
