using System;
using System.IO;
using System.Xml;

namespace Hacknet;

public class SALoadMission : SerializableAction
{
	public string MissionName;

	public override void Trigger(object os_obj)
	{
		ComputerLoader.loadMission(Utils.GetFileLoadPrefix() + MissionName);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SALoadMission sALoadMission = new SALoadMission();
		if (rdr.MoveToAttribute("MissionName"))
		{
			sALoadMission.MissionName = rdr.ReadContentAsString();
		}
		if (sALoadMission.MissionName == null || !File.Exists(Utils.GetFileLoadPrefix() + sALoadMission.MissionName))
		{
			throw new FormatException("Invalid Mission file Path :" + sALoadMission.MissionName);
		}
		return sALoadMission;
	}
}
