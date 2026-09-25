using System;
using System.Xml;

namespace Hacknet;

public class SARunFunction : SerializableAction
{
	public string FunctionName;

	public int FunctionValue = 0;

	public float Delay = 0f;

	public string DelayHost;

	public override void Trigger(object os_obj)
	{
		if (Delay <= 0f)
		{
			MissionFunctions.runCommand(FunctionValue, FunctionName);
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
		SARunFunction sARunFunction = new SARunFunction();
		if (rdr.MoveToAttribute("FunctionName"))
		{
			sARunFunction.FunctionName = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("FunctionValue"))
		{
			sARunFunction.FunctionValue = rdr.ReadContentAsInt();
		}
		if (rdr.MoveToAttribute("Delay"))
		{
			sARunFunction.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sARunFunction.DelayHost = rdr.ReadContentAsString();
		}
		if (string.IsNullOrWhiteSpace(sARunFunction.FunctionName))
		{
			throw new FormatException("Invalid function name :" + sARunFunction.FunctionName);
		}
		return sARunFunction;
	}
}
