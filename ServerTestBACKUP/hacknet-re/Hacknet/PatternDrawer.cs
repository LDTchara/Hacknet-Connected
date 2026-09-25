using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public static class PatternDrawer
{
	public static Texture2D warningStripe;

	public static Texture2D errorTile;

	public static Texture2D binaryTile;

	public static Texture2D thinStripe;

	public static Texture2D star;

	public static Texture2D wipTile;

	public static float time = 0f;

	public static void init(ContentManager content)
	{
		warningStripe = TextureBank.load("StripePattern", content);
		errorTile = TextureBank.load("ErrorTile", content);
		binaryTile = TextureBank.load("BinaryTile", content);
		thinStripe = TextureBank.load("ThinStripe", content);
		star = TextureBank.load("Sprites/Star", content);
		wipTile = TextureBank.load("Sprites/WIPTile", content);
	}

	public static void update(float t)
	{
		time += t;
	}

	public static void draw(Rectangle dest, float offset, Color backingColor, Color patternColor, SpriteBatch sb)
	{
		draw(dest, offset, backingColor, patternColor, sb, warningStripe);
	}

	public static void draw(Rectangle dest, float offset, Color backingColor, Color patternColor, SpriteBatch sb, Texture2D tex)
	{
		int x = dest.X;
		int num = dest.Y;
		int num2 = Math.Min(dest.Height, tex.Height);
		int num3 = (int)(time * offset % 1f * (float)tex.Width);
		Rectangle value = Rectangle.Empty;
		sb.Draw(Utils.white, dest, backingColor);
		Rectangle destinationRectangle = default(Rectangle);
		while (num - dest.Y + num2 <= dest.Height)
		{
			x = dest.X;
			destinationRectangle = new Rectangle(x, num, num3, num2);
			value = new Rectangle(0, 0, num3, num2);
			value.X = tex.Width - value.Width;
			sb.Draw(tex, destinationRectangle, value, patternColor);
			x = (destinationRectangle.X = x + num3);
			destinationRectangle.Width = tex.Width;
			while (x <= dest.X + dest.Width - tex.Width)
			{
				Rectangle? sourceRectangle = null;
				if (dest.Height < tex.Height)
				{
					sourceRectangle = new Rectangle(0, 0, tex.Width, dest.Height);
				}
				sb.Draw(tex, destinationRectangle, sourceRectangle, patternColor);
				x = (destinationRectangle.X = x + tex.Width);
			}
			destinationRectangle.X = x;
			destinationRectangle.Width = dest.X + dest.Width - x;
			value.Width = destinationRectangle.Width;
			value.X = 0;
			sb.Draw(tex, destinationRectangle, value, patternColor);
			num = (destinationRectangle.Y = num + tex.Height);
		}
		destinationRectangle.Height = dest.Height - (num - dest.Y);
		value.Height = destinationRectangle.Height;
		x = (destinationRectangle.X = dest.X);
		destinationRectangle.Y = num;
		destinationRectangle.Width = num3;
		value.Width = num3;
		value.X = tex.Width - value.Width;
		sb.Draw(tex, destinationRectangle, value, patternColor);
		x = (destinationRectangle.X = x + num3);
		destinationRectangle.Width = tex.Width;
		value.Width = tex.Width;
		value.X = 0;
		while (x <= dest.X + dest.Width - tex.Width)
		{
			sb.Draw(tex, destinationRectangle, value, patternColor);
			x = (destinationRectangle.X = x + tex.Width);
		}
		destinationRectangle.X = x;
		destinationRectangle.Width = dest.X + dest.Width - x;
		value.Width = destinationRectangle.Width;
		value.X = 0;
		sb.Draw(tex, destinationRectangle, value, patternColor);
		num += tex.Height;
		destinationRectangle.Y = num;
	}
}
