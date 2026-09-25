using System;
using System.Xml;

namespace Hacknet;

public class SAAddAsset : SerializableAction
{
	public string FileName;

	public string FileContents;

	public string TargetComp;

	public string TargetFolderpath;

	public int FunctionValue = 0;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, TargetComp);
		if (computer == null)
		{
			throw new NullReferenceException("Computer " + TargetComp + " could not be found for AddAssetFunction, adding file: " + FileName);
		}
		Folder folderAtPath = Programs.getFolderAtPath(TargetFolderpath, os, computer.files.root, returnsNullOnNoFind: true);
		if (folderAtPath == null)
		{
			throw new NullReferenceException("Folder " + TargetFolderpath + " could not be found for AddAssetFunction, adding file: " + FileName);
		}
		FileEntry item = new FileEntry(ComputerLoader.filter(FileContents), FileName);
		folderAtPath.files.Add(item);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAAddAsset sAAddAsset = new SAAddAsset();
		if (rdr.MoveToAttribute("FileName"))
		{
			sAAddAsset.FileName = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("FileContents"))
		{
			sAAddAsset.FileContents = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAAddAsset.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetFolderpath"))
		{
			sAAddAsset.TargetFolderpath = rdr.ReadContentAsString();
		}
		return sAAddAsset;
	}
}
