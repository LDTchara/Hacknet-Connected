using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Hacknet;

public class MemoryContents
{
	private const string EncryptionPass = "19474-217316293";

	private const string FileHeader = "MEMORY_DUMP : FORMAT v1.22 ----------\n\n";

	public List<string> DataBlocks = new List<string>();

	public List<string> CommandsRun = new List<string>();

	public List<KeyValuePair<string, string>> FileFragments = new List<KeyValuePair<string, string>>();

	public List<string> Images = new List<string>();

	public string GetSaveString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<Memory>\r\n");
		if (DataBlocks != null && DataBlocks.Count > 0)
		{
			stringBuilder.Append("<Data>");
			for (int i = 0; i < DataBlocks.Count; i++)
			{
				stringBuilder.Append("<Block>" + Folder.Filter(DataBlocks[i]) + "</Block>\r\n");
			}
			stringBuilder.Append("</Data>\r\n");
		}
		if (CommandsRun != null && CommandsRun.Count > 0)
		{
			stringBuilder.Append("<Commands>");
			for (int i = 0; i < CommandsRun.Count; i++)
			{
				stringBuilder.Append("<Command>" + Folder.Filter(CommandsRun[i]) + "</Command>\r\n");
			}
			stringBuilder.Append("</Commands>\r\n");
		}
		if (FileFragments != null && FileFragments.Count > 0)
		{
			stringBuilder.Append("<FileFragments>");
			for (int i = 0; i < CommandsRun.Count; i++)
			{
				stringBuilder.Append("<File name=\"" + Folder.Filter(FileFragments[i].Key) + "\">" + Folder.Filter(FileFragments[i].Value) + "</Command>\r\n");
			}
			stringBuilder.Append("</FileFragments>\r\n");
		}
		if (Images != null && Images.Count > 0)
		{
			stringBuilder.Append("<Images>");
			for (int i = 0; i < Images.Count; i++)
			{
				stringBuilder.Append("<Image>" + Folder.Filter(Images[i]) + "</Image>\r\n");
			}
			stringBuilder.Append("</Images>\r\n");
		}
		stringBuilder.Append("</Memory>");
		return stringBuilder.ToString();
	}

	public static MemoryContents Deserialize(XmlReader rdr)
	{
		MemoryContents ret = new MemoryContents();
		while (rdr.Name != "Memory")
		{
			rdr.Read();
			if (rdr.EOF)
			{
				throw new FormatException("Unexpected end of file looking for start of Memory tag");
			}
		}
		do
		{
			rdr.Read();
			if (rdr.Name == "Memory" && !rdr.IsStartElement())
			{
				return ret;
			}
			Utils.ProcessXmlElementInParent(rdr, "Commands", "Command", delegate
			{
				rdr.MoveToContent();
				string text = rdr.ReadElementContentAsString();
				if (text.Contains("\n"))
				{
					string[] array = text.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
					for (int i = 0; i < array.Length; i++)
					{
						if (string.IsNullOrEmpty(array[i]))
						{
							array[i] = " ";
						}
						ret.CommandsRun.Add(ComputerLoader.filter(Folder.deFilter(array[i])));
					}
				}
				else
				{
					ret.CommandsRun.Add(ComputerLoader.filter(Folder.deFilter(text)));
				}
			});
			Utils.ProcessXmlElementInParent(rdr, "Data", "Block", delegate
			{
				rdr.MoveToContent();
				string s4 = rdr.ReadElementContentAsString();
				ret.DataBlocks.Add(ComputerLoader.filter(Folder.deFilter(s4)));
			});
			Utils.ProcessXmlElementInParent(rdr, "FileFragments", "File", delegate
			{
				string s2 = "UNKNOWN";
				if (rdr.MoveToAttribute("name"))
				{
					s2 = rdr.ReadContentAsString();
				}
				rdr.MoveToContent();
				string s3 = rdr.ReadElementContentAsString();
				ret.FileFragments.Add(new KeyValuePair<string, string>(Folder.deFilter(s2), Folder.deFilter(s3)));
			});
			Utils.ProcessXmlElementInParent(rdr, "Images", "Image", delegate
			{
				rdr.MoveToContent();
				string s = rdr.ReadElementContentAsString();
				ret.Images.Add(Folder.deFilter(s));
			});
		}
		while (!rdr.EOF);
		throw new FormatException("Unexpected end of file trying to deserialize memory contents!");
	}

	private string GetCompactSaveString()
	{
		string saveString = GetSaveString();
		return saveString.Replace("Commands>", "CM>").Replace("Command>", "c>").Replace("Data>", "D>")
			.Replace("Block>", "b>")
			.Replace("FileFragments>", "FF>")
			.Replace("File>", "f>")
			.Replace("Memory>", "M>")
			.Replace("Images>", "Is>")
			.Replace("Image>", "i>");
	}

	private static string ReExpandSaveString(string save)
	{
		save = save.Replace("CM>", "Commands>\r\n").Replace("</c>", "</Command>\r\n").Replace("c>", "Command>")
			.Replace("D>", "Data>\r\n")
			.Replace("</b>", "</Block>\r\n")
			.Replace("b>", "Block>")
			.Replace("FF>", "FileFragments>\r\n")
			.Replace("f>", "File>\r\n")
			.Replace("M>", "Memory>\r\n")
			.Replace("Is>", "Images>")
			.Replace("i>", "Image>");
		return save;
	}

	public string GetEncodedFileString()
	{
		string compactSaveString = GetCompactSaveString();
		compactSaveString = FileEncrypter.EncryptString(compactSaveString, "MEMORY DUMP", "------", "19474-217316293");
		return "MEMORY_DUMP : FORMAT v1.22 ----------\n\n" + Computer.generateBinaryString(512).Substring(0, 400) + "\n\n" + compactSaveString;
	}

	public static MemoryContents GetMemoryFromEncodedFileString(string data)
	{
		string data2 = data.Substring("MEMORY_DUMP : FORMAT v1.22 ----------\n\n".Length + 400 + 2);
		string save = FileEncrypter.DecryptString(data2, "19474-217316293")[2];
		string s = ReExpandSaveString(save);
		using Stream input = Utils.GenerateStreamFromString(s);
		XmlReader rdr = XmlReader.Create(input);
		return Deserialize(rdr);
	}

	public string TestEqualsWithErrorReport(MemoryContents other)
	{
		string text = "";
		if (other == null)
		{
			return "Other memory object is null!";
		}
		if (other.DataBlocks.Count == DataBlocks.Count)
		{
			for (int i = 0; i < DataBlocks.Count; i++)
			{
				if (other.DataBlocks[i] != DataBlocks[i])
				{
					object obj = text;
					text = string.Concat(obj, "Data block difference for item ", i, " - mismatch");
				}
			}
		}
		else
		{
			object obj = text;
			text = string.Concat(obj, "Datablock count difference - found ", other.DataBlocks.Count, " - expected ", DataBlocks.Count);
		}
		if (other.CommandsRun.Count == CommandsRun.Count)
		{
			for (int i = 0; i < CommandsRun.Count; i++)
			{
				if (other.CommandsRun[i] != CommandsRun[i])
				{
					object obj = text;
					text = string.Concat(obj, "\n\nCommandsRun difference for item ", i, " - mismatch.\nFound ", other.CommandsRun[i], "  :vs:  ", CommandsRun[i], "\n");
				}
			}
		}
		else
		{
			object obj = text;
			text = string.Concat(obj, "CommandsRun count difference - found ", other.CommandsRun.Count, " - expected ", CommandsRun.Count);
		}
		if (other.FileFragments.Count == FileFragments.Count)
		{
			for (int i = 0; i < FileFragments.Count; i++)
			{
				if (other.FileFragments[i].Key != FileFragments[i].Key)
				{
					object obj = text;
					text = string.Concat(obj, "FileFragments difference for item ", i, " - key mismatch");
				}
				if (other.FileFragments[i].Value != FileFragments[i].Value)
				{
					object obj = text;
					text = string.Concat(obj, "FileFragments difference for item ", i, " - Value mismatch");
				}
			}
		}
		else
		{
			object obj = text;
			text = string.Concat(obj, "FileFragments count difference - found ", other.FileFragments.Count, " - expected ", FileFragments.Count);
		}
		if (other.Images.Count == Images.Count)
		{
			for (int i = 0; i < Images.Count; i++)
			{
				if (other.Images[i] != Images[i])
				{
					object obj = text;
					text = string.Concat(obj, "\n\nImages difference for item ", i, " - mismatch.\nFound ", other.Images[i], "  :vs:  ", Images[i], "\n");
				}
			}
		}
		else
		{
			object obj = text;
			text = string.Concat(obj, "Images count difference - found ", other.Images.Count, " - expected ", Images.Count);
		}
		return text;
	}
}
