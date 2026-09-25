using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class LogoCustomConnectDisplayDaemon : CustomConnectDisplayDaemon
{
	private const int ButtonLowerHeight = 80;

	public AlignmentX ButtonsAlignment;

	private string logoImageName;

	private string titleImageName;

	private string buttonAlignmentName;

	private Texture2D LogoImage;

	private Texture2D TitleImage;

	private bool LogoShouldClipOverdraw = true;

	public LogoCustomConnectDisplayDaemon(Computer c, OS os, string logoImageName, string titleImageName, bool logoShouldClipoverdraw, string buttonAlignment)
		: base(c, LocaleTerms.Loc("Logo Display Override"), os)
	{
		isListed = false;
		this.logoImageName = logoImageName;
		this.titleImageName = titleImageName;
		LogoShouldClipOverdraw = logoShouldClipoverdraw;
		buttonAlignmentName = buttonAlignment;
		if (!Enum.TryParse<AlignmentX>(buttonAlignment, ignoreCase: true, out ButtonsAlignment))
		{
			ButtonsAlignment = AlignmentX.Right;
		}
		string text = Utils.GetFileLoadPrefix() + logoImageName;
		if (text.EndsWith(".jpg") || text.EndsWith(".png"))
		{
			using FileStream stream = File.OpenRead(text);
			LogoImage = Texture2D.FromStream(Game1.getSingleton().GraphicsDevice, stream);
		}
		else
		{
			LogoImage = os.content.Load<Texture2D>(logoImageName);
		}
		string text2 = Utils.GetFileLoadPrefix() + titleImageName;
		if (text2.EndsWith(".jpg") || text2.EndsWith(".png"))
		{
			using (FileStream stream = File.OpenRead(text2))
			{
				TitleImage = Texture2D.FromStream(Game1.getSingleton().GraphicsDevice, stream);
				return;
			}
		}
		TitleImage = os.content.Load<Texture2D>(titleImageName);
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
	}

	internal override void DrawAdminDisplay(Rectangle bounds, SpriteBatch sb, Computer c)
	{
		DrawLogoAndTitle(bounds, sb);
		int num = bounds.Width;
		float num2 = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(timeInThisState / 1f));
		if (timeInThisState < 1f)
		{
			num = (int)((float)bounds.Width * num2);
		}
		float num3 = 110f;
		float num4 = 40f;
		float num5 = 40f;
		float num6 = 8f;
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
		num9 = (int)((float)bounds.Y + (float)num9 * (1f - num7) - 60f * num7);
		int x = bounds.X + bounds.Width / 2 - num / 2;
		DrawCautionLinedMessage(new Rectangle(x, num9, num, num8), stripHeight, os.highlightColor, (timeInThisState > 0.5f + Utils.randm(0.5f)) ? LocaleTerms.Loc("ACCESS GRANTED") : "", sb, PatternDrawer.warningStripe, -2);
		x = bounds.X + 20;
		num9 = bounds.Y + 80 + 2;
		if (!(timeInThisState < 2f))
		{
			DrawConnectButtons(bounds, sb, c, 20, num9, ButtonsAlignment);
		}
	}

	internal override void DrawNonAdminDisplay(Rectangle dest, SpriteBatch sb)
	{
		DrawLogoAndTitle(dest, sb);
		base.DrawNonAdminDisplay(dest, sb);
	}

	private void DrawLogoAndTitle(Rectangle dest, SpriteBatch sb)
	{
		AlignmentX align = AlignmentX.Left;
		AlignmentX align2 = AlignmentX.Right;
		if (ButtonsAlignment == AlignmentX.Right)
		{
			align = AlignmentX.Right;
			align2 = AlignmentX.Left;
		}
		Color white = Color.White;
		bool flag = comp.adminIP == os.thisComputer.adminIP;
		if (!flag)
		{
			white *= 0.4f;
		}
		int num = (LogoShouldClipOverdraw ? ((int)((float)dest.Width * 0.62f)) : ((int)((float)dest.Width * 0.45f)));
		Rectangle targetBounds = new Rectangle(dest.X + 1 + Utils.GetXForAlignment(align2, dest.Width, LogoShouldClipOverdraw ? (-130) : 0, num), dest.Y + (LogoShouldClipOverdraw ? (-50) : 20), num, (int)((float)num * ((float)LogoImage.Height / (float)LogoImage.Width)));
		Rectangle fullBounds = new Rectangle(dest.X, dest.Y - 20, dest.Width, dest.Height + 10);
		if (!flag)
		{
			fullBounds = dest;
		}
		Utils.RenderSpriteIntoClippedRectDest(fullBounds, targetBounds, LogoImage, white, sb);
		int num2 = dest.Width / 2;
		int num3 = (int)((float)num2 * ((float)TitleImage.Height / (float)TitleImage.Width));
		Utils.RenderSpriteIntoClippedRectDest(targetBounds: new Rectangle(dest.X + Utils.GetXForAlignment(align, dest.Width, 20, num2), dest.Y + 80 - (num3 - 40), num2, num3), fullBounds: dest, tex: TitleImage, c: white, sb: sb);
	}

	public override string getSaveString()
	{
		return string.Format("<LogoCustomConnectDisplayDaemon logo=\"{0}\" title=\"{1}\" overdrawLogo=\"{2}\" buttonAlignment=\"{3}\" />", logoImageName, titleImageName, LogoShouldClipOverdraw ? "true" : "false", buttonAlignmentName);
	}
}
