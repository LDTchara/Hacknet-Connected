using System;
using System.Collections.Generic;
using System.Globalization;
using Hacknet.Localization;
using Steamworks;

namespace Hacknet;

public static class PlatformAPISettings
{
	public static string Report = "";

	public static bool Running = false;

	public static bool RemoteStorageRunning = false;

	public static void InitPlatformAPI()
	{
		if (!Settings.isConventionDemo)
		{
			Running = SteamAPI.Init();
			if (!Running)
			{
				Report = "First Init Failed. ";
				Console.WriteLine("Steam Init Failed!");
				Running = SteamAPI.InitSafe();
				Report = Report + " Second init Running = " + Running;
				Console.WriteLine(Report);
			}
			else
			{
				Report = "Steam API Running :" + Running;
			}
			if (Running)
			{
				RemoteStorageRunning = SteamRemoteStorage.IsCloudEnabledForAccount();
			}
		}
	}

	public static string GetCodeForActiveLanguage(List<LocaleActivator.LanguageInfo> supportedLanguages)
	{
		Console.WriteLine("Scanning for language code...");
		if (Running)
		{
			string currentGameLanguage = SteamApps.GetCurrentGameLanguage();
			for (int i = 0; i < supportedLanguages.Count; i++)
			{
				if (supportedLanguages[i].SteamCode == currentGameLanguage)
				{
					Console.WriteLine("Matched Steam Language Code : " + currentGameLanguage);
					return supportedLanguages[i].Code;
				}
			}
		}
		CultureInfo originalCultureInfo = Game1.OriginalCultureInfo;
		for (int i = 0; i < supportedLanguages.Count; i++)
		{
			if (supportedLanguages[i].Code == originalCultureInfo.Name)
			{
				Console.WriteLine("Found exact language match for " + originalCultureInfo.Name);
				return supportedLanguages[i].Code;
			}
		}
		for (int i = 0; i < supportedLanguages.Count; i++)
		{
			string text = supportedLanguages[i].Code.Substring(0, 2);
			if (originalCultureInfo.Name.StartsWith(text))
			{
				Console.WriteLine("Found close enough language match for " + text);
				return supportedLanguages[i].Code;
			}
		}
		Console.WriteLine("No language match found for " + originalCultureInfo.Name + " - " + originalCultureInfo.DisplayName + ". Reverting to English...");
		return "en-us";
	}
}
