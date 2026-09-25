using System;
using System.Xml;

namespace Hacknet;

public class SCDoesNotHaveFlags : SerializableCondition
{
	public string Flags;

	public override bool Check(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (!string.IsNullOrWhiteSpace(Flags))
		{
			string[] array = Flags.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				if (oS.Flags.HasFlag(array[i]))
				{
					return false;
				}
			}
		}
		return true;
	}

	public static SerializableCondition DeserializeFromReader(XmlReader rdr)
	{
		SCDoesNotHaveFlags sCDoesNotHaveFlags = new SCDoesNotHaveFlags();
		if (rdr.MoveToAttribute("Flags"))
		{
			sCDoesNotHaveFlags.Flags = rdr.ReadContentAsString();
		}
		return sCDoesNotHaveFlags;
	}
}
