using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class FileChangeMission : MisisonGoal
{
	public Folder container;

	public string target;

	public string targetData;

	public string targetKeyword;

	public Computer targetComp;

	public OS os;

	public bool isRemoval = false;

	public bool caseSensitive = false;

	public FileChangeMission(string path, string filename, string computerIP, string targetKeyword, OS _os, bool isRemoval = false)
	{
		target = filename;
		this.targetKeyword = targetKeyword;
		this.isRemoval = isRemoval;
		os = _os;
		Computer computer = Programs.getComputer(os, computerIP);
		if (computer == null)
		{
			throw new NullReferenceException("Computer \"" + computerIP + "\" not found for FileChange mission goal");
		}
		targetComp = computer;
		container = computer.getFolderFromPath(path);
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
		for (int i = 0; i < container.files.Count; i++)
		{
			if (!container.files[i].name.Equals(target))
			{
				continue;
			}
			string text = container.files[i].data;
			string text2 = targetKeyword;
			if (!caseSensitive)
			{
				text = text.ToLower();
				text2 = text2.ToLower();
			}
			if (text.Contains(text2))
			{
				if (isRemoval)
				{
					return false;
				}
				return true;
			}
			if (isRemoval)
			{
				return true;
			}
			return false;
		}
		if (isRemoval)
		{
			Utils.AppendToWarningsFile("FileChangeMissionGoal Error: File " + target + " was not found in the container folder \"" + container.name + "\" - defaulting to true");
			return true;
		}
		return false;
	}

	public override string TestCompletable()
	{
		string text = "";
		if (container.searchForFile(target) == null)
		{
			string text2 = text;
			text = text2 + "File to change (" + container.name + "/" + target + ") does not exist!";
		}
		return text;
	}
}
