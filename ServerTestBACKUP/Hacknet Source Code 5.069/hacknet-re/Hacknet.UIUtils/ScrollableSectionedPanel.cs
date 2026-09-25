using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.UIUtils;

public class ScrollableSectionedPanel
{
	public int PanelHeight = 100;

	public float ScrollDown = 0f;

	public int NumberOfPanels = 0;

	private RenderTarget2D AboveFragment;

	private RenderTarget2D BelowFragment;

	private SpriteBatch fragmentBatch;

	private GraphicsDevice graphics;

	public int ScrollbarUIIndexOffset = 0;

	public bool HasScrollBar = true;

	public ScrollableSectionedPanel(int panelHeight, GraphicsDevice graphics)
	{
		PanelHeight = panelHeight;
		fragmentBatch = new SpriteBatch(graphics);
		fragmentBatch.Name = "AltRTBatch";
		this.graphics = graphics;
	}

	private void UpdateInput(Rectangle dest)
	{
		ScrollDown += GuiData.getMouseWheelScroll() * 20f;
		ScrollDown = Math.Max(Math.Min(ScrollDown, GetMaxScroll(dest)), 0f);
	}

	private float GetMaxScroll(Rectangle dest)
	{
		float num = NumberOfPanels * PanelHeight;
		return num - (float)dest.Height;
	}

	public void Draw(Action<int, Rectangle, SpriteBatch> DrawSection, SpriteBatch sb, Rectangle destination)
	{
		UpdateInput(destination);
		SetRenderTargetsToFrame(destination);
		bool flag = destination.Contains(GuiData.getMousePoint());
		Vector2 scrollOffset = GuiData.scrollOffset;
		Vector2 scrollOffset2 = new Vector2(destination.X, (float)destination.Y - ScrollDown % (float)PanelHeight);
		if (flag)
		{
			GuiData.scrollOffset = scrollOffset2;
		}
		SpriteBatch spriteBatch = GuiData.spriteBatch;
		int num = 0;
		float num2 = ScrollDown;
		while (num2 >= (float)PanelHeight)
		{
			num2 -= (float)PanelHeight;
			num++;
		}
		int num3 = 0;
		if (ScrollDown % (float)PanelHeight != 0f)
		{
			GuiData.spriteBatch = fragmentBatch;
			RenderToTarget(DrawSection, AboveFragment, num, new Rectangle(0, 0, destination.Width, PanelHeight));
			int num4 = PanelHeight - (int)(ScrollDown % (float)PanelHeight);
			Rectangle destinationRectangle = new Rectangle(destination.X, destination.Y, destination.Width, num4);
			sb.Draw(sourceRectangle: new Rectangle(0, PanelHeight - destinationRectangle.Height, destination.Width, num4), texture: AboveFragment, destinationRectangle: destinationRectangle, color: Color.White);
			num3 += num4;
			num++;
		}
		GuiData.spriteBatch = spriteBatch;
		if (flag)
		{
			GuiData.scrollOffset = scrollOffset;
		}
		int num5 = num;
		Rectangle arg = new Rectangle(destination.X, destination.Y + num3, destination.Width, PanelHeight);
		while (num3 + PanelHeight < destination.Height && num5 < NumberOfPanels)
		{
			DrawSection(num5, arg, sb);
			num5++;
			num3 += PanelHeight;
			arg.Y = destination.Y + num3;
		}
		scrollOffset2 = new Vector2(destination.X, arg.Y);
		if (flag)
		{
			GuiData.scrollOffset = scrollOffset2;
		}
		if (num5 < NumberOfPanels && destination.Height - num3 > 0)
		{
			GuiData.spriteBatch = fragmentBatch;
			RenderToTarget(DrawSection, BelowFragment, num5, new Rectangle(0, 0, destination.Width, PanelHeight));
			int num6 = destination.Height - num3;
			sb.Draw(destinationRectangle: new Rectangle(destination.X, arg.Y, destination.Width, num6), sourceRectangle: new Rectangle(0, 0, destination.Width, num6), texture: BelowFragment, color: Color.White);
			num3 += num6;
			num++;
		}
		GuiData.spriteBatch = spriteBatch;
		if (flag)
		{
			GuiData.scrollOffset = scrollOffset;
		}
		if (HasScrollBar)
		{
			int num7 = 7;
			DrawScrollBar(new Rectangle(destination.X + destination.Width - num7 - 2, destination.Y, num7, destination.Height - 1), num7);
		}
	}

	private void DrawScrollBar(Rectangle dest, int width)
	{
		ScrollDown = ScrollBar.doVerticalScrollBar(184602004 + ScrollbarUIIndexOffset, dest.X, dest.Y, dest.Width, dest.Height, NumberOfPanels * PanelHeight, ScrollDown);
	}

	private void RenderToTarget(Action<int, Rectangle, SpriteBatch> DrawSection, RenderTarget2D target, int index, Rectangle destination)
	{
		RenderTarget2D renderTarget = (RenderTarget2D)graphics.GetRenderTargets()[0].RenderTarget;
		graphics.SetRenderTarget(target);
		graphics.Clear(Color.Transparent);
		fragmentBatch.Begin();
		DrawSection(index, destination, fragmentBatch);
		fragmentBatch.End();
		graphics.SetRenderTarget(renderTarget);
	}

	private void SetRenderTargetsToFrame(Rectangle frame)
	{
		if (AboveFragment == null || AboveFragment.Width != frame.Width)
		{
			AboveFragment = new RenderTarget2D(graphics, frame.Width, PanelHeight);
			BelowFragment = new RenderTarget2D(graphics, frame.Width, PanelHeight);
		}
	}
}
