using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Gui;

public static class ScrollablePanel
{
	private static Stack<RenderTarget2D> targets;

	private static Stack<SpriteBatch> batches;

	private static List<RenderTarget2D> targetPool;

	private static List<SpriteBatch> batchPool;

	private static Color scrollBarColor = new Color(120, 120, 120, 80);

	private static Stack<Vector2> offsetStack;

	public static void beginPanel(int id, Rectangle drawbounds, Vector2 scroll)
	{
		if (targets == null)
		{
			targets = new Stack<RenderTarget2D>();
			batches = new Stack<SpriteBatch>();
			targets.Push((RenderTarget2D)GuiData.spriteBatch.GraphicsDevice.GetRenderTargets()[0].RenderTarget);
			targetPool = new List<RenderTarget2D>();
			targetPool.Add(new RenderTarget2D(GuiData.spriteBatch.GraphicsDevice, drawbounds.Width, drawbounds.Height));
			batchPool = new List<SpriteBatch>();
			batchPool.Add(new SpriteBatch(GuiData.spriteBatch.GraphicsDevice));
			batches.Push(GuiData.spriteBatch);
			offsetStack = new Stack<Vector2>();
		}
		if (batchPool.Count <= 0)
		{
			batchPool.Add(new SpriteBatch(GuiData.spriteBatch.GraphicsDevice));
		}
		SpriteBatch item = batchPool[batchPool.Count - 1];
		batchPool.RemoveAt(batchPool.Count - 1);
		batches.Push(item);
		bool flag = false;
		for (int num = targetPool.Count - 1; num >= 0; num--)
		{
			RenderTarget2D renderTarget2D = targetPool[num];
			if (renderTarget2D.Width == drawbounds.Width && renderTarget2D.Height == drawbounds.Height)
			{
				targets.Push(renderTarget2D);
				targetPool.RemoveAt(num);
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			RenderTarget2D renderTarget2D = new RenderTarget2D(GuiData.spriteBatch.GraphicsDevice, drawbounds.Width, drawbounds.Height);
			targets.Push(renderTarget2D);
			Console.WriteLine("Creating RenderTarget");
		}
		offsetStack.Push(GuiData.scrollOffset);
		GuiData.scrollOffset = new Vector2((float)drawbounds.X - scroll.X, (float)drawbounds.Y - scroll.Y);
		GuiData.spriteBatch.GraphicsDevice.SetRenderTarget(targets.Peek());
		GuiData.spriteBatch.GraphicsDevice.Clear(Color.Transparent);
		batches.Peek().Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
		GuiData.spriteBatch = batches.Peek();
	}

	public static Vector2 endPanel(int id, Vector2 scroll, Rectangle bounds, float maxScroll, bool onlyScrollWithMouseOver = false)
	{
		batches.Peek().End();
		RenderTarget2D renderTarget2D = targets.Pop();
		GuiData.spriteBatch.GraphicsDevice.SetRenderTarget(targets.Peek());
		targetPool.Add(renderTarget2D);
		batchPool.Add(batches.Pop());
		GuiData.spriteBatch = batches.Peek();
		GuiData.scrollOffset = offsetStack.Pop();
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = (int)scroll.X;
		tmpRect.Y = (int)scroll.Y;
		tmpRect.Width = bounds.Width;
		tmpRect.Height = bounds.Height;
		try
		{
			GuiData.spriteBatch.Draw(renderTarget2D, bounds, tmpRect, Color.White);
		}
		catch (InvalidOperationException)
		{
			return scroll;
		}
		if (!onlyScrollWithMouseOver || bounds.Contains(GuiData.getMousePoint()))
		{
			scroll.Y += GuiData.getMouseWheelScroll() * 20f;
		}
		scroll.Y = Math.Max(Math.Min(scroll.Y, maxScroll), 0f);
		Rectangle tmpRect2 = GuiData.tmpRect;
		float num = 5f;
		float num2 = (float)bounds.Height / maxScroll;
		num2 *= (float)bounds.Height;
		float num3 = bounds.Height;
		num3 -= 4f;
		float num4 = scroll.Y / maxScroll;
		num4 *= (float)bounds.Height - num2;
		tmpRect2.Y = (int)(num4 - num2 / 2f + num2 / 2f + (float)bounds.Y);
		tmpRect2.X = (int)((double)(bounds.X + bounds.Width) - 1.5 * (double)num - 2.0);
		tmpRect2.Height = (int)num2;
		tmpRect2.Width = (int)num;
		scroll.Y = ScrollBar.doVerticalScrollBar(id, tmpRect2.X, bounds.Y, tmpRect2.Width, bounds.Height, renderTarget2D.Height, scroll.Y);
		scroll.Y = Math.Max(Math.Min(scroll.Y, maxScroll), 0f);
		return scroll;
	}

	public static void ClearCache()
	{
		targets = null;
	}
}
