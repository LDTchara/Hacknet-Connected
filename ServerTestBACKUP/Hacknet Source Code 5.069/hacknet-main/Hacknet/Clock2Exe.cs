using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class Clock2Exe : ExeModule
{
	private Texture2D triangle;

	private Texture2D arc;

	private Texture2D arcThin;

	private Rectangle arcClip;

	private Rectangle arcClipSmaller;

	private bool isLargeMode = false;

	public Clock2Exe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "Clock v2";
		ramCost = 60;
		for (int i = 1; i < p.Length; i++)
		{
			if (p[i].ToLower() == "-c")
			{
				ramCost = 40;
			}
			else if (p[i].ToLower() == "-l")
			{
				ramCost = 160;
				isLargeMode = true;
			}
		}
		IdentifierName = "Clock v2";
		targetIP = os.thisComputer.ip;
		triangle = operatingSystem.content.Load<Texture2D>("DLC/Sprites/Triangle");
		arc = operatingSystem.content.Load<Texture2D>("DLC/Sprites/CircleOutlineThick");
		arcThin = operatingSystem.content.Load<Texture2D>("CircleOutlineLarge");
		arcClip = new Rectangle(arc.Width / 4, 0, arc.Width / 2, arc.Height / 2);
		arcClipSmaller = new Rectangle((int)((double)arc.Width * 0.45), 0, (int)((double)arc.Width * 0.1), arc.Height / 2);
		os.write("Executing ClockV2.exe");
		os.write("Additional Arguments: -c / -l");
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		DateTime now = DateTime.Now;
		Rectangle rectangle = bounds;
		Rectangle rect = new Rectangle(rectangle.X, rectangle.Y, Math.Min(rectangle.Width, rectangle.Height), Math.Min(rectangle.Width, rectangle.Height));
		rect = Utils.InsetRectangle(rect, 1);
		Rectangle rectangle2 = new Rectangle(rectangle.X + rect.Width, rectangle.Y, rectangle.Width - rect.Width, rectangle.Height);
		DrawRadialPointer((float)now.Hour / 24f, rect, (float)rect.Width / 5f, Color.Gray);
		DrawRadialPointer((float)now.Minute / 60f, rect, (float)rect.Width / 3f, os.moduleColorStrong);
		DrawRadialPointer((float)now.Second / 60f, rect, (float)rect.Width / 2.25f, os.highlightColor * 0.5f, small: true);
		DrawRadialPointer((float)now.Millisecond / 1000f, rect, (float)rect.Width / 2f, os.topBarColor * 0.2f, small: true);
		Rectangle rectangle3 = new Rectangle(rectangle2.X, rectangle2.Y + 1, rectangle2.Width - 1, 9);
		spriteBatch.Draw(Utils.gradientLeftRight, rectangle3, os.exeModuleTopBar);
		rectangle3.Y += 5;
		TextItem.doRightAlignedBackingLabel(rectangle3, "ClockV2.exe", GuiData.detailfont, Color.Transparent, os.exeModuleTitleText);
		if (isLargeMode)
		{
			rectangle2 = new Rectangle(bounds.X + 80, bounds.Y + bounds.Height - 41, bounds.Width - 81, 40);
			rectangle3 = new Rectangle(rectangle2.X, rectangle2.Y + 1, rectangle2.Width - 1, 9);
		}
		string text = " " + now.ToShortTimeString();
		Rectangle rectangle4 = new Rectangle(rectangle3.X, rectangle3.Y + rectangle3.Height - 5, rectangle3.Width, rectangle2.Height - rectangle3.Height);
		Vector2 vector = GuiData.font.MeasureString(text);
		float num = vector.X / (float)rectangle4.Width;
		float num2 = (float)(rectangle4.Height / 2) - vector.Y * num / 2f;
		Vector2 position = new Vector2(rectangle4.X, (float)rectangle4.Y + num2);
		for (int i = 0; i < text.Length; i++)
		{
			spriteBatch.DrawString(GuiData.font, string.Concat(text[i]), position, Utils.AddativeWhite * 0.8f, 0f, Vector2.Zero, num, SpriteEffects.None, 0.5f);
			position.X += (float)rectangle4.Width / (float)text.Length;
		}
	}

	private void DrawRadialPointer(float rotationPercent, Rectangle fullArea, float radius, Color c, bool small = false)
	{
		float rotation = (float)((double)rotationPercent * (Math.PI * 2.0));
		Vector2 position = new Vector2((float)fullArea.X + (float)fullArea.Width / 2f, (float)fullArea.Y + (float)fullArea.Height / 2f);
		float value = radius / ((float)arc.Width / 2f);
		spriteBatch.Draw(small ? arcThin : arc, position, null, c * 0.2f, rotation, arc.GetCentreOrigin(), new Vector2(value), SpriteEffects.None, 0.4f);
		spriteBatch.Draw(small ? arcThin : arc, position, arcClip, c, rotation, new Vector2(arcClip.Width / 2, arcClip.Height), new Vector2(value), SpriteEffects.None, 0.4f);
		spriteBatch.Draw(arcThin, position, arcClipSmaller, Utils.AddativeWhite * 0.2f, rotation, new Vector2(arcClipSmaller.Width / 2, arcClipSmaller.Height), new Vector2(value), SpriteEffects.None, 0.4f);
	}
}
