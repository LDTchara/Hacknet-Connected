using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Hacknet.Factions;

internal class CustomFactionAction
{
	public const string XML_ELEMENT_NAME = "Action";

	public int ValueRequiredForTrigger = 10;

	public string FlagsRequiredForTrigger = null;

	public List<SerializableAction> TriggerActions = new List<SerializableAction>();

	public static CustomFactionAction Deserialize(XmlReader rdr)
	{
		CustomFactionAction customFactionAction = new CustomFactionAction();
		while (!rdr.EOF && (!(rdr.Name == "Action") || !rdr.IsStartElement()))
		{
			rdr.Read();
		}
		if (rdr.EOF)
		{
			throw new FormatException("Expected Start element <Action> but did not find it in file!");
		}
		if (rdr.MoveToAttribute("ValueRequired"))
		{
			customFactionAction.ValueRequiredForTrigger = rdr.ReadContentAsInt();
		}
		if (rdr.MoveToAttribute("Flags"))
		{
			customFactionAction.FlagsRequiredForTrigger = rdr.ReadContentAsString();
		}
		rdr.Read();
		while (!rdr.EOF && (!(rdr.Name == "Action") || rdr.IsStartElement()))
		{
			if (string.IsNullOrWhiteSpace(rdr.Name))
			{
				rdr.Read();
				continue;
			}
			SerializableAction item = SerializableAction.Deserialize(rdr);
			customFactionAction.TriggerActions.Add(item);
			rdr.Read();
			while ((rdr.NodeType == XmlNodeType.Comment || rdr.NodeType == XmlNodeType.Whitespace || rdr.NodeType == XmlNodeType.SignificantWhitespace) && !rdr.EOF)
			{
				rdr.Read();
			}
		}
		if (rdr.EOF)
		{
			throw new FormatException("Unexpected end of file: No closing tag for </Action> Found!");
		}
		return customFactionAction;
	}

	public string GetSaveString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<Action ValueRequired=\"" + ValueRequiredForTrigger.ToString(CultureInfo.InvariantCulture) + "\" ");
		if (FlagsRequiredForTrigger != null)
		{
			stringBuilder.Append("Flags=\"" + FlagsRequiredForTrigger + "\" ");
		}
		stringBuilder.Append(">\n");
		for (int i = 0; i < TriggerActions.Count; i++)
		{
			stringBuilder.Append("\t" + TriggerActions[i].GetSaveString());
			stringBuilder.Append("\n");
		}
		stringBuilder.Append("</Action>");
		return stringBuilder.ToString();
	}

	public void Trigger(object os_obj)
	{
		for (int i = 0; i < TriggerActions.Count; i++)
		{
			TriggerActions[i].Trigger(os_obj);
		}
	}
}
