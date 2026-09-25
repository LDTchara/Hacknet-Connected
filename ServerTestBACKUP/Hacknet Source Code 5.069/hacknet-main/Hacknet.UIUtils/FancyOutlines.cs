using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.UIUtils;

public static class FancyOutlines
{
	public static void DrawCornerCutOutline(Rectangle bounds, SpriteBatch sb, float cornerCut, Color col)
	{
		Vector2 vector = new Vector2(bounds.X, (float)bounds.Y + cornerCut);
		Vector2 vector2 = new Vector2((float)bounds.X + cornerCut, bounds.Y);
		Utils.drawLine(sb, vector, vector2, Vector2.Zero, col, 0.6f);
		vector = new Vector2((float)(bounds.X + bounds.Width) - cornerCut, bounds.Y);
		Utils.drawLine(sb, vector2, vector, Vector2.Zero, col, 0.6f);
		vector2 = new Vector2(bounds.X + bounds.Width, (float)bounds.Y + cornerCut);
		Utils.drawLine(sb, vector2, vector, Vector2.Zero, col, 0.6f);
		vector = new Vector2(bounds.X + bounds.Width, (float)(bounds.Y + bounds.Height) - cornerCut);
		Utils.drawLine(sb, vector2, vector, Vector2.Zero, col, 0.6f);
		vector2 = new Vector2((float)(bounds.X + bounds.Width) - cornerCut, bounds.Y + bounds.Height);
		Utils.drawLine(sb, vector2, vector, Vector2.Zero, col, 0.6f);
		vector = new Vector2((float)bounds.X + cornerCut, bounds.Y + bounds.Height);
		Utils.drawLine(sb, vector2, vector, Vector2.Zero, col, 0.6f);
		vector2 = new Vector2(bounds.X, (float)(bounds.Y + bounds.Height) - cornerCut);
		Utils.drawLine(sb, vector2, vector, Vector2.Zero, col, 0.6f);
		vector = new Vector2(bounds.X, (float)bounds.Y + cornerCut);
		Utils.drawLine(sb, vector2, vector, Vector2.Zero, col, 0.6f);
	}
}
