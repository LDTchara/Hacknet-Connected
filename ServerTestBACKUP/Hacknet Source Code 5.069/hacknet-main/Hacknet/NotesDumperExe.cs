using System;

namespace Hacknet;

internal static class NotesDumperExe
{
	public static void RunNotesDumperExe(string[] args, object osObj, Computer target)
	{
		OS oS = (OS)osObj;
		Folder folder = oS.thisComputer.files.root.searchForFolder("home");
		FileEntry fileEntry = folder.searchForFile("Notes.txt");
		if (fileEntry == null)
		{
			oS.write(LocaleTerms.Loc("Dump Notes Output:") + "_______________\n");
			oS.write(" ");
			oS.write(LocaleTerms.Loc("ERROR: No notes found on home system!"));
			oS.write("_______________________________");
			oS.write(" ");
			return;
		}
		string[] array = fileEntry.data.Split(new string[1] { "\n\n----------\n\n" }, StringSplitOptions.RemoveEmptyEntries);
		oS.write(" ");
		oS.write(LocaleTerms.Loc("Notes") + ":________________________");
		oS.write(" ");
		for (int i = 0; i < array.Length; i++)
		{
			oS.write(array[i]);
			oS.write("______________________________");
			oS.write(" ");
		}
	}
}
