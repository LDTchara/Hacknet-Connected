using System;
using System.IO;

namespace Hacknet.Misc;

public class WordCounter
{
	private static string accum;

	private static int charAccum;

	public static void PerformWordCount(string[] folders, string[] fileOnlyFolders)
	{
		int num = 0;
		accum = "";
		charAccum = 0;
		for (int i = 0; i < folders.Length; i++)
		{
			string[] directories = Directory.GetDirectories(folders[i]);
			num += GetWordCountFromFolder(folders[i]);
			for (int j = 0; j < directories.Length; j++)
			{
				num += GetWordCountFromFolder(directories[j]);
			}
		}
		for (int i = 0; i < fileOnlyFolders.Length; i++)
		{
			num += GetWordCountFromFolder(fileOnlyFolders[i]);
		}
		Console.WriteLine("--------------\n\nWORD COUNT COMPLETE::\n\n");
		Console.WriteLine("Total Words: " + num + "\nTotal Chars: " + charAccum + "\n\n");
		object obj = accum;
		accum = string.Concat(obj, "\r\n---------------\r\nTotal Count: ", num, "\r\nChars: ", charAccum, "\r\n");
		File.WriteAllText("WordCount.txt", accum);
	}

	private static int GetWordCountFromFolder(string folderpath)
	{
		if (folderpath.EndsWith("Untranslated"))
		{
			return 0;
		}
		string[] files = Directory.GetFiles(folderpath);
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < files.Length; i++)
		{
			num2 = 0;
			string text = files[i];
			if (text.EndsWith(".xml"))
			{
				Console.WriteLine("Reading " + text);
				try
				{
					num2 += GetTextCountFromXMLFile(text);
					num += num2;
					accum += $"COMPLETE ({num2}) : {text}\r\n";
				}
				catch (Exception)
				{
					accum = accum + "ERROR: Could not process " + text + "\r\n";
				}
			}
			else if (text.EndsWith(".txt"))
			{
				Console.WriteLine("Reading " + text);
				string text2 = File.ReadAllText(text);
				charAccum += text2.Length;
				num2 += CountString(text2);
				num += num2;
				Console.Write("...Complete\n");
				accum += $"COMPLETE ({num2}) : {text}\r\n";
			}
		}
		return num;
	}

	private static int CountString(string input)
	{
		char[] separator = new char[7] { ' ', '.', ',', ';', ':', '\n', '\t' };
		return input.Split(separator, StringSplitOptions.RemoveEmptyEntries).Length;
	}

	private static int GetWordCountFromComputer(Computer c)
	{
		int num = 0;
		num += GetWordCountFromFolder(c.files.root);
		charAccum += c.name.Length;
		return num + CountString(c.name);
	}

	private static int GetWordCountFromFolder(Folder f)
	{
		int num = 0;
		if (f.name == "sys")
		{
			return num;
		}
		for (int i = 0; i < f.folders.Count; i++)
		{
			num += GetWordCountFromFolder(f.folders[i]);
		}
		for (int i = 0; i < f.files.Count; i++)
		{
			charAccum += f.files[i].name.Length;
			charAccum += f.files[i].data.Length;
			num += CountString(f.files[i].name);
			num += CountString(f.files[i].data);
		}
		return num;
	}

	private static int GetWordCountFromMission(ActiveMission m)
	{
		int num = 0;
		charAccum += m.email.body.Length;
		charAccum += m.email.subject.Length;
		charAccum += m.postingBody.Length;
		charAccum += m.postingTitle.Length;
		num += CountString(m.email.body);
		num += CountString(m.email.subject);
		num += CountString(m.postingBody);
		return num + CountString(m.postingTitle);
	}

	private static int GetTextCountFromXMLFile(string path)
	{
		throw new NotSupportedException("Only Supported in XNA!");
	}
}
