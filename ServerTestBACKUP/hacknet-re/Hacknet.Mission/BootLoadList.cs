using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

public static class BootLoadList
{
	public static List<string> getList()
	{
		string text = "";
		if (Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed)
		{
			text = text + "\r\n" + Utils.readEntireFile("Content/DLC/DLCBootList.txt");
		}
		text += Utils.readEntireFile("Content/Computers/BootLoadList.txt");
		return getListFromData(text);
	}

	public static List<string> getDLCList()
	{
		string data = Utils.readEntireFile("Content/DLC/DLCBootList.txt");
		return getListFromData(data);
	}

	public static List<string> getDemoList()
	{
		string data = Utils.readEntireFile("Content/Computers/DemoLoadList.txt");
		return getListFromData(data);
	}

	public static List<string> getAdventureList()
	{
		string data = Utils.readEntireFile("Content/AdventureNetwork/AdventureLoadList.txt");
		return getListFromData(data);
	}

	private static List<string> getListFromData(string data)
	{
		string[] separator = new string[2] { "\n\r", "\r\n" };
		string[] array = data.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		List<string> list = new List<string>();
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].StartsWith("#") && array[i].Length > 1)
			{
				list.Add(array[i]);
			}
		}
		return list;
	}
}
