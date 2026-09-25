using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class GetAdminMission : MisisonGoal
{
	public Computer target;

	public OS os;

	public GetAdminMission(string compIP, OS _os)
	{
		os = _os;
		target = Programs.getComputer(os, compIP);
		if (target == null)
		{
			throw new NullReferenceException("Computer \"" + compIP + "\" not found for FileDeletion mission goal");
		}
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		return target.adminIP.Equals(os.thisComputer.ip);
	}

	public override void reset()
	{
	}
}
