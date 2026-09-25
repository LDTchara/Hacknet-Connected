using System;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class DecypherTrackExe : ExeModule
{
	private enum DecHeadStatus
	{
		Error,
		Loading,
		Complete
	}

	private const float LOADING_TIME = 3.5f;

	private const float COMPLETE_TIME = 10f;

	private const float ERROR_TIME = 6f;

	private Computer targetComputer;

	private Folder destFolder;

	private FileEntry targetFile;

	private string targetFilename;

	private string destFilename;

	private DecHeadStatus status = DecHeadStatus.Loading;

	private float timeOnThisPhase = 0f;

	private float percentComplete = 0f;

	private string errorMessage = "Unknown Error";

	private string displayHeader = "Unknown";

	private string displayIP = "Unknown";

	private static Color LoadingBarColorRed = new Color(196, 29, 60, 80);

	private static Color LoadingBarColorBlue = new Color(29, 113, 196, 80);

	public DecypherTrackExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		IdentifierName = "DEC File Tracer";
		ramCost = 240;
		if (p.Length < 2)
		{
			status = DecHeadStatus.Error;
			errorMessage = "No File Provided";
		}
		else
		{
			InitializeFiles(p[1]);
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
		case DecHeadStatus.Error:
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
					status = DecHeadStatus.Complete;
					GetHeaders();
				}
				timeOnThisPhase = 0f;
			}
			break;
		case DecHeadStatus.Complete:
			num = 10f;
			if (timeOnThisPhase >= 10f)
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
				status = DecHeadStatus.Error;
				errorMessage = "File not found";
				return false;
			}
			if (FileEncrypter.FileIsEncrypted(targetFile.data) == 0)
			{
				status = DecHeadStatus.Error;
				errorMessage = "File is not\nDEC encrypted";
				return false;
			}
			return true;
		}
		catch (Exception)
		{
			status = DecHeadStatus.Error;
			errorMessage = "Fatal error in loading";
			return false;
		}
	}

	private void GetHeaders()
	{
		try
		{
			string[] array = FileEncrypter.DecryptHeaders(targetFile.data);
			displayHeader = array[0];
			displayIP = array[1];
			os.write(" \n \n---------------Header Analysis complete---------------\n \nDEC Encrypted File " + targetFilename + " headers:");
			os.write("Encryption Header    : \"" + displayHeader + "\"");
			os.write("Encryption Source IP: \"" + displayIP + "\"");
			os.write(" \n---------------------------------------------------------\n ");
		}
		catch (Exception ex)
		{
			os.write(" \n \n--------------- ERROR ---------------\n \n");
			os.write("Fatal Error: " + ex.GetType().ToString());
			os.write(ex.Message + "\n\n");
		}
	}

	private Rectangle DrawLoadingMessage(string message, float startPoint, Rectangle dest, bool showLoading = true, bool highlight = false)
	{
		float num = 0.18f;
		float num2 = (percentComplete - startPoint) / num;
		if (percentComplete > startPoint)
		{
			dest.Y += 22;
			spriteBatch.Draw(Utils.white, dest, Color.Black);
			float num3 = 1f;
			num3 = ((!(percentComplete > startPoint + num)) ? num2 : 1f);
			Rectangle rectangle = dest;
			dest.Width = (int)((float)dest.Width * Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(num3)));
			spriteBatch.Draw(Utils.white, dest, (status == DecHeadStatus.Complete) ? LoadingBarColorBlue : LoadingBarColorRed);
			spriteBatch.DrawString(GuiData.tinyfont, message, new Vector2(bounds.X + 6, dest.Y + 2), highlight ? Color.Black : Color.White);
			if (showLoading)
			{
				if (percentComplete > startPoint + num)
				{
					spriteBatch.DrawString(GuiData.tinyfont, LocaleTerms.Loc("COMPLETE"), new Vector2(bounds.X + 172, dest.Y + 2), Color.Black);
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
		Rectangle dest = bounds;
		dest.X++;
		dest.Y++;
		dest.Width -= 2;
		dest.Height -= 2;
		string text = status.ToString();
		switch (status)
		{
		case DecHeadStatus.Error:
		{
			PatternDrawer.draw(dest, 1.2f, Color.Transparent, os.lockedColor, spriteBatch, PatternDrawer.binaryTile);
			Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 20, 200, 50);
			if (bounds.Height > 120)
			{
				spriteBatch.Draw(Utils.white, rectangle, os.lockedColor);
				spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("ERROR"), new Vector2(bounds.X + 6, bounds.Y + 24), Color.White);
			}
			rectangle.Y += 50;
			rectangle.Height = 80;
			rectangle.Width = 250;
			if (bounds.Height > 160)
			{
				spriteBatch.Draw(Utils.white, rectangle, Color.Black);
				spriteBatch.DrawString(GuiData.smallfont, errorMessage, new Vector2(bounds.X + 6, bounds.Y + 74), Color.White);
			}
			break;
		}
		case DecHeadStatus.Loading:
		{
			PatternDrawer.draw(dest, 1.2f, Color.Transparent, os.lockedColor, spriteBatch, PatternDrawer.binaryTile);
			Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 20, 200, 50);
			spriteBatch.Draw(Utils.white, rectangle, Color.Black);
			spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("Loading") + "...", new Vector2(bounds.X + 6, bounds.Y + 24), Color.White);
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
		case DecHeadStatus.Complete:
			PatternDrawer.draw(dest, 1.2f, Color.Transparent, os.highlightColor * 0.5f, spriteBatch, PatternDrawer.binaryTile);
			if (bounds.Height > 70)
			{
				Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 20, 200, 85);
				spriteBatch.Draw(Utils.white, rectangle, Color.Black);
				spriteBatch.DrawString(GuiData.font, LocaleTerms.Loc("Operation\nComplete"), new Vector2(bounds.X + 6, bounds.Y + 24), Color.White);
				rectangle.Height = 20;
				rectangle.Width = 240;
				rectangle.Y += 63;
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height - 10)
				{
					rectangle = DrawLoadingMessage("Headers:", 0f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height - 10)
				{
					rectangle = DrawLoadingMessage("\"" + displayHeader + "\"", 0.1f, rectangle, showLoading: false, highlight: true);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height - 10)
				{
					rectangle = DrawLoadingMessage("Source IP:", 0.2f, rectangle, showLoading: false);
				}
				if (rectangle.Y + rectangle.Height < bounds.Y + bounds.Height - 10)
				{
					rectangle = DrawLoadingMessage("\"" + displayIP + "\"", 0.3f, rectangle, showLoading: false, highlight: true);
				}
			}
			break;
		}
	}
}
