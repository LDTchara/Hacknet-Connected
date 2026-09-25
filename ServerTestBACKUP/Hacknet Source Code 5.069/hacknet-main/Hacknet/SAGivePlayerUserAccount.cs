using System;
using System.Xml;

namespace Hacknet;

public class SAGivePlayerUserAccount : SerializableAction
{
	public string TargetComp;

	public string Username;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS os = (OS)os_obj;
		if (Delay <= 0f)
		{
			Computer computer = Programs.getComputer(os, TargetComp);
			if (computer == null)
			{
				return;
			}
			for (int i = 0; i < computer.users.Count; i++)
			{
				if (computer.users[i].name == Username)
				{
					UserDetail value = computer.users[i];
					value.known = true;
					computer.users[i] = value;
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
		SAGivePlayerUserAccount sAGivePlayerUserAccount = new SAGivePlayerUserAccount();
		if (rdr.MoveToAttribute("Delay"))
		{
			sAGivePlayerUserAccount.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sAGivePlayerUserAccount.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sAGivePlayerUserAccount.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("Username"))
		{
			sAGivePlayerUserAccount.Username = rdr.ReadContentAsString();
		}
		return sAGivePlayerUserAccount;
	}
}
