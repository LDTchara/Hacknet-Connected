using System;
using Hacknet.Gui;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

internal class TraceDangerSequence
{
	private enum TraceDangerState
	{
		WarningScrenIntro,
		WarningScreen,
		WarningScreenExiting,
		Countdown,
		Gameover,
		DisconnectedReboot
	}

	private const float WARNING_INTRO_TIME = 1f;

	private const float WARNING_EXIT_TIME = 13.9f;

	private const float DISCONNECT_REBOOT_TIME = 10f;

	private const float COUNTDOWN_TIME = 130f;

	private const float FLASH_FREQUENCY = 1.9376667f;

	public bool IsActive = false;

	public bool PreventOSRendering = false;

	private SpriteFont titleFont;

	private SpriteFont bodyFont;

	private Rectangle fullscreen;

	private SpriteBatch spriteBatch;

	private SpriteBatch scaleupSpriteBatch;

	private OS os;

	private SoundEffect spinDownSound;

	private SoundEffect spinUpSound;

	private SoundEffect impactSound;

	private float onBeatFlashTimer = 0f;

	private float timeThisState = 0f;

	private float percentComplete = 0f;

	private TraceDangerState state = TraceDangerState.WarningScrenIntro;

	private string oldSong = null;

	private bool warningScreenIsActivating = false;

	private static Color DarkRed = new Color(105, 0, 0, 200);

	private static Color BackgroundRed = new Color(120, 0, 0);

	public TraceDangerSequence(ContentManager content, SpriteBatch sb, Rectangle fullscreenRect, OS os)
	{
		titleFont = GuiData.titlefont;
		bodyFont = GuiData.font;
		fullscreen = fullscreenRect;
		spriteBatch = sb;
		scaleupSpriteBatch = new SpriteBatch(sb.GraphicsDevice);
		this.os = os;
		spinDownSound = os.content.Load<SoundEffect>("Music/Ambient/spiral_gauge_down");
		spinUpSound = os.content.Load<SoundEffect>("Music/Ambient/spiral_gauge_up");
		impactSound = os.content.Load<SoundEffect>("SFX/MeltImpact");
	}

	public void BeginTraceDangerSequence()
	{
		timeThisState = 0f;
		state = TraceDangerState.WarningScrenIntro;
		IsActive = true;
		PreventOSRendering = true;
		oldSong = MusicManager.currentSongName;
		os.execute("dc");
		os.display.command = "";
		os.terminal.inputLocked = true;
		MusicManager.playSongImmediatley("Music/Ambient/dark_drone_008");
		spinDownSound.Play();
	}

	public void CompleteIPResetSucsesfully()
	{
		timeThisState = 0f;
		state = TraceDangerState.DisconnectedReboot;
		IsActive = true;
		PreventOSRendering = true;
		PostProcessor.dangerModeEnabled = false;
		PostProcessor.dangerModePercentComplete = 0f;
		MusicManager.stop();
		spinDownSound.Play(1f, -0.6f, 0f);
		impactSound.Play();
	}

	public void CancelTraceDangerSequence()
	{
		timeThisState = 0f;
		state = TraceDangerState.WarningScrenIntro;
		IsActive = false;
		PreventOSRendering = false;
		PostProcessor.dangerModeEnabled = false;
		MusicManager.stop();
	}

	public void Update(float t)
	{
		timeThisState += t;
		float num = float.MaxValue;
		PostProcessor.dangerModeEnabled = false;
		switch (state)
		{
		case TraceDangerState.WarningScrenIntro:
			num = 1f;
			if (timeThisState > num)
			{
				timeThisState = 0f;
				state = TraceDangerState.WarningScreen;
				warningScreenIsActivating = false;
			}
			break;
		case TraceDangerState.WarningScreenExiting:
			num = 13.9f;
			if (timeThisState > num)
			{
				timeThisState = 0f;
				state = TraceDangerState.Countdown;
				os.display.visible = true;
				os.netMap.visible = true;
				os.terminal.visible = true;
				os.ram.visible = true;
			}
			break;
		case TraceDangerState.Countdown:
			num = 130f;
			if (timeThisState > num)
			{
				timeThisState = 0f;
				state = TraceDangerState.Gameover;
				CancelTraceDangerSequence();
				Game1.getSingleton().Exit();
			}
			if ((os.timer - onBeatFlashTimer) % 1.9376667f < 0.05f)
			{
				os.warningFlash();
			}
			PostProcessor.dangerModePercentComplete = Math.Min(timeThisState / (num * 0.85f), 1f);
			PostProcessor.dangerModeEnabled = true;
			PreventOSRendering = false;
			break;
		case TraceDangerState.DisconnectedReboot:
			num = 10f;
			if (timeThisState > num)
			{
				CancelTraceDangerSequence();
				MusicManager.loadAsCurrentSong(oldSong);
				os.rebootThisComputer();
			}
			break;
		}
		percentComplete = timeThisState / num;
	}

	public void Draw()
	{
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		switch (state)
		{
		case TraceDangerState.WarningScrenIntro:
		{
			DrawFlashingRedBackground();
			Rectangle destinationRectangle = new Rectangle(10, fullscreen.Height / 2 - 2, fullscreen.Width - 20, 4);
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Black);
			destinationRectangle.Width = (int)((float)destinationRectangle.Width * (1f - percentComplete));
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Red);
			break;
		}
		case TraceDangerState.WarningScreenExiting:
		{
			DrawFlashingRedBackground();
			Rectangle destinationRectangle = new Rectangle(10, fullscreen.Height / 2 - 2, fullscreen.Width - 20, 4);
			if (percentComplete > 0.5f)
			{
				int num2 = 4;
				float point = (percentComplete - 0.5f) * 2f;
				point = Utils.QuadraticOutCurve(point);
				num2 = (int)((float)os.fullscreen.Height * 0.7f * point);
				destinationRectangle.Y = fullscreen.Height / 2 - 2 - num2 / 2;
				destinationRectangle.Height = num2;
			}
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Black);
			destinationRectangle.Width = (int)((float)destinationRectangle.Width * Math.Min(1f, Utils.QuadraticOutCurve(percentComplete * 2f)));
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.DarkRed);
			float num3 = Utils.QuadraticOutCurve((percentComplete - 0.5f) * 2f);
			if (percentComplete > 0.5f)
			{
				ThinBarcode thinBarcode = new ThinBarcode(destinationRectangle.Width, destinationRectangle.Height);
				thinBarcode.Draw(spriteBatch, destinationRectangle.X, destinationRectangle.Y, (Utils.randm(1f) > num3) ? Color.Black : ((Utils.randm(1f) > num3 && Utils.randm(1f) > 0.8f) ? Utils.AddativeWhite : Utils.VeryDarkGray));
			}
			TextItem.doFontLabel(new Vector2(fullscreen.Width / 2 - 250, destinationRectangle.Y - 70), "INITIALIZING FAILSAFE", GuiData.titlefont, Color.White, 500f, 70f);
			break;
		}
		case TraceDangerState.WarningScreen:
			DrawWarningScreen();
			break;
		case TraceDangerState.Countdown:
		{
			PreventOSRendering = false;
			float num = timeThisState * 0.5f;
			if (num < 1f)
			{
				os.display.visible = num > Utils.randm(1f);
				os.netMap.visible = num > Utils.randm(1f);
				os.terminal.visible = num > Utils.randm(1f);
				os.ram.visible = num > Utils.randm(1f);
			}
			else
			{
				os.display.visible = true;
				os.netMap.visible = true;
				os.terminal.visible = true;
				os.ram.visible = true;
			}
			DrawCountdownOverlay(titleFont, bodyFont, os);
			break;
		}
		case TraceDangerState.DisconnectedReboot:
			DrawDisconnectedScreen();
			break;
		}
		TextItem.DrawShadow = drawShadow;
	}

	private void DrawDisconnectedScreen()
	{
		spriteBatch.Draw(Utils.white, fullscreen, Color.Black);
		Rectangle destinationRectangle = default(Rectangle);
		destinationRectangle.X = fullscreen.X + 2;
		destinationRectangle.Width = fullscreen.Width - 4;
		destinationRectangle.Y = fullscreen.Y + fullscreen.Height / 6 * 2;
		destinationRectangle.Height = fullscreen.Height / 3;
		spriteBatch.Draw(Utils.white, destinationRectangle, os.indentBackgroundColor);
		Vector2 vector = GuiData.titlefont.MeasureString("DISCONNECTED");
		Vector2 position = new Vector2((float)(destinationRectangle.X + fullscreen.Width / 2) - vector.X / 2f, fullscreen.Y + fullscreen.Height / 2 - 50);
		spriteBatch.DrawString(GuiData.titlefont, "DISCONNECTED", position, os.subtleTextColor);
		Vector2 pos2 = DrawFlashInString(pos: new Vector2(200f, destinationRectangle.Y + destinationRectangle.Height + 20), text: LocaleTerms.Loc("IP Address successfully reset"), offset: 4f, transitionInTime: 0.2f, hasDots: true);
		pos2 = DrawFlashInString(LocaleTerms.Loc("Foreign trace averted"), pos2, 5f, 0.2f, hasDots: true);
		pos2 = DrawFlashInString(LocaleTerms.Loc("Preparing for system reboot"), pos2, 6f, 0.2f, hasDots: true, 0.8f);
		pos2 = DrawFlashInString(LocaleTerms.Loc("Rebooting"), pos2, 9f, 0.2f, hasDots: true);
	}

	private void DrawWarningScreen()
	{
		if (warningScreenIsActivating)
		{
			spriteBatch.Draw(Utils.white, fullscreen, Color.White);
		}
		else
		{
			DrawFlashingRedBackground();
		}
		string text = "WARNING";
		Vector2 vector = titleFont.MeasureString(text);
		float num = (float)fullscreen.Width * 0.65f;
		float num2 = num / vector.X;
		Vector2 vector2 = new Vector2(20f, -10f);
		spriteBatch.DrawString(titleFont, text, vector2, Color.Black, 0f, Vector2.Zero, num2, SpriteEffects.None, 0.5f);
		vector2.Y += vector.Y * num2 - 55f;
		TextItem.doFontLabel(vector2, LocaleTerms.Loc("COMPLETED TRACE DETECTED : EMERGENCY RECOVERY MODE ACTIVE"), Settings.ActiveLocale.StartsWith("en") ? titleFont : GuiData.font, Color.Black, num);
		vector2.Y += 40f;
		vector2 = DrawFlashInString(LocaleTerms.Loc("Unsyndicated foreign connection detected during active trace"), vector2, 0f);
		vector2 = DrawFlashInString(" :: " + LocaleTerms.Loc("Emergency recovery mode activated"), vector2, 0.1f);
		vector2 = DrawFlashInString("-----------------------------------------------------------------------", vector2, 0.2f);
		vector2 = DrawFlashInString(" ", vector2, 0.5f);
		vector2 = DrawFlashInString(LocaleTerms.Loc("Automated screening procedures will divert incoming connections temporarily"), vector2, 0.5f);
		vector2 = DrawFlashInString(LocaleTerms.Loc("This window is a final opportunity to regain anonymity."), vector2, 0.6f);
		vector2 = DrawFlashInString(LocaleTerms.Loc("As your current IP Address is known, it must be changed") + " -", vector2, 0.7f);
		vector2 = DrawFlashInString(LocaleTerms.Loc("This can only be done on your currently active ISP's routing server"), vector2, 0.8f);
		Computer computer = Programs.getComputer(os, "ispComp");
		vector2 = DrawFlashInString(string.Format(LocaleTerms.Loc("Reverse tracerouting has located this ISP server's IP address as {0}"), (computer != null) ? computer.ip : "68.144.93.18"), vector2, 0.9f);
		vector2 = DrawFlashInString(string.Format(LocaleTerms.Loc("Your local ip : {0}  must be tracked here and changed."), os.thisComputer.ip), vector2, 1f);
		vector2 = DrawFlashInString(" ", vector2, 1.1f);
		vector2 = DrawFlashInString(LocaleTerms.Loc("Failure to complete this while active diversion holds will result in complete"), vector2, 1.1f);
		vector2 = DrawFlashInString(LocaleTerms.Loc("and permanent loss of all account data - THIS IS NOT REPEATABLE AND CANNOT BE DELAYED"), vector2, 1.2f);
		if (!warningScreenIsActivating && timeThisState >= 1.2f && Button.doButton(789798001, 20, (int)(vector2.Y + 10f), 400, 40, LocaleTerms.Loc("BEGIN"), Color.Black))
		{
			timeThisState = 0f;
			state = TraceDangerState.WarningScreenExiting;
			PreventOSRendering = true;
			onBeatFlashTimer = os.timer;
			warningScreenIsActivating = true;
			spinUpSound.Play(1f, 0f, 0f);
			os.terminal.inputLocked = false;
			os.delayer.Post(ActionDelayer.Wait(0.1), delegate
			{
				spinUpSound.Play(1f, 0f, 0f);
			});
			os.delayer.Post(ActionDelayer.Wait(0.4), delegate
			{
				spinUpSound.Play(0.4f, 0f, 0f);
			});
			os.delayer.Post(ActionDelayer.Wait(0.8), delegate
			{
				spinUpSound.Play(0.2f, 0.1f, 0f);
			});
			os.delayer.Post(ActionDelayer.Wait(1.3), delegate
			{
				spinUpSound.Play(0.1f, 0.2f, 0f);
			});
			os.delayer.Post(ActionDelayer.Wait(0.01), delegate
			{
				MusicManager.playSongImmediatley("Music/Traced");
			});
		}
	}

	private Vector2 DrawFlashInString(string text, Vector2 pos, float offset, float transitionInTime = 0.2f, bool hasDots = false, float dotsDelayer = 0.2f)
	{
		Vector2 vector = new Vector2(40f, 0f);
		if (timeThisState >= offset)
		{
			float num = Math.Min((timeThisState - offset) / transitionInTime, 1f);
			Vector2 position = pos + vector * (1f - Utils.QuadraticOutCurve(num));
			string text2 = "";
			if (hasDots)
			{
				float num2 = timeThisState - offset;
				while (num2 > 0f && text2.Length < 5)
				{
					num2 -= dotsDelayer;
					text2 += ".";
				}
			}
			float scale = 0.5f;
			float num3 = 17f;
			if (LocaleActivator.ActiveLocaleIsCJK())
			{
				scale = 0.7f;
				num3 = 22f;
			}
			spriteBatch.DrawString(bodyFont, text + text2, position, Color.White * num, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.4f);
			pos.Y += num3;
		}
		return pos;
	}

	public static void DrawCountdownOverlay(SpriteFont titleFont, SpriteFont bodyFont, object osobj, string title = null, string l1 = null, string l2 = null, string l3 = null)
	{
		OS oS = (OS)osobj;
		if (title == null)
		{
			title = "EMERGENCY TRACE AVERSION SEQUENCE";
		}
		if (l1 == null)
		{
			l1 = LocaleTerms.Loc("Reset Assigned Ip Address on ISP Mainframe");
		}
		Computer computer = Programs.getComputer(oS, "ispComp");
		if (l2 == null)
		{
			l2 = string.Format(LocaleTerms.Loc("ISP Mainframe IP: {0}"), (computer != null) ? computer.ip : "68.144.93.18");
		}
		if (l3 == null)
		{
			l3 = string.Format(LocaleTerms.Loc("YOUR Assigned IP: {0}"), oS.thisComputer.ip);
		}
		Rectangle rectangle = Utils.GetFullscreen();
		int num = 110;
		Rectangle destinationRectangle = new Rectangle(0, rectangle.Height - num - 20, 520, num);
		GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, Color.Lerp(DarkRed, Color.Transparent, Utils.randm(0.2f)));
		Vector2 pos = new Vector2(destinationRectangle.X + 6, destinationRectangle.Y + 4);
		TextItem.doFontLabel(pos, title, titleFont, Color.White, destinationRectangle.Width - 12, 35f);
		pos.Y += 32f;
		TextItem.doFontLabel(pos, l1, bodyFont, Color.White, destinationRectangle.Width - 10, 20f);
		pos.Y += 16f;
		TextItem.doFontLabel(pos, l2, bodyFont, Color.White, destinationRectangle.Width - 10, 20f);
		pos.Y += 16f;
		TextItem.doFontLabel(pos, l3, bodyFont, Color.White, destinationRectangle.Width - 10, 20f);
	}

	private void DrawFlashingRedBackground()
	{
		spriteBatch.Draw(Utils.white, fullscreen, Color.Lerp(BackgroundRed, Color.Black, Utils.randm(0.22f)));
	}
}
