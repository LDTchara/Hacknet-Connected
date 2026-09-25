using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content;

namespace Hacknet;

public class FileEntry : FileType
{
	public static List<string> filenames;

	public static List<string> fileData;

	public string name;

	public string data;

	public int size;

	public int secondCreatedAt;

	public FileEntry()
	{
		int index = Utils.random.Next(0, filenames.Count - 1);
		name = filenames[index];
		data = fileData[index];
		size = data.Length * 8;
		secondCreatedAt = (int)OS.currentElapsedTime;
	}

	public FileEntry(string dataEntry, string nameEntry)
	{
		nameEntry = nameEntry.Replace(" ", "_");
		name = nameEntry;
		data = dataEntry;
		size = data.Length * 8;
		secondCreatedAt = (int)OS.currentElapsedTime;
	}

	public string head()
	{
		int i = 0;
		string text = "";
		for (; i < data.Length && data[i] != '\n' && i < 50; i++)
		{
			text += data[i];
		}
		return text;
	}

	public string getName()
	{
		return name;
	}

	public static void init(ContentManager content)
	{
		filenames = new List<string>(128);
		fileData = new List<string>(128);
		DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(content.RootDirectory, "files"));
		FileInfo[] files = directoryInfo.GetFiles("*.*");
		for (int i = 0; i < files.Length; i++)
		{
			filenames.Add(Path.GetFileNameWithoutExtension(files[i].Name));
			string filename = "Content/files/" + Path.GetFileName(files[i].Name);
			fileData.Add(Utils.readEntireFile(filename));
		}
		string text = "";
		string filename2 = (Settings.EducationSafeBuild ? "Content/BashLogs_StudentSafe.txt" : "Content/BashLogs.txt");
		text = Utils.readEntireFile(filename2);
		string[] separator = new string[1] { "\n#" };
		string[] array = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Trim();
			int num = array[i].Length - array[i].IndexOf("\r\n");
			filenames.Add("IRC_Log:" + array[i].Substring(0, array[i].IndexOf("\r\n")).Replace("- [X]", "").Replace(" ", ""));
			string text2 = array[i].Substring(array[i].IndexOf("\r\n")).Replace("\n ", "\n");
			if (Settings.ActiveLocale == "en-us")
			{
				text2 = FileSanitiser.purifyStringForDisplay(text2);
			}
			fileData.Add(text2 + "\n\nArchived Via : http://Bash.org");
		}
	}
}
