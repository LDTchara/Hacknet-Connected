using System;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class CustomConnectDisplayDaemon : CustomConnectDisplayOverride
{
	private MovingBarsEffect topEffect;

	private MovingBarsEffect botEffect;

	private bool HasBeenAdminBefore = false;

	internal float timeInThisState = 0f;

	public CustomConnectDisplayDaemon(Computer c, OS os)
		: base(c, LocaleTerms.Loc("Display Override"), os)
	{
		topEffect = new MovingBarsEffect();
		botEffect = new MovingBarsEffect
		{
			IsInverted = true
		};
	}

	public CustomConnectDisplayDaemon(Computer c, string name, OS os)
		: base(c, name, os)
	{
		topEffect = new MovingBarsEffect();
		botEffect = new MovingBarsEffect
		{
			IsInverted = true
		};
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		timeInThisState += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		topEffect.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		botEffect.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		int num = bounds.X + 20;
		int num2 = bounds.Y + 60;
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		if (!(computer.adminIP == os.thisComputer.adminIP))
		{
			DrawNonAdminDisplay(bounds, sb);
			HasBeenAdminBefore = false;
			return;
		}
		if (!HasBeenAdminBefore)
		{
			timeInThisState = 0f;
			HasBeenAdminBefore = true;
		}
		DrawAdminDisplay(bounds, sb, computer);
	}

	internal virtual void DrawAdminDisplay(Rectangle bounds, SpriteBatch sb, Computer c)
	{
		int num = bounds.Width;
		float num2 = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(timeInThisState / 1f));
		if (timeInThisState < 1f)
		{
			num = (int)((float)bounds.Width * num2);
		}
		float num3 = 120f;
		float num4 = 60f;
		float num5 = 30f;
		float num6 = 10f;
		float num7 = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(Math.Min(1f, (timeInThisState - 1f) / 1f)));
		if (timeInThisState < 1f)
		{
			num7 = 0f;
		}
		int num8 = (int)(num4 + (num3 - num4) * (1f - num7));
		if (timeInThisState < 1f)
		{
			num8 = (int)((float)num8 * (0.5f + 0.5f * num2));
		}
		int stripHeight = (int)(num6 + (num5 - num6) * (1f - num7));
		int num9 = bounds.Height / 2 - num8 / 2;
		num9 = (int)((float)bounds.Y + (float)num9 * (1f - num7));
		int x = bounds.X + bounds.Width / 2 - num / 2;
		DrawCautionLinedMessage(new Rectangle(x, num9, num, num8), stripHeight, os.highlightColor, (timeInThisState > 0.5f + Utils.randm(0.5f)) ? "ACCESS GRANTED" : "", sb, PatternDrawer.warningStripe);
		x = bounds.X + 20;
		num9 = bounds.Y + 80 + 2;
		if (!(timeInThisState < 2f))
		{
			DrawConnectButtons(bounds, sb, c, 20, num9);
		}
	}

	internal virtual void DrawConnectButtons(Rectangle bounds, SpriteBatch sb, Computer c, int margin, int y, AlignmentX ButtonAlignment = AlignmentX.Middle)
	{
		int num = bounds.X + margin;
		int num2 = c.daemons.Count + 6;
		int num3 = 40;
		int num4 = bounds.Height - (y - bounds.Y) - 20;
		num4 -= num2 * 5;
		if ((double)num4 / (double)num2 < (double)num3)
		{
			num3 = (int)((double)num4 / (double)num2);
		}
		int num5 = bounds.Width / 2;
		num = ButtonAlignment switch
		{
			AlignmentX.Left => bounds.X + margin, 
			AlignmentX.Right => bounds.X + bounds.Width - (num5 + margin), 
			_ => bounds.X + bounds.Width / 2 - num5 / 2, 
		};
		if (c.daemons.Count > 0)
		{
			y += num3 + 5;
		}
		for (int i = 0; i < c.daemons.Count; i++)
		{
			if (!(c.daemons[i] is CustomConnectDisplayOverride))
			{
				if (Button.doButton(29000 + i, num, y, num5, num3, c.daemons[i].name, os.highlightColor))
				{
					os.display.command = c.daemons[i].name;
					c.daemons[i].navigatedTo();
				}
				y += num3 + 5;
			}
		}
		if (Button.doButton(300000, num, y, num5, num3, LocaleTerms.Loc("Login"), os.subtleTextColor))
		{
			os.runCommand("login");
			os.terminal.clearCurrentLine();
		}
		y += num3 + 5;
		if (Button.doButton(300002, num, y, num5, num3, LocaleTerms.Loc("Probe System"), os.highlightColor))
		{
			os.runCommand("probe");
		}
		y += num3 + 5;
		if (Button.doButton(300003, num, y, num5, num3, LocaleTerms.Loc("View Filesystem"), os.hasConnectionPermission(admin: false) ? os.highlightColor : os.subtleTextColor))
		{
			os.runCommand("ls");
		}
		y += num3 + 5;
		if (Button.doButton(300006, num, y, num5, num3, LocaleTerms.Loc("View Logs"), os.hasConnectionPermission(admin: false) ? os.highlightColor : os.subtleTextColor))
		{
			os.runCommand("cd log");
		}
		y += num3 + 5;
		if (Button.doButton(300009, num, y, num5, num3, LocaleTerms.Loc("Scan Network"), os.hasConnectionPermission(admin: true) ? os.highlightColor : os.subtleTextColor))
		{
			os.runCommand("scan");
		}
		y = bounds.Y + bounds.Height - 30;
		if (Button.doButton(300012, num, y, num5, 20, LocaleTerms.Loc("Disconnect"), os.lockedColor))
		{
			os.runCommand("dc");
		}
	}

	internal virtual void DrawCautionLinedMessage(Rectangle dest, int stripHeight, Color color, string Message, SpriteBatch sb, Texture2D stripTexture = null, int textOffsetY = 0)
	{
		if (stripTexture == null)
		{
			stripTexture = PatternDrawer.warningStripe;
		}
		Rectangle dest2 = dest;
		dest2.Height = stripHeight;
		PatternDrawer.draw(dest2, 1f, Color.Transparent, color, sb, stripTexture);
		dest2 = new Rectangle(dest2.X, dest.Y + dest.Height - stripHeight, dest2.Width, stripHeight);
		PatternDrawer.draw(dest2, 1f, Color.Transparent, color, sb, stripTexture);
		int height = dest.Height - dest2.Height * 2 + dest.Height / 10;
		Rectangle rectangle = new Rectangle(dest.X, dest.Y + stripHeight + 2 + textOffsetY, dest.Width, height);
		if (Settings.ActiveLocale == "en-us")
		{
			TextItem.doFontLabelToSize(rectangle, Message, GuiData.titlefont, color, doNotOversize: true);
		}
		else
		{
			TextItem.doCenteredFontLabel(rectangle, Message, GuiData.font, color);
		}
		Rectangle rectangle2 = rectangle;
		rectangle2.Y = dest.Y + dest.Height + 2;
		rectangle2.Height = 30;
	}

	internal virtual void DrawNonAdminDisplay(Rectangle dest, SpriteBatch sb)
	{
		sb.Draw(destinationRectangle: new Rectangle(dest.X + 1, dest.Y, dest.Width, 1), texture: Utils.white, color: Utils.AddativeRed);
		int num = 120;
		Rectangle rectangle = new Rectangle(dest.X, dest.Y + (dest.Height / 2 - num), dest.Width, num);
		sb.Draw(Utils.white, rectangle, Color.Black * 0.8f);
		int num2 = 20;
		DrawCautionLinedMessage(Utils.InsetRectangle(rectangle, 4), num2, Color.Red, "ACCESS DENIED", sb);
		int height = 90;
		Rectangle rectangle2 = new Rectangle(rectangle.X, rectangle.Y + num2 + 2, rectangle.Width, height);
		Rectangle rect = rectangle2;
		rect = Utils.InsetRectangle(rect, 20);
		rect.Y = rectangle.Y + rectangle.Height + 2;
		rect.Height = 30;
		TextItem.doFontLabelToSize(rect, LocaleTerms.Loc("Non-Admin Account Detected - Login to Proceed or Disconnect Now"), GuiData.font, Color.Black, doNotOversize: true);
		TextItem.doFontLabelToSize(rect, LocaleTerms.Loc("Non-Admin Account Detected - Login to Proceed or Disconnect Now"), GuiData.font, Color.Red * 0.7f, doNotOversize: true);
		int num3 = 340;
		int x = dest.X + dest.Width / 2 - num3 / 2;
		int num4 = rect.Y + rect.Height + 10;
		int num5 = 40;
		if (Button.doButton(302001, x, num4, num3, num5, LocaleTerms.Loc("Login"), Color.Gray))
		{
			os.runCommand("login");
			os.terminal.clearCurrentLine();
		}
		num4 += num5 + 6;
		if (Button.doButton(302003, x, num4, num3, 40, LocaleTerms.Loc("Disconnect"), Color.Red))
		{
			os.runCommand("dc");
		}
	}

	public override string getSaveString()
	{
		return "<CustomConnectDisplayDaemon />";
	}
}
