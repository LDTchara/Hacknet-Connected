using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class FileUploadMission : MisisonGoal
{
	public Folder container;

	public string target;

	public string targetData;

	public Computer targetComp;

	public Computer uploadTargetComp;

	public OS os;

	public Folder destinationFolder;

	public FileUploadMission(string path, string filename, string computerWithFileIP, string computerToUploadToIP, string destToUploadToPath, OS _os, bool needsDecrypt = false, string decryptPass = "")
	{
		target = filename;
		os = _os;
		Computer computer = Programs.getComputer(os, computerWithFileIP);
		if (computer == null)
		{
			throw new FormatException("Error parsing File Upload Mission - Source comp " + computerWithFileIP + " not found!");
		}
		targetComp = computer;
		container = computer.getFolderFromPath(path);
		for (int i = 0; i < container.files.Count; i++)
		{
			if (container.files[i].name.Equals(target))
			{
				targetData = container.files[i].data;
				if (needsDecrypt)
				{
					targetData = FileEncrypter.DecryptString(targetData, decryptPass)[2];
				}
			}
		}
		Computer computer2 = Programs.getComputer(os, computerToUploadToIP);
		if (computer2 == null)
		{
			throw new FormatException("Error parsing File Upload Mission - Dest comp " + computerWithFileIP + " not found!");
		}
		uploadTargetComp = computer2;
		destinationFolder = computer2.getFolderFromPath(destToUploadToPath);
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		for (int i = 0; i < destinationFolder.files.Count; i++)
		{
			if (targetData == null)
			{
				if (destinationFolder.files[i].name.ToLower().Equals(target.ToLower()))
				{
					return true;
				}
			}
			else if (destinationFolder.files[i].data.Equals(targetData))
			{
				return true;
			}
		}
		bool flag = 0 == 0;
		return false;
	}

	public override string TestCompletable()
	{
		string text = "";
		if (container.searchForFile(target) == null)
		{
			string text2 = text;
			text = text2 + "File to upload (" + container.name + "/" + target + ") does not exist!";
		}
		return text;
	}
}
