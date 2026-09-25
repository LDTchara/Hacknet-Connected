using System.Collections.Generic;

namespace Hacknet.Mission;

internal class DeathRowRecordRemovalMission : MisisonGoal
{
	public Folder container;

	public string fname;

	public string lname;

	public Computer deathRowDatabase;

	public OS os;

	public DeathRowRecordRemovalMission(string firstName, string lastName, OS _os)
	{
		os = _os;
		fname = firstName;
		lname = lastName;
		container = (deathRowDatabase = Programs.getComputer(os, "deathRow")).getFolderFromPath("dr_database/Records");
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		DeathRowDatabaseDaemon deathRowDatabaseDaemon = (DeathRowDatabaseDaemon)deathRowDatabase.getDaemon(typeof(DeathRowDatabaseDaemon));
		return !deathRowDatabaseDaemon.ContainsRecordForName(fname, lname);
	}
}
