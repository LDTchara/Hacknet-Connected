using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Hacknet;

public class RunnableConditionalActions
{
	public const string SerializationKey = "ConditionalActions";

	public List<SerializableConditionalActionSet> Actions = new List<SerializableConditionalActionSet>();

	private bool IsUpdating = false;

	public virtual void Update(float dt, object os)
	{
		IsUpdating = true;
		for (int i = 0; i < Actions.Count; i++)
		{
			if (Actions[i].Condition.Check(os))
			{
				SerializableConditionalActionSet serializableConditionalActionSet = Actions[i];
				Actions.RemoveAt(i);
				i--;
				for (int j = 0; j < serializableConditionalActionSet.Actions.Count; j++)
				{
					serializableConditionalActionSet.Actions[j].Trigger(os);
				}
			}
		}
		IsUpdating = false;
	}

	public string GetSaveString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<ConditionalActions>\r\n");
		for (int i = 0; i < Actions.Count; i++)
		{
			stringBuilder.Append(Actions[i].GetSaveString() + "\r\n");
		}
		stringBuilder.Append("</ConditionalActions>");
		return stringBuilder.ToString();
	}

	public static RunnableConditionalActions Deserialize(XmlReader rdr)
	{
		while (!rdr.EOF && rdr.Name != "ConditionalActions" && !rdr.IsStartElement())
		{
			rdr.Read();
		}
		if (rdr.EOF)
		{
			throw new FormatException("Unexpected end of file trying to deserialize Runnable Conditional Actions!");
		}
		RunnableConditionalActions runnableConditionalActions = new RunnableConditionalActions();
		rdr.Read();
		bool flag = false;
		while (true)
		{
			if (!rdr.EOF && (rdr.IsEmptyElement || string.IsNullOrWhiteSpace(rdr.Name)))
			{
				rdr.Read();
				continue;
			}
			if (rdr.EOF)
			{
				throw new FormatException("Unexpected end of file trying to deserialize Runnable Conditional Actions!");
			}
			flag = ((!(rdr.Name == "ConditionalActions") || rdr.IsStartElement()) ? true : false);
			if (flag)
			{
				SerializableConditionalActionSet item = SerializableConditionalActionSet.Deserialize(rdr);
				runnableConditionalActions.Actions.Add(item);
				rdr.Read();
			}
			if (!flag)
			{
				break;
			}
		}
		return runnableConditionalActions;
	}

	public static void LoadIntoOS(string filepath, object OSobj)
	{
		OS oS = (OS)OSobj;
		using (FileStream input = File.OpenRead(LocalizedFileLoader.GetLocalizedFilepath(Utils.GetFileLoadPrefix() + filepath)))
		{
			XmlReader rdr = XmlReader.Create(input);
			RunnableConditionalActions runnableConditionalActions = Deserialize(rdr);
			for (int i = 0; i < runnableConditionalActions.Actions.Count; i++)
			{
				oS.ConditionalActions.Actions.Add(runnableConditionalActions.Actions[i]);
			}
		}
		if (!oS.ConditionalActions.IsUpdating)
		{
			oS.ConditionalActions.Update(0f, oS);
		}
	}
}
