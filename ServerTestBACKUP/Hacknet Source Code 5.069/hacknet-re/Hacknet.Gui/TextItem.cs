using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Gui;

public static class TextItem
{
	public static bool DrawShadow = false;

	public static Vector2 doMeasuredLabel(Vector2 pos, string text, Color? color)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		Vector2 result = GuiData.font.MeasureString(text);
		GuiData.spriteBatch.DrawString(GuiData.font, text, pos + Vector2.One, Color.Gray);
		GuiData.spriteBatch.DrawString(GuiData.font, text, pos, color.Value);
		return result;
	}

	public static void doLabel(Vector2 pos, string text, Color? color)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		GuiData.spriteBatch.DrawString(GuiData.font, text, pos, color.Value);
	}

	public static void doLabel(Vector2 pos, string text, Color? color, float MaxWidth)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		doFontLabel(pos, text, GuiData.font, color, MaxWidth);
	}

	public static void doFontLabelToSize(Rectangle dest, string text, SpriteFont font, Color color, bool doNotOversize = false, bool offsetToTopLeft = false)
	{
		Vector2 vector = font.MeasureString(text);
		Vector2 scale = new Vector2((float)dest.Width / vector.X, (float)dest.Height / vector.Y);
		float num = Math.Min(scale.X, scale.Y);
		Vector2 zero = Vector2.Zero;
		if (scale.X > num)
		{
			zero.X = vector.X * (scale.X - num) / 2f;
			scale.X = num;
		}
		if (scale.Y > num)
		{
			zero.Y = vector.Y * (scale.Y - num) / 2f;
			scale.Y = num;
		}
		if (doNotOversize)
		{
			scale.X = Math.Min(scale.X, 1f);
			scale.Y = Math.Min(scale.Y, 1f);
		}
		Vector2 input = new Vector2(dest.X, dest.Y);
		if (!offsetToTopLeft)
		{
			input += zero;
		}
		Vector2 position = Utils.ClipVec2ForTextRendering(input);
		GuiData.spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.55f);
	}

	public static void doCenteredFontLabel(Rectangle dest, string text, SpriteFont font, Color color, bool LockToLeft = false)
	{
		Vector2 vector = font.MeasureString(text);
		Vector2 vector2 = new Vector2((float)dest.Width / vector.X, (float)dest.Height / vector.Y);
		if (Math.Min(vector2.X, vector2.Y) < 1f)
		{
			doFontLabelToSize(dest, text, font, color, doNotOversize: true);
			return;
		}
		Vector2 position = Utils.ClipVec2ForTextRendering(new Vector2((float)dest.X + (float)dest.Width / 2f - vector.X / 2f, (float)dest.Y + (float)dest.Height / 2f - vector.Y / 2f));
		if (LockToLeft)
		{
			position.X = dest.X;
		}
		GuiData.spriteBatch.DrawString(font, text, position, color);
	}

	public static void doFontLabel(Vector2 pos, string text, SpriteFont font, Color? color, float widthTo = float.MaxValue, float heightTo = float.MaxValue, bool centreVertically = false)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		Vector2 scale = font.MeasureString(text);
		float y = scale.Y;
		if (scale.X > widthTo)
		{
			scale.X = widthTo / scale.X;
		}
		else
		{
			scale.X = 1f;
		}
		if (scale.Y > heightTo)
		{
			scale.Y = heightTo / scale.Y;
		}
		else
		{
			scale.Y = 1f;
		}
		scale.X = (scale.Y = Math.Min(scale.X, scale.Y));
		if (DrawShadow)
		{
			GuiData.spriteBatch.DrawString(font, text, pos + Vector2.One, Color.Gray, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
		}
		Vector2 vector = Vector2.Zero;
		if (centreVertically && heightTo < float.MaxValue)
		{
			float y2 = (heightTo - y * scale.Y) / 2f;
			vector = new Vector2(0f, y2);
		}
		Vector2 position = Utils.ClipVec2ForTextRendering(pos + vector);
		GuiData.spriteBatch.DrawString(font, text, position, color.Value, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
	}

	public static void doRightAlignedBackingLabel(Rectangle dest, string msg, SpriteFont font, Color back, Color front)
	{
		GuiData.spriteBatch.Draw(Utils.white, dest, back);
		Vector2 vector = font.MeasureString(msg);
		Vector2 position = new Vector2((float)(dest.X + dest.Width - 4) - vector.X, (float)(dest.Y + dest.Height - 2) - vector.Y);
		GuiData.spriteBatch.DrawString(font, msg, position, front);
	}

	public static void doRightAlignedBackingLabelScaled(Rectangle dest, string msg, SpriteFont font, Color back, Color front)
	{
		GuiData.spriteBatch.Draw(Utils.white, dest, back);
		Vector2 vector = font.MeasureString(msg);
		Vector2[] stringScaleForSize = GetStringScaleForSize(font, msg, dest);
		Vector2 vector2 = stringScaleForSize[0];
		Vector2 vector3 = stringScaleForSize[1];
		Vector2 vector4 = vector * vector2;
		Vector2 position = new Vector2((float)(dest.X + dest.Width) - vector4.X, (float)(dest.Y + dest.Height / 2) - vector4.Y / 2f);
		GuiData.spriteBatch.DrawString(font, msg, position, front, 0f, Vector2.Zero, vector2, SpriteEffects.None, 0.6f);
	}

	public static void doRightAlignedBackingLabelFill(Rectangle dest, string msg, SpriteFont font, Color back, Color front)
	{
		GuiData.spriteBatch.Draw(Utils.white, dest, back);
		Vector2 vector = font.MeasureString(msg);
		Rectangle dest2 = dest;
		dest2.Width -= 7;
		dest2.X += 4;
		Vector2[] stringScaleForSize = GetStringScaleForSize(font, msg, dest2);
		Vector2 scale = stringScaleForSize[0];
		Vector2 vector2 = stringScaleForSize[1];
		vector2.Y *= 2f;
		vector2.Y -= 3f;
		Vector2 vector3 = new Vector2(dest2.X, dest.Y);
		GuiData.spriteBatch.DrawString(font, msg, vector3 + vector2, front, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.6f);
		Rectangle destinationRectangle = new Rectangle(dest.X, (int)((float)dest.Y + vector2.Y - 4f + vector.Y * scale.Y), dest.Width, 1);
		GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, front);
	}

	private static Vector2[] GetStringScaleForSize(SpriteFont font, string text, Rectangle dest)
	{
		Vector2 vector = font.MeasureString(text);
		Vector2 vector2 = new Vector2((float)dest.Width / vector.X, (float)dest.Height / vector.Y);
		float num = Math.Min(vector2.X, vector2.Y);
		Vector2 zero = Vector2.Zero;
		if (vector2.X > num)
		{
			zero.X = vector.X * (vector2.X - num) / 2f;
			vector2.X = num;
		}
		if (vector2.Y > num)
		{
			zero.Y = vector.Y * (vector2.Y - num) / 2f;
			vector2.Y = num;
		}
		return new Vector2[2] { vector2, zero };
	}

	public static Vector2 doMeasuredFontLabel(Vector2 pos, string text, SpriteFont font, Color? color, float widthTo = float.MaxValue, float heightTo = float.MaxValue)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		Vector2 vector = font.MeasureString(text);
		if (vector.X > widthTo)
		{
			vector.X = widthTo / vector.X;
		}
		else
		{
			vector.X = 1f;
		}
		if (vector.Y > heightTo)
		{
			vector.Y = heightTo / vector.Y;
		}
		else
		{
			vector.Y = 1f;
		}
		vector.X = (vector.Y = Math.Min(vector.X, vector.Y));
		if (DrawShadow)
		{
			GuiData.spriteBatch.DrawString(font, text, pos + Vector2.One, Color.Gray, 0f, Vector2.Zero, vector, SpriteEffects.None, 0.5f);
		}
		GuiData.spriteBatch.DrawString(font, text, pos, color.Value, 0f, Vector2.Zero, vector, SpriteEffects.None, 0.5f);
		return font.MeasureString(text) * vector;
	}

	public static void doSmallLabel(Vector2 pos, string text, Color? color)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		if (DrawShadow)
		{
			GuiData.spriteBatch.DrawString(GuiData.smallfont, text, pos + Vector2.One, Color.Gray);
		}
		GuiData.spriteBatch.DrawString(GuiData.smallfont, text, pos, color.Value);
	}

	public static void doTinyLabel(Vector2 pos, string text, Color? color)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		GuiData.spriteBatch.DrawString(GuiData.tinyfont, text, pos, color.Value);
	}

	public static void doSmallLabel(Vector2 pos, string text, Color? color, float widthTo, float heightTo)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		Vector2 scale = GuiData.font.MeasureString(text);
		if (scale.X > widthTo)
		{
			scale.X = widthTo / scale.X;
		}
		else
		{
			scale.X = 1f;
		}
		if (scale.Y > heightTo)
		{
			scale.Y = heightTo / scale.Y;
		}
		else
		{
			scale.Y = 1f;
		}
		if (DrawShadow)
		{
			GuiData.spriteBatch.DrawString(GuiData.smallfont, text, pos + Vector2.One, Color.Gray, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
		}
		GuiData.spriteBatch.DrawString(GuiData.smallfont, text, pos, color.Value, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
	}

	public static Vector2 doMeasuredSmallLabel(Vector2 pos, string text, Color? color)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		Vector2 result = GuiData.smallfont.MeasureString(text);
		if (DrawShadow)
		{
			GuiData.spriteBatch.DrawString(GuiData.smallfont, text, pos + Vector2.One, Color.Gray);
		}
		GuiData.spriteBatch.DrawString(GuiData.smallfont, text, pos, color.Value);
		return result;
	}

	public static Vector2 doMeasuredTinyLabel(Vector2 pos, string text, Color? color)
	{
		if (!color.HasValue)
		{
			color = Color.White;
		}
		Vector2 result = GuiData.tinyfont.MeasureString(text);
		if (DrawShadow)
		{
			GuiData.spriteBatch.DrawString(GuiData.tinyfont, text, pos + Vector2.One, Color.Gray);
		}
		GuiData.spriteBatch.DrawString(GuiData.tinyfont, text, pos, color.Value);
		return result;
	}
}
