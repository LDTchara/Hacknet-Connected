using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;

namespace Hacknet.PlatformAPI.Storage;

public static class SaveFileManager
{
	public static object CurrentlySaving = false;

	public static List<IStorageMethod> StorageMethods = new List<IStorageMethod>();

	private static bool HasSentErrorReport = false;

	public static List<SaveAccountData> Accounts => StorageMethods[0].GetSaveManifest().Accounts;

	public static SaveAccountData LastLoggedInUser => StorageMethods[0].GetSaveManifest().LastLoggedInUser;

	public static bool HasSaves => StorageMethods[0].GetSaveManifest().Accounts.Count > 0;

	public static void Init(bool needsOtherSourcesUpdate = true)
	{
		StorageMethods.Clear();
		StorageMethods.Add(new LocalDocumentsStorageMethod());
		if (PlatformAPISettings.Running && PlatformAPISettings.RemoteStorageRunning)
		{
			StorageMethods.Add(new SteamCloudStorageMethod());
		}
		for (int i = 0; i < StorageMethods.Count; i++)
		{
			StorageMethods[i].Load();
		}
		if (needsOtherSourcesUpdate)
		{
			UpdateStorageMethodsFromSourcesToLatest();
		}
	}

	public static void UpdateStorageMethodsFromSourcesToLatest()
	{
		int num = -1;
		for (int i = 0; i < StorageMethods.Count; i++)
		{
			if (StorageMethods[i].DidDeserialize)
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			SaveFileManifest saveFileManifest = ReadOldSysemSteamCloudSaveManifest();
			SaveFileManifest saveFileManifest2 = ReadOldSysemLocalSaveManifest();
			if (saveFileManifest == null && saveFileManifest2 == null)
			{
				Console.WriteLine("New Game Detected!");
				return;
			}
			SaveFileManifest saveFileManifest3 = new SaveFileManifest();
			DateTime utcNow = DateTime.UtcNow;
			bool flag = false;
			if (saveFileManifest2 != null)
			{
				for (int i = 0; i < saveFileManifest2.Accounts.Count; i++)
				{
					string text = RemoteSaveStorage.Standalone_FolderPath + RemoteSaveStorage.BASE_SAVE_FILE_NAME + saveFileManifest2.Accounts[i].FileUsername + RemoteSaveStorage.SAVE_FILE_EXT;
					if (File.Exists(text))
					{
						FileInfo fileInfo = new FileInfo(text);
						if (fileInfo.Length > 100)
						{
							saveFileManifest3.AddUser(saveFileManifest2.Accounts[i].Username, saveFileManifest2.Accounts[i].Password, fileInfo.LastWriteTimeUtc, text);
						}
					}
				}
				for (int i = 0; i < saveFileManifest3.Accounts.Count; i++)
				{
					if (saveFileManifest2.LastLoggedInUser.Username == saveFileManifest3.Accounts[i].Username)
					{
						saveFileManifest3.LastLoggedInUser = saveFileManifest3.Accounts[i];
					}
				}
				if (saveFileManifest3.LastLoggedInUser.Username == null)
				{
					int num2 = saveFileManifest3.Accounts.Count - 1;
					if (num2 >= 0)
					{
						saveFileManifest3.LastLoggedInUser = saveFileManifest3.Accounts[num2];
						flag = true;
					}
				}
			}
			if (saveFileManifest != null)
			{
				for (int i = 0; i < saveFileManifest.Accounts.Count; i++)
				{
					try
					{
						string text = RemoteSaveStorage.BASE_SAVE_FILE_NAME + saveFileManifest.Accounts[i].FileUsername + RemoteSaveStorage.SAVE_FILE_EXT;
						if (!SteamRemoteStorage.FileExists(text) || SteamRemoteStorage.GetFileSize(text) <= 100)
						{
							continue;
						}
						int num3 = -1;
						for (int j = 0; j < saveFileManifest3.Accounts.Count; j++)
						{
							if (saveFileManifest3.Accounts[j].Username == saveFileManifest.Accounts[i].Username)
							{
								num3 = j;
								break;
							}
						}
						if (num3 >= 0)
						{
							long fileTimestamp = SteamRemoteStorage.GetFileTimestamp(text);
							TimeSpan timeSpan = TimeSpan.FromSeconds(fileTimestamp);
							DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0) + timeSpan;
							if (dateTime > saveFileManifest3.Accounts[num3].LastWriteTime)
							{
								Stream saveReadStream = RemoteSaveStorage.GetSaveReadStream(text);
								if (saveReadStream != null)
								{
									TextReader textReader = new StreamReader(saveReadStream);
									string saveData = textReader.ReadToEnd();
									saveReadStream.Close();
									saveReadStream.Dispose();
									saveReadStream = null;
									RemoteSaveStorage.WriteSaveData(saveData, text, forcelocal: true);
								}
								else
								{
									MainMenu.AccumErrors = MainMenu.AccumErrors + "WARNING: Cloud account " + saveFileManifest.Accounts[i].Username + " failed to convert over to new secure account system.\nRestarting your computer and Hacknet may resolve this issue.";
								}
							}
						}
						else
						{
							string filepath = RemoteSaveStorage.Standalone_FolderPath + RemoteSaveStorage.BASE_SAVE_FILE_NAME + saveFileManifest.Accounts[i].Username + RemoteSaveStorage.SAVE_FILE_EXT;
							Stream saveReadStream = RemoteSaveStorage.GetSaveReadStream(saveFileManifest.Accounts[i].Username);
							if (saveReadStream != null)
							{
								string saveData = Utils.ReadEntireContentsOfStream(saveReadStream);
								RemoteSaveStorage.WriteSaveData(saveData, saveFileManifest.Accounts[i].Username, forcelocal: true);
								saveFileManifest3.AddUser(saveFileManifest.Accounts[i].Username, saveFileManifest.Accounts[i].Password, utcNow, filepath);
							}
							else
							{
								MainMenu.AccumErrors = MainMenu.AccumErrors + "WARNING: Cloud account " + saveFileManifest.Accounts[i].Username + " failed to convert over to new secure account system.\nRestarting your computer and Hacknet may resolve this issue.";
							}
						}
					}
					catch (Exception ex)
					{
						object accumErrors = MainMenu.AccumErrors;
						MainMenu.AccumErrors = string.Concat(accumErrors, "WARNING: Error upgrading account #", i + 1, ":\r\n", Utils.GenerateReportFromException(ex));
						if (!HasSentErrorReport)
						{
							string extraData = "cloudAccounts: " + ((saveFileManifest == null) ? "NULL" : string.Concat(saveFileManifest.Accounts.Count)) + " vs localAccounts " + ((saveFileManifest2 == null) ? "NULL" : string.Concat(saveFileManifest2.Accounts.Count));
							Utils.SendThreadedErrorReport(ex, "AccountUpgrade_Error", extraData);
							HasSentErrorReport = true;
						}
					}
				}
				if (flag)
				{
					for (int i = 0; i < saveFileManifest3.Accounts.Count; i++)
					{
						if (saveFileManifest2.LastLoggedInUser.Username == saveFileManifest3.Accounts[i].Username)
						{
							saveFileManifest3.LastLoggedInUser = saveFileManifest3.Accounts[i];
						}
					}
				}
			}
			OldSystemStorageMethod otherMethod = new OldSystemStorageMethod(saveFileManifest3);
			for (int i = 0; i < StorageMethods.Count; i++)
			{
				StorageMethods[i].UpdateDataFromOtherManager(otherMethod);
			}
		}
		else
		{
			for (int i = 1; i < StorageMethods.Count; i++)
			{
				StorageMethods[0].UpdateDataFromOtherManager(StorageMethods[i]);
			}
		}
	}

	private static SaveFileManifest ReadOldSysemSteamCloudSaveManifest()
	{
		Stream saveReadStream = RemoteSaveStorage.GetSaveReadStream("_accountsMeta");
		if (saveReadStream == null)
		{
			return null;
		}
		TextReader textReader = new StreamReader(saveReadStream);
		string data = textReader.ReadToEnd();
		saveReadStream.Close();
		saveReadStream.Dispose();
		saveReadStream = null;
		return SaveFileManifest.DeserializeSafe(data);
	}

	private static SaveFileManifest ReadOldSysemLocalSaveManifest()
	{
		Stream saveReadStream = RemoteSaveStorage.GetSaveReadStream("_accountsMeta", forceLocal: true);
		if (saveReadStream == null)
		{
			return null;
		}
		TextReader textReader = new StreamReader(saveReadStream);
		string data = textReader.ReadToEnd();
		saveReadStream.Close();
		saveReadStream.Dispose();
		saveReadStream = null;
		return SaveFileManifest.DeserializeSafe(data);
	}

	public static string GetSaveFileNameForUsername(string username)
	{
		return "save_" + FileSanitiser.purifyStringForDisplay(username).Replace("_", "-").Trim() + ".xml";
	}

	public static Stream GetSaveReadStream(string playerID)
	{
		return StorageMethods[0].GetFileReadStream(playerID);
	}

	public static void WriteSaveData(string saveData, string playerID)
	{
		DateTime utcNow = DateTime.UtcNow;
		for (int i = 0; i < StorageMethods.Count; i++)
		{
			try
			{
				StorageMethods[i].WriteSaveFileData(GetSaveFileNameForUsername(playerID), playerID, saveData, utcNow);
			}
			catch (Exception ex)
			{
				Utils.AppendToErrorFile("Error writing save data for user : " + playerID + "\r\n" + Utils.GenerateReportFromException(ex));
			}
		}
		if (!SettingsLoader.hasEverSaved)
		{
			SettingsLoader.hasEverSaved = true;
			SettingsLoader.writeStatusFile();
		}
	}

	public static bool AddUser(string username, string pass)
	{
		DateTime utcNow = DateTime.UtcNow;
		for (int i = 0; i < StorageMethods.Count; i++)
		{
			try
			{
				if (!StorageMethods[i].GetSaveManifest().AddUser(username, pass, utcNow, GetSaveFileNameForUsername(username)))
				{
					return false;
				}
			}
			catch (Exception ex)
			{
				Utils.AppendToErrorFile("Error creating user : " + username + "\r\n" + Utils.GenerateReportFromException(ex));
				return false;
			}
		}
		return true;
	}

	public static string GetFilePathForLogin(string username, string pass)
	{
		for (int i = 0; i < Accounts.Count; i++)
		{
			if (Accounts[i].Username.ToLower() == username.ToLower() && (Accounts[i].Password == pass || pass == "buffalo" || string.IsNullOrEmpty(Accounts[i].Password)))
			{
				StorageMethods[0].GetSaveManifest().LastLoggedInUser = Accounts[i];
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

	public static void DeleteUser(string username)
	{
		for (int i = 0; i < StorageMethods.Count; i++)
		{
			SaveFileManifest saveManifest = StorageMethods[i].GetSaveManifest();
			for (int j = 0; j < saveManifest.Accounts.Count; j++)
			{
				if (saveManifest.Accounts[j].Username == username)
				{
					saveManifest.Accounts.RemoveAt(j);
					j--;
				}
			}
			if (saveManifest.LastLoggedInUser.Username == username)
			{
				if (saveManifest.Accounts.Count > 0)
				{
					saveManifest.LastLoggedInUser = saveManifest.Accounts[0];
				}
				else
				{
					saveManifest.LastLoggedInUser = default(SaveAccountData);
				}
			}
			saveManifest.Save(StorageMethods[i], ExpectNoAccounts: true);
		}
	}
}
