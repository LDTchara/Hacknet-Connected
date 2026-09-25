using System;
using System.Xml;

namespace Hacknet;

public class SAAddConditionalActions : SerializableAction
{
	public string Filepath;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		if (Delay <= 0f)
		{
			RunnableConditionalActions.LoadIntoOS(Filepath, os_obj);
			return;
		}
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, DelayHost);
		if (computer == null)
		{
			throw new NullReferenceException(string.Concat("Computer ", computer, " could not be found as DelayHost for Function"));
		}
		float delay = Delay;
		Delay = -1f;
		DelayableActionSystem.FindDelayableActionSystemOnComputer(computer).AddAction(this, delay);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAAddConditionalActions sAAddConditionalActions = new SAAddConditionalActions();
		if (rdr.MoveToAttribute("Filepath"))
		{
			sAAddConditionalActions.Filepath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("Delay"))
		{
			sAAddConditionalActions.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAAddConditionalActions.DelayHost = rdr.ReadContentAsString();
		}
		if (string.IsNullOrWhiteSpace(sAAddConditionalActions.Filepath))
		{
			throw new FormatException("Invalid Filepath");
		}
		return sAAddConditionalActions;
	}
}
