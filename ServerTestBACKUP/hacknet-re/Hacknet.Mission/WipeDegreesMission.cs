using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class WipeDegreesMission : MisisonGoal
{
	public AcademicDatabaseDaemon database;

	private string ownerName;

	public WipeDegreesMission(string targetName, OS _os)
	{
		WipeDegreesMission wipeDegreesMission = this;
		ownerName = targetName;
		Action init = null;
		init = delegate
		{
			if (_os.netMap.academicDatabase == null)
			{
				_os.delayer.Post(ActionDelayer.NextTick(), init);
			}
			else
			{
				wipeDegreesMission.database = (AcademicDatabaseDaemon)_os.netMap.academicDatabase.getDaemon(typeof(AcademicDatabaseDaemon));
			}
		};
		init();
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		return !database.hasDegrees(ownerName);
	}
}
