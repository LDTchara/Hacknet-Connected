using System.Collections.Generic;
using Hacknet.Localization;
using Hacknet.PlatformAPI.Storage;

namespace Hacknet.Misc;

public static class DLCLocalizationTests
{
	public static string TestDLCLocalizations(ScreenManager screenMan, out int errorsAdded)
	{
		string text = "\r\n[";
		int num = 0;
		string activeLocale = Settings.ActiveLocale;
		for (int i = 0; i < LocaleActivator.SupportedLanguages.Count; i++)
		{
			LocaleActivator.ActivateLocale(LocaleActivator.SupportedLanguages[i].Code, Game1.getSingleton().Content);
			int errorsAdded2 = 0;
			string text2 = LoadAndTestOS(screenMan, out errorsAdded2);
			num += errorsAdded2;
			text2 = text2.Replace(".", "").Trim();
			if (string.IsNullOrWhiteSpace(text2))
			{
				text += "==";
				continue;
			}
			string text3 = text;
			text = text3 + "\r\n -- " + LocaleActivator.SupportedLanguages[i].Name + ": " + (string.IsNullOrWhiteSpace(text2) ? "Complete\r\n\r\n" : ("\r\n" + text2 + "\r\n\r\n"));
		}
		LocaleActivator.ActivateLocale(activeLocale, Game1.getSingleton().Content);
		object obj = text;
		text = string.Concat(obj, "]\r\nComplete - ", num, " load stopping errors found");
		errorsAdded = num;
		return text;
	}

	private static string LoadAndTestOS(ScreenManager screenMan, out int errorsAdded)
	{
		int errorCount = 0;
		string text = "";
		string text2 = "__hacknettestaccount";
		string pass = "__testingpassword";
		SaveFileManager.AddUser(text2, pass);
		string saveFileNameForUsername = SaveFileManager.GetSaveFileNameForUsername(text2);
		OS.TestingPassOnly = true;
		OS oS = new OS();
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text2;
		screenMan.AddScreen(oS, screenMan.controllingPlayer);
		SessionAccelerator.AccelerateSessionToDLCEND(oS);
		oS.PreDLCVisibleNodesCache = "123,456,789";
		oS.delayer.RunAllDelayedActions();
		oS.threadedSaveExecute();
		List<Computer> nodes = oS.netMap.nodes;
		screenMan.RemoveScreen(oS);
		OS.WillLoadSave = true;
		oS = new OS();
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text2;
		screenMan.AddScreen(oS, screenMan.controllingPlayer);
		oS.delayer.RunAllDelayedActions();
		Game1.getSingleton().IsMouseVisible = true;
		if (oS.PreDLCVisibleNodesCache != "123,456,789")
		{
			errorCount++;
			text += "PreDLC Visible Node Cache not saving correctly";
		}
		screenMan.RemoveScreen(oS);
		text += TestSuite.getTestingReportForLoadComparison(oS, nodes, errorCount, out errorCount);
		text = text + "\r\n" + TestSuite.TestMissions(oS);
		string text3 = DLCTests.TestDLCFunctionality(screenMan, out errorsAdded);
		text += ((text3.Length > 30) ? ("\r\n" + text3) : "");
		errorsAdded = errorCount;
		return text;
	}
}
