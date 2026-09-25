using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class FileDeleteAllMission : MisisonGoal
{
	public Folder container;

	public Computer targetComp;

	public OS os;

	public FileDeleteAllMission(string path, string computerIP, OS _os)
	{
		os = _os;
		Computer computer = Programs.getComputer(os, computerIP);
		if (computer == null)
		{
			throw new NullReferenceException("Computer \"" + computerIP + "\" not found for FileDeletion mission goal");
		}
		targetComp = computer;
		container = computer.getFolderFromPath(path);
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		return container.files.Count == 0;
	}

	public override string TestCompletable()
	{
		return "";
	}
}
