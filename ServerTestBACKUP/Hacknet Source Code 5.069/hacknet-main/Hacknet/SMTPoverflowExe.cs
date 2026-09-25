using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class SMTPoverflowExe : ExeModule
{
	public static float DURATION = 12f;

	public static float BAR_MOVEMENT = 30f;

	public static float BAR_HEIGHT = 2f;

	public float progress;

	public bool hasCompleted;

	private float sucsessTimer = 0.5f;

	private float timeAccum = 0f;

	private float barSize = 0f;

	private List<Vector2> leftBars;

	private List<Vector2> rightBars;

	private int completedIndex = 0;

	private Color activeBarColor = new Color(34, 82, 64, 255);

	private Color activeBarHighlightColor = new Color(0, 186, 99, 0);

	public SMTPoverflowExe(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		needsProxyAccess = true;
		ramCost = 356;
		IdentifierName = "SMTP Overflow";
		activeBarColor = os.unlockedColor;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		barSize = BAR_HEIGHT + 1.4f;
		int num = (int)((float)bounds.Height / barSize);
		int num2 = bounds.Width / 2;
		num2--;
		leftBars = new List<Vector2>(num);
		rightBars = new List<Vector2>(num);
		for (int i = 0; i < num; i++)
		{
			leftBars.Add(new Vector2(Utils.random.Next(0, num2), Utils.random.Next(0, num2)));
			rightBars.Add(new Vector2(Utils.random.Next(0, num2), Utils.random.Next(0, num2)));
		}
		barSize = BAR_HEIGHT;
		Computer computer = Programs.getComputer(os, targetIP);
		computer.hostileActionTaken();
	}

	public override void Update(float t)
	{
		base.Update(t);
		timeAccum += t * 5f;
		progress += t / DURATION;
		if (progress >= 1f)
		{
			progress = 1f;
			if (!hasCompleted)
			{
				Completed();
				hasCompleted = true;
			}
			sucsessTimer -= t;
			if (sucsessTimer <= 0f)
			{
				isExiting = true;
			}
		}
		Vector2 zero = Vector2.Zero;
		for (int i = 0; i < leftBars.Count; i++)
		{
			if (i > completedIndex)
			{
				zero = leftBars[i];
				zero.X += (float)(Math.Sin(timeAccum + (float)(i * i)) * (double)t) * BAR_MOVEMENT;
				zero.Y += (float)(Math.Sin(timeAccum + (float)i) * (double)t) * BAR_MOVEMENT;
				leftBars[i] = zero;
				zero = rightBars[i];
				zero.X += (float)(Math.Sin(timeAccum + (float)(i * i)) * (double)t) * BAR_MOVEMENT;
				zero.Y += (float)(Math.Sin(timeAccum + (float)i) * (double)t) * BAR_MOVEMENT;
				rightBars[i] = zero;
			}
		}
		completedIndex = (int)(progress * (float)(leftBars.Count - 1));
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		spriteBatch.DrawString(GuiData.UISmallfont, "SMTP Mail Server Overflow", new Vector2(bounds.X + 5, bounds.Y + 12), os.subtleTextColor);
		Vector2 vector = new Vector2(bounds.X + 2, bounds.Y + 57);
		int num = (bounds.Width - 4) / 2;
		Rectangle destinationRectangle = new Rectangle(bounds.X + 2, bounds.Y + 38, (bounds.Width - 4) / 2, (int)barSize);
		Rectangle destinationRectangle2 = new Rectangle(bounds.X + 2, bounds.Y + 38, (bounds.Width - 4) / 2, (int)barSize);
		bool flag = false;
		for (int i = 0; i < leftBars.Count; i++)
		{
			flag = i <= completedIndex;
			destinationRectangle.Width = (int)leftBars[i].Y;
			spriteBatch.Draw(Utils.white, destinationRectangle, (flag ? Color.Lerp(os.outlineColor, Utils.VeryDarkGray, Utils.randm(0.2f) * Utils.randm(1f)) : os.subtleTextColor) * fade);
			destinationRectangle.Y += (int)barSize + 1;
			Vector2 vector2 = rightBars[i];
			destinationRectangle2.Width = (int)vector2.Y;
			destinationRectangle2.X = bounds.X + bounds.Width - 4 - (int)vector2.Y;
			spriteBatch.Draw(Utils.white, destinationRectangle2, (flag ? Color.Lerp(os.outlineColor, Utils.VeryDarkGray, Utils.randm(0.2f) * Utils.randm(1f)) : os.subtleTextColor) * fade);
			destinationRectangle2.Y += (int)barSize + 1;
			if (destinationRectangle2.Y + destinationRectangle2.Height >= bounds.Y + bounds.Height)
			{
				break;
			}
		}
		destinationRectangle = new Rectangle(bounds.X + 2, bounds.Y + 38, (bounds.Width - 4) / 2, (int)barSize);
		destinationRectangle2 = new Rectangle(bounds.X + 2, bounds.Y + 38, (bounds.Width - 4) / 2, (int)barSize);
		bool flag2 = flag;
		for (int i = 0; i < leftBars.Count; i++)
		{
			flag = i <= completedIndex || flag2;
			destinationRectangle.Width = (int)leftBars[i].X;
			DrawBar(destinationRectangle, flag, isLeft: true);
			destinationRectangle.Y += (int)barSize + 1;
			Vector2 vector2 = rightBars[i];
			destinationRectangle2.Width = (int)vector2.X;
			destinationRectangle2.X = bounds.X + bounds.Width - 4 - (int)vector2.X;
			DrawBar(destinationRectangle2, flag, isLeft: false);
			destinationRectangle2.Y += (int)barSize + 1;
			if (destinationRectangle2.Y + destinationRectangle2.Height >= bounds.Y + bounds.Height)
			{
				break;
			}
		}
		flag = flag2;
	}

	private void DrawBar(Rectangle dest, bool barActive, bool isLeft)
	{
		float num = Math.Min(1f, (float)dest.Width / ((float)bounds.Width / 2f));
		spriteBatch.Draw(Utils.white, dest, (hasCompleted ? Color.Lerp(os.highlightColor, Utils.AddativeWhite, Math.Max(0f, sucsessTimer)) : (barActive ? activeBarColor : Color.White)) * fade);
		if (barActive)
		{
			int height = dest.Height;
			dest.Height = 1;
			spriteBatch.Draw(Utils.gradientLeftRight, dest, null, activeBarHighlightColor * 0.6f * fade * num, 0f, Vector2.Zero, (!isLeft) ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.3f);
			dest.Y++;
			spriteBatch.Draw(Utils.gradientLeftRight, dest, null, activeBarHighlightColor * 0.2f * fade * num, 0f, Vector2.Zero, (!isLeft) ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.3f);
			dest.Y--;
			dest.Height = height;
		}
	}

	public override void Completed()
	{
		base.Completed();
		Programs.getComputer(os, targetIP)?.openPort(25, os.thisComputer.ip);
	}
}
