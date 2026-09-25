using System;
using System.Xml;

namespace Hacknet;

public class SAShowNode : SerializableAction
{
	public string DelayHost;

	public float Delay;

	public string Target;

	public override void Trigger(object os_obj)
	{
		if (Delay <= 0f)
		{
			OS oS = (OS)os_obj;
			Computer computer = Programs.getComputer(oS, Target);
			if (computer != null)
			{
				oS.netMap.discoverNode(computer);
			}
			else if (OS.DEBUG_COMMANDS)
			{
				oS.write("Error revealing node " + Target + " : NODE NOT FOUND");
			}
		}
		else
		{
			Computer computer2 = Programs.getComputer((OS)os_obj, DelayHost);
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
		SAShowNode sAShowNode = new SAShowNode();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAShowNode.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAShowNode.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("Target"))
		{
			sAShowNode.Target = rdr.ReadContentAsString();
		}
		return sAShowNode;
	}
}
