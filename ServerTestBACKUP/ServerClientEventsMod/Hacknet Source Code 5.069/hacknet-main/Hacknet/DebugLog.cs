using System.Collections.Generic;

namespace Hacknet;

public static class DebugLog
{
	public static char[] delimiters = new char[2] { '\n', '\r' };

	public static List<string> data = new List<string>(64);

	public static void add(string s)
	{
		string[] array = s.Split(delimiters);
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].Equals(""))
			{
				data.Add(array[i]);
			}
		}
	}

	public static string GetDump()
	{
		string text = "";
		for (int i = 0; i < data.Count; i++)
		{
			text = text + data[i] + "\r\n";
		}
		return text;
	}
}
