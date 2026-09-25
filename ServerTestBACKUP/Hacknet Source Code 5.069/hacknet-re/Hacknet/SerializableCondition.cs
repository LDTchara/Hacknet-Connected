using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Hacknet;

public abstract class SerializableCondition
{
	public abstract bool Check(object os_obj);

	public string GetSaveString(string bodyContent)
	{
		Type type = GetType();
		string text = type.Name;
		if (text.StartsWith("Hacknet."))
		{
			text = text.Substring("Hacknet.".Length);
		}
		if (text.StartsWith("SC"))
		{
			text = text.Substring("SC".Length);
		}
		StringBuilder stringBuilder = new StringBuilder("<" + text + " ");
		FieldInfo[] fields = type.GetFields();
		for (int i = 0; i < fields.Length; i++)
		{
			stringBuilder.Append(fields[i].Name + "=\"");
			stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}", new object[1] { fields[i].GetValue(this) });
			stringBuilder.Append("\" ");
		}
		stringBuilder.Append(">\r\n");
		stringBuilder.Append(bodyContent);
		stringBuilder.Append("\r\n</" + text + ">");
		return stringBuilder.ToString();
	}

	public static SerializableCondition Deserialize(XmlReader rdr, Action<XmlReader, string> bodyContentReadAction)
	{
		Dictionary<string, Func<XmlReader, SerializableCondition>> dictionary = new Dictionary<string, Func<XmlReader, SerializableCondition>>();
		dictionary.Add("OnAdminGained", SCOnAdminGained.DeserializeFromReader);
		dictionary.Add("OnConnect", SCOnConnect.DeserializeFromReader);
		dictionary.Add("HasFlags", SCHasFlags.DeserializeFromReader);
		dictionary.Add("Instantly", SCInstantly.DeserializeFromReader);
		dictionary.Add("OnDisconnect", SCOnDisconnect.DeserializeFromReader);
		while (!rdr.EOF && (!rdr.IsStartElement() || !dictionary.ContainsKey(rdr.Name)))
		{
			rdr.Read();
		}
		if (rdr.EOF)
		{
			throw new FormatException("Unexpected end of file!");
		}
		string name = rdr.Name;
		SerializableCondition result = dictionary[rdr.Name](rdr);
		rdr.Read();
		bodyContentReadAction?.Invoke(rdr, name);
		rdr.Read();
		return result;
	}
}
