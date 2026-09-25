using System.Collections.Generic;

namespace Hacknet.Mission;

internal class GetAdminPasswordStringMission : MisisonGoal
{
	public Computer target;

	public OS os;

	public GetAdminPasswordStringMission(string compIP, OS _os)
	{
		os = _os;
		target = Programs.getComputer(os, compIP);
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		if (additionalDetails == null)
		{
			return false;
		}
		for (int i = 0; i < additionalDetails.Count; i++)
		{
			if (additionalDetails[i].Contains(target.adminPass))
			{
				return true;
			}
		}
		return false;
	}
}
