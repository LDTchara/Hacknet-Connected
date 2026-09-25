using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class NotesExe : ExeModule
{
	private const float RAM_CHANGE_PS = 350f;

	private const int BASE_PANEL_HEIGHT = 22;

	private const float NO_MEMORY_WARNING_TIME = 4f;

	internal const string NotesSaveFilename = "Notes.txt";

	public const string NotesReopenOnLoadFile = "Notes_Reopener.bat";

	internal const string NotesSaveFileDelimiter = "\n\n----------\n\n";

	private static Texture2D crossTexture;

	private static Texture2D circleTexture;

	private static int noteBaseCost = 8;

	private static int noteCostPerLine = 14;

	public List<string> notes = new List<string>();

	private int targetRamUse = 100;

	private int baseRamCost = 100;

	private bool gettingNewNote = false;

	public float MemoryWarningFlashTime = 0f;

	public NotesExe(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		IdentifierName = "Notes";
		ramCost = Module.PANEL_HEIGHT + 22;
		baseRamCost = ramCost;
		targetRamUse = ramCost;
		targetIP = os.thisComputer.ip;
		if (crossTexture == null)
		{
			crossTexture = os.content.Load<Texture2D>("cross");
		}
		if (circleTexture == null)
		{
			circleTexture = os.content.Load<Texture2D>("CircleOutline");
		}
	}

	public override void LoadContent()
	{
		base.LoadContent();
		LoadNotesFromDrive();
		Folder folder = os.thisComputer.files.root.searchForFolder("sys");
		if (folder.searchForFile("Notes_Reopener.bat") == null)
		{
			folder.files.Add(new FileEntry("true", "Notes_Reopener.bat"));
		}
	}

	public override void Killed()
	{
		base.Killed();
		RemoveReopnener();
	}

	public void DisplayOutOfMemoryWarning()
	{
		MemoryWarningFlashTime = 4f;
	}

	private void RemoveReopnener()
	{
		Folder folder = os.thisComputer.files.root.searchForFolder("sys");
		for (int i = 0; i < folder.files.Count; i++)
		{
			if (folder.files[i].name == "Notes_Reopener.bat")
			{
				folder.files.RemoveAt(i);
				break;
			}
		}
	}

	public static bool NoteExists(string note, OS os)
	{
		for (int i = 0; i < os.exes.Count; i++)
		{
			if (os.exes[i] is NotesExe notesExe)
			{
				return notesExe.HasNote(note);
			}
		}
		return false;
	}

	public static void AddNoteToOS(string note, OS os, bool isRecursiveSelfAdd = false)
	{
		for (int i = 0; i < os.exes.Count; i++)
		{
			if (os.exes[i] is NotesExe notesExe)
			{
				notesExe.AddNote(note);
				return;
			}
		}
		if (!isRecursiveSelfAdd)
		{
			os.runCommand("notes");
		}
		Action action = delegate
		{
			AddNoteToOS(note, os, isRecursiveSelfAdd: true);
		};
		os.delayer.Post(ActionDelayer.NextTick(), action);
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (targetRamUse != ramCost)
		{
			if (targetRamUse < ramCost)
			{
				ramCost -= (int)(t * 350f);
				if (ramCost < targetRamUse)
				{
					ramCost = targetRamUse;
				}
			}
			else
			{
				int num = (int)(t * 350f);
				if (os.ramAvaliable >= num)
				{
					ramCost += num;
					if (ramCost > targetRamUse)
					{
						ramCost = targetRamUse;
					}
				}
			}
		}
		if (gettingNewNote)
		{
			string data = null;
			if (Programs.parseStringFromGetStringCommand(os, out data))
			{
				gettingNewNote = false;
				AddNote(data);
			}
		}
	}

	public void AddNote(string note)
	{
		string text = Utils.SuperSmartTwimForWidth(note, os.ram.bounds.Width - 8, GuiData.UITinyfont);
		for (int i = 0; i < notes.Count; i++)
		{
			if (notes[i] == text)
			{
				return;
			}
		}
		notes.Add(text);
		recalcualteRamCost();
		SaveNotesToDrive();
	}

	public bool HasNote(string note)
	{
		string text = Utils.SuperSmartTwimForWidth(note, os.ram.bounds.Width - 8, GuiData.UITinyfont);
		for (int i = 0; i < notes.Count; i++)
		{
			if (notes[i] == text)
			{
				return true;
			}
		}
		return false;
	}

	private void LoadNotesFromDrive()
	{
		Folder folder = os.thisComputer.files.root.searchForFolder("home");
		FileEntry fileEntry = folder.searchForFile("Notes.txt");
		if (fileEntry != null)
		{
			string[] array = fileEntry.data.Split(new string[1] { "\n\n----------\n\n" }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length >= 0)
			{
				notes.AddRange(array);
				recalcualteRamCost();
			}
		}
	}

	private void SaveNotesToDrive()
	{
		Folder folder = os.thisComputer.files.root.searchForFolder("home");
		FileEntry fileEntry = folder.searchForFile("Notes.txt");
		if (fileEntry == null)
		{
			fileEntry = new FileEntry("", "Notes.txt");
			folder.files.Add(fileEntry);
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < notes.Count; i++)
		{
			if (i != 0)
			{
				stringBuilder.Append("\n\n----------\n\n");
			}
			stringBuilder.Append(notes[i]);
		}
		fileEntry.data = stringBuilder.ToString();
	}

	private void recalcualteRamCost()
	{
		targetRamUse = baseRamCost;
		for (int i = 0; i < notes.Count; i++)
		{
			targetRamUse += noteBaseCost + notes[i].Split(Utils.newlineDelim).Length * noteCostPerLine;
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		if (MemoryWarningFlashTime > 0f)
		{
			MemoryWarningFlashTime -= (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		}
		if (ramCost >= Module.PANEL_HEIGHT)
		{
			drawOutline();
			drawTarget("");
		}
		else
		{
			drawFrame();
		}
		Rectangle dest = new Rectangle(bounds.X + 2, bounds.Y + 2 + Module.PANEL_HEIGHT, bounds.Width - 4, ramCost - Module.PANEL_HEIGHT - 22);
		DrawNotes(dest);
		if (ramCost >= Module.PANEL_HEIGHT + 22)
		{
			Rectangle dest2 = new Rectangle(bounds.X + 2, bounds.Y + bounds.Height - 22, bounds.Width - 4, 22);
			DrawBasePanel(dest2);
		}
	}

	private void DrawBasePanel(Rectangle dest)
	{
		int num = dest.Width - 8;
		int num2 = (int)((double)num * 0.4);
		int width = (int)((double)num * 0.6);
		if (!gettingNewNote && Button.doButton(631012 + os.exes.IndexOf(this), dest.X + dest.Width - num2, dest.Y + 4, num2, dest.Height - 6, LocaleTerms.Loc("Close"), os.lockedColor * fade))
		{
			if (gettingNewNote)
			{
				os.terminal.executeLine();
				gettingNewNote = false;
			}
			Completed();
			RemoveReopnener();
			isExiting = true;
		}
		if (!gettingNewNote && Button.doButton(611014 + os.exes.IndexOf(this), dest.X, dest.Y + 4, width, dest.Height - 6, LocaleTerms.Loc("Add Note"), os.highlightColor * fade))
		{
			os.runCommand("getString Note");
			os.getStringCache = "";
			gettingNewNote = true;
		}
		else if (gettingNewNote)
		{
			Rectangle dest2 = new Rectangle(dest.X, dest.Y + 4, num, dest.Height - 6);
			PatternDrawer.draw(dest2, 1f, Color.Transparent, os.highlightColor * 0.5f, spriteBatch, PatternDrawer.thinStripe);
			TextItem.doFontLabelToSize(dest2, LocaleTerms.Loc("Type In Terminal..."), GuiData.smallfont, Color.White);
		}
	}

	private void DrawNotes(Rectangle dest)
	{
		int num = dest.Y;
		for (int i = 0; i < notes.Count; i++)
		{
			string[] array = notes[i].Split(Utils.newlineDelim);
			int num2 = noteBaseCost + array.Length * noteCostPerLine;
			if (num - dest.Y + num2 > dest.Height + 1)
			{
				break;
			}
			Rectangle destinationRectangle = new Rectangle(dest.X, num + noteBaseCost / 2 - 2, dest.Width, num2 - noteBaseCost / 2);
			spriteBatch.Draw(Utils.white, destinationRectangle, os.highlightColor * 0.2f);
			int num3 = num + noteBaseCost / 2;
			for (int j = 0; j < array.Length; j++)
			{
				spriteBatch.DrawString(GuiData.UITinyfont, array[j], new Vector2(destinationRectangle.X + 2, num3), Color.White);
				num3 += noteCostPerLine;
			}
			int num4 = 13;
			if (Button.doButton(539261 + i * 100 + os.exes.IndexOf(this) * 2000, destinationRectangle.X + destinationRectangle.Width - num4 - 1, num + noteBaseCost / 2 + 1, num4, num4, "", Color.White * 0.5f, crossTexture))
			{
				notes.RemoveAt(i);
				recalcualteRamCost();
				SaveNotesToDrive();
				i--;
			}
			if (MemoryWarningFlashTime > 0f)
			{
				float num5 = Math.Min(1f, MemoryWarningFlashTime);
				Color color = Color.Lerp(os.lockedColor, os.brightLockedColor, Utils.rand(0.4f)) * num5;
				if (4f - MemoryWarningFlashTime < 0.3f)
				{
					float num6 = 1f - (4f - MemoryWarningFlashTime) / 0.3f;
					color = Color.Lerp(color, Utils.AddativeWhite, num6 * 0.7f);
				}
				spriteBatch.Draw(Utils.white, destinationRectangle, color);
				int num7 = 60;
				int num8 = (destinationRectangle.Height - num7) / 2;
				Rectangle rectangle = new Rectangle(destinationRectangle.X, destinationRectangle.Y + num8, destinationRectangle.Width, num7);
				spriteBatch.Draw(Utils.white, rectangle, Color.Black * num5 * 0.8f);
				num7 -= 10;
				rectangle.Y += 5;
				rectangle.X += 10;
				rectangle.Width -= 20;
				rectangle.Height = num7 / 2;
				TextItem.doFontLabelToSize(rectangle, Utils.FlipRandomChars(LocaleTerms.Loc("Insufficient Memory"), 0.007000000216066837), GuiData.font, Color.White * num5);
				rectangle.Y += num7 / 2;
				TextItem.doFontLabelToSize(rectangle, Utils.FlipRandomChars(LocaleTerms.Loc("Close Notes to Free Space"), 0.007000000216066837), GuiData.font, Color.White * num5);
				Rectangle rectangle2 = new Rectangle(destinationRectangle.X + destinationRectangle.Width - num4 - 1, num + noteBaseCost / 2 + 1, num4, num4);
				spriteBatch.Draw(crossTexture, rectangle2, Color.Red * num5);
				double num9 = (1.0 + Math.Sin(os.timer)) / 2.0;
				rectangle2 = Utils.InsetRectangle(rectangle2, (int)(num9 * -20.0));
				spriteBatch.Draw(crossTexture, rectangle2, Utils.AddativeWhite * num5 * (float)(1.0 - num9));
			}
			num += num2;
		}
	}
}
