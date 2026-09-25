using System;
using System.Xml;

namespace Hacknet;

public class SAAppendToFile : SerializableAction
{
	[XMLContent]
	public string DataToAdd;

	public string TargetComp;

	public string TargetFolderpath;

	public string TargetFilename;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		if (Delay <= 0f)
		{
			Computer computer = Programs.getComputer(os, TargetComp);
			Folder folderAtPath = Programs.getFolderAtPath(TargetFolderpath, os, computer.files.root, returnsNullOnNoFind: true);
			if (folderAtPath != null)
			{
				FileEntry fileEntry = folderAtPath.searchForFile(TargetFilename);
				fileEntry.data = fileEntry.data + "\n" + DataToAdd;
			}
			return;
		}
		Computer computer2 = Programs.getComputer(os, DelayHost);
		if (computer2 == null)
		{
			throw new NullReferenceException(string.Concat("Computer ", computer2, " could not be found as DelayHost for Function"));
		}
		float delay = Delay;
		Delay = -1f;
		DelayableActionSystem.FindDelayableActionSystemOnComputer(computer2).AddAction(this, delay);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAAppendToFile sAAppendToFile = new SAAppendToFile();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAAppendToFile.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAAppendToFile.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAAppendToFile.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetFolderpath"))
		{
			sAAppendToFile.TargetFolderpath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetFilename"))
		{
			sAAppendToFile.TargetFilename = rdr.ReadContentAsString();
		}
		rdr.MoveToContent();
		sAAppendToFile.DataToAdd = ComputerLoader.filter(rdr.ReadElementContentAsString());
		return sAAppendToFile;
	}
}
