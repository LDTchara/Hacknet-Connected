namespace Hacknet;

internal static class ShellOverloaderExe
{
	public static void RunShellOverloaderExe(string[] args, object osObj, Computer target)
	{
		OS oS = (OS)osObj;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		if (args.Length > 1)
		{
			if (args[1].ToLower() == "-c")
			{
				flag = false;
				flag2 = false;
				flag3 = true;
			}
			else if (args[1].ToLower() == "-o")
			{
				flag = false;
				flag2 = true;
			}
			else if (args[1].ToLower() == "-e")
			{
				flag = true;
				flag2 = false;
				flag3 = false;
			}
		}
		if (!flag2 && !flag && !flag3)
		{
			oS.write("--------------------------------------");
			oS.write("ConShell " + LocaleTerms.Loc("ERROR: Not enough arguments!"));
			oS.write(LocaleTerms.Loc("Usage:") + " ConShell [-" + LocaleTerms.Loc("option") + "]");
			oS.write(LocaleTerms.Loc("Valid Options:") + " [-e (" + LocaleTerms.Loc("Exit") + ")] [-o (" + LocaleTerms.Loc("Overload") + ")] [-c (" + LocaleTerms.Loc("Cancel Overload") + ")]");
			oS.write("--------------------------------------");
			return;
		}
		if (oS.exes.Count <= 0)
		{
			oS.write("--------------------------------------");
			oS.write("ConShell " + LocaleTerms.Loc("ERROR: No active shells"));
			oS.write("--------------------------------------");
			return;
		}
		for (int i = 0; i < oS.exes.Count; i++)
		{
			if (oS.exes[i] is ShellExe shellExe)
			{
				if (flag)
				{
					shellExe.Completed();
				}
				else if (flag3)
				{
					shellExe.cancelTarget();
				}
				else if (flag2)
				{
					shellExe.StartOverload();
				}
			}
		}
	}
}
