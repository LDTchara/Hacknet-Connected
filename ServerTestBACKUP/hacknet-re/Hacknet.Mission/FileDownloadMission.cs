using System.Collections.Generic;

namespace Hacknet.Mission;

internal class FileDownloadMission : FileDeletionMission
{
	public FileDownloadMission(string path, string filename, string computerIP, OS os)
		: base(path, filename, computerIP, os)
	{
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		Folder root = os.thisComputer.files.root;
		for (int i = 0; i < root.folders.Count; i++)
		{
			if (root.folders[i].containsFileWithData(targetData))
			{
				return true;
			}
		}
		return false;
	}

	public override string TestCompletable()
	{
		string text = "";
		if (container.searchForFile(target) == null)
		{
			string text2 = text;
			text = text2 + "File to download (" + container.name + "/" + target + ") does not exist!";
		}
		return text;
	}
}
