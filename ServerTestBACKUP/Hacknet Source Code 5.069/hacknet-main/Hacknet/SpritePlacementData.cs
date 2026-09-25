using Microsoft.Xna.Framework;

namespace Hacknet;

public struct SpritePlacementData
{
	public Vector2 pos;

	public float depth;

	public Vector2 scale;

	public int spriteIndex;

	public SpritePlacementData(Vector2 position, Vector2 scales, float layerDepth)
	{
		pos = position;
		scale = scales;
		depth = layerDepth;
		spriteIndex = 0;
	}

	public SpritePlacementData(Vector2 position, Vector2 scales, float layerDepth, int SpriteIndex)
	{
		pos = position;
		scale = scales;
		depth = layerDepth;
		spriteIndex = SpriteIndex;
	}
}
