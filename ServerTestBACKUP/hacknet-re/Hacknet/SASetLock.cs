using System;
using System.Xml;

namespace Hacknet;

public class SASetLock : SerializableAction
{
	public string DelayHost;

	public float Delay;

	public string Module;

	public bool IsLocked = false;

	public bool IsHidden = false;

	public override void Trigger(object os_obj)
	{
		if (Delay <= 0f)
		{
			OS oS = (OS)os_obj;
			switch (Module.ToLower())
			{
			case "terminal":
				oS.terminal.inputLocked = IsLocked;
				oS.terminal.visible = !IsHidden;
				break;
			case "netmap":
				oS.netMap.inputLocked = IsLocked;
				oS.netMap.visible = !IsHidden;
				break;
			case "ram":
				oS.ram.inputLocked = IsLocked;
				oS.ram.visible = !IsHidden;
				break;
			case "display":
				oS.display.inputLocked = IsLocked;
				oS.display.visible = !IsHidden;
				break;
			}
		}
		else
		{
			Computer computer = Programs.getComputer((OS)os_obj, DelayHost);
			if (computer == null)
			{
				throw new NullReferenceException(string.Concat("Computer ", computer, " could not be found as DelayHost for Function"));
			}
			float delay = Delay;
			Delay = -1f;
			DelayableActionSystem.FindDelayableActionSystemOnComputer(computer).AddAction(this, delay);
		}
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SASetLock sASetLock = new SASetLock();
		if (rdr.MoveToAttribute("Delay"))
		{
			sASetLock.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sASetLock.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("Module"))
		{
			sASetLock.Module = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("IsLocked"))
		{
			sASetLock.IsLocked = rdr.ReadContentAsString().ToLower().StartsWith("t");
		}
		if (rdr.MoveToAttribute("IsHidden"))
		{
			sASetLock.IsHidden = rdr.ReadContentAsString().ToLower().StartsWith("t");
		}
		return sASetLock;
	}
}
