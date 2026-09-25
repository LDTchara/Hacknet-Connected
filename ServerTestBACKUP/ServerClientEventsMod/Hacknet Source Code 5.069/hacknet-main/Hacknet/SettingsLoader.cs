using System;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SDL2;

namespace Hacknet;

public static class SettingsLoader
{
	public static int resWidth;

	public static int resHeight;

	public static bool isFullscreen = false;

	public static bool didLoad = false;

	public static bool hasEverSaved = false;

	public static bool ShouldMultisample = true;

	public static bool ShouldDrawMusicVis = true;

	private static readonly string settingsPath = GetSettingsPath();

	private static string GetSettingsPath()
	{
		string text = SDL.SDL_GetPlatform();
		if (text.Equals("Linux"))
		{
			string text2 = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
			if (string.IsNullOrEmpty(text2))
			{
				text2 = Environment.GetEnvironmentVariable("HOME");
				if (string.IsNullOrEmpty(text2))
				{
					return "Settings.txt";
				}
				text2 += "/.config";
			}
			text2 += "/Hacknet";
			if (!Directory.Exists(text2))
			{
				Directory.CreateDirectory(text2);
			}
			return text2 + "/Settings.txt";
		}
		if (text.Equals("Mac OS X"))
		{
			string text2 = Environment.GetEnvironmentVariable("HOME");
			if (string.IsNullOrEmpty(text2))
			{
				return "Settings.txt";
			}
			text2 += "/Library/Application Support/Hacknet";
			if (!Directory.Exists(text2))
			{
				Directory.CreateDirectory(text2);
			}
			return text2 + "/Settings.txt";
		}
		if (!text.Equals("Windows"))
		{
			throw new NotSupportedException("Unhandled SDL2 platform!");
		}
		return "Settings.txt";
	}

	public static void checkStatus()
	{
		if (!File.Exists(settingsPath))
		{
			return;
		}
		StreamReader streamReader = new StreamReader(TitleContainer.OpenStream(settingsPath));
		string text = streamReader.ReadToEnd();
		string[] separator = new string[2] { "\r\n", "\n" };
		string[] array = text.Split(separator, StringSplitOptions.None);
		resWidth = Convert.ToInt32(array[0]);
		resHeight = Convert.ToInt32(array[1]);
		isFullscreen = array[2].ToLower().Equals("true");
		if (array.Length > 3)
		{
			string text2 = array[3].Substring(array[3].IndexOf(' ') + 1);
			PostProcessor.bloomEnabled = text2 == "true";
			text2 = array[4].Substring(array[4].IndexOf(' ') + 1);
			PostProcessor.scanlinesEnabled = text2 == "true";
		}
		if (array.Length > 6)
		{
			string text2 = array[5].Substring(array[5].IndexOf(' ') + 1);
			MusicManager.setIsMuted(text2 == "true");
			string value = array[6].Substring(array[6].IndexOf(' ') + 1).Trim();
			float volume = MusicManager.getVolume();
			try
			{
				volume = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
				MusicManager.setVolume(volume);
			}
			catch (FormatException)
			{
			}
			MusicManager.dataLoadedFromOutsideFile = true;
		}
		if (array.Length > 7)
		{
			string name = array[7].Substring(array[7].IndexOf(' ') + 1);
			GuiData.ActiveFontConfig.name = name;
		}
		if (array.Length > 8)
		{
			string text3 = array[8].Substring(array[8].IndexOf(' ') + 1);
			hasEverSaved = text3 == "True";
		}
		if (array.Length > 9)
		{
			string text4 = array[9].Substring(array[9].IndexOf(' ') + 1);
			ShouldMultisample = text4.ToLower() == "true";
		}
		if (array.Length > 10 && !string.IsNullOrWhiteSpace(array[10]))
		{
			string text5 = array[10].Substring(array[10].IndexOf(' ') + 1);
			if (string.IsNullOrWhiteSpace(text5))
			{
				text5 = "en-us";
			}
			Settings.ActiveLocale = text5;
		}
		if (array.Length > 11 && !string.IsNullOrWhiteSpace(array[11]))
		{
			string text6 = array[11].Substring(array[11].IndexOf(' ') + 1);
			ShouldDrawMusicVis = text6.ToLower().Trim() == "true";
		}
		didLoad = true;
	}

	public static void writeStatusFile()
	{
		GraphicsDevice graphicsDevice = Game1.getSingleton().GraphicsDevice;
		if (graphicsDevice != null)
		{
			string text = graphicsDevice.PresentationParameters.BackBufferWidth + "\r\n";
			text = text + graphicsDevice.PresentationParameters.BackBufferHeight + "\r\n";
			text += (Game1.getSingleton().graphics.IsFullScreen ? "true" : "false");
			text += "\r\n";
			text = text + "bloom: " + (PostProcessor.bloomEnabled ? "true" : "false") + "\r\n";
			text = text + "scanlines: " + (PostProcessor.scanlinesEnabled ? "true" : "false") + "\r\n";
			text = text + "muted: " + (MusicManager.isMuted ? "true" : "false") + "\r\n";
			text = text + "volume: " + MusicManager.getVolume().ToString(CultureInfo.InvariantCulture) + "\r\n";
			text = text + "fontConfig: " + GuiData.ActiveFontConfig.name + "\r\n";
			object obj = text;
			text = string.Concat(obj, "hasSaved: ", hasEverSaved, "\r\n");
			obj = text;
			text = string.Concat(obj, "shouldMultisample: ", ShouldMultisample, "\r\n");
			text = text + "defaultLocale: " + Settings.ActiveLocale + "\r\n";
			obj = text;
			text = string.Concat(obj, "drawMusicVis: ", ShouldDrawMusicVis, "\r\n");
			Utils.writeToFile(text, settingsPath);
		}
	}
}
