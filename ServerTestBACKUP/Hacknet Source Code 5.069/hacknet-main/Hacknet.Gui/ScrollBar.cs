using System;
using Microsoft.Xna.Framework;

namespace Hacknet.Gui;

public static class ScrollBar
{
	public static bool AlwaysDrawUnderBar = false;

	public static float doVerticalScrollBar(int id, int xPos, int yPos, int drawWidth, int drawHeight, int contentHeight, float scroll)
	{
		if (drawHeight > contentHeight)
		{
			contentHeight = drawHeight;
		}
		Rectangle destinationRectangle = new Rectangle(xPos, yPos, drawWidth, drawHeight);
		if (AlwaysDrawUnderBar || destinationRectangle.Contains(GuiData.getMousePoint()) || GuiData.active == id)
		{
			GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, Color.Gray * 0.1f);
		}
		float result = scroll;
		float num = contentHeight - drawHeight;
		float num2 = (float)drawHeight / (float)contentHeight * (float)drawHeight;
		float num3 = scroll / num;
		float num4 = num3 * ((float)drawHeight - num2);
		float y = DraggableRectangle.doDraggableRectangle(id, xPos, (float)yPos + num4, drawWidth, (int)num2, drawWidth, Color.White, Color.Gray, canMoveY: true, canMoveX: false, xPos, (float)(yPos + drawHeight) - num2, xPos, yPos).Y;
		if (Math.Abs(y) > 0.1f)
		{
			result = (num4 + y) / ((float)drawHeight - num2) * num;
		}
		return result;
	}
}
