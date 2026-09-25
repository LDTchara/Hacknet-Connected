using System;
using System.Xml;

namespace Hacknet;

public class SAHideNode : SerializableAction
{
	public string TargetComp;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (Delay <= 0f)
		{
			Computer computer = Programs.getComputer(oS, TargetComp);
			if (computer != null)
			{
				do
				{
					oS.netMap.visibleNodes.Remove(oS.netMap.nodes.IndexOf(computer));
				}
				while (oS.netMap.visibleNodes.Contains(oS.netMap.nodes.IndexOf(computer)));
			}
			return;
		}
		Computer computer2 = Programs.getComputer(oS, DelayHost);
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
		SAHideNode sAHideNode = new SAHideNode();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAHideNode.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAHideNode.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAHideNode.TargetComp = rdr.ReadContentAsString();
		}
		return sAHideNode;
	}
}
