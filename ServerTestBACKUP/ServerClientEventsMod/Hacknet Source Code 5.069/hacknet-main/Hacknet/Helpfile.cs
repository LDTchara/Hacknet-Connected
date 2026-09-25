using System.Collections.Generic;
using System.Text;

namespace Hacknet;

internal class Helpfile
{
	private static int ITEMS_PER_PAGE = 10;

	public static List<string> help;

	public static string prefix = "---------------------------------\n" + LocaleTerms.Loc("Command List - Page [PAGENUM] of [TOTALPAGES]") + ":\n";

	private static string postfix = "help [PAGE NUMBER]\n " + LocaleTerms.Loc("Displays the specified page of commands.") + "\n---------------------------------\n";

	private static string LoadedLanguage = "en-us";

	public static void init()
	{
		help = new List<string>();
		string text = "\n    ";
		help.Add("help [PAGE NUMBER]" + text + LocaleTerms.Loc("Displays the specified page of commands.").Replace("\n", text));
		help.Add("scp [filename] [OPTIONAL: destination]" + text + LocaleTerms.Loc("Copies file named [filename] from remote machine to specified local folder (/bin default)").Replace("\n", text));
		help.Add("scan" + text + LocaleTerms.Loc("Scans for links on the connected machine and adds them to the Map").Replace("\n", text));
		help.Add("rm [filename (or use * for all files in folder)]" + text + LocaleTerms.Loc("Deletes specified file(s)").Replace("\n", text));
		help.Add("ps" + text + LocaleTerms.Loc("Lists currently running processes and their PIDs").Replace("\n", text));
		help.Add("kill [PID]" + text + LocaleTerms.Loc("Kills Process number [PID]").Replace("\n", text));
		help.Add("ls" + text + LocaleTerms.Loc("Lists all files in current directory").Replace("\n", text));
		help.Add("cd [foldername]" + text + LocaleTerms.Loc("Moves current working directory to the specified folder").Replace("\n", text));
		help.Add("mv [FILE] [DESTINATION]" + text + LocaleTerms.Loc("Moves or renames [FILE] to [DESTINATION]").Replace("\n", text) + text + "(i.e: mv hi.txt ../bin/hi.txt)");
		help.Add("connect [ip]" + text + LocaleTerms.Loc("Connect to an External Computer").Replace("\n", text));
		help.Add("probe" + text + string.Format(LocaleTerms.Loc("Scans the connected machine for{0}active ports and security level"), text).Replace("\n", text));
		help.Add("exe" + text + LocaleTerms.Loc("Lists all available executables in the local /bin/ folder (Includes hidden and embedded executables)").Replace("\n", text));
		help.Add("disconnect" + text + LocaleTerms.Loc("Terminate the current open connection.").Replace("\n", text) + " ALT: \"dc\"");
		help.Add("cat [filename]" + text + LocaleTerms.Loc("Displays contents of file").Replace("\n", text));
		help.Add("openCDTray" + text + LocaleTerms.Loc("Opens the connected Computer's CD Tray").Replace("\n", text));
		help.Add("closeCDTray" + text + LocaleTerms.Loc("Closes the connected Computer's CD Tray").Replace("\n", text));
		help.Add("reboot [OPTIONAL: -i]" + text + LocaleTerms.Loc("Reboots the connected computer. The -i flag reboots instantly").Replace("\n", text));
		help.Add("replace [filename] \"target\" \"replacement\"" + text + LocaleTerms.Loc("Replaces the target text in the file with the replacement").Replace("\n", text));
		help.Add("analyze" + text + LocaleTerms.Loc("Performs an analysis pass on the firewall of the target machine").Replace("\n", text));
		help.Add("solve [FIREWALL SOLUTION]" + text + LocaleTerms.Loc("Attempts to solve the firewall of target machine to allow UDP Traffic").Replace("\n", text));
		help.Add("login" + text + LocaleTerms.Loc("Requests a username and password to log in to the connected system").Replace("\n", text));
		help.Add("upload [LOCAL FILE PATH]" + text + LocaleTerms.Loc("Uploads the indicated file on your local machine to the current connected directory").Replace("\n", text));
		help.Add("clear" + text + LocaleTerms.Loc("Clears the terminal").Replace("\n", text));
		help.Add("addNote [NOTE]" + text + LocaleTerms.Loc("Add Note").Replace("\n", text));
		help.Add("append [FILENAME] [DATA]" + text + LocaleTerms.Loc("Appends a line containing [DATA] to [FILENAME]").Replace("\n", text));
		help.Add("shell" + text + LocaleTerms.Loc("Opens a remote access shell on target machine with Proxy overload\n and IP trap capabilities").Replace("\n", text));
		LoadedLanguage = Settings.ActiveLocale;
	}

	public static void writeHelp(OS os, int page = 0)
	{
		if (LoadedLanguage != Settings.ActiveLocale)
		{
			init();
		}
		if (page == 0)
		{
			page = 1;
		}
		int num = (page - 1) * ITEMS_PER_PAGE;
		if (num >= help.Count)
		{
			num = 0;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(prefix.Replace("[PAGENUM]", string.Concat(page)).Replace("[TOTALPAGES]", string.Concat(getNumberOfPages())) + "\n");
		for (int i = num; i < help.Count && i < num + ITEMS_PER_PAGE; i++)
		{
			stringBuilder.Append(((i == 0) ? " " : "") + help[i] + "\n  \n ");
		}
		os.write(string.Concat(stringBuilder, "\n", postfix));
	}

	public static int getNumberOfPages()
	{
		return help.Count / ITEMS_PER_PAGE + 1;
	}
}
