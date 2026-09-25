using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MessageBoxScreen : GameScreen
{
	public static float HEIGHT_BUFFER = 20f;

	private string message;

	private Texture2D top;

	private Texture2D mid;

	private Texture2D bottom;

	private Texture2D inputGuide;

	private SpriteFont guideFont;

	private Rectangle contentBounds;

	private Vector2 topLeft;

	private bool hasEscGuide = false;

	public string OverrideAcceptedText = null;

	public string OverrideCancelText = null;

	public Action AcceptedClicked;

	public Action CancelClicked;

	public event EventHandler<PlayerIndexEventArgs> Accepted;

	public event EventHandler<PlayerIndexEventArgs> Cancelled;

	public MessageBoxScreen(string message)
		: this(message, includeUsageText: false)
	{
	}

	public MessageBoxScreen(string message, bool includeUsageText)
	{
		if (includeUsageText)
		{
			this.message = message + "\nA button, Space, Enter = ok\nB button, Esc = cancel";
		}
		else
		{
			this.message = message;
		}
		base.IsPopup = true;
		base.TransitionOnTime = TimeSpan.FromSeconds(0.2);
		base.TransitionOffTime = TimeSpan.FromSeconds(0.05);
	}

	public MessageBoxScreen(string message, bool includesUsageText, bool hasEscPrompt)
		: this(message, includeUsageText: false)
	{
		hasEscGuide = hasEscPrompt;
	}

	public override void LoadContent()
	{
		ContentManager content = base.ScreenManager.Game.Content;
		top = TextureBank.load("PopupFrame", content);
		mid = TextureBank.load("PopupFrame", content);
		if (base.ScreenManager.usingGamePad)
		{
			bottom = TextureBank.load("PopupBase", content);
		}
		else
		{
			bottom = TextureBank.load("PopupBase", content);
		}
		float num = GuiData.font.MeasureString(message).Y + HEIGHT_BUFFER;
		topLeft = new Vector2((float)base.ScreenManager.GraphicsDevice.Viewport.Width / 2f - (float)top.Width / 2f, (float)base.ScreenManager.GraphicsDevice.Viewport.Height / 2f - num / 2f - (float)top.Height);
		contentBounds = new Rectangle((int)topLeft.X, (int)topLeft.Y + top.Height, top.Width, (int)num);
		if (hasEscGuide)
		{
			guideFont = GuiData.font;
		}
	}

	public override void HandleInput(InputState input)
	{
		if (input.IsMenuSelect(base.ControllingPlayer, out var playerIndex))
		{
			if (this.Accepted != null)
			{
				this.Accepted(this, new PlayerIndexEventArgs(playerIndex));
			}
			if (AcceptedClicked != null)
			{
				AcceptedClicked();
			}
			ExitScreen();
		}
		else if (input.IsMenuCancel(base.ControllingPlayer, out playerIndex))
		{
			if (this.Cancelled != null)
			{
				this.Cancelled(this, new PlayerIndexEventArgs(playerIndex));
			}
			if (CancelClicked != null)
			{
				CancelClicked();
			}
			ExitScreen();
		}
		GuiData.doInput(input);
	}

	public override void Draw(GameTime gameTime)
	{
		SpriteBatch spriteBatch = base.ScreenManager.SpriteBatch;
		SpriteFont font = GuiData.font;
		base.ScreenManager.FadeBackBufferToBlack(Math.Min(base.TransitionAlpha, (byte)140));
		Viewport viewport = base.ScreenManager.GraphicsDevice.Viewport;
		Vector2 vector = new Vector2(viewport.Width, viewport.Height);
		Vector2 vector2 = font.MeasureString(message);
		Vector2 position = vector / 2f - vector2 / 2f;
		Color color = new Color(22, 22, 22, 255);
		float num = 1f - base.TransitionPosition;
		if (base.ScreenState == ScreenState.TransitionOff)
		{
			return;
		}
		spriteBatch.Begin();
		spriteBatch.Draw(top, topLeft, color);
		spriteBatch.Draw(mid, contentBounds, color);
		spriteBatch.Draw(top, topLeft + new Vector2(0f, top.Height + contentBounds.Height), color);
		spriteBatch.DrawString(font, message, position, Color.White);
		int num2 = 150;
		int x = contentBounds.X + contentBounds.Width - (num2 + 5);
		int y = contentBounds.Y + contentBounds.Height + top.Height - 40;
		string input = ((OverrideCancelText == null) ? "Resume" : OverrideCancelText);
		if (Button.doButton(331, x, y, num2, 27, LocaleTerms.Loc(input), null))
		{
			if (this.Cancelled != null)
			{
				this.Cancelled(this, new PlayerIndexEventArgs(base.ScreenManager.controllingPlayer));
			}
			if (CancelClicked != null)
			{
				CancelClicked();
			}
			ExitScreen();
		}
		x = contentBounds.X + 5;
		string input2 = ((OverrideAcceptedText == null) ? "Quit Hacknet" : OverrideAcceptedText);
		if (Button.doButton(332, x, y, num2, 27, LocaleTerms.Loc(input2), null))
		{
			if (this.Accepted != null)
			{
				this.Accepted(this, new PlayerIndexEventArgs(base.ScreenManager.controllingPlayer));
			}
			if (AcceptedClicked != null)
			{
				AcceptedClicked();
			}
			ExitScreen();
		}
		spriteBatch.End();
	}

	public override void inputMethodChanged(bool usingGamePad)
	{
	}
}
