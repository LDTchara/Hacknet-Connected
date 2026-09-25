using System;
using System.Xml;

namespace Hacknet;

public class SAKillExe : SerializableAction
{
	public string ExeName;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (Delay <= 0f)
		{
			if (ExeName == "*")
			{
				ExeName = "";
			}
			for (int i = 0; i < oS.exes.Count; i++)
			{
				if (oS.exes[i].name.ToLower().Contains(ExeName.ToLower()))
				{
					oS.exes[i].isExiting = true;
				}
			}
		}
		else
		{
			Computer computer = Programs.getComputer(oS, DelayHost);
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
		SAKillExe sAKillExe = new SAKillExe();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAKillExe.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAKillExe.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("ExeName"))
		{
			sAKillExe.ExeName = rdr.ReadContentAsString();
		}
		return sAKillExe;
	}
}
