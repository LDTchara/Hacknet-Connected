using System;
using System.Collections.Generic;

namespace Hacknet;

[Serializable]
public class SaveData
{
	private List<Computer> nodes;

	private ActiveMission mission;

	public void addNodes(object nodesToAdd)
	{
		nodes = (List<Computer>)nodesToAdd;
	}

	public void setMission(object missionToAdd)
	{
		mission = (ActiveMission)missionToAdd;
	}

	public object getNodes()
	{
		return nodes;
	}

	public object getMission()
	{
		return mission;
	}
}
