using System;
using System.Xml;

namespace Hacknet;

public class SARemoveMissionFromHubServer : SerializableAction
{
	public string MissionFilepath;

	public string TargetComp;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, TargetComp);
		if (computer == null)
		{
			throw new NullReferenceException("Computer " + TargetComp + " could not be found for SARemoveMissionFromHubServer Function, removing mission: " + MissionFilepath);
		}
		if (computer.getDaemon(typeof(MissionHubServer)) is MissionHubServer missionHubServer)
		{
			missionHubServer.RemoveMissionFromListings(MissionFilepath);
			return;
		}
		if (computer.getDaemon(typeof(DLCHubServer)) is DLCHubServer dLCHubServer)
		{
			dLCHubServer.RemoveMission(MissionFilepath);
			return;
		}
		if (computer.getDaemon(typeof(MissionListingServer)) is MissionListingServer missionListingServer)
		{
			missionListingServer.removeMission(MissionFilepath);
			return;
		}
		throw new NullReferenceException("Computer " + TargetComp + " does not contain a MissionHubServer, MissionListingServer or DLCHubServer daemon for remove mission function adding mission: " + MissionFilepath);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SARemoveMissionFromHubServer sARemoveMissionFromHubServer = new SARemoveMissionFromHubServer();
		if (rdr.MoveToAttribute("MissionFilepath"))
		{
			sARemoveMissionFromHubServer.MissionFilepath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sARemoveMissionFromHubServer.TargetComp = rdr.ReadContentAsString();
		}
		if (string.IsNullOrWhiteSpace(sARemoveMissionFromHubServer.MissionFilepath))
		{
			throw new FormatException("Invalid MissionFilepath");
		}
		if (string.IsNullOrWhiteSpace(sARemoveMissionFromHubServer.TargetComp))
		{
			throw new FormatException("Invalid TargetComp");
		}
		return sARemoveMissionFromHubServer;
	}
}
