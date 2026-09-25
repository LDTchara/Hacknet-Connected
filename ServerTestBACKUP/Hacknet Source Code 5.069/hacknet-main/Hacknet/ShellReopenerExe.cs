using System;
using System.Collections.Generic;
using System.Text;

namespace Hacknet;

internal static class ShellReopenerExe
{
	private const string Filename = "ShellSources.txt";

	public static void RunShellReopenerExe(string[] args, object osObj, Computer target)
	{
		OS os = (OS)osObj;
		bool flag = false;
		bool flag2 = false;
		if (args.Length > 1)
		{
			if (args[1].ToLower() == "-s")
			{
				flag2 = true;
			}
			else if (args[1].ToLower() == "-o")
			{
				flag = true;
			}
		}
		if (!flag && !flag2)
		{
			os.write("--------------------------------------");
			os.write("OpShell " + LocaleTerms.Loc("ERROR: Not enough arguments!"));
			os.write(LocaleTerms.Loc("Usage:") + " OpShell [-" + LocaleTerms.Loc("option") + "]");
			os.write(LocaleTerms.Loc("Valid Options:") + " [-s (" + LocaleTerms.Loc("Save state") + ")] [-o (" + LocaleTerms.Loc("Re-open") + ")]");
			os.write("--------------------------------------");
			return;
		}
		Folder folder = os.thisComputer.files.root.searchForFolder("sys");
		FileEntry fileEntry = folder.searchForFile("ShellSources.txt");
		List<ShellExe> list = new List<ShellExe>();
		for (int i = 0; i < os.exes.Count; i++)
		{
			if (os.exes[i] is ShellExe item)
			{
				list.Add(item);
			}
		}
		if (flag)
		{
			if (fileEntry == null)
			{
				os.write("--------------------------------------");
				os.write("OpShell " + LocaleTerms.Loc("ERROR: No shell sources saved. Save a setup first."));
				os.write("--------------------------------------");
				return;
			}
			string[] lines = fileEntry.data.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
			double num = 0.2;
			os.runCommand("disconnect");
			for (int i = 1; i < lines.Length; i++)
			{
				int index = i;
				os.delayer.Post(ActionDelayer.Wait(num), delegate
				{
					os.runCommand("connect " + lines[index]);
				});
				num += 0.2;
				os.delayer.Post(ActionDelayer.Wait(num), delegate
				{
					os.runCommand("shell");
				});
				num += 0.2;
			}
			os.delayer.Post(ActionDelayer.Wait(num), delegate
			{
				os.runCommand("disconnect");
				os.write("--------------------------------------");
				os.write("OpShell : " + LocaleTerms.Loc("Operation complete - ran shell on " + (lines.Length - 1) + " nodes"));
				os.write("--------------------------------------");
			});
		}
		else
		{
			if (!flag2)
			{
				return;
			}
			if (list.Count <= 0)
			{
				os.write("--------------------------------------");
				os.write("OpShell " + LocaleTerms.Loc("ERROR: No active shells"));
		     	os.write("--------------------------------------");
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("#OpShell_IP_SourceCache\n");
			for (int i = 0; i < list.Count; i++)
			{
				stringBuilder.Append(list[i].targetIP + "\n");
			}
			if (fileEntry != null)
			{
				fileEntry.data = stringBuilder.ToString();
			}
			else
			{
				folder.files.Add(new FileEntry(stringBuilder.ToString(), "ShellSources.txt"));
			}
			os.write("--------------------------------------");
			os.write("OpShell : " + string.Format(LocaleTerms.Loc("Saved {0} active shell sources successfully"), list.Count));
			os.write("--------------------------------------");
		}
	}
}
