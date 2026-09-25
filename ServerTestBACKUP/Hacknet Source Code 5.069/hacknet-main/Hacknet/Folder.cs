using System.Collections.Generic;
using System.Xml;

namespace Hacknet;

public class Folder : FileType
{
	private static string ampersandReplacer = "|##AMP##|";

	private static string backslashReplacer = "|##BS##|";

	private static string rightSBReplacer = "|##RSB##|";

	private static string leftSBReplacer = "|##LSB##|";

	private static string rightABReplacer = "|##RAB##|";

	private static string leftABReplacer = "|##LAB##|";

	private static string quoteReplacer = "|##QOT##|";

	private static string singlequoteReplacer = "|##SIQ##|";

	public List<FileEntry> files = new List<FileEntry>();

	public List<Folder> folders = new List<Folder>();

	public string name;

	public Folder(string foldername)
	{
		name = foldername;
	}

	public string getName()
	{
		return name;
	}

	public bool containsFile(string name, string data)
	{
		for (int i = 0; i < files.Count; i++)
		{
			if (files[i].name.Equals(name) && files[i].data.Equals(data))
			{
				return true;
			}
		}
		return false;
	}

	public bool containsFileWithData(string data)
	{
		for (int i = 0; i < files.Count; i++)
		{
			if (files[i].data.Equals(data))
			{
				return true;
			}
		}
		return false;
	}

	public bool containsFile(string name)
	{
		for (int i = 0; i < files.Count; i++)
		{
			if (files[i].name.Equals(name))
			{
				return true;
			}
		}
		return false;
	}

	public Folder searchForFolder(string folderName)
	{
		for (int i = 0; i < folders.Count; i++)
		{
			if (folders[i].name == folderName)
			{
				return folders[i];
			}
		}
		return null;
	}

	public FileEntry searchForFile(string fileName)
	{
		for (int i = 0; i < files.Count; i++)
		{
			if (files[i].name == fileName)
			{
				return files[i];
			}
		}
		return null;
	}

	public string getSaveString()
	{
		string text = "<folder name=\"" + Filter(name) + "\">\n";
		for (int i = 0; i < folders.Count; i++)
		{
			text += folders[i].getSaveString();
		}
		for (int i = 0; i < files.Count; i++)
		{
			text = text + "<file name=\"" + Filter(files[i].name) + "\">";
			text += Filter(files[i].data);
			text += "</file>\n";
		}
		return text + "</folder>\n";
	}

	public static string Filter(string s)
	{
		return s.Replace("&", ampersandReplacer).Replace("\\", backslashReplacer).Replace("[", leftSBReplacer)
			.Replace("]", rightSBReplacer)
			.Replace(">", rightABReplacer)
			.Replace("<", leftABReplacer)
			.Replace("\"", quoteReplacer)
			.Replace("'", singlequoteReplacer);
	}

	public static string deFilter(string s)
	{
		return s.Replace(ampersandReplacer, "&").Replace(backslashReplacer, "\\").Replace(rightSBReplacer, "]")
			.Replace(leftSBReplacer, "[")
			.Replace(rightABReplacer, ">")
			.Replace(leftABReplacer, "<")
			.Replace(quoteReplacer, "\"")
			.Replace(singlequoteReplacer, "'");
	}

	public static Folder load(XmlReader reader)
	{
		while (reader.Name != "folder" || reader.NodeType == XmlNodeType.EndElement)
		{
			reader.Read();
			if (reader.EOF)
			{
				return null;
			}
		}
		reader.MoveToAttribute("name");
		string s = reader.ReadContentAsString();
		s = deFilter(s);
		Folder folder = new Folder(s);
		reader.Read();
		while (reader.Name != "folder" && reader.Name != "file")
		{
			reader.Read();
			if (reader.EOF || (reader.Name == "folder" && reader.NodeType == XmlNodeType.EndElement))
			{
				return folder;
			}
		}
		while (reader.Name == "folder")
		{
			if (reader.NodeType == XmlNodeType.EndElement)
			{
				return folder;
			}
			folder.folders.Add(load(reader));
			reader.Read();
			while (reader.Name != "folder" && reader.Name != "file")
			{
				reader.Read();
				if (reader.EOF || reader.Name == "computer")
				{
					return folder;
				}
			}
		}
		while (reader.Name != "folder" && reader.Name != "file")
		{
			reader.Read();
		}
		while (reader.Name == "file" && reader.NodeType != XmlNodeType.EndElement)
		{
			reader.MoveToAttribute("name");
			string s2 = reader.ReadContentAsString();
			bool flag = true;
			if (reader.MoveToAttribute("EduSafe"))
			{
				flag = reader.ReadContentAsBoolean();
			}
			s2 = deFilter(s2);
			reader.MoveToElement();
			string s3 = reader.ReadElementContentAsString();
			s3 = deFilter(s3);
			if (flag || !Settings.EducationSafeBuild)
			{
				folder.files.Add(new FileEntry(s3, s2));
			}
			reader.Read();
			while (reader.Name != "folder" && reader.Name != "file")
			{
				reader.Read();
			}
		}
		reader.Read();
		return folder;
	}

	public void load(string data)
	{
	}

	public string TestEqualsFolder(Folder f)
	{
		string text = null;
		if (!Utils.CheckStringIsRenderable(name) || !Utils.CheckStringIsRenderable(f.name))
		{
			text = text + "Folder name includes Invalid Chars! " + f.name;
		}
		if (name != f.name)
		{
			string text2 = text;
			text = text2 + "Name Mismatch : Expected \"" + name + "\" But got \"" + f.name + "\"\r\n";
		}
		if (f.folders.Count != folders.Count)
		{
			object obj = text;
			text = string.Concat(obj, "Folder Count Mismatch : Expected \"", folders.Count, "\" But got \"", f.folders.Count, "\"\r\n");
		}
		if (f.files.Count != files.Count)
		{
			object obj = text;
			text = string.Concat(obj, "File Count Mismatch In folder \"", f.name, "\" : Expected (loaded) \"", files.Count, "\" But got (original creation) \"", f.files.Count, "\"\r\nFound Files:\r\n");
			for (int i = 0; i < f.files.Count; i++)
			{
				string text2 = text;
				text = text2 + f.files[i].name + "\r\n--------\r\n" + f.files[i].data + "\r\n#######END FILE#############\r\n\r\n";
			}
		}
		if (text != null)
		{
			return text + "Previous errors are blocking. Abandoning examination.\r\n";
		}
		for (int i = 0; i < folders.Count; i++)
		{
			string text3 = folders[i].TestEqualsFolder(f.folders[i]);
			if (text3 != null)
			{
				text = text + "\r\n" + text3;
			}
		}
		for (int i = 0; i < files.Count; i++)
		{
			if ((!Utils.CheckStringIsRenderable(files[i].name) || !Utils.CheckStringIsRenderable(f.files[i].name)) && Settings.ActiveLocale == "en-us")
			{
				text = text + "File name includes Invalid Chars! " + f.files[i].name;
			}
			if (files[i].name != f.files[i].name)
			{
				object obj = text;
				text = string.Concat(obj, "Filename Mismatch (", i, ") expected \"", files[i].name, "\" but got \"", f.files[i].name, "\"\r\n");
			}
			if (files[i].data != f.files[i].data && files[i].data.Replace("\r\n", "\n") != f.files[i].data.Replace("\r\n", "\n"))
			{
				object obj = text;
				text = string.Concat(obj, "Data Mismatch (", i, ") expected ------\r\n", files[i].data, "\r\n----- but got ------\r\n", f.files[i].data, "\r\n-----\r\n");
			}
			else if (Settings.ActiveLocale == "en-us" && Utils.CheckStringIsRenderable(files[i].data))
			{
			}
		}
		return text;
	}
}
