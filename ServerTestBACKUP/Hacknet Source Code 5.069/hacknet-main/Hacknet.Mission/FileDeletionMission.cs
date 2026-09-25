using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class FileDeletionMission : MisisonGoal
{
	public Folder container;

	public string target;

	public string targetData;

	public string targetComp;

	public string targetPath;

	public OS os;

	public FileDeletionMission(string path, string filename, string computerIP, OS _os)
	{
		target = filename;
		os = _os;
		targetComp = computerIP;
		Computer computer = Programs.getComputer(os, targetComp);
		if (computer == null && Settings.IsInExtensionMode)
		{
			throw new NullReferenceException("Computer \"" + computerIP + "\" was not found for File Deletion mission goal!");
		}
		targetPath = path;
		if (computer == null)
		{
			return;
		}
		container = computer.getFolderFromPath(path);
		if (container == null)
		{
			throw new NullReferenceException("Folder " + path + " was not found on computer " + computerIP + " for file deletion mission goal");
		}
		for (int i = 0; i < container.files.Count; i++)
		{
			if (container.files[i].name.Equals(target))
			{
				targetData = container.files[i].data;
			}
		}
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		Computer computer = Programs.getComputer(os, targetComp);
		if (computer == null)
		{
			return true;
		}
		container = computer.getFolderFromPath(targetPath);
		if (container == null)
		{
			return true;
		}
		for (int i = 0; i < container.files.Count; i++)
		{
			if (container.files[i].name.Equals(target) && (container.files[i].data.Equals(targetData) || targetData == null))
			{
				return false;
			}
		}
		return true;
	}

	public override string TestCompletable()
	{
		string text = "";
		if (container.searchForFile(target) == null)
		{
			string text2 = text;
			text = text2 + "File to delete (" + container.name + "/" + target + ") does not exist!";
		}
		return text;
	}
}
