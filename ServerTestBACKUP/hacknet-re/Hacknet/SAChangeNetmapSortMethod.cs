using System;
using System.Xml;

namespace Hacknet;

public class SAChangeNetmapSortMethod : SerializableAction
{
	public string Method;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (Delay <= 0f)
		{
			NetmapSortingAlgorithm sortingAlgorithm = NetmapSortingAlgorithm.Grid;
			switch (Method.ToLower())
			{
			case "scatter":
				sortingAlgorithm = NetmapSortingAlgorithm.Scatter;
				break;
			case "grid":
				sortingAlgorithm = NetmapSortingAlgorithm.Grid;
				break;
			case "chaos":
				sortingAlgorithm = NetmapSortingAlgorithm.Chaos;
				break;
			case "scangrid":
			case "seqgrid":
			case "sequencegrid":
			case "sequence grid":
				sortingAlgorithm = NetmapSortingAlgorithm.LockGrid;
				break;
			}
			oS.netMap.SortingAlgorithm = sortingAlgorithm;
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
		SAChangeNetmapSortMethod sAChangeNetmapSortMethod = new SAChangeNetmapSortMethod();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAChangeNetmapSortMethod.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAChangeNetmapSortMethod.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("Method"))
		{
			sAChangeNetmapSortMethod.Method = rdr.ReadContentAsString();
		}
		return sAChangeNetmapSortMethod;
	}
}
