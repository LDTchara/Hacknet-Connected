using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace Hacknet;

public class CustomTheme
{
	public Color defaultHighlightColor = new Color(0, 139, 199, 255);

	public Color defaultTopBarColor = new Color(130, 65, 27);

	public Color warningColor = Color.Red;

	public Color subtleTextColor = new Color(90, 90, 90);

	public Color darkBackgroundColor = new Color(8, 8, 8);

	public Color indentBackgroundColor = new Color(12, 12, 12);

	public Color outlineColor = new Color(68, 68, 68);

	public Color lockedColor = new Color(65, 16, 16, 200);

	public Color brightLockedColor = new Color(160, 0, 0);

	public Color brightUnlockedColor = new Color(0, 160, 0);

	public Color unlockedColor = new Color(39, 65, 36);

	public Color lightGray = new Color(180, 180, 180);

	public Color shellColor = new Color(222, 201, 24);

	public Color shellButtonColor = new Color(105, 167, 188);

	public Color moduleColorSolidDefault = new Color(50, 59, 90, 255);

	public Color moduleColorStrong = new Color(14, 28, 40, 80);

	public Color moduleColorBacking = new Color(5, 6, 7, 10);

	public Color semiTransText = new Color(120, 120, 120, 0);

	public Color terminalTextColor = new Color(213, 245, 255);

	public Color topBarTextColor = new Color(126, 126, 126, 100);

	public Color superLightWhite = new Color(2, 2, 2, 30);

	public Color connectedNodeHighlight = new Color(222, 0, 0, 195);

	public Color exeModuleTopBar = new Color(130, 65, 27, 80);

	public Color exeModuleTitleText = new Color(155, 85, 37, 0);

	public Color netmapToolTipColor = new Color(213, 245, 255, 0);

	public Color netmapToolTipBackground = new Color(0, 0, 0, 70);

	public Color topBarIconsColor = Color.White;

	public Color AFX_KeyboardMiddle = new Color(0, 120, 255);

	public Color AFX_KeyboardOuter = new Color(255, 150, 0);

	public Color AFX_WordLogo = new Color(0, 120, 255);

	public Color AFX_Other = new Color(0, 100, 255);

	public Color thisComputerNode = new Color(95, 220, 83);

	public Color scanlinesColor = new Color(255, 255, 255, 15);

	public string themeLayoutName = null;

	public string backgroundImagePath = null;

	public Color BackgroundImageFillColor = Color.Black;

	public bool UseAspectPreserveBackgroundScaling = false;

	public static CustomTheme Deserialize(string filepath)
	{
		using FileStream s = File.OpenRead(filepath);
		return (CustomTheme)Utils.DeserializeObject(s, typeof(CustomTheme));
	}

	public string GetSaveString()
	{
		return Utils.SerializeObject(this);
	}

	public void LoadIntoOS(object os_obj)
	{
		OS oS = (OS)os_obj;
		FieldInfo[] fields = GetType().GetFields();
		FieldInfo[] fields2 = oS.GetType().GetFields();
		for (int i = 0; i < fields2.Length; i++)
		{
			for (int j = 0; j < fields.Length; j++)
			{
				if (fields2[i].Name == fields[j].Name)
				{
					fields2[i].SetValue(oS, fields[j].GetValue(this));
				}
			}
		}
	}

	public OSTheme GetThemeForLayout()
	{
		if (themeLayoutName == null)
		{
			return OSTheme.HacknetBlue;
		}
		switch (themeLayoutName.ToLower())
		{
		default:
			return OSTheme.HacknetPurple;
		case "blue":
			return OSTheme.HacknetBlue;
		case "green":
			return OSTheme.HackerGreen;
		case "greencompact":
			return OSTheme.GreenCompact;
		case "white":
		case "csec":
			return OSTheme.HacknetWhite;
		case "mint":
		case "teal":
			return OSTheme.HacknetMint;
		case "colamaeleon":
		case "cola":
			return OSTheme.Colamaeleon;
		case "riptide":
			return OSTheme.Riptide;
		case "riptide2":
			return OSTheme.Riptide2;
		}
	}
}
