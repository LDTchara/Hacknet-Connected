using System;
using System.Xml;

namespace Hacknet;

public class SACancelScreenBleedEffect : SerializableAction
{
	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (Delay <= 0f)
		{
			oS.EffectsUpdater.CancelScreenBleedEffect();
			return;
		}
		Computer computer = Programs.getComputer(oS, DelayHost);
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
		SACancelScreenBleedEffect sACancelScreenBleedEffect = new SACancelScreenBleedEffect();
		if (rdr.MoveToAttribute("Delay"))
		{
			sACancelScreenBleedEffect.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sACancelScreenBleedEffect.DelayHost = rdr.ReadContentAsString();
		}
		return sACancelScreenBleedEffect;
	}
}
