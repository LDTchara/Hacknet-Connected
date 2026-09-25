using System;

namespace Hacknet;

public static class FileSanitiser
{
	public static string purifyStringForDisplay(string data)
	{
		if (data == null)
		{
			return null;
		}
		data = data.Replace("\t", "    ").Replace("“", "\"").Replace("”", "\"");
		for (int i = 0; i < data.Length; i++)
		{
			if ((data[i] < ' ' || data[i] > '\u007f') && data[i] != '\n' && data[i] != '\v' && data[i] != '\f' && data[i] != '\n')
			{
				data = data.Replace(data[i], ' ');
			}
			if (GuiData.font != null && !GuiData.font.Characters.Contains(data[i]) && data[i] != '\n')
			{
				data = data.Replace(data[i], '_');
			}
		}
		return data;
	}

	public static void purifyVehicleFile(string path)
	{
		string text = Utils.readEntireFile(path);
		text = text.Replace('\t', '#');
		text = text.Replace("\r", "");
		for (int i = 0; i < text.Length; i++)
		{
			if (!GuiData.font.Characters.Contains(text[i]) && text[i] != '\n')
			{
				text = replaceChar(text, i, '_');
			}
		}
		Utils.writeToFile(text, "SanitisedFile.txt");
	}

	public static string replaceChar(string data, int index, char replacer)
	{
		return data.Substring(0, index - 1) + replacer + data.Substring(index + 1, data.Length - index - 2);
	}

	public static void purifyNameFile(string path)
	{
		string text = Utils.readEntireFile(path);
		string[] array = text.Split(Utils.newlineDelim);
		string text2 = "";
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(Utils.spaceDelim, StringSplitOptions.RemoveEmptyEntries);
			text2 = text2 + array2[0] + "\n";
		}
		Utils.writeToFile(text2, "SanitisedNameFile.txt");
	}

	public static void purifyLocationFile(string path)
	{
		string text = Utils.readEntireFile(path);
		char[] separator = new char[1] { '\t' };
		string[] array = text.Split(Utils.newlineDelim);
		string text2 = "";
		for (int i = 1; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(separator, StringSplitOptions.RemoveEmptyEntries);
			for (int j = 1; j < array2.Length; j++)
			{
				text2 = text2 + array2[j].Trim() + "#";
			}
			text2 += "\n";
		}
		Utils.writeToFile(text2, "SanitisedLocFile.txt");
	}
}
