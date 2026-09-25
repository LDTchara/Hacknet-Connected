using System;
using System.Xml;

namespace Hacknet;

public class SAChangeIP : SerializableAction
{
	public string TargetComp;

	public string NewIP;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		if (Delay <= 0f)
		{
			Computer computer = Programs.getComputer(os, TargetComp);
			if (computer != null)
			{
				if (string.IsNullOrWhiteSpace(NewIP) || NewIP.StartsWith("#RANDOM"))
				{
					NewIP = NetworkMap.generateRandomIP();
				}
				computer.ip = NewIP;
			}
		}
		else
		{
			Computer computer2 = Programs.getComputer(os, DelayHost);
			if (computer2 == null)
			{
				throw new NullReferenceException(string.Concat("Computer ", computer2, " could not be found as DelayHost for Function"));
			}
			float delay = Delay;
			Delay = -1f;
			DelayableActionSystem.FindDelayableActionSystemOnComputer(computer2).AddAction(this, delay);
		}
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAChangeIP sAChangeIP = new SAChangeIP();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAChangeIP.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAChangeIP.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAChangeIP.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("NewIP"))
		{
			sAChangeIP.NewIP = rdr.ReadContentAsString();
		}
		return sAChangeIP;
	}
}
