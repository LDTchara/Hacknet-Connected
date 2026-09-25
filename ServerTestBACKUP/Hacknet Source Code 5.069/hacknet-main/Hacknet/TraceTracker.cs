using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class TraceTracker
{
	private OS os;

	private float timer;

	private float lastFrameTime;

	private float startingTimer;

	public bool active;

	public float timeSinceFreezeRequest = 0f;

	public float trackSpeedFactor = 1f;

	private static SpriteFont font;

	private static SoundEffect beep;

	private Computer target;

	private string drawtext;

	private Color timerColor;

	public TraceTracker(OS _os)
	{
		os = _os;
		timerColor = new Color(170, 0, 0);
		timer = 0f;
		active = false;
		if (font == null)
		{
			font = os.content.Load<SpriteFont>("Kremlin");
			font.Spacing = 11f;
			beep = os.content.Load<SoundEffect>("SFX/beep");
		}
		drawtext = "TRACE :";
	}

	public void Update(float t)
	{
		bool flag = false;
		timeSinceFreezeRequest += t;
		if (timeSinceFreezeRequest < 0f)
		{
			timeSinceFreezeRequest = 1f;
		}
		if (timeSinceFreezeRequest < 0.2f)
		{
			flag = true;
		}
		if (!active)
		{
			return;
		}
		if (os.connectedComp == null || !os.connectedComp.ip.Equals(target.ip))
		{
			active = false;
			if (timer < 0.5f)
			{
				AchievementsManager.Unlock("trace_close");
			}
			if (os.connectedComp == null)
			{
				Console.WriteLine("Trace Ended - connection null");
			}
			else
			{
				Console.WriteLine("Trace Ended - connection changed - was " + target.ip + " now is " + os.connectedComp.ip);
			}
		}
		else if (!flag)
		{
			float num = (Settings.AllTraceTimeSlowed ? 0.055f : 1f);
			timer -= t * trackSpeedFactor * num;
			if (timer <= 0f)
			{
				timer = 0f;
				active = false;
				os.timerExpired();
			}
		}
		float num2 = timer / startingTimer * 100f;
		float num3 = ((!(num2 < 45f)) ? 10f : ((num2 < 15f) ? 1f : 5f));
		if (num2 % num3 > lastFrameTime % num3)
		{
			beep.Play(0.5f, 0f, 0f);
			os.warningFlash();
		}
		lastFrameTime = num2;
	}

	public void start(float t)
	{
		if (!active)
		{
			trackSpeedFactor = 1f;
			startingTimer = t;
			timer = t;
			active = true;
			os.warningFlash();
			target = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
			Console.WriteLine("Warning flash");
		}
	}

	public void stop()
	{
		active = false;
		trackSpeedFactor = 1f;
	}

	public void Draw(SpriteBatch sb)
	{
		if (active)
		{
			string text = (timer / startingTimer * 100f).ToString("00.00");
			Vector2 vector = font.MeasureString(text);
			Vector2 position = new Vector2(10f, (float)sb.GraphicsDevice.Viewport.Height - vector.Y);
			sb.DrawString(font, text, position, timerColor);
			position.Y -= 25f;
			sb.DrawString(font, drawtext, position, timerColor, 0f, Vector2.Zero, new Vector2(0.3f), SpriteEffects.None, 0.5f);
		}
	}
}
