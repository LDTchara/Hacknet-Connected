using System;
using System.Collections.Generic;
using System.IO;

namespace Hacknet;

public static class LocaleTerms
{
	private const string NewlineReplacer = "[%\\n%]";

	public static Dictionary<string, string> ActiveTerms = new Dictionary<string, string>();

	public static void ReadInTerms(string termsFilepath, bool clearPreviouslyLoadedTerms = true)
	{
		char[] separator = new char[1] { '\t' };
		if (clearPreviouslyLoadedTerms)
		{
			ActiveTerms.Clear();
		}
		string[] array = File.ReadAllLines(termsFilepath);
		for (int i = 1; i < array.Length; i++)
		{
			if (array[i].StartsWith("\t"))
			{
				string[] array2 = array[i].Split(separator, StringSplitOptions.RemoveEmptyEntries);
				if (array2.Length > 1 && !ActiveTerms.ContainsKey(array2[0]) && !ActiveTerms.ContainsKey(RemoveQuotes(array2[0])))
				{
					string input = array2[1].Replace("[%\\n%]", "\n");
					ActiveTerms.Add(RemoveQuotes(array2[0]), RemoveQuotes(input));
				}
			}
		}
	}

	private static string RemoveQuotes(string input)
	{
		if (input.StartsWith("\""))
		{
			input = input.Substring(1);
		}
		if (input.EndsWith("\""))
		{
			input = input.Substring(0, input.Length - 1);
		}
		return input;
	}

	public static void ClearForEnUS()
	{
		ActiveTerms.Clear();
	}

	public static string Loc(string input)
	{
		if (Settings.ActiveLocale == "en-us")
		{
			return input;
		}
		if (ActiveTerms.ContainsKey(input))
		{
			return ActiveTerms[input];
		}
		return input;
	}
}
