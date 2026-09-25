namespace Hacknet.Misc;

public static class EduEditionTests
{
	public static string TestEDUFunctionality(ScreenManager screenMan, out int errorsAdded)
	{
		string text = "";
		int errorsAdded2 = 0;
		int num = 0;
		bool educationSafeBuild = Settings.EducationSafeBuild;
		text += TestEduSafeFileFlags(screenMan, out errorsAdded2);
		num += errorsAdded2;
		object obj = text;
		text = string.Concat(obj, (errorsAdded2 > 0) ? "\r\n" : " ", "Complete - ", errorsAdded2, " errors found");
		errorsAdded = num;
		Settings.EducationSafeBuild = educationSafeBuild;
		return text;
	}

	public static string TestEduSafeFileFlags(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		Settings.EducationSafeBuild = true;
		Computer computer = (Computer)ComputerLoader.loadComputer("Content/Tests/TestComputer.xml");
		Settings.EducationSafeBuild = false;
		Computer computer2 = (Computer)ComputerLoader.loadComputer("Content/Tests/TestComputer.xml");
		Folder folder = computer.files.root.searchForFolder("testfolder");
		Folder folder2 = computer2.files.root.searchForFolder("testfolder");
		if (folder.containsFile("eduUnsafeFile.txt") || !folder.containsFile("eduSafeFile.txt") || !folder.containsFile("eduSafeExplicit.txt") || !folder.containsFile("eduSafeOnlyFile.txt"))
		{
			num++;
			text += "\nError in Education File Flags - EDU Safe version has invalid file set";
		}
		if (!folder2.containsFile("eduUnsafeFile.txt") || !folder2.containsFile("eduSafeFile.txt") || !folder2.containsFile("eduSafeExplicit.txt") || folder2.containsFile("eduSafeOnlyFile.txt"))
		{
			num++;
			text += "\nError in Education File Flags - EDU Unsafe version has invalid file set";
		}
		errorsAdded = num;
		return text;
	}
}
