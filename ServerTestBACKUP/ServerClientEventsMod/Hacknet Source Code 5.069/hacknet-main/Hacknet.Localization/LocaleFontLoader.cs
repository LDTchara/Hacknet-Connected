using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Localization;

public static class LocaleFontLoader
{
	public static void LoadFontConfigForLocale(string locale, ContentManager content)
	{
		if (!GuiData.LocaleFontConfigs.ContainsKey(locale))
		{
			GuiData.LocaleFontConfigs.Add(locale, LoadFontConfigSetForLocale(locale, "", content));
		}
		GuiData.ActivateFontConfig(GuiData.ActiveFontConfig.name);
		GuiData.UITinyfont = GuiData.LocaleFontConfigs[locale][0].tinyFont;
		GuiData.UISmallfont = GuiData.LocaleFontConfigs[locale][0].smallFont;
	}

	private static List<GuiData.FontCongifOption> LoadFontConfigSetForLocale(string locale, string fontPrefix, ContentManager content)
	{
		List<GuiData.FontCongifOption> list = new List<GuiData.FontCongifOption>();
		string text = "Locales/" + locale + "/Fonts/" + locale + "_" + fontPrefix;
		bool flag = LocaleActivator.ActiveLocaleIsCJK();
		list.Add(new GuiData.FontCongifOption
		{
			name = "default",
			detailFont = content.Load<SpriteFont>(text + "Font7"),
			smallFont = content.Load<SpriteFont>(text + "Font12"),
			tinyFont = content.Load<SpriteFont>(text + "Font10"),
			bigFont = content.Load<SpriteFont>(text + "Font23"),
			tinyFontCharHeight = (flag ? 15f : 10f)
		});
		if (flag)
		{
			list[0].smallFont.LineSpacing += 2;
			list[0].detailFont.LineSpacing += 2;
		}
		list.Add(new GuiData.FontCongifOption
		{
			name = "medium",
			detailFont = content.Load<SpriteFont>(text + "Font7"),
			smallFont = content.Load<SpriteFont>(text + "Font14"),
			tinyFont = content.Load<SpriteFont>(text + "Font12"),
			bigFont = content.Load<SpriteFont>(text + "Font23"),
			tinyFontCharHeight = (flag ? 17f : 14f)
		});
		list.Add(new GuiData.FontCongifOption
		{
			name = "large",
			detailFont = content.Load<SpriteFont>(text + "Font7"),
			smallFont = content.Load<SpriteFont>(text + "Font16"),
			tinyFont = content.Load<SpriteFont>(text + "Font14"),
			bigFont = content.Load<SpriteFont>(text + "Font23"),
			tinyFontCharHeight = (flag ? 19f : 16f)
		});
		return list;
	}
}
