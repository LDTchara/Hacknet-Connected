using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class MemoryDumpDownloader : ExeModule
{
	private const float DownloadTime = 6f;

	private const float FailTime = 2f;

	private const float ExitTime = 5f;

	private bool DownloadComplete = false;

	private bool DidFail = false;

	private string savedFileName = "Unknown";

	private float elapsedTime = 0f;

	private Computer target;

	private MemoryDumpDownloader(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		name = "MemDumpGenerator";
		ramCost = 80;
		IdentifierName = "MemoryDumpGenerator";
		target = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
	}

	public static MemoryDumpDownloader GenerateInstanceOrNullFromArguments(string[] args, Rectangle location, object osObj, Computer target)
	{
		OS oS = (OS)osObj;
		if (oS.hasConnectionPermission(admin: true))
		{
			return new MemoryDumpDownloader(location, oS);
		}
		oS.write(LocaleTerms.Loc("Admin access required to generate memory dump"));
		return null;
	}

	public override void Update(float t)
	{
		base.Update(t);
		elapsedTime += t;
		if (DownloadComplete)
		{
			if (elapsedTime > 5f)
			{
				isExiting = true;
			}
		}
		else if (elapsedTime > 2f && target.Memory == null)
		{
			DownloadComplete = true;
			DidFail = true;
			elapsedTime = 0f;
		}
		else if (elapsedTime > 6f && target.Memory != null)
		{
			DownloadComplete = true;
			DidFail = false;
			elapsedTime = 0f;
			DownloadMemoryDump();
		}
	}

	private void DownloadMemoryDump()
	{
		string encodedFileString = target.Memory.GetEncodedFileString();
		if (os.thisComputer == target)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < os.exes.Count; i++)
			{
				if (os.exes[i] is NotesExe notesExe)
				{
					list.AddRange(notesExe.notes);
				}
			}
			MemoryContents memoryContents = new MemoryContents();
			memoryContents.DataBlocks = new List<string>(list.ToArray());
			memoryContents.CommandsRun = os.terminal.GetRecentTerminalHistoryList();
			MemoryContents memoryContents2 = memoryContents;
			encodedFileString = memoryContents2.GetEncodedFileString();
		}
		string reportFilename = getReportFilename(target.name);
		Folder folder = os.thisComputer.files.root.searchForFolder("home");
		Folder folder2 = folder.searchForFolder("MemDumps");
		if (folder2 == null)
		{
			folder.folders.Add(new Folder("MemDumps"));
			folder2 = folder.searchForFolder("MemDumps");
		}
		reportFilename = Utils.GetNonRepeatingFilename(reportFilename, ".mem", folder2);
		folder2.files.Add(new FileEntry(encodedFileString, reportFilename));
		savedFileName = "home/MemDumps/" + reportFilename;
	}

	private string getReportFilename(string s)
	{
		return Utils.CleanStringToRenderable(s).Replace(" ", "_").ToLower() + "_dump";
	}

	public override void Completed()
	{
		base.Completed();
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle contentAreaDest = GetContentAreaDest();
		if (contentAreaDest.Height < 5)
		{
			return;
		}
		PatternDrawer.draw(contentAreaDest, 0.1f, Color.Transparent, os.highlightColor * 0.1f, spriteBatch, PatternDrawer.wipTile);
		int num = Math.Min(contentAreaDest.Height / 2, 30);
		Rectangle rectangle = new Rectangle(contentAreaDest.X, contentAreaDest.Y + contentAreaDest.Height / 2 - num, contentAreaDest.Width, num);
		spriteBatch.Draw(Utils.white, rectangle, Color.Black);
		if (DownloadComplete)
		{
			spriteBatch.Draw(Utils.white, rectangle, DidFail ? (Color.Red * 0.5f) : (os.highlightColor * 0.5f));
			TextItem.doFontLabelToSize(Utils.InsetRectangle(rectangle, 4), DidFail ? LocaleTerms.Loc("Empty Scan Detected") : LocaleTerms.Loc("Download Complete"), GuiData.smallfont, Utils.AddativeWhite, doNotOversize: true);
			rectangle.Y += rectangle.Height;
			rectangle.Height -= 4;
			rectangle.X += 4;
			rectangle.Width -= 8;
			if (!DidFail)
			{
				TextItem.doFontLabelToSize(rectangle, savedFileName, GuiData.font, Utils.AddativeWhite, doNotOversize: true);
			}
		}
		else
		{
			float num2 = elapsedTime / 6f;
			rectangle.Width = (int)((float)rectangle.Width * num2);
			spriteBatch.Draw(Utils.white, rectangle, os.highlightColor * 0.5f);
		}
	}
}
