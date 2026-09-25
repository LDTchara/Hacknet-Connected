using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

public class FlyoutEffect : IDisposable
{
	private static RenderTarget2D PooledTarget;

	private static RenderTarget2D PooledBackTarget;

	private RenderTarget2D DrawTarget;

	private RenderTarget2D BackTarget;

	private SpriteBatch innerBatch;

	private Vector2 offset = Vector2.Zero;

	private Texture2D UsedSprite;

	private Texture2D FlashSprite;

	private float timeSinceRender = 0f;

	private float rotation = 0f;

	private float elapsedTime = 0f;

	public float TimeBetweenRenderings = 0.1f;

	public FlyoutEffect(GraphicsDevice gd, ContentManager c, int width, int height)
	{
		if (PooledTarget != null && PooledTarget.Width == width && PooledTarget.Height == height)
		{
			DrawTarget = PooledTarget;
			BackTarget = PooledBackTarget;
			PooledTarget = null;
		}
		else
		{
			DrawTarget = new RenderTarget2D(gd, width, height);
			BackTarget = new RenderTarget2D(gd, width, height);
		}
		innerBatch = new SpriteBatch(gd);
		UsedSprite = c.Load<Texture2D>("CircleOutlineLarge");
		FlashSprite = c.Load<Texture2D>("EffectFiles/PointClicker/Star");
	}

	public void Draw(float dt, Rectangle dest, SpriteBatch sb, float cornerIn, int spiralElements, float spiralRadius, Color color, bool drawFlashFromMiddle, bool flashBackground)
	{
		elapsedTime += dt;
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		sb.GraphicsDevice.SetRenderTarget(DrawTarget);
		sb.GraphicsDevice.Clear(Color.Transparent);
		Rectangle rectangle = new Rectangle(0, 0, DrawTarget.Width, DrawTarget.Height);
		Rectangle destinationRectangle = Utils.InsetRectangle(rectangle, 2);
		rotation += dt * 3f;
		offset = new Vector2((float)(Math.Cos(elapsedTime * 2f) * 40.0), -1f * (float)(Math.Cos(elapsedTime * 8f) * 40.0));
		Vector2 vector = new Vector2(DrawTarget.Width / 2, DrawTarget.Height / 2);
		innerBatch.Begin();
		destinationRectangle.X = DrawTarget.Width / 2;
		destinationRectangle.Y = DrawTarget.Height / 2;
		innerBatch.Draw(BackTarget, destinationRectangle, null, new Color(250, 250, 250), 0f, vector, SpriteEffects.None, 0.5f);
		if (flashBackground)
		{
			GridEffect.DrawGridBackground(rectangle, innerBatch, 4, Utils.AddativeWhite);
		}
		for (int i = 0; i < spiralElements; i++)
		{
			Vector2 vector2 = Utils.PolarToCartesian((float)i / (float)spiralElements * ((float)Math.PI * 2f) + rotation, spiralRadius);
			innerBatch.Draw(UsedSprite, vector2 + new Vector2(DrawTarget.Width / 2, DrawTarget.Height / 2), null, flashBackground ? Utils.AddativeWhite : color, rotation, UsedSprite.GetCentreOrigin(), Vector2.One * 0.1f, SpriteEffects.None, 0.4f);
		}
		if (drawFlashFromMiddle)
		{
			int num = 4;
			float num2 = (float)dest.Width - 50f;
			for (int i = 0; i < num; i++)
			{
				innerBatch.Draw(FlashSprite, vector - new Vector2(3f), null, Color.Lerp(color, Utils.AddativeWhite, 0.5f) * 0.4f, 0f, FlashSprite.GetCentreOrigin(), Vector2.One * (2.5f + Utils.randm(3f)), SpriteEffects.None, 0.8f);
			}
		}
		innerBatch.End();
		sb.GraphicsDevice.SetRenderTarget(currentRenderTarget);
		sb.Draw(DrawTarget, dest, Color.White);
		RenderTarget2D backTarget = BackTarget;
		BackTarget = DrawTarget;
		DrawTarget = backTarget;
	}

	public void Draw(float dt, Rectangle dest, SpriteBatch sb, Action<SpriteBatch, Rectangle> render)
	{
		elapsedTime += dt;
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		sb.GraphicsDevice.SetRenderTarget(DrawTarget);
		sb.GraphicsDevice.Clear(Color.Transparent);
		Rectangle rectangle = new Rectangle(0, 0, DrawTarget.Width, DrawTarget.Height);
		Rectangle destinationRectangle = Utils.InsetRectangle(rectangle, 2);
		rotation += dt * 3f;
		offset = new Vector2((float)(Math.Cos(elapsedTime * 2f) * 40.0), -1f * (float)(Math.Cos(elapsedTime * 8f) * 40.0));
		Vector2 origin = new Vector2(DrawTarget.Width / 2, DrawTarget.Height / 2);
		innerBatch.Begin();
		destinationRectangle.X = DrawTarget.Width / 2;
		destinationRectangle.Y = DrawTarget.Height / 2;
		innerBatch.Draw(BackTarget, destinationRectangle, null, new Color(250, 250, 250), 0f, origin, SpriteEffects.None, 0.5f);
		render?.Invoke(innerBatch, rectangle);
		innerBatch.End();
		sb.GraphicsDevice.SetRenderTarget(currentRenderTarget);
		sb.Draw(DrawTarget, dest, Color.White);
		RenderTarget2D backTarget = BackTarget;
		BackTarget = DrawTarget;
		DrawTarget = backTarget;
	}

	public void Dispose()
	{
		if (PooledTarget == null)
		{
			PooledTarget = DrawTarget;
			PooledBackTarget = BackTarget;
		}
		else
		{
			DrawTarget.Dispose();
			BackTarget.Dispose();
		}
		DrawTarget = null;
	}
}
