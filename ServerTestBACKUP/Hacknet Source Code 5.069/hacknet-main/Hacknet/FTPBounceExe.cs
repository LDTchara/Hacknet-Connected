using System;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class FTPBounceExe : ExeModule
{
	public static float DURATION = 15f;

	public static float SCROLL_RATE = 0.08f;

	private string binary;

	private byte[] acceptedBinary1;

	private byte[] acceptedBinary2;

	private int binaryChars = 0;

	private float progress = 0f;

	private float timeLeft = DURATION;

	private float binaryScrollTimer = SCROLL_RATE;

	private int binaryIndex = 0;

	private bool complete = false;

	private int unlockedChars1 = 0;

	private int unlockedChars2 = 0;

	private int[] unlockOrder;

	public FTPBounceExe(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		needsProxyAccess = true;
		ramCost = 210;
		IdentifierName = "FTP Bounce";
	}

	public override void LoadContent()
	{
		base.LoadContent();
		binary = Computer.generateBinaryString(1024);
		int num = 12;
		binaryChars = (bounds.Width - 4) / num;
		acceptedBinary1 = new byte[binaryChars];
		acceptedBinary2 = new byte[binaryChars];
		for (int i = 0; i < num; i++)
		{
			acceptedBinary1[i] = ((!(Utils.random.NextDouble() > 0.5)) ? ((byte)1) : ((byte)0));
			acceptedBinary2[i] = ((!(Utils.random.NextDouble() > 0.5)) ? ((byte)1) : ((byte)0));
		}
		unlockOrder = new int[binaryChars];
		for (int i = 0; i < binaryChars; i++)
		{
			unlockOrder[i] = i;
		}
		int num2 = 300;
		for (int i = 0; i < num2; i++)
		{
			int num3 = Utils.random.Next(0, unlockOrder.Length - 1);
			int num4 = Utils.random.Next(0, unlockOrder.Length - 1);
			int num5 = unlockOrder[num3];
			unlockOrder[num3] = unlockOrder[num4];
			unlockOrder[num4] = num5;
		}
		Computer computer = Programs.getComputer(os, targetIP);
		computer.hostileActionTaken();
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (!complete)
		{
			binaryScrollTimer -= t;
			if (binaryScrollTimer <= 0f)
			{
				binaryIndex++;
				binaryScrollTimer = SCROLL_RATE;
			}
		}
		timeLeft -= t;
		if (timeLeft <= 0f && !complete)
		{
			complete = true;
			Completed();
		}
		progress = Math.Min(Math.Abs(1f - timeLeft / DURATION), 1f);
		unlockedChars1 = Math.Min((int)((float)binaryChars * (progress * 2f)), binaryChars);
		unlockedChars2 = Math.Min((int)((float)binaryChars * progress), binaryChars);
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Vector2 vector = new Vector2(bounds.X + 6, bounds.Y + 12);
		for (int i = 0; i < 6; i++)
		{
			for (int j = 0; j < binaryChars; j++)
			{
				spriteBatch.DrawString(GuiData.UITinyfont, string.Concat(binary[(binaryIndex + j + i * 20) % (binary.Length - 1)]), vector, Color.White);
				vector.X += 12f;
			}
			vector.Y += 12f;
			vector.X = bounds.X + 6;
			if (vector.Y - (float)bounds.Y + 24f > (float)bounds.Height)
			{
				return;
			}
		}
		vector.Y += 16f;
		if (vector.Y - (float)bounds.Y + 24f > (float)bounds.Height)
		{
			return;
		}
		spriteBatch.DrawString(GuiData.UISmallfont, "Working ::", vector, os.subtleTextColor);
		vector.Y += 20f;
		Rectangle destinationRectangle = new Rectangle((int)vector.X, (int)vector.Y, bounds.Width - 12, 80);
		destinationRectangle.Height = (int)Math.Min(destinationRectangle.Height, (float)bounds.Height - (vector.Y - (float)bounds.Y));
		spriteBatch.Draw(Utils.white, destinationRectangle, (complete ? os.unlockedColor : os.lockedColor) * fade);
		if (!(vector.Y - (float)bounds.Y + 80f > (float)bounds.Height))
		{
			for (int j = 0; j < unlockedChars1; j++)
			{
				int num = unlockOrder[j];
				int num2 = unlockOrder[binaryChars - j - 1];
				vector.X = bounds.X + 6 + 12 * num;
				spriteBatch.DrawString(GuiData.UISmallfont, string.Concat(acceptedBinary1[num]), vector, Color.White * fade);
				spriteBatch.DrawString(GuiData.UISmallfont, string.Concat(acceptedBinary1[num2]), vector + new Vector2(0f, 32f), Color.White * fade);
			}
			vector.Y += 16f;
			for (int j = 0; j < unlockedChars2; j++)
			{
				int num = unlockOrder[binaryChars - j - 1];
				int num2 = unlockOrder[j];
				vector.X = bounds.X + 6 + 12 * num;
				spriteBatch.DrawString(GuiData.UISmallfont, string.Concat(acceptedBinary2[num]), vector, Color.White * fade);
				spriteBatch.DrawString(GuiData.UISmallfont, string.Concat(acceptedBinary2[num2]), vector + new Vector2(0f, 32f), Color.White * fade);
			}
		}
	}

	public override void Completed()
	{
		base.Completed();
		Programs.getComputer(os, targetIP)?.openPort(21, os.thisComputer.ip);
		isExiting = true;
	}
}
