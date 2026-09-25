using System;
using System.Xml;

namespace Hacknet;

public class SCHasFlags : SerializableCondition
{
	public string requiredFlags;

	public override bool Check(object os_obj)
	{
		OS oS = (OS)os_obj;
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
		return true;
	}

	public static SerializableCondition DeserializeFromReader(XmlReader rdr)
	{
		SCHasFlags sCHasFlags = new SCHasFlags();
		if (rdr.MoveToAttribute("requiredFlags"))
		{
			sCHasFlags.requiredFlags = rdr.ReadContentAsString();
		}
		return sCHasFlags;
	}
}
