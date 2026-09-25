using System;
using System.Xml;

namespace Hacknet;

public class SALaunchHackScript : SerializableAction
{
	public string Filepath;

	public string DelayHost;

	public float Delay;

	public string SourceComp;

	public string TargetComp;

	public bool RequireLogsOnSource;

	public bool RequireSourceIntact;

	public override void Trigger(object os_obj)
	{
		if (Delay <= 0f)
		{
			Computer computer = Programs.getComputer((OS)os_obj, SourceComp);
			Computer computer2 = Programs.getComputer((OS)os_obj, TargetComp);
			if (RequireLogsOnSource)
			{
				if (computer == null)
				{
					throw new NullReferenceException("Launch Hacker Script Error: Source comp " + SourceComp + " does not exist");
				}
				if (computer2 == null)
				{
					throw new NullReferenceException("Launch Hacker Script Error: Target comp " + TargetComp + " does not exist");
				}
				Folder folder = computer.files.root.searchForFolder("log");
				bool flag = false;
				for (int i = 0; i < folder.files.Count; i++)
				{
					if (TrackerCompleteSequence.CompShouldStartTrackerFromLogs(os_obj, computer, computer2.ip))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					return;
				}
				if (RequireSourceIntact)
				{
					Folder folder2 = computer.files.root.searchForFolder("sys");
					bool flag2 = false;
					for (int i = 0; i < folder2.files.Count; i++)
					{
						if (folder2.files[i].name == "netcfgx.dll" && folder2.files[i].data.Contains("1") && folder2.files[i].data.Contains("0"))
						{
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						return;
					}
				}
			}
			HackerScriptExecuter.runScript(Filepath, os_obj, SourceComp, TargetComp);
		}
		else
		{
			OS os = (OS)os_obj;
			Computer computer3 = Programs.getComputer(os, DelayHost);
			if (computer3 == null)
			{
				throw new NullReferenceException(string.Concat("Computer ", computer3, " could not be found as DelayHost for Function"));
			}
			float delay = Delay;
			Delay = -1f;
			DelayableActionSystem.FindDelayableActionSystemOnComputer(computer3).AddAction(this, delay);
		}
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SALaunchHackScript sALaunchHackScript = new SALaunchHackScript();
		if (rdr.MoveToAttribute("Filepath"))
		{
			sALaunchHackScript.Filepath = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("Delay"))
		{
			sALaunchHackScript.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sALaunchHackScript.DelayHost = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("SourceComp"))
		{
			sALaunchHackScript.SourceComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("TargetComp"))
		{
			sALaunchHackScript.TargetComp = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("RequireLogsOnSource"))
		{
			sALaunchHackScript.RequireLogsOnSource = rdr.ReadContentAsString().ToLower() == "true";
		}
		if (rdr.MoveToAttribute("RequireSourceIntact"))
		{
			sALaunchHackScript.RequireSourceIntact = rdr.ReadContentAsString().ToLower() == "true";
		}
		if (string.IsNullOrWhiteSpace(sALaunchHackScript.Filepath))
		{
			throw new FormatException("Invalid Filepath");
		}
		return sALaunchHackScript;
	}
}
