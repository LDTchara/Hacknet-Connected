using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class DelayMission : MisisonGoal
{
	public float time;

	private DateTime? firstRequest = null;

	public DelayMission(float time)
	{
		this.time = time;
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		if (!firstRequest.HasValue)
		{
			firstRequest = DateTime.Now;
			return false;
		}
		return (DateTime.Now - firstRequest.Value).TotalSeconds >= (double)time;
	}
}
