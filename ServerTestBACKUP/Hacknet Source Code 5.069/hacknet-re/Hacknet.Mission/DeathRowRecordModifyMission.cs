using System.Collections.Generic;

namespace Hacknet.Mission;

internal class DeathRowRecordModifyMission : MisisonGoal
{
	public Folder container;

	public string fname;

	public string lname;

	public Computer deathRowDatabase;

	public OS os;

	public string lastWords;

	public DeathRowRecordModifyMission(string firstName, string lastName, string lastWords, OS _os)
	{
		os = _os;
		fname = firstName;
		lname = lastName;
		this.lastWords = lastWords;
		container = (deathRowDatabase = Programs.getComputer(os, "deathRow")).getFolderFromPath("dr_database/Records");
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		DeathRowDatabaseDaemon deathRowDatabaseDaemon = (DeathRowDatabaseDaemon)deathRowDatabase.getDaemon(typeof(DeathRowDatabaseDaemon));
		DeathRowDatabaseDaemon.DeathRowEntry recordForName = deathRowDatabaseDaemon.GetRecordForName(fname, lname);
		if (recordForName.RecordNumber != null)
		{
			if (lastWords != null)
			{
				string text = recordForName.Statement.ToLower().Replace("\r", "").Replace(",", "")
					.Replace(".", "");
				string text2 = lastWords.ToLower().Replace("\r", "").Replace(",", "")
					.Replace(".", "");
				if (text.Contains(text2) || text == text2 || text.StartsWith(text2))
				{
					return true;
				}
			}
			return true;
		}
		return false;
	}
}
