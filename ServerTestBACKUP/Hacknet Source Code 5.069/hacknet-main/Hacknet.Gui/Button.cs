using System;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hacknet.Gui;

public static class Button
{
	public const int BORDER_WIDTH = 1;

	public static bool wasPressedDown = false;

	public static bool wasReleased = false;

	public static bool drawingOutline = true;

	public static bool outlineOnly = false;

	public static bool smallButtonDraw = false;

	public static bool DisableIfAnotherIsActive = false;

	public static bool ForceNoColorTag = false;

	public static bool doButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor)
	{
		return doButton(myID, x, y, width, height, text, selectedColor, Utils.white);
	}

	public static bool doButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor, Texture2D tex)
	{
		bool result = false;
		if (GuiData.hot == myID && !GuiData.blockingInput && GuiData.active == myID && (GuiData.mouseLeftUp() || GuiData.mouse.LeftButton == ButtonState.Released))
		{
			result = true;
			GuiData.active = -1;
		}
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = height;
		if (tmpRect.Contains(GuiData.getMousePoint()) && !GuiData.blockingInput)
		{
			GuiData.hot = myID;
			if (GuiData.isMouseLeftDown() && (!DisableIfAnotherIsActive || GuiData.active == -1))
			{
				GuiData.active = myID;
			}
		}
		else
		{
			if (GuiData.hot == myID)
			{
				GuiData.hot = -1;
			}
			if (GuiData.isMouseLeftDown() && GuiData.active == myID && GuiData.active == myID)
			{
				GuiData.active = -1;
			}
		}
		drawModernButton(myID, x, y, width, height, text, selectedColor, tex);
		return result;
	}

	private static void drawButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor, Texture2D tex)
	{
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = height;
		tmpRect.X--;
		tmpRect.Width += 2;
		tmpRect.Y--;
		tmpRect.Height += 2;
		if (outlineOnly)
		{
			RenderedRectangle.doRectangleOutline(tmpRect.X, tmpRect.Y, tmpRect.Width, tmpRect.Height, 1, (GuiData.hot != myID) ? GuiData.Default_Backing_Color : ((GuiData.active == myID) ? GuiData.Default_Selected_Color : GuiData.Default_Lit_Backing_Color));
		}
		else if (drawingOutline)
		{
			GuiData.spriteBatch.Draw(tex, tmpRect, (GuiData.hot == myID) ? GuiData.Default_Lit_Backing_Color : GuiData.Default_Backing_Color);
		}
		tmpRect.X++;
		tmpRect.Width -= 2;
		tmpRect.Y++;
		tmpRect.Height -= 2;
		if (!outlineOnly)
		{
			GuiData.spriteBatch.Draw(tex, tmpRect, (GuiData.active == myID) ? GuiData.Default_Unselected_Color : selectedColor.Value);
		}
		Vector2 scale = GuiData.tinyfont.MeasureString(text);
		if (scale.X > (float)(width - 4))
		{
			scale.X = (float)(width - 4) / scale.X;
		}
		else
		{
			scale.X = 1f;
		}
		if (scale.Y > (float)(height - 4))
		{
			scale.Y = (float)(height - 2) / scale.Y;
		}
		else
		{
			scale.Y = 1f;
		}
		scale.X = Math.Min(scale.X, scale.Y);
		scale.Y = scale.X;
		GuiData.spriteBatch.DrawString(GuiData.tinyfont, text, new Vector2(x + 2 + 1, y + 1 + 1), Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
		GuiData.spriteBatch.DrawString(GuiData.tinyfont, text, new Vector2(x + 2, y + 1), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
	}

	private static void drawModernButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor, Texture2D tex)
	{
		int num = ((!ForceNoColorTag && width > 65) ? 13 : 0);
		if (!selectedColor.HasValue)
		{
			selectedColor = GuiData.Default_Trans_Grey_Strong;
		}
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = height;
		if (tex.Equals(Utils.white))
		{
			if (!outlineOnly)
			{
				GuiData.spriteBatch.Draw(Utils.white, tmpRect, (GuiData.hot != myID) ? GuiData.Default_Trans_Grey : ((GuiData.active == myID) ? GuiData.Default_Trans_Grey_Dark : GuiData.Default_Trans_Grey_Bright));
				tmpRect.Width = num;
				GuiData.spriteBatch.Draw(Utils.white, tmpRect, selectedColor.Value);
			}
			RenderedRectangle.doRectangleOutline(x, y, width, height, 1, outlineOnly ? selectedColor : new Color?(GuiData.Default_Trans_Grey_Solid));
		}
		else
		{
			GuiData.spriteBatch.Draw(tex, tmpRect, (GuiData.hot != myID) ? selectedColor.Value : ((GuiData.active == myID) ? GuiData.Default_Unselected_Color : GuiData.Default_Lit_Backing_Color));
		}
		SpriteFont spriteFont = (smallButtonDraw ? GuiData.detailfont : GuiData.tinyfont);
		Vector2 scale = spriteFont.MeasureString(text);
		float num2 = (LocaleActivator.ActiveLocaleIsCJK() ? 4f : 0f);
		float y2 = scale.Y;
		if (scale.X > (float)(width - 4))
		{
			scale.X = (float)(width - (4 + num + 5)) / scale.X;
		}
		else
		{
			scale.X = 1f;
		}
		if (scale.Y > (float)height + num2 - 0f)
		{
			scale.Y = ((float)height + num2 - 0f) / scale.Y;
		}
		else
		{
			scale.Y = 1f;
		}
		scale.X = Math.Min(scale.X, scale.Y);
		scale.Y = scale.X;
		if (Utils.FloatEquals(1f, scale.Y))
		{
			scale = Vector2.One;
		}
		num += 4;
		float num3 = y2 * scale.Y;
		float num4 = (float)y + (float)height / 2f - num3 / 2f + 1f - num2 * scale.Y / 2f;
		GuiData.spriteBatch.DrawString(spriteFont, text, new Vector2((int)((double)(x + 2 + num) + 0.5), (int)((double)num4 + 0.5)), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
	}

	public static bool doHoldDownButton(int myID, int x, int y, int width, int height, string text, bool hasOutline, Color? outlineColor, Color? selectedColor)
	{
		wasPressedDown = false;
		wasReleased = false;
		if (!outlineColor.HasValue)
		{
			outlineColor = Color.White;
		}
		if (!selectedColor.HasValue)
		{
			selectedColor = GuiData.Default_Selected_Color;
		}
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = height;
		bool result = GuiData.isMouseLeftDown() && GuiData.active == myID;
		if (tmpRect.Contains(GuiData.getMousePoint()))
		{
			GuiData.hot = myID;
			if (GuiData.mouseWasPressed())
			{
				wasPressedDown = true;
			}
			if (GuiData.isMouseLeftDown())
			{
				GuiData.active = myID;
				result = true;
			}
		}
		else
		{
			if (GuiData.hot == myID)
			{
				GuiData.hot = -1;
			}
			if (!GuiData.isMouseLeftDown() && GuiData.active == myID && GuiData.active == myID)
			{
				GuiData.active = -1;
			}
		}
		if (GuiData.mouseLeftUp())
		{
			wasReleased = true;
		}
		GuiData.spriteBatch.Draw(Utils.white, tmpRect, (GuiData.active == myID) ? selectedColor.Value : ((GuiData.hot == myID) ? GuiData.Default_Lit_Backing_Color : GuiData.Default_Light_Backing_Color));
		if (hasOutline)
		{
			RenderedRectangle.doRectangleOutline(x, y, width, height, 2, outlineColor);
		}
		return result;
	}
}
