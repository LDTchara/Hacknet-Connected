using System;
using System.Collections.Generic;
using System.Xml;

namespace Hacknet;

public class SAAddMissionToHubServer : SerializableAction
{
	public string MissionFilepath;

	public string TargetComp;

	public string AssignmentTag;

	public bool StartsComplete;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		Computer computer = Programs.getComputer(oS, TargetComp);
		if (computer == null)
		{
			throw new NullReferenceException("Computer " + TargetComp + " could not be found for SAAddMissionToHubServer Function, adding mission: " + MissionFilepath);
		}
		if (computer.getDaemon(typeof(MissionHubServer)) is MissionHubServer missionHubServer)
		{
			missionHubServer.AddMissionToListings(Utils.GetFileLoadPrefix() + MissionFilepath);
			return;
		}
		if (computer.getDaemon(typeof(DLCHubServer)) is DLCHubServer dLCHubServer)
		{
			dLCHubServer.AddMission(Utils.GetFileLoadPrefix() + MissionFilepath, AssignmentTag, StartsComplete);
			return;
		}
		if (computer.getDaemon(typeof(MissionListingServer)) is MissionListingServer missionListingServer)
		{
			List<ActiveMission> branchMissions = oS.branchMissions;
			ActiveMission m = (ActiveMission)ComputerLoader.readMission(Utils.GetFileLoadPrefix() + MissionFilepath);
			oS.branchMissions = branchMissions;
			missionListingServer.addMisison(m, AssignmentTag.ToLower() == "top");
			return;
		}
		throw new NullReferenceException("Computer " + TargetComp + " does not contain a MissionHubServer, MissionListingServer or DLCHubServer daemon for addMission function adding mission: " + MissionFilepath);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAAddMissionToHubServer sAAddMissionToHubServer = new SAAddMissionToHubServer();
		if (rdr.MoveToAttribute("MissionFilepath"))
		{
			sAAddMissionToHubServer.MissionFilepath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAAddMissionToHubServer.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("AssignmentTag"))
		{
			sAAddMissionToHubServer.AssignmentTag = rdr.ReadContentAsString();
		}
		if (string.IsNullOrWhiteSpace(sAAddMissionToHubServer.AssignmentTag))
		{
			sAAddMissionToHubServer.AssignmentTag = null;
		}
		if (rdr.MoveToAttribute("StartsComplete"))
		{
			sAAddMissionToHubServer.StartsComplete = rdr.ReadContentAsString().ToLower() == "true";
		}
		if (string.IsNullOrWhiteSpace(sAAddMissionToHubServer.MissionFilepath))
		{
			throw new FormatException("Invalid MissionFilepath");
		}
		if (string.IsNullOrWhiteSpace(sAAddMissionToHubServer.TargetComp))
		{
			throw new FormatException("Invalid TargetComp");
		}
		return sAAddMissionToHubServer;
	}
}
