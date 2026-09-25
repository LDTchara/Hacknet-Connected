using System;
using System.Xml;
using Hacknet.Daemons.Helpers;

namespace Hacknet;

public class SAAddIRCMessage : SerializableAction
{
	[XMLContent]
	public string Message;

	public string Author;

	public string TargetComp;

	public float Delay = 0f;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		Computer computer = Programs.getComputer(os, TargetComp);
		if (computer == null)
		{
			throw new NullReferenceException("Computer " + TargetComp + " could not be found for SAAddIRCMessage Function, adding message: " + Message);
		}
		IRCSystem iRCSystem = null;
		if (computer.getDaemon(typeof(IRCDaemon)) is IRCDaemon iRCDaemon)
		{
			iRCSystem = iRCDaemon.System;
		}
		else
		{
			if (!(computer.getDaemon(typeof(DLCHubServer)) is DLCHubServer dLCHubServer))
			{
				throw new NullReferenceException("Computer " + TargetComp + " does not contain an IRC server daemon for SAAddIRCMessage function adding message: " + Message);
			}
			iRCSystem = dLCHubServer.IRCSystem;
		}
		if (Delay <= 0f)
		{
			if (Math.Abs(Delay) < 0.001f)
			{
				iRCSystem.AddLog(Author, Message);
				return;
			}
			DateTime now = DateTime.Now;
			now -= TimeSpan.FromSeconds(Delay);
			string timestamp = now.Hour.ToString("00") + ":" + now.Minute.ToString("00");
			iRCSystem.AddLog(Author, Message, timestamp);
		}
		else
		{
			float delay = Delay;
			Delay = -1f;
			DelayableActionSystem.FindDelayableActionSystemOnComputer(computer).AddAction(this, delay);
		}
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAAddIRCMessage sAAddIRCMessage = new SAAddIRCMessage();
		if (rdr.MoveToAttribute("Author"))
		{
			sAAddIRCMessage.Author = ComputerLoader.filter(rdr.ReadContentAsString());
		}
		if (rdr.MoveToAttribute("Delay"))
		{
			sAAddIRCMessage.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAAddIRCMessage.TargetComp = rdr.ReadContentAsString();
		}
		rdr.MoveToContent();
		sAAddIRCMessage.Message = ComputerLoader.filter(rdr.ReadElementContentAsString());
		if (string.IsNullOrWhiteSpace(sAAddIRCMessage.TargetComp))
		{
			throw new FormatException("Invalid Target Comp");
		}
		if (string.IsNullOrWhiteSpace(sAAddIRCMessage.Message))
		{
			throw new FormatException("Invalid or Empty Message!");
		}
		return sAAddIRCMessage;
	}
}
