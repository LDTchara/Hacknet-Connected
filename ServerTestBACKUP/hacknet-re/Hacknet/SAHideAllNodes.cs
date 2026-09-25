using System;
using System.Xml;

namespace Hacknet;

public class SAHideAllNodes : SerializableAction
{
	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		if (Delay <= 0f)
		{
			OS oS = (OS)os_obj;
			oS.netMap.visibleNodes.Clear();
			return;
		}
		Computer computer = Programs.getComputer((OS)os_obj, DelayHost);
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
		SAHideAllNodes sAHideAllNodes = new SAHideAllNodes();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAHideAllNodes.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAHideAllNodes.DelayHost = rdr.ReadContentAsString();
		}
		return sAHideAllNodes;
	}
}
