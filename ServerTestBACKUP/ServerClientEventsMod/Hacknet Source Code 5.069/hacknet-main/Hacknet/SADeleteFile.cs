using System;
using System.Xml;

namespace Hacknet;

public class SADeleteFile : SerializableAction
{
	public string TargetComp;

	public string FilePath;

	public string FileName;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, TargetComp);
		if (Delay <= 0f)
		{
			Folder folderAtPath = Programs.getFolderAtPath(FilePath, os, computer.files.root, returnsNullOnNoFind: true);
			if (folderAtPath != null)
			{
				FileEntry fileEntry = folderAtPath.searchForFile(FileName);
				if (fileEntry != null)
				{
					folderAtPath.files.Remove(fileEntry);
				}
			}
		}
		else
		{
			Computer computer2 = Programs.getComputer(os, DelayHost);
			if (computer2 == null)
			{
				throw new NullReferenceException(string.Concat("Computer ", computer2, " could not be found as DelayHost for Function"));
			}
			float delay = Delay;
			Delay = -1f;
			DelayableActionSystem.FindDelayableActionSystemOnComputer(computer2).AddAction(this, delay);
		}
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SADeleteFile sADeleteFile = new SADeleteFile();
		if (rdr.MoveToAttribute("Delay"))
		{
			sADeleteFile.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sADeleteFile.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("FilePath"))
		{
			sADeleteFile.FilePath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("FileName"))
		{
			sADeleteFile.FileName = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sADeleteFile.DelayHost = rdr.ReadContentAsString();
		}
		return sADeleteFile;
	}
}
