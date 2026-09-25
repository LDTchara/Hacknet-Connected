using System.Xml;

namespace Hacknet;

public class SCInstantly : SerializableCondition
{
	public bool needsMissionComplete;

	public override bool Check(object os_obj)
	{
		if (needsMissionComplete && !((OS)os_obj).currentMission.isComplete())
		{
			return false;
		}
		return true;
	}

	public static SerializableCondition DeserializeFromReader(XmlReader rdr)
	{
		SCInstantly sCInstantly = new SCInstantly();
		if (rdr.MoveToAttribute("needsMissionComplete"))
		{
			sCInstantly.needsMissionComplete = rdr.ReadContentAsString().ToLower() == "true";
		}
		return sCInstantly;
	}
}
