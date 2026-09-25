using System;
using System.Collections.Generic;

namespace Hacknet.Security;

public static class PortRemappingSerializer
{
	private static char[] EqualsDelimiter = new char[1] { '=' };

	public static string GetSaveString(Dictionary<int, int> input)
	{
		if (input == null || input.Count == 0)
		{
			return "";
		}
		string text = "<portRemap>";
		foreach (KeyValuePair<int, int> item in input)
		{
			object obj = text;
			text = string.Concat(obj, item.Key, "=", item.Value, ",");
		}
		text = text.Substring(0, text.Length - 1);
		return text + "</portRemap>\n";
	}

	public static Dictionary<int, int> Deserialize(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return null;
		}
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		string[] array = input.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Trim().Split(EqualsDelimiter, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length >= 2)
			{
				int num = -1;
				string text = array2[0].ToLower();
				dictionary.Add(text switch
				{
					"ftp" => 21, 
					"web" => 80, 
					"ssh" => 22, 
					"smtp" => 25, 
					"sql" => 1433, 
					"medical" => 104, 
					"torrent" => 6881, 
					"ssl" => 443, 
					_ => Convert.ToInt32(text), 
				}, Convert.ToInt32(array2[1]));
			}
		}
		return dictionary;
	}
}
