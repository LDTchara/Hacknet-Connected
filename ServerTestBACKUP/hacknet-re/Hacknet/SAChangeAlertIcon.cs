using System;
using System.Xml;

namespace Hacknet;

public class SAChangeAlertIcon : SerializableAction
{
	private const string TypeFlag = "_changeAlertIconType:";

	private const string TargetFlag = "_changeAlertIconTarget:";

	public string Type;

	public string Target;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (Delay <= 0f)
		{
			string flagStartingWith = oS.Flags.GetFlagStartingWith("_changeAlertIconType:");
			string flagStartingWith2 = oS.Flags.GetFlagStartingWith("_changeAlertIconTarget:");
			if (flagStartingWith != null)
			{
				oS.Flags.RemoveFlag(flagStartingWith);
			}
			if (flagStartingWith2 != null)
			{
				oS.Flags.RemoveFlag(flagStartingWith2);
			}
			oS.Flags.AddFlag("_changeAlertIconType:" + Type);
			oS.Flags.AddFlag("_changeAlertIconTarget:" + Target);
			if (!oS.Flags.HasFlag("_alertIconChanged"))
			{
				oS.Flags.AddFlag("_alertIconChanged");
			}
			UpdateAlertIcon(oS);
		}
		else
		{
			Computer computer = Programs.getComputer(oS, DelayHost);
			if (computer == null)
			{
				throw new NullReferenceException(string.Concat("Computer ", computer, " could not be found as DelayHost for Function"));
			}
			float delay = Delay;
			Delay = -1f;
			DelayableActionSystem.FindDelayableActionSystemOnComputer(computer).AddAction(this, delay);
		}
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SAChangeAlertIcon sAChangeAlertIcon = new SAChangeAlertIcon();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAChangeAlertIcon.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAChangeAlertIcon.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("Type"))
		{
			sAChangeAlertIcon.Type = rdr.ReadContentAsString().ToLower();
		}
		if (rdr.MoveToAttribute("Target"))
		{
			sAChangeAlertIcon.Target = rdr.ReadContentAsString();
		}
		if (sAChangeAlertIcon.Type == null)
		{
			throw new FormatException("Type tag for ChangeAlertIconAction not found! Make sure you have written it correctly (capital T)!");
		}
		if (sAChangeAlertIcon.Type != "mail" && sAChangeAlertIcon.Type != "irc" && sAChangeAlertIcon.Type != "board" && sAChangeAlertIcon.Type != "irchub")
		{
			throw new FormatException("Provided type " + sAChangeAlertIcon.Type + " for ChangeAlertIconAction is invalid! Accepted types: mail, irc, board");
		}
		if (sAChangeAlertIcon.Type != "mail" && !DLC1SessionUpgrader.HasDLC1Installed)
		{
			throw new NotSupportedException("Changing alert icon to something other than mail requires the Hacknet Labyrinths DLC to be installed.");
		}
		return sAChangeAlertIcon;
	}

	public static void UpdateAlertIcon(object osobj)
	{
		OS oS = (OS)osobj;
		string flagStartingWith = oS.Flags.GetFlagStartingWith("_changeAlertIconType:");
		string flagStartingWith2 = oS.Flags.GetFlagStartingWith("_changeAlertIconTarget:");
		if (flagStartingWith == null || flagStartingWith2 == null)
		{
			return;
		}
		string text = flagStartingWith.Substring("_changeAlertIconType:".Length);
		string text2 = flagStartingWith2.Substring("_changeAlertIconTarget:".Length);
		Computer computer = Programs.getComputer(oS, text2);
		switch (text.ToLower())
		{
		case "mail":
		{
			MailServer mailServer = (MailServer)computer.getDaemon(typeof(MailServer));
			bool flag = false;
			for (int i = 0; i < mailServer.comp.users.Count; i++)
			{
				if (mailServer.comp.users[i].name == oS.defaultUser.name)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				throw new FormatException("Mail server " + text2 + " does not have a user account for the player!\nA mail server must have a player account to be used as the alert icon");
			}
			oS.mailicon.UpdateTargetServer(mailServer);
			oS.ShowDLCAlertsIcon = false;
			break;
		}
		case "irc":
		{
			IRCDaemon iRCDaemon = (IRCDaemon)computer.getDaemon(typeof(IRCDaemon));
			oS.ShowDLCAlertsIcon = true;
			oS.hubServerAlertsIcon.UpdateTarget(iRCDaemon, iRCDaemon.comp);
			break;
		}
		case "irchub":
		{
			DLCHubServer dLCHubServer = (DLCHubServer)computer.getDaemon(typeof(DLCHubServer));
			oS.ShowDLCAlertsIcon = true;
			oS.hubServerAlertsIcon.UpdateTarget(dLCHubServer, dLCHubServer.comp);
			break;
		}
		case "board":
		{
			MessageBoardDaemon messageBoardDaemon = (MessageBoardDaemon)computer.getDaemon(typeof(MessageBoardDaemon));
			oS.ShowDLCAlertsIcon = true;
			oS.hubServerAlertsIcon.UpdateTarget(messageBoardDaemon, messageBoardDaemon.comp);
			break;
		}
		}
	}
}
