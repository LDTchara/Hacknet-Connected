using Hacknet.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Hacknet;

public class DemoEndScreen : GameScreen
{
	private Rectangle Fullscreen;

	public bool StopsMusic = true;

	public bool IsDLCDemoScreen = false;

	private double timeOnThisScreen = 0.0;

	private PointGatherEffect pointEffect = new PointGatherEffect();

	private HexGridBackground HexBackground;

	public override void LoadContent()
	{
		base.LoadContent();
		Fullscreen = new Rectangle(0, 0, base.ScreenManager.GraphicsDevice.Viewport.Width, base.ScreenManager.GraphicsDevice.Viewport.Height);
		if (StopsMusic)
		{
			MediaPlayer.Stop();
		}
		PostProcessor.EndingSequenceFlashOutActive = false;
		PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
		PostProcessor.dangerModeEnabled = false;
		if (IsDLCDemoScreen)
		{
			pointEffect.Init(base.ScreenManager.Game.Content);
			pointEffect.GravityConstant = 2E-05f;
			pointEffect.GlowScaleMod = 5f;
			pointEffect.CircleTex = base.ScreenManager.Game.Content.Load<Texture2D>("NodeCircle");
			pointEffect.NodeColor = Utils.AddativeRed * 0.5f;
			pointEffect.Explode(300);
			pointEffect.timeRemainingWithoutAttract = 2f;
			pointEffect.LineTexture = Utils.gradientLeftRight;
			pointEffect.LineLengthPercentage = 1f;
			OS.currentInstance.highlightColor = Utils.AddativeRed;
			HexBackground = new HexGridBackground(base.ScreenManager.Game.Content);
		}
	}

	public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
	{
		base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
		if (IsDLCDemoScreen)
		{
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			pointEffect.Update(dt);
			HexBackground.Update(dt);
		}
		if (!otherScreenHasFocus && !coveredByOtherScreen)
		{
			timeOnThisScreen += gameTime.ElapsedGameTime.TotalSeconds;
		}
		if (timeOnThisScreen > 5.0 && Keyboard.GetState().GetPressedKeys().Length > 0)
		{
			if (Settings.isConventionDemo)
			{
				OS.currentInstance.ScreenManager.AddScreen(new MainMenu());
				OS.currentInstance.ExitScreen();
				OS.currentInstance = null;
			}
			else
			{
				Game1.getSingleton().Exit();
			}
		}
		else if (timeOnThisScreen > (double)(IsDLCDemoScreen ? 51.5f : 20f))
		{
			if (Settings.isConventionDemo)
			{
				OS.currentInstance.ScreenManager.AddScreen(new MainMenu());
				OS.currentInstance.ExitScreen();
				OS.currentInstance = null;
				ExitScreen();
				MainMenu.resetOS();
			}
			else
			{
				Game1.getSingleton().Exit();
			}
		}
		PostProcessor.EndingSequenceFlashOutActive = false;
		PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
		PostProcessor.dangerModeEnabled = false;
	}

	public override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);
		PostProcessor.begin();
		GuiData.startDraw();
		GuiData.spriteBatch.Draw(Utils.white, Fullscreen, Color.Black);
		if (IsDLCDemoScreen)
		{
			HexBackground.Draw(Utils.GetFullscreen(), GuiData.spriteBatch, Utils.AddativeRed * 0.2f, Color.Black, HexGridBackground.ColoringAlgorithm.OutlinedSinWash);
			pointEffect.Render(Utils.GetFullscreen(), GuiData.spriteBatch);
			GuiData.spriteBatch.Draw(Utils.white, Fullscreen, Color.Black * 0.5f);
		}
		Rectangle dest = Utils.InsetRectangle(Fullscreen, 200);
		dest.Y = dest.Y + dest.Height / 2 - 200;
		dest.Height = 400;
		Rectangle destinationRectangle = new Rectangle(Fullscreen.X, dest.Y + 50, Fullscreen.Width, dest.Height - 148);
		GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, Utils.AddativeRed * (0.5f + Utils.randm(0.1f)));
		string text = "HACKNET";
		FlickeringTextEffect.DrawLinedFlickeringText(dest, text, IsDLCDemoScreen ? 5f : 18f, IsDLCDemoScreen ? 0.8f : 0.7f, GuiData.titlefont, null, Color.White, 6);
		dest.Y += 400;
		dest.Height = 120;
		SpriteFont font = GuiData.titlefont;
		if (Settings.ActiveLocale != "en-us")
		{
			font = GuiData.font;
		}
		string input = (IsDLCDemoScreen ? "EXPANSION COMING DECEMBER" : "MORE SOON");
		FlickeringTextEffect.DrawFlickeringText(dest, Utils.FlipRandomChars(LocaleTerms.Loc(input), IsDLCDemoScreen ? 0.0045 : 0.008), -8f, 0.7f, font, null, Color.Gray);
		FlickeringTextEffect.DrawFlickeringText(dest, Utils.FlipRandomChars(LocaleTerms.Loc(input), 0.03), -8f, 0.7f, font, null, Utils.AddativeWhite * 0.15f);
		GuiData.endDraw();
		PostProcessor.end();
	}
}
