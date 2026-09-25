using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MultiplayerGameOverScreen : GameScreen
{
	private Rectangle contentRect;

	private int screenWidth;

	private int screenHeight;

	private bool isWinner;

	private Color winBacking;

	private Color winPattern;

	private Color lossBacking;

	private Color lossPattern;

	private SpriteFont font;

	public MultiplayerGameOverScreen(bool winner)
	{
		isWinner = winner;
		base.TransitionOnTime = TimeSpan.FromSeconds(3.5);
		base.TransitionOffTime = TimeSpan.FromSeconds(0.20000000298023224);
		base.IsPopup = true;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		winBacking = new Color(5, 30, 8);
		lossBacking = new Color(34, 8, 5);
		winPattern = new Color(11, 66, 23);
		lossPattern = new Color(86, 11, 11);
		screenWidth = base.ScreenManager.GraphicsDevice.Viewport.Width;
		screenHeight = base.ScreenManager.GraphicsDevice.Viewport.Height;
		contentRect = new Rectangle(0, screenHeight / 6, screenWidth, screenHeight - screenHeight / 3);
		font = base.ScreenManager.Game.Content.Load<SpriteFont>("Kremlin");
	}

	public override void HandleInput(InputState input)
	{
		base.HandleInput(input);
		GuiData.doInput(input);
	}

	public override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);
		base.ScreenManager.FadeBackBufferToBlack(Math.Min(base.TransitionAlpha, (byte)150));
		GuiData.startDraw();
		float num = 1f - base.TransitionPosition;
		PatternDrawer.draw(contentRect, 1f, (isWinner ? winBacking : lossBacking) * num, (isWinner ? winPattern : lossPattern) * num, GuiData.spriteBatch);
		string text = (isWinner ? "VICTORY" : "DEFEAT");
		Vector2 pos = font.MeasureString(text);
		pos.X = (float)(contentRect.X + contentRect.Width / 2) - pos.X / 2f;
		pos.Y = (float)(contentRect.Y + contentRect.Height / 2) - pos.Y / 2f;
		TextItem.DrawShadow = false;
		TextItem.doFontLabel(pos, text, font, Color.White * num);
		if (Button.doButton(1008, contentRect.X + 10, contentRect.Y + contentRect.Height - 60, 230, 55, LocaleTerms.Loc("Exit"), Color.Black))
		{
			if (OS.currentInstance != null)
			{
				OS.currentInstance.ExitScreen();
			}
			ExitScreen();
			base.ScreenManager.AddScreen(new MainMenu());
		}
		GuiData.endDraw();
	}
}
