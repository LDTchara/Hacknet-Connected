using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Daemons.Helpers;

public static class AircraftAltitudeIndicator
{
	private static Texture2D WarningIcon;

	private static Texture2D PlaneIcon;

	public static void Init(ContentManager content)
	{
		WarningIcon = content.Load<Texture2D>("Sprites/Icons/CautionIcon");
		PlaneIcon = content.Load<Texture2D>("DLC/Sprites/Airplane");
	}

	public static void RenderAltitudeIndicator(Rectangle dest, SpriteBatch sb, int currentAltitude, bool IsInCriticalDescenet, bool IconFlashIsVisible, int maxAltitude = 50000, int upperReccomended = 40000, int lowerReccomended = 30000, int warningArea = 14000, int criticalFailureArea = 3000)
	{
		if (WarningIcon == null)
		{
			Init(OS.currentInstance.content);
		}
		bool flag = currentAltitude <= 0;
		if (flag)
		{
			currentAltitude = maxAltitude;
		}
		int num = Math.Min(dest.Width, 100);
		Rectangle rectangle = new Rectangle(dest.X + dest.Width - num, dest.Y, num, dest.Height);
		int num2 = 200;
		Rectangle dest2 = new Rectangle(dest.X + dest.Width - num2, rectangle.Y, num2, 21);
		Color color = (IsInCriticalDescenet ? Utils.AddativeRed : OS.currentInstance.highlightColor);
		Rectangle rectangle2 = rectangle;
		rectangle2.Width = num / 2;
		rectangle2.X = dest.X + dest.Width - rectangle2.Width;
		sb.Draw(Utils.gradientLeftRight, rectangle2, color * 0.2f);
		int heightForAltitude = GetHeightForAltitude(currentAltitude, maxAltitude, rectangle2);
		rectangle.Y += heightForAltitude;
		rectangle.Height -= heightForAltitude;
		sb.Draw(Utils.gradientLeftRight, rectangle, color);
		DrawIndicatorForAltitude(dest2, maxAltitude, LocaleTerms.Loc("Maximum Altitude"), maxAltitude, rectangle2, sb, color, LineAtTop: true, useGradientBacking: true);
		DrawIndicatorForAltitude(dest2, upperReccomended, LocaleTerms.Loc("Maximum Cruising Altitude"), maxAltitude, rectangle2, sb, color);
		DrawIndicatorForAltitude(dest2, lowerReccomended, LocaleTerms.Loc("Minimum Cruising Altitude"), maxAltitude, rectangle2, sb, color);
		DrawIndicatorForAltitude(dest2, warningArea, LocaleTerms.Loc("Unsafe Altitude Margin"), maxAltitude, rectangle2, sb, color);
		dest2.Height *= 2;
		DrawIndicatorForAltitude(dest2, criticalFailureArea, LocaleTerms.Loc("Critical Failure Region") + "\n- " + LocaleTerms.Loc("POINT OF NO RETURN") + " -", maxAltitude, rectangle2, sb, Utils.makeColorAddative(color), LineAtTop: true);
		dest2 = new Rectangle(dest2.X - 20, dest2.Y, dest2.Width + 20, dest2.Height + 10);
		DrawIndicatorForAltitude(dest2, currentAltitude, flag ? (LocaleTerms.Loc("CRITICAL ERROR") + "\n" + LocaleTerms.Loc("SIGNAL LOST")) : (LocaleTerms.Loc("Current Altitude") + "\n" + $"{currentAltitude}ft"), maxAltitude, rectangle2, sb, color, LineAtTop: true);
		int num3 = dest2.Height - 4;
		Rectangle rect = new Rectangle(dest2.X - num3 - 4, dest2.Y + GetHeightForAltitude(currentAltitude, maxAltitude, rectangle2), num3, num3);
		Rectangle destinationRectangle = new Rectangle(dest2.X - num3 - 4, rect.Y, num3 + 4, dest2.Height);
		if (currentAltitude < lowerReccomended)
		{
			Rectangle dest3 = new Rectangle(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width + dest2.Width, destinationRectangle.Height);
			PatternDrawer.draw(dest3, 0.2f, Color.Transparent, Color.Red * 0.2f, sb);
		}
		sb.Draw(Utils.white, destinationRectangle, Color.Black * 0.4f);
		destinationRectangle.Height = 1;
		sb.Draw(Utils.white, destinationRectangle, color);
		rect.Y += 2;
		rect.X += 2;
		rect = Utils.InsetRectangle(rect, 4);
		if (IsInCriticalDescenet)
		{
			sb.Draw(WarningIcon, rect, Color.Red * (IconFlashIsVisible ? 1f : 0.3f));
		}
		else
		{
			sb.Draw(PlaneIcon, rect, color);
		}
	}

	private static int GetHeightForAltitude(int altitude, int maxAltitude, Rectangle glowBar)
	{
		float num = (float)altitude / (float)maxAltitude;
		return (int)((float)glowBar.Height * (1f - num));
	}

	private static void DrawIndicatorForAltitude(Rectangle dest, int altitude, string ElementTitle, int totalAltitude, Rectangle totalBar, SpriteBatch sb, Color c, bool LineAtTop = false, bool useGradientBacking = false)
	{
		dest.Y = totalBar.Y + GetHeightForAltitude(altitude, totalAltitude, totalBar);
		if (LineAtTop)
		{
			dest.Y++;
			dest.Height--;
		}
		sb.Draw(useGradientBacking ? Utils.gradientLeftRight : Utils.white, dest, null, Color.Black * (useGradientBacking ? 1f : 0.5f), 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0.4f);
		TextItem.doFontLabelToSize(dest, ElementTitle, GuiData.font, c, doNotOversize: true, offsetToTopLeft: true);
		if (LineAtTop)
		{
			dest.Y--;
			dest.Height = 1;
		}
		else
		{
			dest.Y += dest.Height - 2;
			dest.Height = 1;
		}
		sb.Draw(Utils.white, dest, c);
	}

	public static bool GetFlashRateFromTimer(float timer)
	{
		return (double)timer % 0.3 < 0.15000000596046448;
	}
}
