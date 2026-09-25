using System.Xml;

namespace Hacknet;

public class SCOnDisconnect : SerializableCondition
{
	private bool hasHadFrameWhereThisWasFalse = false;

	public string target;

	public override bool Check(object os_obj)
	{
		if (!hasHadFrameWhereThisWasFalse)
		{
			hasHadFrameWhereThisWasFalse = true;
			return false;
		}
		bool flag = ((OS)os_obj).connectedComp == null || ((OS)os_obj).connectedComp == ((OS)os_obj).thisComputer;
		if (string.IsNullOrWhiteSpace(target) || target.ToLower() == "none")
		{
			return flag;
		}
		OS oS = (OS)os_obj;
		Computer computer = Programs.getComputer(oS, target);
		if (computer == null)
		{
			return flag;
		}
		if (oS.connectedIPLastFrame == computer.ip && (flag || (oS.connectedComp != null && oS.connectedComp.ip != computer.ip)))
		{
			return true;
		}
		return false;
	}

	public static SerializableCondition DeserializeFromReader(XmlReader rdr)
	{
		SCOnDisconnect sCOnDisconnect = new SCOnDisconnect();
		if (rdr.MoveToAttribute("target"))
		{
			sCOnDisconnect.target = rdr.ReadContentAsString();
		}
		return sCOnDisconnect;
	}
}
