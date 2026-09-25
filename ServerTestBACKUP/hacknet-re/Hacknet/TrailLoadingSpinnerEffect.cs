using System;
using Hacknet.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class TrailLoadingSpinnerEffect
{
	private RenderTarget2D target;

	private SpriteBatch internalSB;

	private Texture2D circle;

	private FlyoutEffect flyout;

	public TrailLoadingSpinnerEffect(OS operatingSystem)
	{
		circle = TextureBank.load("Circle", operatingSystem.content);
	}

	public void Draw(Rectangle bounds, SpriteBatch spriteBatch, float totalTime, float timeRemaining, float extraTime = 0f, Color? color = null)
	{
		Color c = (color.HasValue ? color.Value : OS.currentInstance.highlightColor);
		Rectangle destinationRectangle = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 3);
		bool flag = false;
		if (target == null)
		{
			target = new RenderTarget2D(spriteBatch.GraphicsDevice, destinationRectangle.Width, destinationRectangle.Height, mipMap: false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
			internalSB = new SpriteBatch(spriteBatch.GraphicsDevice);
			flag = true;
		}
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		spriteBatch.GraphicsDevice.SetRenderTarget(target);
		Rectangle dest = new Rectangle(0, 0, destinationRectangle.Width, destinationRectangle.Height);
		internalSB.Begin();
		if (flag)
		{
			internalSB.GraphicsDevice.Clear(Color.Transparent);
		}
		DrawLoading(timeRemaining, totalTime, dest, internalSB, c, extraTime);
		internalSB.End();
		spriteBatch.GraphicsDevice.SetRenderTarget(currentRenderTarget);
		spriteBatch.Draw(target, destinationRectangle, Color.White);
	}

	public void Draw2(Rectangle bounds, SpriteBatch spriteBatch, float totalTime, float timeRemaining, float extraTime = 0f, float dt = 0f)
	{
		if (flyout == null)
		{
			flyout = new FlyoutEffect(GuiData.spriteBatch.GraphicsDevice, OS.currentInstance.content, bounds.Width, bounds.Height);
		}
		flyout.Draw(dt, bounds, spriteBatch, delegate(SpriteBatch sb, Rectangle dest)
		{
			DrawLoading(timeRemaining, totalTime, dest, sb, OS.currentInstance.highlightColor, extraTime);
		});
	}

	private void DrawLoading(float timeRemaining, float totalTime, Rectangle dest, SpriteBatch sb, Color c, float timeAdd = 0f)
	{
		float num = 20f;
		float num2 = (float)dest.Width / 2f - (num + 2f);
		Vector2 loaderCentre = new Vector2((float)dest.X + (float)dest.Width / 2f, (float)dest.Y + (float)dest.Height / 2f);
		float num3 = totalTime - timeRemaining + timeAdd;
		float num4 = Math.Min(1f, (num3 + timeAdd) / totalTime);
		sb.Draw(destinationRectangle: new Rectangle(0, 0, target.Width, target.Height), texture: Utils.white, color: Utils.VeryDarkGray * 0.05f);
		DrawLoadingCircle(timeRemaining, totalTime, dest, loaderCentre, num, num3 * 0.2f, 1f, sb, Utils.AddativeWhite);
		DrawLoadingCircle(timeRemaining, totalTime, dest, loaderCentre, num + num2 / 2f * num4, num3 * -0.4f, 0.7f, sb, Utils.AddativeWhite * (1f - num4), 10, 0.8f);
		DrawLoadingCircle(timeRemaining, totalTime, dest, loaderCentre, num + num2 * num4, num3 * 0.5f, 0.52f, sb, Utils.AddativeWhite * (1f - num4), 10, 0.8f);
		int num5 = 30;
		for (int i = 0; i < num5; i++)
		{
			float num6 = (float)i / (float)num5;
			DrawLoadingCircle(timeRemaining, totalTime, dest, loaderCentre, num + num2 * num6 * num4, num3 * -0.4f * num6, 2f * num6, sb, c, 6, 0.2f + num6 * 0.2f);
		}
	}

	private void DrawLoadingCircle(float timeRemaining, float totalTime, Rectangle dest, Vector2 loaderCentre, float loaderRadius, float baseRotationAdd, float rotationRateRPS, SpriteBatch sb, Color c, int NumberOfCircles = 10, float scaleMod = 1f)
	{
		float num = totalTime - timeRemaining;
		for (int i = 0; i < NumberOfCircles; i++)
		{
			float num2 = (float)i / (float)NumberOfCircles;
			float num3 = 2f;
			float num4 = 1f;
			float num5 = (float)Math.PI * 2f;
			float num6 = num5 + num4;
			float num7 = num2 * num3;
			float num8 = 0f;
			if (num > num7)
			{
				num8 = num / num7 * rotationRateRPS % num6;
				if (num8 >= num5)
				{
					num8 = 0f;
				}
				num8 = num5 * Utils.QuadraticOutCurve(num8 / num5);
				num8 += baseRotationAdd;
				Vector2 vector = loaderCentre + Utils.PolarToCartesian(num8, loaderRadius);
				sb.Draw(circle, vector, null, c, 0f, Vector2.Zero, scaleMod * 0.1f * (loaderRadius / 120f), SpriteEffects.None, 0.3f);
				if (Utils.random.NextDouble() < 0.001)
				{
					Vector2 vector2 = loaderCentre + Utils.PolarToCartesian(num8, 20f + Utils.randm(45f));
					sb.Draw(Utils.white, vector, Utils.AddativeWhite);
					Utils.drawLine(sb, vector, vector2, Vector2.Zero, Utils.AddativeWhite * 0.4f, 0.1f);
				}
			}
		}
	}
}
