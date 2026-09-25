using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hacknet.ScreenManagement;

internal class MessagePopup : GameScreen
{
	private static int BOX_WIDTH = 500;

	private static int BOX_HEIGHT = 350;

	private Texture2D blankTexture = null;

	private string messageContent = "";

	public MessagePopup(string message)
	{
		base.TransitionOnTime = TimeSpan.FromSeconds(0.5);
		base.TransitionOffTime = TimeSpan.FromSeconds(0.5);
		messageContent = message;
	}

	public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
	{
	}

	public override void HandleInput(InputState input)
	{
		KeyboardState state = Keyboard.GetState();
		if (state.IsKeyDown(Keys.Space) || state.IsKeyDown(Keys.Up) || state.IsKeyDown(Keys.Z) || state.IsKeyDown(Keys.Left) || state.IsKeyDown(Keys.Right) || state.IsKeyDown(Keys.X))
		{
			ExitScreen();
		}
	}

	public override void Draw(GameTime gameTime)
	{
		SpriteBatch spriteBatch = base.ScreenManager.SpriteBatch;
		Viewport viewport = base.ScreenManager.GraphicsDevice.Viewport;
		Rectangle rectangle = new Rectangle(0, 0, viewport.Width, viewport.Height);
		Rectangle destinationRectangle = new Rectangle(viewport.Width / 2 - BOX_WIDTH / 2, viewport.Height / 2 - BOX_HEIGHT / 2, BOX_WIDTH, BOX_HEIGHT);
		byte transitionAlpha = base.TransitionAlpha;
		spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
		spriteBatch.Draw(Utils.white, destinationRectangle, new Color(transitionAlpha, transitionAlpha, transitionAlpha));
		SpriteFont font = base.ScreenManager.Font;
		spriteBatch.DrawString(origin: new Vector2(0f, font.LineSpacing / 2), position: new Vector2(destinationRectangle.X, destinationRectangle.Y), spriteFont: font, text: messageContent, color: Color.White, rotation: 0f, scale: 1f, effects: SpriteEffects.None, layerDepth: 0f);
		spriteBatch.End();
	}

	public override void UnloadContent()
	{
	}
}
