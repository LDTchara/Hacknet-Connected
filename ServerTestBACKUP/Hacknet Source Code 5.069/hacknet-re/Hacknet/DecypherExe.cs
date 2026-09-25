using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class DecypherExe : ExeModule
{
	private enum DecypherStatus
	{
		Error,
		Loading,
		Working,
		Complete
	}

	private const float LOADING_TIME = 3.5f;

	private const float WORKING_TIME = 10f;

	private const float COMPLETE_TIME = 3f;

	private const float ERROR_TIME = 6f;

	private Computer targetComputer;

	private Folder destFolder;

	private FileEntry targetFile;

	private string targetFilename;

	private string destFilename;

	private string password = "";

	private DecypherStatus status = DecypherStatus.Loading;

	private float timeOnThisPhase = 0f;

	private float percentComplete = 0f;

	private string errorMessage = "Unknown Error";

	private string displayHeader = "Unknown";

	private string displayIP = "Unknown";

	private string writtenFilename = "Unknown";

	private List<int> rowsActive = new List<int>();

	private List<int> columnsActive = new List<int>();

	private float lastLockedPercentage = 0f;

	private int rowsDrawn = 10;

	private int columnsDrawn = 10;

	private int lcgSeed = 1;

	public DecypherExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		IdentifierName = "Decypher Module";
		ramCost = 370;
		if (p.Length < 2)
		{
			status = DecypherStatus.Error;
			errorMessage = "No File Provided";
			return;
		}
		InitializeFiles(p[1]);
		if (p.Length > 2)
		{
			password = p[2];
		}
	}

	private void InitializeFiles(string filename)
	{
		targetComputer = os.thisComputer;
		if (os.connectedComp != null)
		{
			targetComputer = os.connectedComp;
		}
		destFolder = Programs.getCurrentFolder(os);
		targetFilename = filename;
		destFilename = filename.Replace(".dec", "[NUMBER][EXT]");
	}

	public override void Update(float t)
	{
		base.Update(t);
		timeOnThisPhase += t;
		float num = 3.5f;
		switch (status)
		{
		case DecypherStatus.Error:
			num = 6f;
			if (timeOnThisPhase >= 6f)
			{
				isExiting = true;
			}
			break;
		default:
			num = 3.5f;
			if (timeOnThisPhase >= 3.5f)
			{
				if (CompleteLoading())
				{
					status = DecypherStatus.Working;
				}
				timeOnThisPhase = 0f;
			}
			break;
		case DecypherStatus.Working:
			num = 10f;
			if (timeOnThisPhase >= 10f)
			{
				try
				{
					CompleteWorking();
				}
				catch (Exception)
				{
					status = DecypherStatus.Error;
					timeOnThisPhase = 0f;
					errorMessage = LocaleTerms.Loc("Fatal error in decryption\nfile may be corrupt");
				}
				status = DecypherStatus.Complete;
				timeOnThisPhase = 0f;
			}
			else
			{
				if (!(percentComplete % 0.1f < 0.01f))
				{
					break;
				}
				lastLockedPercentage = percentComplete;
				lcgSeed = Utils.random.Next();
				rowsActive.Clear();
				columnsActive.Clear();
				for (int i = 0; i < columnsDrawn; i++)
				{
					if (Utils.random.NextDouble() < 0.2)
					{
						rowsActive.Add(i);
					}
				}
				for (int j = 0; j < rowsDrawn; j++)
				{
					if (Utils.random.NextDouble() < 0.2)
					{
						columnsActive.Add(j);
					}
				}
			}
			break;
		case DecypherStatus.Complete:
			num = 3f;
			if (timeOnThisPhase >= 3f)
			{
				isExiting = true;
			}
			break;
		}
		percentComplete = timeOnThisPhase / num;
	}

	private bool CompleteLoading()
	{
		try
		{
			targetFile = destFolder.searchForFile(targetFilename);
			if (targetFile == null)
			{
				status = DecypherStatus.Error;
				errorMessage = LocaleTerms.Loc("File not found");
				return false;
			}
			switch (FileEncrypter.FileIsEncrypted(targetFile.data, password))
			{
			case 0:
				status = DecypherStatus.Error;
				errorMessage = LocaleTerms.Loc("File is not\nDEC encrypted");
				return false;
			case 2:
				status = DecypherStatus.Error;
				if (password == "")
				{
					errorMessage = LocaleTerms.Loc("This File Requires\n a Password");
				}
				else
				{
					errorMessage = LocaleTerms.Loc("Provided Password\nIs Incorrect");
				}
				return false;
			default:
				return true;
			}
		}
		catch (Exception)
		{
			status = DecypherStatus.Error;
			errorMessage = LocaleTerms.Loc("Fatal error in loading");
			return false;
		}
	}

	private void CompleteWorking()
	{
		string[] array = FileEncrypter.DecryptString(targetFile.data, password);
		if (!destFilename.Contains("[NUMBER]"))
		{
			destFilename += "[NUMBER]";
		}
		string text = destFilename.Replace("[NUMBER]", "");
		text = ((array[3] == null) ? text.Replace("[EXT]", ".txt") : text.Replace("[EXT]", array[3]));
		if (destFolder.containsFile(text))
		{
			int num = 1;
			do
			{
				text = destFilename.Replace("[NUMBER]", "(" + num + ")");
				num++;
			}
			while (destFolder.containsFile(text));
		}
		FileEntry item = new FileEntry(array[2], text);
		writtenFilename = text;
		destFolder.files.Add(item);
		os.write("Decryption complete - file " + targetFilename + " decrypted to target file " + targetFilename);
		os.write("Encryption Header    : \"" + array[0] + "\"");
		os.write("Encryption Source IP: \"" + array[1] + "\"");
		displayHeader = array[0];
		displayIP = array[1];
	}

	private Rectangle DrawLoadingMessage(string message, float startPoint, Rectangle dest, bool showLoading = true)
	{
		float num = 0.18f;
		float num2 = (percentComplete - startPoint) / num;
		if (percentComplete > startPoint)
		{
			dest.Y += 22;
			spriteBatch.Draw(Utils.white, dest, Color.Black);
			spriteBatch.DrawString(GuiData.tinyfont, message, new Vector2(bounds.X + 6, dest.Y + 2), Color.White);
			if (showLoading)
			{
				if (percentComplete > startPoint + num)
				{
					spriteBatch.DrawString(GuiData.tinyfont, LocaleTerms.Loc("COMPLETE"), new Vector2(bounds.X + 172, dest.Y + 2), Color.DarkGreen);
				}
				else
				{
					spriteBatch.DrawString(GuiData.tinyfont, (num2 * 100f).ToString("00") + "%", new Vector2(bounds.X + 195, dest.Y + 2), Color.White);
				}
			}
		}
		return dest;
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		string text = status.ToString();
		switch (status)
		{
		case DecypherStatus.Error:
		{
			PatternDrawer.draw(bounds, 1.2f, Color.Transparent, os.lockedColor, spriteBatch, PatternDrawer.errorTile);
			Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 20, 200, 50);
			spriteBatch.Draw(Utils.white, rectangle, os.lockedColor);
			spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("ERROR"), new Vector2(bounds.X + 6, bounds.Y + 24), Color.White);
			rectangle.Y += 50;
			rectangle.Height = 80;
			rectangle.Width = 250;
			spriteBatch.Draw(Utils.white, rectangle, Color.Black);
			spriteBatch.DrawString(GuiData.smallfont, errorMessage, new Vector2(bounds.X + 6, bounds.Y + 74), Color.White);
			break;
		}
		case DecypherStatus.Loading:
		{
			PatternDrawer.draw(bounds, 1.2f, Color.Transparent, os.highlightColor * 0.2f, spriteBatch, PatternDrawer.thinStripe);
			Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 20, 200, 50);
			spriteBatch.Draw(Utils.white, rectangle, Color.Black);
			spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("Loading..."), new Vector2(bounds.X + 6, bounds.Y + 24), Color.White);
			rectangle.Height = 20;
			rectangle.Width = 240;
			rectangle.Y += 28;
			rectangle = DrawLoadingMessage("Reading File...", 0f, rectangle);
			rectangle = DrawLoadingMessage("Parsing Headers...", 0.18f, rectangle);
			rectangle = DrawLoadingMessage("Verifying Content...", 0.36f, rectangle);
			rectangle = DrawLoadingMessage("Checking DEC...", 0.54f, rectangle);
			rectangle = DrawLoadingMessage("Reading Codes...", 0.72f, rectangle);
			break;
		}
		case DecypherStatus.Working:
		{
			Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 1, 200, 40);
			spriteBatch.Draw(Utils.white, rectangle, Color.Black);
			spriteBatch.DrawString(GuiData.font, "Decode:: " + (percentComplete * 100f).ToString("00.0") + " %", new Vector2(bounds.X + 2, bounds.Y + 2), Color.White);
			Rectangle rectangle2 = new Rectangle(bounds.X, bounds.Y + 50, bounds.Width, bounds.Height - 50);
			int num = rectangle2.X;
			int num2 = 12;
			int y = rectangle2.Y;
			int num3 = 13;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			Utils.LCG.reSeed(lcgSeed);
			while (num + num2 < rectangle2.X + rectangle2.Width)
			{
				y = rectangle2.Y;
				num6 = 0;
				while (y + num3 < rectangle2.Y + rectangle2.Height)
				{
					bool flag = rowsActive.Contains(num5) || columnsActive.Contains(num6);
					float num7 = (flag ? percentComplete : lastLockedPercentage);
					int index = (int)((num7 % 10f * (float)targetFile.data.Length * 3f + (float)(num4 * 222)) % (float)targetFile.data.Length);
					char c = targetFile.data[index];
					spriteBatch.DrawString(GuiData.UITinyfont, string.Concat(c), new Vector2(num, y), Color.Lerp(Color.White, os.highlightColor, flag ? Utils.randm(1f) : Utils.LCG.NextFloat()));
					y += num3;
					num4++;
					num6++;
				}
				num += num2 + 2;
				num5++;
			}
			columnsDrawn = num5;
			rowsDrawn = num6;
			break;
		}
		case DecypherStatus.Complete:
			PatternDrawer.draw(bounds, 1.2f, Color.Transparent, os.highlightColor * 0.2f, spriteBatch, PatternDrawer.thinStripe);
			if (bounds.Height > 60)
			{
				Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 20, 200, 85);
				spriteBatch.Draw(Utils.white, rectangle, Color.Black);
				spriteBatch.DrawString(GuiData.font, "Operation\nComplete", new Vector2(bounds.X + 6, bounds.Y + 24), Color.White);
				rectangle.Height = 20;
				rectangle.Width = 240;
				rectangle.Y += 63;
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage(LocaleTerms.Loc("Headers:"), 0f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage("\"" + displayHeader + "\"", 0.1f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage(LocaleTerms.Loc("Source IP:"), 0.3f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage("\"" + displayIP + "\"", 0.4f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage(LocaleTerms.Loc("Output File:"), 0.6f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage("\"" + writtenFilename + "\"", 0.7f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage(LocaleTerms.Loc("Operation Complete"), 0.9f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height)
				{
					rectangle = DrawLoadingMessage(LocaleTerms.Loc("Shutting Down"), 0.95f, rectangle, showLoading: false);
				}
			}
			break;
		}
	}
}
