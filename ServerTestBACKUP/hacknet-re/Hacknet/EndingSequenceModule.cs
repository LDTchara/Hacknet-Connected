using System;
using Hacknet.Effects;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Hacknet;

internal class EndingSequenceModule : Module
{
	private const float SpeechTextHashDelay = 1f;

	private const float SpeechTextPercDelay = 0.5f;

	private const float SpeechTextCharDelay = 0.05f;

	public bool IsActive = false;

	private float elapsedTime = 0f;

	private float HacknetTitleFreezeTime = 10f;

	private float creditsPixelsScrollPerSecond = 65f;

	private SoundEffect speech;

	private SoundEffectInstance speechinstance;

	private WaveformRenderer waveRender;

	private bool IsInCredits = false;

	private float creditsScroll = 0f;

	private string[] CreditsData;

	private SoundEffect spinUpEffect;

	private SoundEffect traceDownEffect;

	private string BitSpeechText;

	private int SpeechTextIndex = 0;

	private float SpeechTextTimer = 0f;

	public EndingSequenceModule(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		bounds = location;
		creditsScroll = os.fullscreen.Height / 2;
		spinUpEffect = os.content.Load<SoundEffect>("Music/Ambient/spiral_gauge_up");
		traceDownEffect = os.content.Load<SoundEffect>("SFX/TraceKill");
		BitSpeechText = Utils.readEntireFile("Content/Post/BitSpeech.txt");
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (!IsActive)
		{
			return;
		}
		if (!IsInCredits)
		{
			if (waveRender == null)
			{
				traceDownEffect.Play();
				speech = os.content.Load<SoundEffect>("SFX/Ending/EndingSpeech");
				waveRender = new WaveformRenderer("Content/SFX/Ending/EndingSpeech.wav");
				speechinstance = speech.CreateInstance();
				speechinstance.IsLooped = false;
				CreditsData = Utils.readEntireFile("Content/Post/CreditsData.txt").Split(Utils.newlineDelim, StringSplitOptions.None);
				MusicManager.stop();
			}
			if (speechinstance.State == SoundState.Playing)
			{
				elapsedTime += t;
				if (elapsedTime > (float)speech.Duration.TotalSeconds)
				{
					RollCredits();
					return;
				}
				SpeechTextTimer += t;
				if (SpeechTextIndex >= BitSpeechText.Length)
				{
					return;
				}
				if (BitSpeechText[SpeechTextIndex] == '#')
				{
					if (SpeechTextTimer >= 1f)
					{
						SpeechTextTimer -= 1f;
						SpeechTextIndex++;
					}
				}
				else if (BitSpeechText[SpeechTextIndex] == '%')
				{
					if (SpeechTextTimer >= 0.5f)
					{
						SpeechTextTimer -= 0.5f;
						SpeechTextIndex++;
					}
				}
				else if (SpeechTextTimer >= 0.05f)
				{
					SpeechTextTimer -= 0.05f;
					SpeechTextIndex++;
				}
			}
			else
			{
				speechinstance.Play();
			}
		}
		else
		{
			elapsedTime += t;
			if (elapsedTime > HacknetTitleFreezeTime)
			{
				float num = Math.Min(1f, (elapsedTime - 10f) / 8f);
				creditsScroll -= t * creditsPixelsScrollPerSecond * num;
			}
		}
	}

	private void RollCredits()
	{
		if (os.TraceDangerSequence.IsActive)
		{
			os.TraceDangerSequence.CancelTraceDangerSequence();
		}
		IsInCredits = true;
		if (speechinstance != null)
		{
			speechinstance.Stop();
		}
		Settings.soundDisabled = false;
		elapsedTime = 0f;
		os.delayer.Post(ActionDelayer.Wait(1.0), delegate
		{
			MusicManager.playSongImmediatley("Music\\Bit(Ending)");
			MediaPlayer.IsRepeating = false;
			AchievementsManager.Unlock("progress_complete");
		});
		PostProcessor.dangerModeEnabled = false;
		PostProcessor.dangerModePercentComplete = 0f;
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		Rectangle fullscreen = os.fullscreen;
		spriteBatch.Draw(Utils.white, fullscreen, Color.Black);
		if (waveRender == null)
		{
			return;
		}
		if (!IsInCredits)
		{
			int width = os.fullscreen.Width;
			int height = os.fullscreen.Height;
			Rectangle rectangle = new Rectangle(0, os.fullscreen.Height / 2 - height / 2, width, height);
			waveRender.RenderWaveform(elapsedTime, speech.Duration.TotalSeconds, spriteBatch, rectangle);
			string[] array = BitSpeechText.Substring(0, SpeechTextIndex).Replace("#", "").Replace("%", "")
				.Split(Utils.newlineDelim, StringSplitOptions.None);
			Vector2 position = new Vector2((float)bounds.X + 150f, (float)(bounds.Y + bounds.Height) - 100f);
			float num = 1f;
			int num2 = array.Length - 1;
			while (num2 >= 0 && num2 > array.Length - 5)
			{
				spriteBatch.DrawString(GuiData.smallfont, array[num2], position, Utils.AddativeWhite * num);
				num *= 0.6f;
				position.Y -= GuiData.ActiveFontConfig.tinyFontCharHeight + 8f;
				num2--;
			}
		}
		else
		{
			DrawCredits();
		}
	}

	private void DrawCredits()
	{
		float y = creditsScroll;
		Vector2 vector = new Vector2(0f, y);
		int num = (int)((float)os.fullscreen.Width * 0.5f);
		int num2 = (int)((float)os.fullscreen.Height * 0.4f);
		Rectangle dest = new Rectangle(os.fullscreen.Width / 2 - num / 2, (int)(vector.Y - (float)(num2 / 2)), num, num2);
		Rectangle destinationRectangle = new Rectangle(os.fullscreen.X, dest.Y + 65, os.fullscreen.Width, dest.Height - 135);
		if (elapsedTime >= 1.71f)
		{
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Lerp(Utils.AddativeRed, Color.Red, 0.2f + Utils.randm(0.05f)) * 0.5f);
			FlickeringTextEffect.DrawLinedFlickeringText(dest, "HACKNET", 16f, 0.4f, GuiData.titlefont, os, Color.White, 5);
		}
		vector.Y += (float)os.fullscreen.Height / 2f;
		for (int i = 0; i < CreditsData.Length; i++)
		{
			float num3 = 20f;
			string text = CreditsData[i];
			if (!string.IsNullOrEmpty(text))
			{
				string text2 = text;
				SpriteFont spriteFont = GuiData.font;
				Color color = Color.White * 0.7f;
				if (text.StartsWith("^"))
				{
					text2 = text.Substring(1);
					color = Color.Gray * 0.6f;
				}
				else if (text.StartsWith("%"))
				{
					text2 = text.Substring(1);
					spriteFont = GuiData.titlefont;
					num3 = 90f;
				}
				if (text.StartsWith("$"))
				{
					text2 = text.Substring(1);
					color = Color.Gray * 0.6f;
					spriteFont = GuiData.smallfont;
				}
				Vector2 vector2 = spriteFont.MeasureString(text2);
				Vector2 position = vector + new Vector2((float)(os.fullscreen.Width / 2) - vector2.X / 2f, 0f);
				text2 = Utils.CleanStringToRenderable(text2);
				spriteBatch.DrawString(spriteFont, text2, position, color);
				vector.Y += num3;
			}
			vector.Y += num3;
		}
		if (vector.Y < -500f)
		{
			CompleteAndReturnToMenu();
		}
	}

	private void CompleteAndReturnToMenu()
	{
		os.Flags.AddFlag("Victory");
		Programs.disconnect(new string[0], os);
		Computer computer = Programs.getComputer(os, "porthackHeart");
		os.netMap.visibleNodes.Remove(os.netMap.nodes.IndexOf(computer));
		computer.disabled = true;
		computer.daemons.Clear();
		computer.ip = NetworkMap.generateRandomIP();
		os.terminal.inputLocked = false;
		os.ram.inputLocked = false;
		os.netMap.inputLocked = false;
		os.DisableTopBarButtons = false;
		os.canRunContent = true;
		IsActive = false;
		ComputerLoader.loadMission("Content/Missions/CreditsMission.xml");
		os.threadedSaveExecute();
		MediaPlayer.IsRepeating = true;
		MusicManager.playSongImmediatley("Music\\Bit(Ending)");
		if (Settings.isPirateBuild)
		{
			os.delayer.Post(ActionDelayer.Wait(15.0), delegate
			{
				try
				{
					ComputerLoader.loadMission("Content/Missions/CreditsMission_p.xml");
				}
				catch (Exception)
				{
				}
			});
		}
		if (!Settings.sendsDLC1PromoEmailAtEnd)
		{
			return;
		}
		os.delayer.Post(ActionDelayer.Wait(30.0), delegate
		{
			try
			{
				string body = Utils.readEntireFile("Content/LocPost/DLCMessage.txt");
				string subject = "Labyrinths";
				string sender = "Matt Trobbiani";
				string mail = MailServer.generateEmail(subject, body, sender);
				if (os.netMap.mailServer.getDaemon(typeof(MailServer)) is MailServer mailServer)
				{
					mailServer.addMail(mail, os.defaultUser.name);
				}
			}
			catch (Exception)
			{
			}
		});
	}
}
