using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class Terminal : CoreModule
{
	public static float PROMPT_OFFSET = 0f;

	private List<string> history;

	private List<string> runCommands = new List<string>();

	private int commandHistoryOffset;

	public string currentLine;

	public string lastRunCommand;

	public string prompt;

	public bool usingTabExecution = false;

	public bool preventingExecution = false;

	public bool executionPreventionIsInteruptable = false;

	private Color outlineColor = new Color(68, 68, 68);

	private Color backColor = new Color(8, 8, 8);

	private Color historyTextColor = new Color(220, 220, 220);

	private Color currentTextColor = Color.White;

	public Terminal(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
	}

	public override void LoadContent()
	{
		history = new List<string>(512);
		runCommands = new List<string>(512);
		commandHistoryOffset = 0;
		currentLine = "";
		lastRunCommand = "";
		prompt = "> ";
	}

	public override void Update(float t)
	{
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		float tinyFontCharHeight = GuiData.ActiveFontConfig.tinyFontCharHeight;
		spriteBatch.Draw(Utils.white, bounds, os.displayModuleExtraLayerBackingColor);
		int num = (int)((float)(bounds.Height - 12) / (tinyFontCharHeight + 1f));
		num -= 3;
		num = Math.Min(num, history.Count);
		Vector2 input = new Vector2(bounds.X + 4, (float)(bounds.Y + bounds.Height) - tinyFontCharHeight * 5f);
		if (num > 0)
		{
			for (int num2 = history.Count; num2 > history.Count - num; num2--)
			{
				try
				{
					spriteBatch.DrawString(GuiData.tinyfont, history[num2 - 1], Utils.ClipVec2ForTextRendering(input), os.terminalTextColor);
					input.Y -= tinyFontCharHeight + 1f;
				}
				catch (Exception)
				{
				}
			}
		}
		doGui();
	}

	public void executeLine()
	{
		string text = currentLine;
		if (TextBox.MaskingText)
		{
			text = "";
			for (int i = 0; i < currentLine.Length; i++)
			{
				text += "*";
			}
		}
		history.Add(prompt + text);
		lastRunCommand = currentLine;
		runCommands.Add(currentLine);
		if (!preventingExecution)
		{
			commandHistoryOffset = 0;
			os.execute(currentLine);
			if (currentLine.Length > 0)
			{
				StatsManager.IncrementStat("commands_run", 1);
			}
		}
		currentLine = "";
		TextBox.cursorPosition = 0;
		TextBox.textDrawOffsetPosition = 0;
		executionPreventionIsInteruptable = false;
	}

	public string GetRecentTerminalHistoryString()
	{
		string text = "";
		int num = history.Count - 1;
		while (num > history.Count - 30 && history.Count > num)
		{
			text = text + history[num] + "\r\n";
			num--;
		}
		return text;
	}

	public List<string> GetRecentTerminalHistoryList()
	{
		List<string> list = new List<string>();
		for (int i = 0; i < 30 && i < history.Count; i++)
		{
			list.Add(history[i]);
		}
		return list;
	}

	public void NonThreadedInstantExecuteLine()
	{
		string text = currentLine;
		if (TextBox.MaskingText)
		{
			text = "";
			for (int i = 0; i < currentLine.Length; i++)
			{
				text += "*";
			}
		}
		history.Add(prompt + text);
		lastRunCommand = currentLine;
		runCommands.Add(currentLine);
		if (!preventingExecution)
		{
			commandHistoryOffset = 0;
			ProgramRunner.ExecuteProgram(os, currentLine.Split(' '));
		}
		currentLine = "";
		TextBox.cursorPosition = 0;
		TextBox.textDrawOffsetPosition = 0;
		executionPreventionIsInteruptable = false;
	}

	public void doGui()
	{
		SpriteFont tinyfont = GuiData.tinyfont;
		float tinyFontCharHeight = GuiData.ActiveFontConfig.tinyFontCharHeight;
		int num = -4;
		int num2 = (int)((float)(bounds.Y + bounds.Height - 16) - tinyFontCharHeight - (float)num);
		int num3 = (int)tinyfont.MeasureString(prompt).X;
		if (bounds.Width > 0)
		{
			while (num3 >= (int)((double)bounds.Width * 0.7))
			{
				prompt = prompt.Substring(1);
				num3 = (int)tinyfont.MeasureString(prompt).X;
			}
		}
		spriteBatch.DrawString(tinyfont, prompt, new Vector2(bounds.X + 3, num2), currentTextColor);
		if (LocaleActivator.ActiveLocaleIsCJK())
		{
			num -= 4;
		}
		num2 += num;
		if (!os.inputEnabled || inputLocked)
		{
			return;
		}
		TextBox.LINE_HEIGHT = (int)(tinyFontCharHeight + 15f);
		currentLine = TextBox.doTerminalTextField(7001, bounds.X + 3 + (int)PROMPT_OFFSET + (int)tinyfont.MeasureString(prompt).X, num2, bounds.Width - num3 - 4, bounds.Height, 1, currentLine, tinyfont);
		if (TextBox.BoxWasActivated)
		{
			executeLine();
		}
		if (TextBox.UpWasPresed && runCommands.Count > 0)
		{
			commandHistoryOffset++;
			if (commandHistoryOffset > runCommands.Count)
			{
				commandHistoryOffset = runCommands.Count;
			}
			currentLine = runCommands[runCommands.Count - commandHistoryOffset];
			TextBox.cursorPosition = currentLine.Length;
		}
		if (TextBox.DownWasPresed && commandHistoryOffset > 0)
		{
			commandHistoryOffset--;
			if (commandHistoryOffset < 0)
			{
				commandHistoryOffset = 0;
			}
			if (commandHistoryOffset <= 0)
			{
				currentLine = "";
			}
			else
			{
				currentLine = runCommands[runCommands.Count - commandHistoryOffset];
			}
			TextBox.cursorPosition = currentLine.Length;
		}
		if (TextBox.TabWasPresed)
		{
			if (usingTabExecution)
			{
				executeLine();
			}
			else
			{
				doTabComplete();
			}
		}
	}

	public void doTabComplete()
	{
		List<string> list = new List<string>();
		if (currentLine.Length == 0)
		{
			return;
		}
		int num = currentLine.IndexOf(' ');
		if (num >= 1)
		{
			string text = currentLine.Substring(num + 1);
			string text2 = currentLine.Substring(0, num);
			if (text2.Equals("upload") || text2.Equals("up"))
			{
				int num2 = text.LastIndexOf('/');
				if (num2 < 0)
				{
					num2 = 0;
				}
				string text3 = text.Substring(0, num2) + "/";
				if (text3.StartsWith("/"))
				{
					text3 = text3.Substring(1);
				}
				string text4 = text.Substring(num2 + ((num2 != 0) ? 1 : 0));
				Folder folder = Programs.getFolderAtPathAsFarAsPossible(text, os, os.thisComputer.files.root);
				bool flag = false;
				if (folder == os.thisComputer.files.root && text3.Length > 1)
				{
					flag = true;
				}
				if (folder == null)
				{
					folder = os.thisComputer.files.root;
				}
				if (!flag)
				{
					for (int i = 0; i < folder.folders.Count; i++)
					{
						if (folder.folders[i].name.ToLower().StartsWith(text4.ToLower(), StringComparison.InvariantCultureIgnoreCase))
						{
							list.Add(text2 + " " + text3 + folder.folders[i].name + "/");
						}
					}
					for (int i = 0; i < folder.files.Count; i++)
					{
						if (folder.files[i].name.ToLower().StartsWith(text4.ToLower()))
						{
							list.Add(text2 + " " + text3 + folder.files[i].name);
						}
					}
				}
			}
			else
			{
				if (text == null || ((text.Equals("") || text.Length < 1) && !text2.Equals("exe")))
				{
					return;
				}
				Folder folder = Programs.getCurrentFolder(os);
				for (int i = 0; i < folder.folders.Count; i++)
				{
					if (folder.folders[i].name.StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
					{
						list.Add(text2 + " " + folder.folders[i].name + "/");
					}
				}
				for (int i = 0; i < folder.files.Count; i++)
				{
					if (folder.files[i].name.StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
					{
						list.Add(text2 + " " + folder.files[i].name);
					}
				}
				if (list.Count == 0)
				{
					for (int i = 0; i < folder.files.Count; i++)
					{
						if (folder.files[i].name.StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
						{
							list.Add(text2 + " " + folder.files[i].name);
						}
					}
				}
			}
		}
		else
		{
			List<string> list2 = new List<string>();
			list2.AddRange(ProgramList.programs);
			list2.AddRange(ProgramList.getExeList(os));
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i].ToLower().StartsWith(currentLine.ToLower()))
				{
					list.Add(list2[i]);
				}
			}
		}
		if (list.Count == 1)
		{
			currentLine = list[0];
			TextBox.moveCursorToEnd(currentLine);
		}
		else
		{
			if (list.Count <= 1)
			{
				return;
			}
			os.write(prompt + currentLine);
			string text5 = list[0];
			for (int i = 0; i < list.Count; i++)
			{
				os.write(list[i]);
				for (int j = 0; j < text5.Length; j++)
				{
					if (list[i].Length <= j || string.Concat(text5[j]).ToLowerInvariant()[0] != string.Concat(list[i][j]).ToLowerInvariant()[0])
					{
						text5 = text5.Substring(0, j);
						break;
					}
				}
				currentLine = text5;
				TextBox.moveCursorToEnd(currentLine);
			}
		}
	}

	public void writeLine(string text)
	{
		text = Utils.SuperSmartTwimForWidth(text, bounds.Width - 6, GuiData.tinyfont);
		string[] array = text.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			history.Add(array[i]);
		}
	}

	public void write(string text)
	{
		if (history.Count <= 0 || GuiData.tinyfont.MeasureString(history[history.Count - 1] + text).X > (float)(bounds.Width - 6))
		{
			writeLine(text);
		}
		else
		{
			history[history.Count - 1] += text;
		}
	}

	public void clearCurrentLine()
	{
		currentLine = "";
		TextBox.cursorPosition = 0;
		TextBox.textDrawOffsetPosition = 0;
	}

	public void reset()
	{
		history.Clear();
		clearCurrentLine();
	}

	public int commandsRun()
	{
		return runCommands.Count;
	}

	public string getLastRunCommand()
	{
		return lastRunCommand;
	}
}
