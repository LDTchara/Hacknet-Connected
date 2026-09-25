using System;
using System.Xml;

namespace Hacknet;

public class SCOnAdminGained : SerializableCondition
{
	public string target;

	public override bool Check(object os_obj)
	{
		OS oS = (OS)os_obj;
		Computer computer = Programs.getComputer(oS, target);
		if (computer == null)
		{
			return false;
		}
		return computer.adminIP == oS.thisComputer.ip;
	}

	public static SerializableCondition DeserializeFromReader(XmlReader rdr)
	{
		SCOnAdminGained sCOnAdminGained = new SCOnAdminGained();
		if (rdr.MoveToAttribute("target"))
		{
			sCOnAdminGained.target = rdr.ReadContentAsString();
		}
		if (sCOnAdminGained.target == null)
		{
			throw new FormatException("Target computer not specified in OnAdminGained condition");
		}
		return sCOnAdminGained;
	}
}
