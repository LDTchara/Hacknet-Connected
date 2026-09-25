using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content;

namespace Hacknet.Localization;

public static class LocaleActivator
{
	public struct LanguageInfo
	{
		public string Name;

		public string Code;

		public string SteamCode;
	}

	private static List<LanguageInfo> supportedLanguages = null;

	public static List<LanguageInfo> SupportedLanguages
	{
		get
		{
			if (supportedLanguages == null)
			{
				LoadSupportedLocales();
			}
			return supportedLanguages;
		}
	}

	private static void LoadSupportedLocales()
	{
		supportedLanguages = new List<LanguageInfo>();
		string text = Utils.readEntireFile("Content/Locales/SupportedLanguages.txt");
		string[] array = text.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length > 2)
			{
				supportedLanguages.Add(new LanguageInfo
				{
					Name = array2[0],
					Code = array2[1],
					SteamCode = array2[2]
				});
			}
		}
	}

	public static void ActivateLocale(string localeCode, ContentManager content)
	{
		if (localeCode == "en-us")
		{
			LocaleTerms.ClearForEnUS();
		}
		else
		{
			string text = "Content/Locales/" + localeCode + "/UI_Terms.txt";
			string text2 = "Content/Locales/" + localeCode + "/Hacknet_UI_Terms.txt";
			if (!File.Exists(text))
			{
				if (!File.Exists(text2))
				{
					throw new NotSupportedException("Locale " + localeCode + " does not exist or is not supported");
				}
				text = text2;
			}
			LocaleTerms.ReadInTerms(text);
			if (DLC1SessionUpgrader.HasDLC1Installed)
			{
				string text3 = "Content/Locales/" + localeCode + "/DLC/Hacknet_UI_Terms.txt";
				if (File.Exists(text3))
				{
					LocaleTerms.ReadInTerms(text3, clearPreviouslyLoadedTerms: false);
				}
			}
		}
		Settings.ActiveLocale = localeCode;
		LocaleFontLoader.LoadFontConfigForLocale(localeCode, content);
		FileEntry.init(content);
	}

	public static bool ActiveLocaleIsCJK()
	{
		return Settings.ActiveLocale.StartsWith("zh") || Settings.ActiveLocale.StartsWith("ja") || Settings.ActiveLocale.StartsWith("ko");
	}
}
