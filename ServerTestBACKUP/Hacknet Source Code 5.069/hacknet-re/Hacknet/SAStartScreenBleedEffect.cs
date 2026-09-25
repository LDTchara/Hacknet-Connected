using System;
using System.Collections.Generic;
using System.Xml;

namespace Hacknet;

public class SAStartScreenBleedEffect : SerializableAction
{
	[XMLContent]
	public string ContentLines;

	public string AlertTitle;

	public string CompleteAction;

	public float TotalDurationSeconds = 200f;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (Delay <= 0f)
		{
			string[] collection = ContentLines.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
			List<string> list = new List<string>(collection);
			while (list.Count < 3)
			{
				list.Add("");
			}
			oS.EffectsUpdater.StartScreenBleed(TotalDurationSeconds, AlertTitle, list[0], list[1], list[2], CompleteAction);
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
		SAStartScreenBleedEffect sAStartScreenBleedEffect = new SAStartScreenBleedEffect();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAStartScreenBleedEffect.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAStartScreenBleedEffect.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("AlertTitle"))
		{
			sAStartScreenBleedEffect.AlertTitle = ComputerLoader.filter(rdr.ReadContentAsString());
		}
		if (rdr.MoveToAttribute("CompleteAction"))
		{
			sAStartScreenBleedEffect.CompleteAction = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TotalDurationSeconds"))
		{
			sAStartScreenBleedEffect.TotalDurationSeconds = (float)rdr.ReadContentAsDouble();
		}
		rdr.MoveToContent();
		sAStartScreenBleedEffect.ContentLines = ComputerLoader.filter(rdr.ReadElementContentAsString());
		return sAStartScreenBleedEffect;
	}
}
