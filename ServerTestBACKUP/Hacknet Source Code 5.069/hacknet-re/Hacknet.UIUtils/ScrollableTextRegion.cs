using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.UIUtils;

public class ScrollableTextRegion
{
	private ScrollableSectionedPanel Panel;

	private string activeFontConfigName = null;

	public ScrollableTextRegion(GraphicsDevice gd)
	{
		Panel = new ScrollableSectionedPanel((int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 2f), gd);
		activeFontConfigName = GuiData.ActiveFontConfig.name;
	}

	public void Draw(Rectangle dest, string text, SpriteBatch sb)
	{
		Draw(dest, text, sb, Color.White);
	}

	public void Draw(Rectangle dest, string text, SpriteBatch sb, Color TextDrawColor)
	{
		try
		{
			if (GuiData.ActiveFontConfig.name != activeFontConfigName)
			{
				Panel.ScrollDown = 0f;
				Panel.PanelHeight = (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 2f + (float)GuiData.ActiveFontConfig.tinyFont.LineSpacing);
				activeFontConfigName = GuiData.ActiveFontConfig.name;
			}
			string[] data = text.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
			Panel.NumberOfPanels = data.Length;
			Panel.Draw(delegate(int index, Rectangle panelDest, SpriteBatch spriteBatch)
			{
				spriteBatch.DrawString(GuiData.tinyfont, data[index], Utils.ClipVec2ForTextRendering(new Vector2(panelDest.X, panelDest.Y)), TextDrawColor);
			}, sb, dest);
		}
		catch (Exception)
		{
			TextItem.doFontLabelToSize(dest, text, GuiData.tinyfont, TextDrawColor);
		}
	}

	public void Draw(Rectangle dest, List<string> textLines, SpriteBatch sb, Color TextDrawColor)
	{
		if (GuiData.ActiveFontConfig.name != activeFontConfigName)
		{
			Panel.ScrollDown = 0f;
			Panel.PanelHeight = (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 2f);
			activeFontConfigName = GuiData.ActiveFontConfig.name;
		}
		Panel.NumberOfPanels = textLines.Count;
		Panel.Draw(delegate(int index, Rectangle panelDest, SpriteBatch spriteBatch)
		{
			spriteBatch.DrawString(GuiData.tinyfont, textLines[index], Utils.ClipVec2ForTextRendering(new Vector2(panelDest.X, panelDest.Y)), TextDrawColor);
		}, sb, dest);
	}

	public void UpdateScroll(float newScroll)
	{
		Panel.ScrollDown = newScroll;
	}

	public float GetScrollDown()
	{
		return Panel.ScrollDown;
	}

	public void SetScrollbarUIIndexOffset(int index)
	{
		Panel.ScrollbarUIIndexOffset = index;
	}
}
