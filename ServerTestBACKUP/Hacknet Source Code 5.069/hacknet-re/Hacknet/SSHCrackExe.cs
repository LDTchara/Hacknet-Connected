using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class SSHCrackExe : ExeModule
{
	private struct SSHCrackGridEntry
	{
		public float TimeTillActive;

		public float TimeTillSolved;

		public float TimeSinceActivated;

		public byte CurrentValue;
	}

	public static float DURATION = 8f;

	private static float GRID_REVEAL_DELAY = 0.6f;

	private static float ENDING_FLASH = 0.7f;

	private static float SHEEN_FLASH_DELAY = 0.03f;

	private int width;

	private int height;

	private float timeLeft = DURATION;

	private bool complete = false;

	private bool GridShowingSheen = false;

	private SSHCrackGridEntry[,] Grid;

	private int GridEntryWidth;

	private int GridEntryHeight;

	private Color unlockedFlashColor;

	public SSHCrackExe(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		needsProxyAccess = true;
		IdentifierName = "SecureShellCrack";
		ramCost = 242;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		GridEntryWidth = 31;
		GridEntryHeight = 15;
		width = (int)(((float)bounds.Width - 4f) / (float)GridEntryWidth);
		height = (int)(((float)bounds.Height - 24f) / (float)GridEntryHeight);
		Grid = new SSHCrackGridEntry[width, height];
		int num = 0;
		float num2 = 0f;
		float num3 = DURATION - GRID_REVEAL_DELAY - ENDING_FLASH;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int num4 = Math.Max(Math.Abs(j - i / 2), Math.Abs(j + i / 2));
				int num5 = i / 2 + num4;
				float num6 = num3 - num2;
				if (num % 2 == 0)
				{
					num6 = Utils.randm(num3);
					num2 = num6;
				}
				ref SSHCrackGridEntry reference = ref Grid[j, i];
				reference = new SSHCrackGridEntry
				{
					TimeSinceActivated = 0f,
					CurrentValue = Utils.getRandomByte(),
					TimeTillActive = (float)num5 * SHEEN_FLASH_DELAY,
					TimeTillSolved = num6
				};
				num++;
			}
		}
		unlockedFlashColor = Color.Lerp(os.unlockedColor, os.brightUnlockedColor, 0.4f);
		Computer computer = Programs.getComputer(os, targetIP);
		computer.hostileActionTaken();
		os.write("SecureShellCrack Running...");
	}

	public override void Update(float t)
	{
		base.Update(t);
		timeLeft -= t;
		if (timeLeft <= 0f && !complete)
		{
			complete = true;
			Completed();
			isExiting = true;
		}
		if (timeLeft < GRID_REVEAL_DELAY && GRID_REVEAL_DELAY - timeLeft <= t)
		{
			GridShowingSheen = true;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					int num = Math.Max(Math.Abs(j - i / 2), Math.Abs(j + i / 2));
					int num2 = i / 2 + num;
					SSHCrackGridEntry sSHCrackGridEntry = Grid[j, i];
					sSHCrackGridEntry.TimeSinceActivated = -1f * ((float)num2 * SHEEN_FLASH_DELAY);
					Grid[j, i] = sSHCrackGridEntry;
				}
			}
		}
		float num3 = DURATION - timeLeft;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				SSHCrackGridEntry sSHCrackGridEntry = Grid[j, i];
				sSHCrackGridEntry.TimeTillActive -= t;
				if (Utils.randm(0.5f) <= t)
				{
					sSHCrackGridEntry.CurrentValue = Utils.getRandomByte();
				}
				if (sSHCrackGridEntry.TimeTillActive <= 0f)
				{
					sSHCrackGridEntry.TimeTillActive = 0f;
					sSHCrackGridEntry.TimeSinceActivated += t;
					if (num3 > GRID_REVEAL_DELAY)
					{
						sSHCrackGridEntry.TimeTillSolved -= t;
						if (sSHCrackGridEntry.TimeTillSolved <= 0f)
						{
							sSHCrackGridEntry.CurrentValue = 0;
						}
					}
				}
				Grid[j, i] = sSHCrackGridEntry;
			}
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		Rectangle empty = Rectangle.Empty;
		drawOutline();
		drawTarget("");
		TextItem.doFontLabel(new Vector2(bounds.X + 2, bounds.Y + 14), complete ? "Operation Complete" : "SSH Crack in operation...", GuiData.UITinyfont, Utils.AddativeWhite * 0.8f * fade, bounds.Width - 6);
		int num = bounds.Y + 30;
		int num2 = bounds.X + 2;
		float num3 = 0.4f;
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + 16, GridEntryWidth - 2, GridEntryHeight - 2);
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				SSHCrackGridEntry sSHCrackGridEntry = Grid[i, j];
				rectangle.X = num2 + i * GridEntryWidth;
				rectangle.Y = num + j * GridEntryHeight;
				if (rectangle.Y + 1 > bounds.Y + bounds.Height || !(sSHCrackGridEntry.TimeTillActive <= 0f))
				{
					continue;
				}
				Color color = os.lockedColor;
				if (sSHCrackGridEntry.TimeSinceActivated < num3)
				{
					float amount = sSHCrackGridEntry.TimeSinceActivated / num3;
					color = Color.Lerp(os.brightLockedColor, os.lockedColor, amount);
				}
				if (sSHCrackGridEntry.TimeTillSolved <= 0f)
				{
					color = os.unlockedColor;
					if (sSHCrackGridEntry.TimeTillSolved > -1f * num3)
					{
						float amount2 = -1f * sSHCrackGridEntry.TimeTillSolved / num3;
						color = Color.Lerp(os.unlockedColor, unlockedFlashColor, amount2);
					}
				}
				if (GridShowingSheen)
				{
					float amount2 = 0f;
					if (sSHCrackGridEntry.TimeSinceActivated >= 0f)
					{
						amount2 = sSHCrackGridEntry.TimeSinceActivated / (num3 / 2f);
						if (sSHCrackGridEntry.TimeSinceActivated > num3 / 2f)
						{
							amount2 = 1f - Math.Min(1f, (sSHCrackGridEntry.TimeSinceActivated - num3 / 2f) / (num3 / 2f));
						}
					}
					if (amount2 < 0.25f)
					{
						amount2 = 0f;
					}
					else if (amount2 < 0.75f)
					{
						amount2 = 0.5f;
					}
					color = Color.Lerp(os.unlockedColor, unlockedFlashColor, amount2);
				}
				Rectangle destinationRectangle = rectangle;
				bool flag = true;
				if (rectangle.Y + rectangle.Height > bounds.Y + bounds.Height)
				{
					destinationRectangle.Height = rectangle.Height - (rectangle.Y + rectangle.Height - (bounds.Y + bounds.Height));
					flag = false;
				}
				spriteBatch.Draw(Utils.white, destinationRectangle, color * fade);
				float num4 = ((sSHCrackGridEntry.CurrentValue < 10) ? 8f : ((sSHCrackGridEntry.CurrentValue >= 100) ? 1f : 5f));
				if (flag)
				{
					spriteBatch.DrawString(GuiData.UITinyfont, string.Concat(sSHCrackGridEntry.CurrentValue), new Vector2((float)rectangle.X + num4, (float)rectangle.Y - 1.5f), Color.White * fade);
				}
			}
		}
	}

	public override void Completed()
	{
		base.Completed();
		Computer computer = Programs.getComputer(os, targetIP);
		if (computer != null)
		{
			computer.openPort(22, os.thisComputer.ip);
			os.write("-- SecureShellCrack Complete --");
		}
	}
}
