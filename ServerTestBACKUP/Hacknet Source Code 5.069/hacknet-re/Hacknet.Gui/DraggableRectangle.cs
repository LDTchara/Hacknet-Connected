using System;
using Microsoft.Xna.Framework;

namespace Hacknet.Gui;

public static class DraggableRectangle
{
	public static bool isDragging = false;

	public static Vector2 originalClickPos;

	public static Vector2 originalClickOffset;

	public static Vector2 doDraggableRectangle(int myID, float x, float y, int width, int height)
	{
		return doDraggableRectangle(myID, x, y, width, height, -1f, null, null);
	}

	public static Vector2 doDraggableRectangle(int myID, float x, float y, int width, int height, float selectableBorder, Color? selectedColor, Color? deselectedColor)
	{
		return doDraggableRectangle(myID, x, y, width, height, selectableBorder, selectedColor, deselectedColor, canMoveY: true, canMoveX: true, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
	}

	public static Vector2 doDraggableRectangle(int myID, float x, float y, int width, int height, float selectableBorder, Color? selectedColor, Color? deselectedColor, bool canMoveY, bool canMoveX, float xMax, float yMax, float xMin, float yMin)
	{
		isDragging = false;
		if (!selectedColor.HasValue)
		{
			selectedColor = GuiData.Default_Selected_Color;
		}
		if (!deselectedColor.HasValue)
		{
			deselectedColor = GuiData.Default_Unselected_Color;
		}
		Vector2 temp = GuiData.temp;
		temp.X = 0f;
		temp.Y = 0f;
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = (int)x;
		tmpRect.Y = (int)y;
		tmpRect.Width = width;
		tmpRect.Height = height;
		Rectangle tmpRect2 = GuiData.tmpRect;
		tmpRect2.X = (int)(x + selectableBorder);
		tmpRect2.Y = (int)(y + selectableBorder);
		tmpRect2.Width = ((selectableBorder != -1f) ? ((int)((float)width - 2f * selectableBorder)) : 0);
		tmpRect2.Height = ((selectableBorder != -1f) ? ((int)((float)height - 2f * selectableBorder)) : 0);
		if (tmpRect.Contains(GuiData.getMousePoint()) && !tmpRect2.Contains(GuiData.getMousePoint()))
		{
			GuiData.hot = myID;
			if (GuiData.active != myID && GuiData.mouseWasPressed())
			{
				GuiData.active = myID;
				originalClickPos = GuiData.getMousePos();
				originalClickPos.X -= x;
				originalClickPos.Y -= y;
				originalClickOffset = new Vector2(originalClickPos.X - x, originalClickPos.Y - y);
			}
		}
		else if (GuiData.hot == myID)
		{
			GuiData.hot = -1;
		}
		if (GuiData.active == myID)
		{
			if (GuiData.mouseLeftUp())
			{
				GuiData.active = -1;
			}
			else
			{
				if (canMoveX)
				{
					temp.X = (float)GuiData.mouse.X - x - originalClickPos.X;
					temp.X = Math.Min(Math.Max((float)tmpRect.X + temp.X, xMin), xMax) - (float)tmpRect.X;
				}
				if (canMoveY)
				{
					temp.Y = (float)GuiData.mouse.Y - y - originalClickPos.Y;
					temp.Y = Math.Min(Math.Max((float)tmpRect.Y + temp.Y, yMin), yMax) - (float)tmpRect.Y;
				}
				tmpRect.X += (int)temp.X;
				tmpRect.Y += (int)temp.Y;
				isDragging = true;
			}
		}
		if (GuiData.active == myID || GuiData.hot == myID)
		{
			GuiData.blockingInput = true;
		}
		GuiData.spriteBatch.Draw(Utils.white, tmpRect, (GuiData.hot == myID || GuiData.active == myID) ? selectedColor.Value : deselectedColor.Value);
		return temp;
	}
}
