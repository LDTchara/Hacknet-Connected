using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Extensions;

public class ExtensionInfo
{
	public const string INFO_FILENAME = "ExtensionInfo.xml";

	public const string LOGO_FILENAME = "Logo";

	public const string NODES_FOLDER = "/Nodes";

	public const string MISSIONS_FOLDER = "/Missions";

	public string Name;

	public string Language;

	public string FolderPath;

	public string StartingMissionPath;

	public string StartingActionsPath;

	public string Description = "";

	public bool AllowSave = true;

	public bool StartsWithTutorial = false;

	public bool HasIntroStartup = true;

	public string Theme = "HacknetBlue";

	public string IntroStartupSong = null;

	public float IntroStartupSongDelay = 0f;

	public string[] StartingVisibleNodes = new string[0];

	public List<string> FactionDescriptorPaths = new List<string>();

	public Texture2D LogoImage;

	public string SequencerTargetID;

	public string SequencerFlagRequiredForStart;

	public string ActionsToRunOnSequencerStart;

	public float SequencerSpinUpTime = 17f;

	public string WorkshopDescription;

	public string WorkshopLanguage;

	public byte WorkshopVisibility = 2;

	public string WorkshopTags;

	public string WorkshopPreviewImagePath;

	public string WorkshopPublishID;

	public string GetFullFolderPath()
	{
		return Path.Combine(Directory.GetCurrentDirectory(), FolderPath).Replace("\\", "/");
	}

	public static ExtensionInfo ReadExtensionInfo(string folderpath)
	{
		if (ExtensionExists(folderpath))
		{
			ExtensionInfo extensionInfo = new ExtensionInfo();
			extensionInfo.FolderPath = folderpath;
			extensionInfo.Language = "en-us";
			using (FileStream input = File.OpenRead(folderpath + "/ExtensionInfo.xml"))
			{
				XmlReader xmlReader = XmlReader.Create(input);
				while (!xmlReader.EOF)
				{
					if (xmlReader.Name == "Name")
					{
						xmlReader.MoveToContent();
						extensionInfo.Name = Utils.CleanStringToLanguageRenderable(xmlReader.ReadElementContentAsString());
					}
					if (xmlReader.Name == "Language")
					{
						xmlReader.MoveToContent();
						extensionInfo.Language = xmlReader.ReadElementContentAsString();
					}
					if (xmlReader.Name == "AllowSaves")
					{
						xmlReader.MoveToContent();
						extensionInfo.AllowSave = xmlReader.ReadElementContentAsBoolean();
					}
					if (xmlReader.Name == "StartingVisibleNodes")
					{
						xmlReader.MoveToContent();
						string text = xmlReader.ReadElementContentAsString();
						extensionInfo.StartingVisibleNodes = text.Split(new char[6] { ',', ' ', '\t', '\n', '\r', '/' }, StringSplitOptions.RemoveEmptyEntries);
					}
					if (xmlReader.Name == "StartingMission")
					{
						xmlReader.MoveToContent();
						extensionInfo.StartingMissionPath = xmlReader.ReadElementContentAsString();
						if (extensionInfo.StartingMissionPath == "NONE")
						{
							extensionInfo.StartingMissionPath = null;
						}
					}
					if (xmlReader.Name == "StartingActions")
					{
						xmlReader.MoveToContent();
						extensionInfo.StartingActionsPath = xmlReader.ReadElementContentAsString();
						if (extensionInfo.StartingActionsPath == "NONE")
						{
							extensionInfo.StartingActionsPath = null;
						}
					}
					if (xmlReader.Name == "Description")
					{
						xmlReader.MoveToContent();
						extensionInfo.Description = Utils.CleanFilterStringToRenderable(xmlReader.ReadElementContentAsString());
					}
					if (xmlReader.Name == "Faction")
					{
						xmlReader.MoveToContent();
						extensionInfo.FactionDescriptorPaths.Add(xmlReader.ReadElementContentAsString());
					}
					if (xmlReader.Name == "StartsWithTutorial")
					{
						xmlReader.MoveToContent();
						extensionInfo.StartsWithTutorial = xmlReader.ReadElementContentAsString().ToLower() == "true";
					}
					if (xmlReader.Name == "HasIntroStartup")
					{
						xmlReader.MoveToContent();
						extensionInfo.HasIntroStartup = xmlReader.ReadElementContentAsString().ToLower() == "true";
					}
					if (xmlReader.Name == "StartingTheme")
					{
						xmlReader.MoveToContent();
						string theme = xmlReader.ReadElementContentAsString().ToLower();
						extensionInfo.Theme = theme;
					}
					if (xmlReader.Name == "IntroStartupSong")
					{
						xmlReader.MoveToContent();
						string introStartupSong = xmlReader.ReadElementContentAsString();
						extensionInfo.IntroStartupSong = introStartupSong;
					}
					if (xmlReader.Name == "IntroStartupSongDelay")
					{
						xmlReader.MoveToContent();
						float introStartupSongDelay = xmlReader.ReadElementContentAsFloat();
						extensionInfo.IntroStartupSongDelay = introStartupSongDelay;
					}
					if (xmlReader.Name == "SequencerSpinUpTime")
					{
						xmlReader.MoveToContent();
						float sequencerSpinUpTime = xmlReader.ReadElementContentAsFloat();
						extensionInfo.SequencerSpinUpTime = sequencerSpinUpTime;
					}
					if (xmlReader.Name == "ActionsToRunOnSequencerStart")
					{
						xmlReader.MoveToContent();
						string actionsToRunOnSequencerStart = xmlReader.ReadElementContentAsString();
						extensionInfo.ActionsToRunOnSequencerStart = actionsToRunOnSequencerStart;
					}
					if (xmlReader.Name == "SequencerFlagRequiredForStart")
					{
						xmlReader.MoveToContent();
						string sequencerFlagRequiredForStart = xmlReader.ReadElementContentAsString();
						extensionInfo.SequencerFlagRequiredForStart = sequencerFlagRequiredForStart;
					}
					if (xmlReader.Name == "SequencerTargetID")
					{
						xmlReader.MoveToContent();
						string sequencerTargetID = xmlReader.ReadElementContentAsString();
						extensionInfo.SequencerTargetID = sequencerTargetID;
					}
					if (xmlReader.Name == "WorkshopDescription")
					{
						xmlReader.MoveToContent();
						extensionInfo.WorkshopDescription = xmlReader.ReadElementContentAsString();
					}
					if (xmlReader.Name == "WorkshopVisibility")
					{
						xmlReader.MoveToContent();
						extensionInfo.WorkshopVisibility = (byte)xmlReader.ReadElementContentAsInt();
					}
					if (xmlReader.Name == "WorkshopTags")
					{
						xmlReader.MoveToContent();
						extensionInfo.WorkshopTags = xmlReader.ReadElementContentAsString();
					}
					if (xmlReader.Name == "WorkshopPreviewImagePath")
					{
						xmlReader.MoveToContent();
						extensionInfo.WorkshopPreviewImagePath = xmlReader.ReadElementContentAsString();
					}
					if (xmlReader.Name == "WorkshopLanguage")
					{
						xmlReader.MoveToContent();
						extensionInfo.WorkshopLanguage = xmlReader.ReadElementContentAsString();
					}
					if (xmlReader.Name == "WorkshopPublishID")
					{
						xmlReader.MoveToContent();
						extensionInfo.WorkshopPublishID = xmlReader.ReadElementContentAsString();
					}
					xmlReader.Read();
				}
			}
			string text2 = folderpath + "/Logo";
			bool flag = false;
			if (File.Exists(text2 + ".png"))
			{
				text2 += ".png";
				flag = true;
			}
			else if (File.Exists(text2 + ".jpg"))
			{
				text2 += ".png";
				flag = true;
			}
			if (flag)
			{
				using FileStream input = File.OpenRead(text2);
				extensionInfo.LogoImage = Texture2D.FromStream(Game1.getSingleton().GraphicsDevice, input);
			}
			return extensionInfo;
		}
		throw new FileNotFoundException("No extension info exists for folder " + folderpath);
	}

	public static void VerifyExtensionInfo(ExtensionInfo info)
	{
		if (info.Name == null)
		{
			throw new NullReferenceException("Extension must have a Name");
		}
		if (string.IsNullOrWhiteSpace(info.GetFoldersafeName()))
		{
			throw new NullReferenceException("Extension names require at least one letter or number in them");
		}
		if (!Directory.Exists(info.FolderPath))
		{
			throw new DirectoryNotFoundException("Directory folderpath could not be found");
		}
		if (!File.Exists(info.FolderPath + "/" + info.StartingMissionPath))
		{
			throw new FileNotFoundException("Starting Mission File does not exist!");
		}
		if (info.IntroStartupSong == null)
		{
			return;
		}
		string introStartupSong = info.IntroStartupSong;
		string text = introStartupSong;
		if (!introStartupSong.EndsWith(".ogg"))
		{
			text = introStartupSong + ".ogg";
		}
		string path = info.FolderPath + "/" + text;
		if (File.Exists(path))
		{
			MusicManager.loadAsCurrentSongUnsafe((info.FolderPath.StartsWith("Extensions") ? "../" : "") + info.FolderPath + "/" + introStartupSong);
			return;
		}
		path = "Music/" + text;
		if (File.Exists("Content/" + path))
		{
			MusicManager.loadAsCurrentSong(path.Replace(".ogg", ""));
		}
	}

	public static bool ExtensionExists(string folderpath)
	{
		return File.Exists(folderpath + "/ExtensionInfo.xml");
	}

	public string GetFoldersafeName()
	{
		string name = Name;
		name = name.Replace(" ", "_");
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char c in invalidFileNameChars)
		{
			name = name.Replace(string.Concat(c), "");
		}
		return name;
	}
}
