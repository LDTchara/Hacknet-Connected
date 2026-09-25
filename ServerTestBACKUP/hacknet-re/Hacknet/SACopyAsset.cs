using System;
using System.Xml;

namespace Hacknet;

public class SACopyAsset : SerializableAction
{
	public string DestFileName;

	public string DestFilePath;

	public string DestComp;

	public string SourceComp;

	public string SourceFileName;

	public string SourceFilePath;

	public int FunctionValue = 0;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, DestComp);
		Computer computer2 = Programs.getComputer(os, SourceComp);
		if (computer == null)
		{
			throw new NullReferenceException(string.Concat("Destination Computer ", computer, " could not be found for SACopyAsset, copying file: ", SourceFileName));
		}
		if (computer2 == null)
		{
			throw new NullReferenceException(string.Concat("Source Computer ", computer2, " could not be found for SACopyAsset, copying file: ", SourceFileName));
		}
		Folder folderAtPath = Programs.getFolderAtPath(SourceFilePath, os, computer2.files.root, returnsNullOnNoFind: true);
		if (folderAtPath == null)
		{
			throw new NullReferenceException("Source Folder " + SourceFilePath + " could not be found for SACopyAsset, adding file: " + SourceFileName);
		}
		Folder folderFromPath = computer.getFolderFromPath(DestFilePath, createFoldersThatDontExist: true);
		if (folderFromPath != null)
		{
			FileEntry fileEntry = folderAtPath.searchForFile(SourceFileName);
			if (fileEntry != null)
			{
				FileEntry item = new FileEntry(fileEntry.data, DestFileName);
				folderFromPath.files.Add(item);
			}
		}
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SACopyAsset sACopyAsset = new SACopyAsset();
		if (rdr.MoveToAttribute("DestFileName"))
		{
			sACopyAsset.DestFileName = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("DestFilePath"))
		{
			sACopyAsset.DestFilePath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("DestComp"))
		{
			sACopyAsset.DestComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("SourceComp"))
		{
			sACopyAsset.SourceComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("SourceFileName"))
		{
			sACopyAsset.SourceFileName = rdr.ReadContentAsString();
			if (string.IsNullOrWhiteSpace(sACopyAsset.DestFileName))
			{
				sACopyAsset.DestFileName = sACopyAsset.SourceFileName;
			}
		}
		if (rdr.MoveToAttribute("SourceFilePath"))
		{
			sACopyAsset.SourceFilePath = rdr.ReadContentAsString();
		}
		return sACopyAsset;
	}
}
