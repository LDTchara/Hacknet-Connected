using System.Collections.Generic;

namespace Hacknet.Mission;

internal class GetStringMission : MisisonGoal
{
	public string target;

	public GetStringMission(string targetData)
	{
		target = targetData;
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		if (additionalDetails == null)
		{
			return false;
		}
		for (int i = 0; i < additionalDetails.Count; i++)
		{
			if (additionalDetails[i].ToLower().Contains(target.ToLower()))
			{
				return true;
			}
		}
		return false;
	}
}
