using System;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class DLCTraceSlower : ExeModule
{
	private const int TargetRamUse = 600;

	private const float RAM_CHANGE_PS = 200f;

	private const int RAM_STARTING = 50;

	private string ActiveConnectedCompIP = null;

	private DepthDotGridEffect dotEffect;

	private DLCTraceSlower(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		name = "SignalScramble";
		ramCost = 50;
		IdentifierName = "SignalScramble";
		dotEffect = new DepthDotGridEffect(os.content);
		ActiveConnectedCompIP = ((os.connectedComp == null) ? os.thisComputer.ip : os.connectedComp.ip);
	}

	public static DLCTraceSlower GenerateInstanceOrNullFromArguments(string[] args, Rectangle location, object osObj, Computer target)
	{
		OS operatingSystem = (OS)osObj;
		target.hostileActionTaken();
		return new DLCTraceSlower(location, operatingSystem);
	}

	public override void Update(float t)
	{
		base.Update(t);
		string text = ((os.connectedComp == null) ? os.thisComputer.ip : os.connectedComp.ip);
		if (ActiveConnectedCompIP != text)
		{
			isExiting = true;
		}
		if (600 != ramCost)
		{
			if (600 < ramCost)
			{
				ramCost -= (int)(t * 200f);
				if (ramCost < 600)
				{
					ramCost = 600;
				}
			}
			else
			{
				int num = (int)(t * 200f);
				if (os.ramAvaliable >= num)
				{
					ramCost += num;
					if (ramCost > 600)
					{
						ramCost = 600;
					}
				}
			}
		}
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		bool flag = !isExiting && computer.PlayerHasAdminPermissions();
		float point = ((float)ramCost - 50f) / 550f;
		if (flag)
		{
			float val = 1f - Utils.QuadraticOutCurve(point);
			os.traceTracker.trackSpeedFactor = Math.Min(os.traceTracker.trackSpeedFactor, val);
		}
	}

	public override void Killed()
	{
		base.Killed();
		os.traceTracker.trackSpeedFactor = 1f;
	}

	public override void Completed()
	{
		base.Completed();
		os.traceTracker.trackSpeedFactor = 1f;
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle rectangle = Utils.InsetRectangle(new Rectangle(bounds.X, bounds.Y + Module.PANEL_HEIGHT, bounds.Width, bounds.Height - Module.PANEL_HEIGHT), 2);
		Rectangle rectangle2 = rectangle;
		rectangle2.Height = (int)((float)rectangle.Height * 1.125f);
		bool flag = os.connectedComp != null && ramCost >= 600 && os.connectedComp.adminIP == os.thisComputer.ip;
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		bool flag2 = computer.PlayerHasAdminPermissions();
		dotEffect.DrawGrid(rectangle, Vector2.Zero, spriteBatch, 30f, 10, flag2 ? Color.Gray : new Color(200, 15, 15, 0), 60f, 10f, 0f, os.timer, (!flag) ? 1f : (1f - (float)ramCost / 600f));
		if (bounds.Height < Module.PANEL_HEIGHT + 4)
		{
			return;
		}
		string text = null;
		string text2 = null;
		if (ramCost < 600)
		{
			if (isExiting)
			{
				text = LocaleTerms.Loc("Spinning down...");
			}
			else
			{
				text = LocaleTerms.Loc("Spinning up...");
				text2 = ((float)ramCost / 600f * 100f).ToString("00") + "." + Utils.randm(99f).ToString("00") + "%";
			}
		}
		else
		{
			text = LocaleTerms.Loc("Suppression Active");
			if (!flag2)
			{
				text = LocaleTerms.Loc("Administrator Access Required");
			}
		}
		int num = 27;
		if (bounds.Height > 40)
		{
			text = "[ " + text + " ]";
			Rectangle destinationRectangle = new Rectangle(rectangle.X, rectangle.Y + rectangle.Height - num - 10 - num, (int)((float)rectangle.Width * 0.92f), num + 2);
			destinationRectangle.Width += 8;
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Black * 0.9f);
			destinationRectangle.Width -= 8;
			destinationRectangle.Y += 4;
			destinationRectangle.Height -= 8;
			Vector2 vector = GuiData.font.MeasureString(text);
			float num2 = ((text2 != null) ? 0.7f : 1f);
			float num3 = Math.Min(1f, Math.Min((float)destinationRectangle.Width * num2 / vector.X, (float)destinationRectangle.Height / vector.Y));
			Vector2 vector2 = new Vector2((float)destinationRectangle.Width - vector.X * num3, (float)(destinationRectangle.Height / 2) - vector.Y * num3 / 2f);
			Color color = Color.Lerp(Color.White, os.highlightColor, 0.4f + Utils.randm(0.1f));
			spriteBatch.DrawString(GuiData.font, text, new Vector2(destinationRectangle.X, destinationRectangle.Y) + vector2, color, 0f, Vector2.Zero, new Vector2(num3), SpriteEffects.None, 0.5f);
			if (text2 != null)
			{
				Vector2 vector3 = new Vector2((float)destinationRectangle.X + 2f, (float)destinationRectangle.Y + vector2.Y);
				for (int i = 0; i < text2.Length; i++)
				{
					spriteBatch.DrawString(GuiData.font, string.Concat(text2[i]), vector3 + ((text2[i] == '.') ? new Vector2(3f, 0f) : Vector2.Zero), color, 0f, Vector2.Zero, new Vector2(num3), SpriteEffects.None, 0.5f);
					vector3.X += vector.X / (float)text.Length * num3 + 2f;
				}
			}
		}
		Rectangle destinationRectangle2 = new Rectangle(rectangle.X, rectangle.Y + rectangle.Height - num, rectangle.Width, 25);
		spriteBatch.Draw(Utils.white, destinationRectangle2, Color.Black);
		if (Button.doButton(19371002 + PID, destinationRectangle2.X + 1, destinationRectangle2.Y, destinationRectangle2.Width - 2, destinationRectangle2.Height, LocaleTerms.Loc("Exit"), os.lockedColor))
		{
			isExiting = true;
			os.traceTracker.trackSpeedFactor = 1f;
		}
	}
}
