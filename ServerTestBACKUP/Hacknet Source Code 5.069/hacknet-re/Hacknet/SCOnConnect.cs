using System;
using System.Xml;

namespace Hacknet;

public class SCOnConnect : SerializableCondition
{
	public string target;

	public bool needsMissionComplete;

	public string requiredFlags;

	public override bool Check(object os_obj)
	{
		OS oS = (OS)os_obj;
		Computer computer = Programs.getComputer(oS, target);
		if (computer == null)
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(requiredFlags))
		{
			string[] array = requiredFlags.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				if (!oS.Flags.HasFlag(array[i]))
				{
					return false;
				}
			}
		}
		if ((!needsMissionComplete || (oS.currentMission != null && oS.currentMission.isComplete())) && oS.connectedComp != null && oS.connectedComp.ip == computer.ip)
		{
			return true;
		}
		return false;
	}

	public static SerializableCondition DeserializeFromReader(XmlReader rdr)
	{
		SCOnConnect sCOnConnect = new SCOnConnect();
		if (rdr.MoveToAttribute("target"))
		{
			sCOnConnect.target = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("needsMissionComplete"))
		{
			sCOnConnect.needsMissionComplete = rdr.ReadContentAsString().ToLower() == "true";
		}
		if (rdr.MoveToAttribute("requiredFlags"))
		{
			sCOnConnect.requiredFlags = rdr.ReadContentAsString();
		}
		if (sCOnConnect.target == null)
		{
			throw new FormatException("Target computer not specified in OnConnect condition");
		}
		return sCOnConnect;
	}
}
