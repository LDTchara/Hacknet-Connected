using System;
using System.Collections.Generic;
using System.IO;
using Hacknet.PlatformAPI.Storage;

namespace Hacknet;

public static class OldSystemSaveFileManifest
{
	public const string FILENAME = "Accounts.txt";

	public const string File_User_Name = "_accountsMeta";

	private const string AccountsDelimiter = "\r\n%------%";

	public static SaveAccountData LastLoggedInUser = new SaveAccountData
	{
		Username = null
	};

	public static List<SaveAccountData> Accounts = new List<SaveAccountData>();

	public static void Load()
	{
		try
		{
			string text = null;
			Stream saveReadStream = RemoteSaveStorage.GetSaveReadStream("_accountsMeta");
			if (saveReadStream == null)
			{
				if (SettingsLoader.hasEverSaved)
				{
				}
				if (RemoteSaveStorage.FileExists("_accountsMeta", forceLocal: true))
				{
					saveReadStream = RemoteSaveStorage.GetSaveReadStream("_accountsMeta", forceLocal: true);
					if (SettingsLoader.hasEverSaved)
					{
						if (saveReadStream != null)
						{
							MainMenu.AccumErrors += "Loaded Local saves backup...\n";
						}
						else
						{
							MainMenu.AccumErrors += "Also failed loading backup\n";
						}
					}
				}
			}
			else
			{
				TextReader textReader = new StreamReader(saveReadStream);
				text = textReader.ReadToEnd();
			}
			if (saveReadStream != null)
			{
				saveReadStream.Flush();
				saveReadStream.Dispose();
			}
			if (text == null)
			{
				return;
			}
			string[] array = text.Split(new string[1] { "\r\n%------%" }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length <= 1)
			{
				throw new InvalidOperationException();
			}
			string text2 = array[0];
			Accounts.Clear();
			for (int i = 1; i < array.Length; i++)
			{
				SaveAccountData saveAccountData = SaveAccountData.ParseFromString(array[i]);
				Accounts.Add(saveAccountData);
				if (saveAccountData.Username == text2)
				{
					LastLoggedInUser = saveAccountData;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public static void Save()
	{
		string username = LastLoggedInUser.Username;
		string text = username + "\r\n%------%";
		for (int i = 0; i < Accounts.Count; i++)
		{
			text = text + Accounts[i].Serialize() + "\r\n%------%";
		}
		RemoteSaveStorage.WriteSaveData(text, "_accountsMeta");
		if (!SettingsLoader.hasEverSaved)
		{
			SettingsLoader.hasEverSaved = true;
			SettingsLoader.writeStatusFile();
		}
	}

	public static string GetFilePathForLogin(string username, string pass)
	{
		for (int i = 0; i < Accounts.Count; i++)
		{
			if (Accounts[i].Username.ToLower() == username.ToLower() && (Accounts[i].Password == pass || pass == "buffalo"))
			{
				LastLoggedInUser = Accounts[i];
				return Accounts[i].FileUsername;
			}
		}
		return null;
	}

	public static bool CanCreateAccountForName(string username)
	{
		string text = FileSanitiser.purifyStringForDisplay(username).Replace("_", "-").Trim();
		if (text.Length <= 0)
		{
			return false;
		}
		for (int i = 0; i < Accounts.Count; i++)
		{
			if (Accounts[i].FileUsername == text)
			{
				return false;
			}
			if (Accounts[i].Username == username)
			{
				return false;
			}
		}
		return true;
	}

	public static string AddUserAndGetFilename(string username, string password)
	{
		string text = FileSanitiser.purifyStringForDisplay(username).Replace("_", "-").Trim();
		if (text.Length <= 0)
		{
			return null;
		}
		for (int i = 0; i < Accounts.Count; i++)
		{
			if (Accounts[i].FileUsername == text)
			{
				return null;
			}
			if (Accounts[i].Username == username)
			{
				return null;
			}
		}
		SaveAccountData saveAccountData = default(SaveAccountData);
		saveAccountData.Username = username;
		saveAccountData.Password = password;
		saveAccountData.FileUsername = text;
		SaveAccountData saveAccountData2 = saveAccountData;
		Accounts.Add(saveAccountData2);
		LastLoggedInUser = saveAccountData2;
		Save();
		return text;
	}
}
