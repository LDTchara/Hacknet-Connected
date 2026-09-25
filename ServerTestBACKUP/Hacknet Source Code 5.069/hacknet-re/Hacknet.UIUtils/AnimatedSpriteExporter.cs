using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.UIUtils;

public static class AnimatedSpriteExporter
{
	public static void ExportAnimation(string folderPath, string nameStarter, int width, int height, float framesPerSecond, float totalTime, GraphicsDevice gd, Action<float> update, Action<SpriteBatch, Rectangle> draw, int antialiasingMultiplier = 1)
	{
		int width2 = width * antialiasingMultiplier;
		int height2 = height * antialiasingMultiplier;
		RenderTarget2D renderTarget2D = new RenderTarget2D(gd, width2, height2, mipMap: false, SurfaceFormat.Rgba64, DepthFormat.Depth16, 8, RenderTargetUsage.PlatformContents);
		SpriteBatch spriteBatch = new SpriteBatch(gd);
		gd.PresentationParameters.MultiSampleCount = 8;
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		gd.SetRenderTarget(renderTarget2D);
		float num = 1f / framesPerSecond;
		float num2 = 0f;
		int num3 = 0;
		if (!Directory.Exists(folderPath))
		{
			Directory.CreateDirectory(folderPath);
		}
		SpriteBatch spriteBatch2 = GuiData.spriteBatch;
		GuiData.spriteBatch = spriteBatch;
		Rectangle arg = new Rectangle(0, 0, width2, height2);
		for (; num2 < totalTime; num2 += num)
		{
			gd.Clear(Color.Transparent);
			spriteBatch.Begin();
			draw(spriteBatch, arg);
			spriteBatch.End();
			update(num);
			gd.SetRenderTarget(null);
			string text = nameStarter + "_" + num3 + ".png";
			using (FileStream stream = File.Create(folderPath + "/" + text))
			{
				renderTarget2D.SaveAsPng(stream, width, height);
			}
			gd.SetRenderTarget(renderTarget2D);
			num3++;
		}
		GuiData.spriteBatch = spriteBatch2;
		gd.SetRenderTarget(currentRenderTarget);
	}
}
