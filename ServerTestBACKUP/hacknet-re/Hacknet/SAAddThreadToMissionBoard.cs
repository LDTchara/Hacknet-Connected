using System;
using System.Xml;

namespace Hacknet;

public class SAAddThreadToMissionBoard : SerializableAction
{
	public string ThreadFilepath;

	public string TargetComp;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, TargetComp);
		if (computer == null)
		{
			throw new NullReferenceException("Computer " + TargetComp + " could not be found for SAAddThreadToMissionBoard Function, adding thread: " + ThreadFilepath);
		}
		if (!(computer.getDaemon(typeof(MessageBoardDaemon)) is MessageBoardDaemon messageBoardDaemon))
		{
			throw new NullReferenceException("Computer " + TargetComp + " does not contain a MessageBoard daemon for SAAddThreadToMissionBoard function adding thread: " + ThreadFilepath);
		}
		string threadData = Utils.readEntireFile(Utils.GetFileLoadPrefix() + ThreadFilepath);
		messageBoardDaemon.AddThread(threadData);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAAddThreadToMissionBoard sAAddThreadToMissionBoard = new SAAddThreadToMissionBoard();
		if (rdr.MoveToAttribute("ThreadFilepath"))
		{
			sAAddThreadToMissionBoard.ThreadFilepath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAAddThreadToMissionBoard.TargetComp = rdr.ReadContentAsString();
		}
		if (string.IsNullOrWhiteSpace(sAAddThreadToMissionBoard.ThreadFilepath))
		{
			throw new FormatException("Invalid MissionFilepath");
		}
		if (string.IsNullOrWhiteSpace(sAAddThreadToMissionBoard.TargetComp))
		{
			throw new FormatException("Invalid TargetComp");
		}
		return sAAddThreadToMissionBoard;
	}
}
