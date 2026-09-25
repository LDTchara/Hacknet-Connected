using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Hacknet;

public class SerializableConditionalActionSet
{
	public SerializableCondition Condition;

	public List<SerializableAction> Actions = new List<SerializableAction>();

	public string GetSaveString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < Actions.Count; i++)
		{
			stringBuilder.Append(Actions[i].GetSaveString());
			stringBuilder.Append("\r\n");
		}
		return Condition.GetSaveString(stringBuilder.ToString());
	}

	public static SerializableConditionalActionSet Deserialize(XmlReader rdr)
	{
		SerializableConditionalActionSet ret = new SerializableConditionalActionSet();
		Action<XmlReader, string> bodyContentReadAction = delegate(XmlReader xmlReader, string EndKeyName)
		{
			while (!rdr.EOF && (string.IsNullOrWhiteSpace(rdr.Name) || rdr.NodeType == XmlNodeType.Comment || rdr.NodeType == XmlNodeType.Whitespace))
			{
				rdr.Read();
			}
			bool flag = !xmlReader.EOF && (!(xmlReader.Name == EndKeyName) || xmlReader.IsStartElement());
			while (flag)
			{
				SerializableAction item = SerializableAction.Deserialize(xmlReader);
				ret.Actions.Add(item);
				do
				{
					xmlReader.Read();
				}
				while (xmlReader.NodeType == XmlNodeType.Whitespace || xmlReader.NodeType == XmlNodeType.Comment);
				flag = !xmlReader.EOF && (!(xmlReader.Name == EndKeyName) || xmlReader.IsStartElement());
			}
		};
		ret.Condition = SerializableCondition.Deserialize(rdr, bodyContentReadAction);
		return ret;
	}
}
