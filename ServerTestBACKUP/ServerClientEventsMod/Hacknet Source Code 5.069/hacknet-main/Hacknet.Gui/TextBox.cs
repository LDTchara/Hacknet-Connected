using System;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hacknet.Gui;

public static class TextBox
{
	private const float DELAY_BEFORE_KEY_REPEAT_START = 0.44f;

	private const float KEY_REPEAT_DELAY = 0.04f;

	private const int OUTLINE_WIDTH = 2;

	private static Keys lastHeldKey;

	private static float keyRepeatDelay = 0.44f;

	public static int LINE_HEIGHT = 25;

	public static int cursorPosition = 0;

	public static int textDrawOffsetPosition = 0;

	private static int FramesSelected = 0;

	public static bool MaskingText = false;

	public static bool BoxWasActivated = false;

	public static bool UpWasPresed = false;

	public static bool DownWasPresed = false;

	public static bool TabWasPresed = false;

	public static string doTextBox(int myID, int x, int y, int width, int lines, string str, SpriteFont font)
	{
		string text = str;
		if (font == null)
		{
			font = GuiData.smallfont;
		}
		BoxWasActivated = false;
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = lines * LINE_HEIGHT;
		if (tmpRect.Contains(GuiData.getMousePoint()))
		{
			GuiData.hot = myID;
		}
		else if (GuiData.hot == myID)
		{
			GuiData.hot = -1;
		}
		if (GuiData.mouseWasPressed())
		{
			if (GuiData.hot == myID)
			{
				if (GuiData.active == myID)
				{
					int num = GuiData.mouse.X - x;
					bool flag = false;
					for (int i = 1; i <= str.Length; i++)
					{
						if (font.MeasureString(str.Substring(0, i)).X > (float)num)
						{
							cursorPosition = i - 1;
							flag = true;
							break;
						}
						if (!flag)
						{
							cursorPosition = str.Length;
						}
					}
				}
				else
				{
					GuiData.active = myID;
					cursorPosition = str.Length;
				}
			}
			else if (GuiData.active == myID)
			{
				GuiData.active = -1;
			}
		}
		if (GuiData.active == myID)
		{
			GuiData.willBlockTextInput = true;
			text = getStringInput(text, GuiData.getKeyboadState(), GuiData.getLastKeyboadState());
			if (GuiData.getKeyboadState().IsKeyDown(Keys.Enter) && GuiData.getLastKeyboadState().IsKeyDown(Keys.Enter))
			{
				BoxWasActivated = true;
				GuiData.active = -1;
			}
		}
		FramesSelected++;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = lines * LINE_HEIGHT;
		GuiData.spriteBatch.Draw(Utils.white, tmpRect, (GuiData.active == myID) ? GuiData.Default_Lit_Backing_Color : ((GuiData.hot == myID) ? GuiData.Default_Selected_Color : GuiData.Default_Dark_Background_Color));
		tmpRect.X += 2;
		tmpRect.Y += 2;
		tmpRect.Width -= 4;
		tmpRect.Height -= 4;
		GuiData.spriteBatch.Draw(Utils.white, tmpRect, GuiData.Default_Light_Backing_Color);
		float num2 = ((float)LINE_HEIGHT - font.MeasureString(text).Y) / 2f;
		GuiData.spriteBatch.DrawString(font, text, new Vector2(x + 2, (float)y + num2), Color.White);
		if (GuiData.active == myID)
		{
			tmpRect.X = (int)((float)x + font.MeasureString(text.Substring(0, cursorPosition)).X) + 3;
			tmpRect.Y = y + 2;
			tmpRect.Width = 1;
			tmpRect.Height = LINE_HEIGHT - 4;
			GuiData.spriteBatch.Draw(Utils.white, tmpRect, (FramesSelected % 60 < 40) ? Color.White : Color.Gray);
		}
		return text;
	}

	public static string doTerminalTextField(int myID, int x, int y, int width, int selectionHeight, int lines, string str, SpriteFont font)
	{
		string s = str;
		if (font == null)
		{
			font = GuiData.smallfont;
		}
		BoxWasActivated = false;
		UpWasPresed = false;
		DownWasPresed = false;
		TabWasPresed = false;
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = 0;
		if (tmpRect.Contains(GuiData.getMousePoint()))
		{
			GuiData.hot = myID;
		}
		else if (GuiData.hot == myID)
		{
			GuiData.hot = -1;
		}
		if (GuiData.mouseWasPressed())
		{
			if (GuiData.hot == myID)
			{
				if (GuiData.active == myID)
				{
					int num = GuiData.mouse.X - x;
					bool flag = false;
					for (int i = 1; i <= str.Length; i++)
					{
						if (font.MeasureString(str.Substring(0, i)).X > (float)num)
						{
							cursorPosition = i - 1;
							flag = true;
							break;
						}
						if (!flag)
						{
							cursorPosition = str.Length;
						}
					}
				}
				else
				{
					GuiData.active = myID;
					cursorPosition = str.Length;
				}
			}
			else if (GuiData.active == myID)
			{
				GuiData.active = -1;
			}
		}
		_ = GuiData.active;
		bool flag2 = 1 == 0;
		s = getFilteredStringInput(s, GuiData.getKeyboadState(), GuiData.getLastKeyboadState());
		if (GuiData.getKeyboadState().IsKeyDown(Keys.Enter) && !GuiData.getLastKeyboadState().IsKeyDown(Keys.Enter))
		{
			BoxWasActivated = true;
			cursorPosition = 0;
			textDrawOffsetPosition = 0;
		}
		tmpRect.Height = lines * LINE_HEIGHT;
		FramesSelected++;
		tmpRect.X = x;
		tmpRect.Y = y;
		tmpRect.Width = width;
		tmpRect.Height = 10;
		tmpRect.X += 2;
		tmpRect.Y += 2;
		tmpRect.Width -= 4;
		tmpRect.Height -= 4;
		float num2 = ((float)LINE_HEIGHT - font.MeasureString(s).Y) / 2f;
		string text = s;
		int num3 = 0;
		int num4 = 0;
		string text2 = text;
		while (font.MeasureString(text2).X > (float)(width - 5))
		{
			num3++;
			int num5 = text.Length - num4 - (num3 - num4);
			if (num5 < 0)
			{
				break;
			}
			text2 = text.Substring(num4, num5);
		}
		if (cursorPosition < textDrawOffsetPosition)
		{
			textDrawOffsetPosition = Math.Max(0, textDrawOffsetPosition - 1);
		}
		while (cursorPosition > textDrawOffsetPosition + (text.Length - num3))
		{
			textDrawOffsetPosition++;
		}
		if (text.Length <= num3 || textDrawOffsetPosition < 0)
		{
			if (textDrawOffsetPosition <= text.Length - num3)
			{
				textDrawOffsetPosition = text.Length - num3;
			}
			else
			{
				textDrawOffsetPosition = 0;
			}
		}
		else if (textDrawOffsetPosition > num3)
		{
			num3 = textDrawOffsetPosition;
		}
		if (num3 > text.Length)
		{
			num3 = text.Length - 1;
		}
		if (textDrawOffsetPosition >= text.Length)
		{
			textDrawOffsetPosition = 0;
		}
		text = text.Substring(textDrawOffsetPosition, text.Length - num3);
		if (MaskingText)
		{
			string text3 = "";
			for (int i = 0; i < s.Length; i++)
			{
				text3 += "*";
			}
			text = text3;
		}
		GuiData.spriteBatch.DrawString(font, text, Utils.ClipVec2ForTextRendering(new Vector2(x + 2, (float)y + num2)), Color.White);
		_ = GuiData.active;
		flag2 = 1 == 0;
		if (s != "")
		{
			int num6 = 0;
			num6 = Math.Min(cursorPosition - textDrawOffsetPosition, text.Length);
			if (num6 <= 0)
			{
				num6 = 1;
			}
			if (text.Length == 0)
			{
				tmpRect.X = x;
			}
			else
			{
				tmpRect.X = (int)((float)x + font.MeasureString(text.Substring(0, num6)).X) + 3;
			}
		}
		else
		{
			tmpRect.X = x + 3;
		}
		tmpRect.Y = y + 2;
		tmpRect.Width = 1;
		tmpRect.Height = LINE_HEIGHT - 4;
		if (LocaleActivator.ActiveLocaleIsCJK())
		{
			tmpRect.Y += 4;
		}
		GuiData.spriteBatch.Draw(Utils.white, tmpRect, (FramesSelected % 60 < 40) ? Color.White : Color.Gray);
		return s;
	}

	private static string getStringInput(string s, KeyboardState input, KeyboardState lastInput)
	{
		Keys[] pressedKeys = input.GetPressedKeys();
		for (int i = 0; i < pressedKeys.Length; i++)
		{
			if (lastInput.IsKeyDown(pressedKeys[i]))
			{
				continue;
			}
			if (!IsSpecialKey(pressedKeys[i]))
			{
				string text = ConvertKeyToChar(pressedKeys[i], input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.CapsLock));
				string text2 = s.Substring(0, cursorPosition) + text;
				s = text2 + s.Substring(cursorPosition);
				cursorPosition++;
				continue;
			}
			switch (pressedKeys[i])
			{
			case Keys.Back:
			case Keys.Delete:
			case Keys.OemClear:
				if (s.Length > 0 && cursorPosition > 0)
				{
					string text2 = s.Substring(0, cursorPosition - 1);
					s = text2 + s.Substring(cursorPosition);
					cursorPosition--;
				}
				break;
			case Keys.Left:
				cursorPosition--;
				if (cursorPosition < 0)
				{
					cursorPosition = 0;
				}
				break;
			case Keys.Right:
				cursorPosition++;
				if (cursorPosition > s.Length)
				{
					cursorPosition = s.Length;
				}
				break;
			}
		}
		return s;
	}

	private static string getFilteredStringInput(string s, KeyboardState input, KeyboardState lastInput)
	{
		char[] filteredKeys = GuiData.getFilteredKeys();
		foreach (char c in filteredKeys)
		{
			string text = s.Substring(0, cursorPosition) + c;
			s = text + s.Substring(cursorPosition);
			cursorPosition++;
		}
		Keys[] pressedKeys = input.GetPressedKeys();
		if (pressedKeys.Length == 1 && lastInput.IsKeyDown(pressedKeys[0]))
		{
			if (pressedKeys[0] == lastHeldKey && IsSpecialKey(pressedKeys[0]))
			{
				keyRepeatDelay -= GuiData.lastTimeStep;
				if (keyRepeatDelay <= 0f)
				{
					s = forceHandleKeyPress(s, pressedKeys[0], input, lastInput);
					keyRepeatDelay = 0.04f;
				}
			}
			else
			{
				lastHeldKey = pressedKeys[0];
				keyRepeatDelay = 0.44f;
			}
		}
		else
		{
			for (int i = 0; i < pressedKeys.Length; i++)
			{
				if (lastInput.IsKeyDown(pressedKeys[i]) || !IsSpecialKey(pressedKeys[i]))
				{
					continue;
				}
				switch (pressedKeys[i])
				{
				case Keys.Delete:
					if (s.Length > 0 && cursorPosition < s.Length)
					{
						string text = s.Substring(0, cursorPosition);
						s = text + s.Substring(cursorPosition + 1);
					}
					break;
				case Keys.Back:
				case Keys.OemClear:
					if (s.Length > 0 && cursorPosition > 0)
					{
						string text = s.Substring(0, cursorPosition - 1);
						s = text + s.Substring(cursorPosition);
						cursorPosition--;
					}
					break;
				case Keys.Left:
					cursorPosition--;
					if (cursorPosition < 0)
					{
						cursorPosition = 0;
					}
					break;
				case Keys.Right:
					cursorPosition++;
					if (cursorPosition > s.Length)
					{
						cursorPosition = s.Length;
					}
					break;
				case Keys.Home:
					cursorPosition = 0;
					break;
				case Keys.End:
					cursorPosition = (cursorPosition = s.Length);
					break;
				case Keys.Up:
					UpWasPresed = true;
					break;
				case Keys.Down:
					DownWasPresed = true;
					break;
				case Keys.Tab:
					TabWasPresed = true;
					break;
				}
			}
		}
		return s;
	}

	private static string forceHandleKeyPress(string s, Keys key, KeyboardState input, KeyboardState lastInput)
	{
		if (!IsSpecialKey(key))
		{
			string text = ConvertKeyToChar(key, input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.CapsLock) || input.IsKeyDown(Keys.RightAlt));
			string text2 = s.Substring(0, cursorPosition) + text;
			s = text2 + s.Substring(cursorPosition);
			cursorPosition++;
		}
		else
		{
			switch (key)
			{
			case Keys.Back:
			case Keys.Delete:
			case Keys.OemClear:
				if (s.Length > 0 && cursorPosition > 0)
				{
					string text2 = s.Substring(0, cursorPosition - 1);
					s = text2 + s.Substring(cursorPosition);
					cursorPosition--;
				}
				break;
			case Keys.Left:
				cursorPosition--;
				if (cursorPosition < 0)
				{
					cursorPosition = 0;
				}
				break;
			case Keys.Right:
				cursorPosition++;
				if (cursorPosition > s.Length)
				{
					cursorPosition = s.Length;
				}
				break;
			case Keys.Up:
				UpWasPresed = true;
				break;
			case Keys.Down:
				DownWasPresed = true;
				break;
			case Keys.Tab:
				TabWasPresed = true;
				break;
			}
		}
		return s;
	}

	public static bool IsSpecialKey(Keys key)
	{
		if ((key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9) || key == Keys.Space || key == Keys.OemPeriod || key == Keys.OemComma || key == Keys.OemTilde || key == Keys.OemMinus || key == Keys.OemPipe || key == Keys.OemOpenBrackets || key == Keys.OemCloseBrackets || key == Keys.OemQuotes || key == Keys.OemQuestion || key == Keys.OemPlus)
		{
			return false;
		}
		return true;
	}

	public static string ConvertKeyToChar(Keys key, bool shift)
	{
		return key switch
		{
			Keys.Space => " ", 
			Keys.Enter => "\n", 
			Keys.Tab => "\t", 
			Keys.D0 => shift ? ")" : "0", 
			Keys.D1 => shift ? "!" : "1", 
			Keys.D2 => shift ? "@" : "2", 
			Keys.D3 => shift ? "#" : "3", 
			Keys.D4 => shift ? "$" : "4", 
			Keys.D5 => shift ? "%" : "5", 
			Keys.D6 => shift ? "^" : "6", 
			Keys.D7 => shift ? "&" : "7", 
			Keys.D8 => shift ? "*" : "8", 
			Keys.D9 => shift ? "(" : "9", 
			Keys.NumPad0 => "0", 
			Keys.NumPad1 => "1", 
			Keys.NumPad2 => "2", 
			Keys.NumPad3 => "3", 
			Keys.NumPad4 => "4", 
			Keys.NumPad5 => "5", 
			Keys.NumPad6 => "6", 
			Keys.NumPad7 => "7", 
			Keys.NumPad8 => "8", 
			Keys.NumPad9 => "9", 
			Keys.Add => "+", 
			Keys.Subtract => "-", 
			Keys.Multiply => "*", 
			Keys.Divide => "/", 
			Keys.Decimal => ".", 
			Keys.A => shift ? "A" : "a", 
			Keys.B => shift ? "B" : "b", 
			Keys.C => shift ? "C" : "c", 
			Keys.D => shift ? "D" : "d", 
			Keys.E => shift ? "E" : "e", 
			Keys.F => shift ? "F" : "f", 
			Keys.G => shift ? "G" : "g", 
			Keys.H => shift ? "H" : "h", 
			Keys.I => shift ? "I" : "i", 
			Keys.J => shift ? "J" : "j", 
			Keys.K => shift ? "K" : "k", 
			Keys.L => shift ? "L" : "l", 
			Keys.M => shift ? "M" : "m", 
			Keys.N => shift ? "N" : "n", 
			Keys.O => shift ? "O" : "o", 
			Keys.P => shift ? "P" : "p", 
			Keys.Q => shift ? "Q" : "q", 
			Keys.R => shift ? "R" : "r", 
			Keys.S => shift ? "S" : "s", 
			Keys.T => shift ? "T" : "t", 
			Keys.U => shift ? "U" : "u", 
			Keys.V => shift ? "V" : "v", 
			Keys.W => shift ? "W" : "w", 
			Keys.X => shift ? "X" : "x", 
			Keys.Y => shift ? "Y" : "y", 
			Keys.Z => shift ? "Z" : "z", 
			Keys.OemOpenBrackets => shift ? "{" : "[", 
			Keys.OemCloseBrackets => shift ? "}" : "]", 
			Keys.OemComma => shift ? "<" : ",", 
			Keys.OemPeriod => shift ? ">" : ".", 
			Keys.OemMinus => shift ? "_" : "-", 
			Keys.OemPlus => shift ? "+" : "=", 
			Keys.OemQuestion => shift ? "?" : "/", 
			Keys.OemSemicolon => shift ? ":" : ";", 
			Keys.OemQuotes => shift ? "\"" : "'", 
			Keys.OemPipe => shift ? "|" : "\\", 
			Keys.OemTilde => shift ? "~" : "`", 
			_ => string.Empty, 
		};
	}

	public static void moveCursorToEnd(string targetString)
	{
		cursorPosition = targetString.Length;
	}
}
