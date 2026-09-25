using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public class ScreenManager : DrawableGameComponent
{
	private List<GameScreen> screens = new List<GameScreen>();

	private List<GameScreen> screensToUpdate = new List<GameScreen>();

	private InputState input = new InputState();

	private SpriteBatch spriteBatch;

	private SpriteFont font;

	private SoundEffect alertSound;

	public SpriteFont hugeFont;

	private Texture2D blankTexture;

	private bool isInitialized;

	private bool traceEnabled;

	public PlayerIndex controllingPlayer;

	public Color screenFillColor = Color.Black;

	public bool usingGamePad = false;

	public AudioEngine audioEngine;

	public WaveBank waveBank;

	public WaveBank musicBank;

	public SoundBank soundBank;

	public SpriteBatch SpriteBatch => spriteBatch;

	public SpriteFont Font => font;

	public bool TraceEnabled
	{
		get
		{
			return traceEnabled;
		}
		set
		{
			traceEnabled = value;
		}
	}

	public ScreenManager(Game game)
		: base(game)
	{
	}

	public override void Initialize()
	{
		base.Initialize();
		isInitialized = true;
		traceEnabled = false;
	}

	protected override void LoadContent()
	{
		ContentManager content = base.Game.Content;
		spriteBatch = new SpriteBatch(base.GraphicsDevice);
		GuiData.spriteBatch = new SpriteBatch(base.GraphicsDevice);
		font = content.Load<SpriteFont>("Font12");
		blankTexture = new Texture2D(base.GraphicsDevice, 1, 1, mipMap: false, SurfaceFormat.Color);
		blankTexture.SetData(new Color[1] { Color.White });
		try
		{
			alertSound = content.Load<SoundEffect>("SFX/Bip");
		}
		catch (Exception)
		{
			Settings.soundDisabled = true;
		}
		foreach (GameScreen screen in screens)
		{
			screen.LoadContent();
		}
		controllingPlayer = PlayerIndex.One;
	}

	protected override void UnloadContent()
	{
		foreach (GameScreen screen in screens)
		{
			screen.UnloadContent();
		}
	}

	public override void Update(GameTime gameTime)
	{
		GameScreen gameScreen = null;
		try
		{
			bool flag = usingGamePad;
			usingGamePad = false;
			for (int i = 0; i < input.CurrentGamePadStates.Length; i++)
			{
				if (input.CurrentGamePadStates[i].IsConnected)
				{
					usingGamePad = true;
				}
			}
			input.Update();
			screensToUpdate.Clear();
			foreach (GameScreen screen in screens)
			{
				screensToUpdate.Add(screen);
			}
			if (screensToUpdate.Count == 0)
			{
				foreach (GameScreen screen2 in screens)
				{
					screensToUpdate.Add(screen2);
				}
			}
			bool flag2 = !base.Game.IsActive;
			bool coveredByOtherScreen = false;
			bool flag3 = false;
			while (screensToUpdate.Count > 0)
			{
				gameScreen = screensToUpdate[screensToUpdate.Count - 1];
				screensToUpdate.RemoveAt(screensToUpdate.Count - 1);
				if (!flag2 && (gameScreen.ScreenState == ScreenState.TransitionOn || gameScreen.ScreenState == ScreenState.Active))
				{
					if (flag != usingGamePad)
					{
						gameScreen.inputMethodChanged(usingGamePad);
					}
					gameScreen.HandleInput(input);
					flag3 = true;
				}
				gameScreen.Update(gameTime, flag2, coveredByOtherScreen);
				if (flag3)
				{
					flag2 = true;
				}
				if ((gameScreen.ScreenState == ScreenState.TransitionOn || gameScreen.ScreenState == ScreenState.Active) && !gameScreen.IsPopup)
				{
					coveredByOtherScreen = true;
				}
			}
			if (traceEnabled)
			{
				TraceScreens();
			}
		}
		catch (Exception)
		{
			if (gameScreen != null)
			{
				RemoveScreen(gameScreen);
			}
		}
	}

	private void TraceScreens()
	{
		List<string> list = new List<string>();
		foreach (GameScreen screen in screens)
		{
			list.Add(screen.GetType().Name);
		}
	}

	public override void Draw(GameTime gameTime)
	{
		base.GraphicsDevice.Clear(screenFillColor);
		for (int i = 0; i < screens.Count; i++)
		{
			if (screens[i].ScreenState != ScreenState.Hidden)
			{
				screens[i].Draw(gameTime);
			}
		}
	}

	private void handleCriticalErrorBoxAccepted(object sender, PlayerIndexEventArgs e)
	{
		try
		{
			spriteBatch.End();
		}
		catch
		{
		}
		handleCriticalError();
	}

	public void handleCriticalError()
	{
		if (screens.Count <= 1)
		{
			AddScreen(new MainMenu(), controllingPlayer);
		}
	}

	public void AddScreen(GameScreen screen)
	{
		AddScreen(screen, controllingPlayer);
	}

	public void AddScreen(GameScreen screen, PlayerIndex? controllingPlayer)
	{
		screen.ControllingPlayer = controllingPlayer;
		screen.ScreenManager = this;
		screen.IsExiting = false;
		if (isInitialized)
		{
			screen.LoadContent();
		}
		screens.Add(screen);
	}

	public void ShowPopup(string message)
	{
		AddScreen(new MessageBoxScreen(clipStringForMessageBox(message), includeUsageText: false), controllingPlayer);
	}

	public void playAlertSound()
	{
		if (!Settings.soundDisabled)
		{
			alertSound.Play(0.3f, 0f, 0f);
		}
	}

	public void RemoveScreen(GameScreen screen)
	{
		if (isInitialized)
		{
			screen.UnloadContent();
		}
		screens.Remove(screen);
		screensToUpdate.Remove(screen);
		if (screens.Count <= 0)
		{
			handleCriticalError();
		}
	}

	public GameScreen[] GetScreens()
	{
		return screens.ToArray();
	}

	public void FadeBackBufferToBlack(int alpha)
	{
		Viewport viewport = base.GraphicsDevice.Viewport;
		spriteBatch.Begin();
		spriteBatch.Draw(blankTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, (byte)alpha));
		spriteBatch.End();
	}

	public string clipStringForMessageBox(string s)
	{
		string text = "";
		int num = 0;
		foreach (char c in s)
		{
			text = ((c == '\n') ? (text + ' ') : (text + c));
			num++;
			if (num > 50)
			{
				text += '\n';
				num = 0;
			}
		}
		return text;
	}

	private void ConfirmExitMessageBoxAccepted(object sender, PlayerIndexEventArgs e)
	{
	}
}
