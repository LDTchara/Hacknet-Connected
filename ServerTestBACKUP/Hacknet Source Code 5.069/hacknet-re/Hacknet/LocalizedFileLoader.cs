using System.IO;
using System.Linq;
using System.Text;

namespace Hacknet;

public static class LocalizedFileLoader
{
	public static string Read(string filepath)
	{
		return FilterStringForLocalization(File.ReadAllText(GetLocalizedFilepath(filepath)));
	}

	public static string GetLocalizedFilepath(string filepath)
	{
		filepath = filepath.Replace("\\", "/");
		string text = "Content/Locales/" + Settings.ActiveLocale;
		string text2 = filepath.Replace("Content/", text + "/");
		if (File.Exists(text2))
		{
			return text2;
		}
		return filepath;
	}

	public static string SafeFilterString(string data)
	{
		string source = "\r\n\t ";
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < data.Length; i++)
		{
			if (GuiData.tinyfont.Characters.Contains(data[i]) || source.Contains(data[i]))
			{
				stringBuilder.Append(data[i]);
			}
			else
			{
				stringBuilder.Append("?");
			}
		}
		return stringBuilder.ToString();
	}

	public static string FilterStringForLocalization(string data)
	{
		return data.Replace("&quot;", "'").Replace("\u00a0", "").Replace("[PRÉNOM]#[NOM]#[NUM_DOSSIER]#32#Rural#N/A#N/A#N/A#[DERNIERS_MOTS]", "[FIRST_NAME]#[LAST_NAME]#[RECORD_NUM]#32#Rural#N/A#N/A#N/A#[LAST_WORDS]")
			.Replace("[PRÉNOM]", "[FIRST_NAME]");
	}
}
