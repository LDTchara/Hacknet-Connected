using System;
using System.IO;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Misc;

public static class MultiLanguageScreenshotTool
{
	public static void CaptureMultiLanguageScreen(object os_obj)
	{
		OS oS = (OS)os_obj;
		GameTime gameTime = new GameTime(oS.lastGameTime.TotalGameTime, TimeSpan.Zero, isRunningSlowly: false);
		string text = "Screenshots/";
		string activeLocale = Settings.ActiveLocale;
		for (int i = 0; i < LocaleActivator.SupportedLanguages.Count; i++)
		{
			string code = LocaleActivator.SupportedLanguages[i].Code;
			LocaleActivator.ActivateLocale(code, oS.content);
			Texture2D texture2D = null;
			if (!Directory.Exists(text + code))
			{
				Directory.CreateDirectory(text + code);
			}
			string text2 = Guid.NewGuid().ToString() + ".png";
			using FileStream stream = File.Create(text + code + "/" + text2);
			texture2D.SaveAsPng(stream, texture2D.Width, texture2D.Height);
		}
		LocaleActivator.ActivateLocale(activeLocale, oS.content);
	}
}
