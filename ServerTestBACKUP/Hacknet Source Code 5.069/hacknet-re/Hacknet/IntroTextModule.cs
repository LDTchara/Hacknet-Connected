using System;
using System.IO;
using System.Linq;
using Hacknet.Extensions;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hacknet;

internal class IntroTextModule : Module
{
	private static float FLASH_TIME = 3.5f;

	private static float STAY_ONSCREEN_TIME = 3f;

	private static float MODULE_FLASH_TIME = 2f;

	private static float CHAR_TIME = 0.048f;

	private static float LINE_TIME = 0.455f;

	private static float DELAY_FROM_START_MUSIC_TIMER = (Settings.isConventionDemo ? 6.2f : 15.06f);

	private static float DEMO_DELAY_FROM_START_MUSIC_TIMER = 5.18f;

	private float timer;

	private float charTimer;

	private float lineTimer;

	public bool complete;

	private bool finishedText;

	private string[] text;

	private int textIndex;

	private int charIndex = 0;

	private static string[] delims = new string[1] { "\r\n" };

	private Rectangle fullscreen;

	public IntroTextModule(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		bounds = location;
		timer = 0f;
		complete = false;
		textIndex = 0;
		finishedText = false;
		fullscreen = new Rectangle(0, 0, spriteBatch.GraphicsDevice.Viewport.Width, spriteBatch.GraphicsDevice.Viewport.Height);
		string text = (os.multiplayer ? "Content/MultiplayerIntroText.txt" : (os.IsDLCConventionDemo ? "Content/DLC/Docs/DemoIntroText.txt" : "Content/BitSpeech.txt"));
		if (Settings.IsInExtensionMode)
		{
			text = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "Intro.txt");
			if (!File.Exists(text))
			{
				text = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "intro.txt");
			}
		}
		string text2 = "";
		if (File.Exists(text))
		{
			text2 = LocalizedFileLoader.Read(text).Replace("\t", "    ");
			text2 = Utils.CleanFilterStringToRenderable(text2);
			this.text = text2.Split(delims, StringSplitOptions.RemoveEmptyEntries);
		}
		else
		{
			this.text = new string[1] { "   " };
		}
	}

	public override void Update(float t)
	{
		base.Update(t);
		double num = timer;
		timer += t;
		float num2 = (Settings.isDemoMode ? DEMO_DELAY_FROM_START_MUSIC_TIMER : DELAY_FROM_START_MUSIC_TIMER);
		if (Settings.IsInExtensionMode)
		{
			num2 = ExtensionLoader.ActiveExtensionInfo.IntroStartupSongDelay;
		}
		if (num < (double)num2 && timer >= num2)
		{
			MusicManager.playSong();
		}
		float num3 = 1f;
		if (LocaleActivator.ActiveLocaleIsCJK())
		{
			num3 = 0.6f;
		}
		if (finishedText)
		{
			if (timer > STAY_ONSCREEN_TIME && timer > STAY_ONSCREEN_TIME + MODULE_FLASH_TIME)
			{
				complete = true;
			}
		}
		else if (timer > FLASH_TIME)
		{
			charTimer += t * num3;
			if (!(charTimer >= CHAR_TIME))
			{
				return;
			}
			charTimer = (Settings.isConventionDemo ? (CHAR_TIME * (GuiData.getKeyboadState().IsKeyDown(Keys.LeftShift) ? 0.99f : 0.5f)) : 0f);
			charIndex++;
			if (charIndex < text[textIndex].Length)
			{
				return;
			}
			charTimer = CHAR_TIME;
			charIndex = text[textIndex].Length - 1;
			lineTimer += t;
			if (!(lineTimer >= LINE_TIME))
			{
				return;
			}
			lineTimer = (Settings.isConventionDemo ? (LINE_TIME * (GuiData.getKeyboadState().IsKeyDown(Keys.LeftShift) ? 0.99f : 0.2f)) : 0f);
			textIndex++;
			charIndex = 0;
			if (textIndex >= text.Length)
			{
				if (!MusicManager.isPlaying)
				{
					MusicManager.playSong();
				}
				finishedText = true;
				timer = 0f;
			}
		}
		else if (Settings.isConventionDemo && GuiData.getKeyboadState().IsKeyDown(Keys.LeftShift))
		{
			timer += t + t;
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		spriteBatch.Draw(Utils.white, bounds, os.darkBackgroundColor);
		if (Utils.random.NextDouble() < (double)(timer / FLASH_TIME) || timer > FLASH_TIME || finishedText)
		{
			os.drawBackground();
		}
		Color color = os.terminalTextColor * (finishedText ? (1f - timer / STAY_ONSCREEN_TIME) : 1f);
		Vector2 vector = new Vector2(120f, 100f);
		if (timer > FLASH_TIME || finishedText)
		{
			for (int i = 0; i < textIndex; i++)
			{
				string screensizeSplitVersionOfString = GetScreensizeSplitVersionOfString(text[i], vector);
				spriteBatch.DrawString(GuiData.smallfont, screensizeSplitVersionOfString, vector, color);
				vector.Y += 16f;
				if (screensizeSplitVersionOfString.Contains('\n'))
				{
					vector.Y += 16f;
				}
			}
			if (!finishedText)
			{
				string input = text[textIndex].Substring(0, charIndex + 1);
				string screensizeSplitVersionOfString = GetScreensizeSplitVersionOfString(input, vector);
				spriteBatch.DrawString(GuiData.smallfont, screensizeSplitVersionOfString, vector, color);
			}
		}
		if (finishedText && timer > STAY_ONSCREEN_TIME && Utils.random.NextDouble() < (double)((timer - STAY_ONSCREEN_TIME) / MODULE_FLASH_TIME))
		{
			os.drawModules(os.lastGameTime);
		}
		os.drawScanlines();
	}

	private string GetScreensizeSplitVersionOfString(string input, Vector2 dpos)
	{
		int num = spriteBatch.GraphicsDevice.Viewport.Width - (int)dpos.X - 10;
		if (GuiData.smallfont.MeasureString(input).X > (float)num)
		{
			return Utils.SuperSmartTwimForWidth(input, num, GuiData.smallfont);
		}
		return input;
	}
}
