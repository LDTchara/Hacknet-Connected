using System.Collections.Generic;

namespace Hacknet.Mission;

internal class SendEmailMission : MisisonGoal
{
	private string mailSubject;

	private string mailRecipient;

	private MailServer server;

	public SendEmailMission(string mailServerID, string mailRecipient, string proposedEmailSubject, OS _os)
	{
		Computer computer = Programs.getComputer(_os, mailServerID);
		server = (MailServer)computer.getDaemon(typeof(MailServer));
		mailSubject = proposedEmailSubject;
		this.mailRecipient = mailRecipient;
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		if (server == null)
		{
			return true;
		}
		return server.MailWithSubjectExists(mailRecipient, mailSubject);
	}

	public override void reset()
	{
	}
}
