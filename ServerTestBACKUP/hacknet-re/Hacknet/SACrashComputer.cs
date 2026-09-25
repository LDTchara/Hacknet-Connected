using System;
using System.Xml;

namespace Hacknet;

public class SACrashComputer : SerializableAction
{
	public string TargetComp;

	public string CrashSource;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, TargetComp);
		if (computer == null && !OS.DEBUG_COMMANDS)
		{
			return;
		}
		if (Delay <= 0f)
		{
			computer.crash(CrashSource);
			return;
		}
		Computer computer2 = Programs.getComputer(os, DelayHost);
		if (computer2 == null)
		{
			throw new NullReferenceException(string.Concat("Computer ", computer2, " could not be found as DelayHost for Function"));
		}
		float delay = Delay;
		Delay = -1f;
		DelayableActionSystem.FindDelayableActionSystemOnComputer(computer2).AddAction(this, delay);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SACrashComputer sACrashComputer = new SACrashComputer();
		if (rdr.MoveToAttribute("Delay"))
		{
			sACrashComputer.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sACrashComputer.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("CrashSource"))
		{
			sACrashComputer.CrashSource = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sACrashComputer.DelayHost = rdr.ReadContentAsString();
		}
		return sACrashComputer;
	}
}
