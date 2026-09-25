using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Hacknet;

public class ProgressionFlags
{
	private List<string> Flags = new List<string>();

	public bool HasFlag(string flag)
	{
		return Flags.Contains(flag);
	}

	public void AddFlag(string flag)
	{
		if (!HasFlag(flag))
		{
			Flags.Add(flag);
		}
	}

	public void RemoveFlag(string flag)
	{
		Flags.Remove(flag);
	}

	public string GetFlagStartingWith(string start)
	{
		for (int i = 0; i < Flags.Count; i++)
		{
			if (Flags[i].StartsWith(start))
			{
				return Flags[i];
			}
		}
		return null;
	}

	public void Load(XmlReader rdr)
	{
		Flags.Clear();
		while (!(rdr.Name == "Flags") || !rdr.IsStartElement())
		{
			rdr.Read();
			if (rdr.EOF)
			{
				throw new InvalidOperationException("XML reached End of file too fast!");
			}
		}
		rdr.MoveToContent();
		string text = rdr.ReadElementContentAsString();
		string[] array = text.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Replace("[%%COMMAREPLACED%%]", ",");
			if (text2 == "décrypté")
			{
				text2 = "decypher";
			}
			Flags.Add(text2);
		}
	}

	public string GetSaveString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < Flags.Count; i++)
		{
			stringBuilder.Append(Flags[i].Replace(",", "[%%COMMAREPLACED%%]"));
			stringBuilder.Append(",");
		}
		if (stringBuilder.Length > 0)
		{
			stringBuilder.Remove(stringBuilder.Length - 1, 1);
		}
		return "<Flags>" + stringBuilder.ToString() + "</Flags>\r\n";
	}
}
