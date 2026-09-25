using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public static class FlickeringTextEffect
{
	private static float internalTimer = 0f;

	private static RenderTarget2D LinedItemTarget = null;

	private static SpriteBatch LinedItemSB;

	public static void DrawFlickeringText(Rectangle dest, string text, float maxOffset, float rarity, SpriteFont font, object os_Obj, Color BaseCol)
	{
		OS os_Obj2 = null;
		if (os_Obj != null)
		{
			os_Obj2 = (OS)os_Obj;
		}
		else
		{
			internalTimer += 1f / 60f;
		}
		Color color = new Color(BaseCol.R, 0, 0, 0);
		Color color2 = new Color(0, BaseCol.G, 0, 0);
		Color color3 = new Color(0, 0, BaseCol.B, 0);
		TextItem.doFontLabelToSize(RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(1.3f, 12.3f, rarity, os_Obj2))), text, font, color);
		TextItem.doFontLabelToSize(RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(0.8f, 29f, rarity, os_Obj2))), text, font, color2);
		TextItem.doFontLabelToSize(RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(0.5f, -939.7f, rarity, os_Obj2))), text, font, color3);
	}

	public static void DrawLinedFlickeringText(Rectangle dest, string text, float maxOffset, float rarity, SpriteFont font, object os_Obj, Color BaseCol, int segmentHeight = 2)
	{
		GraphicsDevice graphicsDevice = GuiData.spriteBatch.GraphicsDevice;
		if (LinedItemTarget == null)
		{
			LinedItemTarget = new RenderTarget2D(graphicsDevice, 1920, 1080, mipMap: false, SurfaceFormat.Rgba64, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
			LinedItemSB = new SpriteBatch(graphicsDevice);
		}
		if (dest.Width > LinedItemTarget.Width || dest.Height > LinedItemTarget.Height)
		{
			throw new InvalidOperationException("Target area is too large for the supported rendertarget size!");
		}
		OS os_Obj2 = null;
		if (os_Obj != null)
		{
			os_Obj2 = (OS)os_Obj;
		}
		Rectangle dest2 = new Rectangle(0, 0, dest.Width, dest.Height);
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		graphicsDevice.SetRenderTarget(LinedItemTarget);
		graphicsDevice.Clear(Color.Transparent);
		SpriteBatch spriteBatch = GuiData.spriteBatch;
		LinedItemSB.Begin();
		GuiData.spriteBatch = LinedItemSB;
		DrawFlickeringText(dest2, text, maxOffset, rarity, font, os_Obj, BaseCol);
		LinedItemSB.End();
		GuiData.spriteBatch = spriteBatch;
		graphicsDevice.SetRenderTarget(currentRenderTarget);
		int num = (int)((float)dest.Height / 19f);
		int num2 = (int)((float)dest.Height / (float)(num + 1));
		float num3 = (float)dest.Height / (float)(num + 1);
		for (int i = 0; i < num + 1; i++)
		{
			Rectangle value = new Rectangle(0, (int)((float)i * (float)num2 - (float)segmentHeight), dest.Width, num2 - segmentHeight);
			Rectangle destinationRectangle = new Rectangle(dest.X + dest2.X, dest.Y + value.Y, value.Width, value.Height);
			GuiData.spriteBatch.Draw(LinedItemTarget, destinationRectangle, value, Color.White);
			Rectangle rectangle = new Rectangle(0, value.Y + value.Height, value.Width, segmentHeight);
			Rectangle destinationRectangle2 = rectangle;
			destinationRectangle2.X += dest.X;
			destinationRectangle2.Y += dest.Y;
			int num4 = (int)(maxOffset * 1.2f * GetOffsetForSinTime((float)(1.2999999523162842 + Math.Sin((double)i * 2.5)), 12.3f * (float)i, rarity, os_Obj2));
			destinationRectangle2.X += num4;
			GuiData.spriteBatch.Draw(LinedItemTarget, destinationRectangle2, rectangle, Color.White);
		}
	}

	public static void DrawFlickeringSprite(SpriteBatch sb, Rectangle dest, Texture2D texture, float maxOffset, float rarity, object os_Obj, Color BaseCol)
	{
		OS os_Obj2 = null;
		if (os_Obj != null)
		{
			os_Obj2 = (OS)os_Obj;
		}
		else
		{
			internalTimer += 1f / 60f;
		}
		Color color = new Color(BaseCol.R, 0, 0, 0);
		Color color2 = new Color(0, BaseCol.G, 0, 0);
		Color color3 = new Color(0, 0, BaseCol.B, 0);
		sb.Draw(texture, RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(1.3f, 12.3f, rarity, os_Obj2))), color);
		sb.Draw(texture, RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(0.8f, 29f, rarity, os_Obj2))), color2);
		sb.Draw(texture, RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(0.5f, -939.7f, rarity, os_Obj2))), color3);
	}

	public static void DrawFlickeringSpriteAltWeightings(SpriteBatch sb, Rectangle dest, Texture2D texture, float maxOffset, float rarity, object os_Obj, Color BaseCol)
	{
		OS os_Obj2 = null;
		if (os_Obj != null)
		{
			os_Obj2 = (OS)os_Obj;
		}
		else
		{
			internalTimer += 1f / 60f;
		}
		Color color = new Color(BaseCol.R, 0, 0, 0);
		Color color2 = new Color(0, BaseCol.G, 0, 0);
		Color color3 = new Color(0, 0, BaseCol.B, 0);
		sb.Draw(texture, RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(4f, 0f, rarity, os_Obj2))), color);
		sb.Draw(texture, RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(2f, 2f, rarity, os_Obj2))), color2);
		sb.Draw(texture, RectAddX(dest, (int)(maxOffset * GetOffsetForSinTime(0.5f, -4f, rarity, os_Obj2))), color3);
	}

	public static void DrawFlickeringSpriteFull(SpriteBatch sb, Vector2 pos, float rotation, Vector2 scale, Vector2 origin, Texture2D texture, float timerOffset, float maxOffset, float rarity, object os_Obj, Color BaseCol)
	{
		OS os_Obj2 = null;
		if (os_Obj != null)
		{
			os_Obj2 = (OS)os_Obj;
		}
		else
		{
			internalTimer += 1f / 60f;
		}
		Color color = new Color(BaseCol.R, 0, 0, 0);
		Color color2 = new Color(0, BaseCol.G, 0, 0);
		Color color3 = new Color(0, 0, BaseCol.B, 0);
		sb.Draw(texture, VecAddX(pos, maxOffset * GetOffsetForSinTime(1.3f, 12.3f + timerOffset, rarity, os_Obj2)), null, color, rotation, origin, scale, SpriteEffects.None, 0.9f);
		sb.Draw(texture, VecAddX(pos, maxOffset * GetOffsetForSinTime(0.8f, 29f + timerOffset, rarity, os_Obj2)), null, color2, rotation, origin, scale, SpriteEffects.None, 0.9f);
		sb.Draw(texture, VecAddX(pos, maxOffset * GetOffsetForSinTime(0.5f, -939.7f + timerOffset, rarity, os_Obj2)), null, color3, rotation, origin, scale, SpriteEffects.None, 0.9f);
	}

	public static float GetOffsetForSinTime(float frequency, float offset, float rarity, object os_Obj)
	{
		float num = ((os_Obj != null) ? ((OS)os_Obj).timer : internalTimer);
		float num2 = (float)Math.Sin((num + offset) * frequency) - rarity;
		if (num2 < 0f)
		{
			num2 = 0f;
		}
		float num3 = 0.5f + Utils.QuadraticOutCurve(Utils.randm(num2));
		num3 = 2f * (num3 - 0.5f);
		return num3 - num2;
	}

	private static Vector2 VecAddX(Vector2 vec, float x)
	{
		return vec + new Vector2(x, 0f);
	}

	private static Rectangle RectAddX(Rectangle rect, int x)
	{
		rect.X += x;
		return rect;
	}

	public static string GetReportString()
	{
		return string.Concat("Target : ", LinedItemTarget, "\r\nTargetDisposed : ", LinedItemTarget.IsDisposed);
	}
}
