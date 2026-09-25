using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.UIUtils;

public static class GetStringUIControl
{
	public static void StartGetString(string prompt, object os_obj)
	{
		((OS)os_obj).execute("getString " + prompt);
	}

	public static string DrawGetStringControl(string prompt, Rectangle bounds, Action errorOccurs, Action cancelled, SpriteBatch sb, object os_obj, Color SearchButtonColor, Color CancelButtonColor, string upperPrompt = null, Color? BackingPanelColor = null)
	{
		OS oS = (OS)os_obj;
		upperPrompt = ((upperPrompt == null) ? prompt : upperPrompt);
		string text = "";
		int num = bounds.X + 6;
		int num2 = bounds.Y + 2;
		Vector2 zero = Vector2.Zero;
		if (upperPrompt.Length > 0)
		{
			num2 += (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), upperPrompt, null).Y + 10;
		}
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		string[] array = oS.getStringCache.Split(separator, StringSplitOptions.None);
		if (array.Length > 1)
		{
			text = array[1];
			if (text.Equals(""))
			{
				text = oS.terminal.currentLine;
			}
		}
		Rectangle destinationRectangle = new Rectangle(num, num2, bounds.Width - 12, bounds.Height - 46);
		sb.Draw(Utils.white, destinationRectangle, BackingPanelColor.HasValue ? BackingPanelColor.Value : oS.darkBackgroundColor);
		num2 += 18;
		string bracketedBit = "";
		string text2 = Utils.ExtractBracketedSection(prompt, out bracketedBit);
		zero = TextItem.doMeasuredSmallLabel(new Vector2(num, num2), text2 + text, null);
		TextItem.doSmallLabel(new Vector2(num, (float)num2 + zero.Y + 2f), bracketedBit, null);
		destinationRectangle.X = num + (int)zero.X + 2;
		destinationRectangle.Y = num2;
		destinationRectangle.Width = 7;
		destinationRectangle.Height = 20;
		if (oS.timer % 1f < 0.3f)
		{
			sb.Draw(Utils.white, destinationRectangle, oS.outlineColor);
		}
		num2 += bounds.Height - 44;
		if (array.Length > 2 || Button.doButton(30, num, num2, 300, 22, LocaleTerms.Loc("Search"), SearchButtonColor))
		{
			if (array.Length <= 2)
			{
				oS.terminal.executeLine();
			}
			if (text.Length > 0)
			{
				return text;
			}
			errorOccurs();
			return null;
		}
		if (Button.doButton(38, num, num2 + 24, 300, 22, LocaleTerms.Loc("Cancel"), CancelButtonColor))
		{
			cancelled();
			oS.terminal.clearCurrentLine();
			oS.terminal.executeLine();
		}
		return null;
	}

	public static void DrawGetStringControlInactive(string prompt, string valueText, Rectangle bounds, SpriteBatch sb, object os_obj, string upperPrompt = null)
	{
		OS oS = (OS)os_obj;
		upperPrompt = ((upperPrompt == null) ? prompt : upperPrompt);
		int num = bounds.X + 6;
		int num2 = bounds.Y + 2;
		num2 += (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), upperPrompt, null).Y + 10;
		Rectangle destinationRectangle = new Rectangle(num, num2, bounds.Width - 12, bounds.Height - 46);
		sb.Draw(Utils.white, destinationRectangle, oS.darkBackgroundColor);
		num2 += 28;
		destinationRectangle.X = num + (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), prompt + valueText, null).X + 2;
		destinationRectangle.Y = num2;
		destinationRectangle.Width = 7;
		destinationRectangle.Height = 20;
		num2 += bounds.Height - 44;
	}
}
