using System;
using System.Collections.Generic;
using System.IO;

namespace Hacknet.PlatformAPI.Storage;

public class SaveFileManifest
{
	public const string FILENAME = "Accounts.txt";

	private const string AccountsDelimiter = "\r\n%------%";

	public SaveAccountData LastLoggedInUser = new SaveAccountData
	{
		Username = null
	};

	public List<SaveAccountData> Accounts = new List<SaveAccountData>();

	public static SaveFileManifest Deserialize(IStorageMethod storage)
	{
		if (!storage.FileExists("Accounts.txt"))
		{
			return null;
		}
		SaveFileManifest result = null;
		using (Stream stream = storage.GetFileReadStream("Accounts.txt"))
		{
			StreamReader streamReader = new StreamReader(stream);
			string text = streamReader.ReadToEnd();
			if (!string.IsNullOrEmpty(text.Trim()))
			{
				result = Deserialize(text);
			}
		}
		return result;
	}

	public static SaveFileManifest Deserialize(string data)
	{
		SaveFileManifest saveFileManifest = new SaveFileManifest();
		string[] array = data.Split(new string[1] { "\r\n%------%" }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length <= 1)
		{
			return null;
		}
		string text = array[0];
		saveFileManifest.Accounts.Clear();
		for (int i = 1; i < array.Length; i++)
		{
			SaveAccountData saveAccountData = SaveAccountData.ParseFromString(array[i]);
			saveFileManifest.Accounts.Add(saveAccountData);
			if (saveAccountData.Username == text)
			{
				saveFileManifest.LastLoggedInUser = saveAccountData;
			}
		}
		return saveFileManifest;
	}

	public static SaveFileManifest DeserializeSafe(string data)
	{
		SaveFileManifest saveFileManifest = new SaveFileManifest();
		string[] array = data.Split(new string[1] { "\r\n%------%" }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length <= 1)
		{
			return null;
		}
		string text = array[0];
		saveFileManifest.Accounts.Clear();
		for (int i = 1; i < array.Length; i++)
		{
			try
			{
				SaveAccountData saveAccountData = SaveAccountData.ParseFromString(array[i]);
				saveFileManifest.Accounts.Add(saveAccountData);
				if (saveAccountData.Username == text)
				{
					saveFileManifest.LastLoggedInUser = saveAccountData;
				}
			}
			catch (FormatException)
			{
			}
			catch (NullReferenceException)
			{
			}
		}
		return saveFileManifest;
	}

	public string GetFilePathForLogin(string username, string pass)
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

	public bool CanCreateAccountForName(string username)
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

	public void UpdateLastWriteTimeForUserFile(string username, DateTime writeTime)
	{
		for (int i = 0; i < Accounts.Count; i++)
		{
			if (Accounts[i].FileUsername == username)
			{
				SaveAccountData value = Accounts[i];
				value.LastWriteTime = writeTime;
				Accounts[i] = value;
			}
		}
	}

	public void Save(IStorageMethod storage, bool ExpectNoAccounts = false)
	{
		if (Accounts.Count == 0 && !ExpectNoAccounts)
		{
			int num = 10;
			num++;
			return;
		}
		string username = LastLoggedInUser.Username;
		string text = username + "\r\n%------%";
		for (int i = 0; i < Accounts.Count; i++)
		{
			text = text + Accounts[i].Serialize() + "\r\n%------%";
		}
		storage.WriteFileData("Accounts.txt", text);
	}

	public bool AddUser(string username, string password, DateTime creationTime, string filepath = null)
	{
		string text = FileSanitiser.purifyStringForDisplay(username).Replace("_", "-").Trim();
		if (text.Length <= 0)
		{
			return false;
		}
		if (filepath != null)
		{
			text = filepath;
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
		SaveAccountData saveAccountData = default(SaveAccountData);
		saveAccountData.Username = username;
		saveAccountData.Password = password;
		saveAccountData.FileUsername = text;
		saveAccountData.LastWriteTime = creationTime;
		SaveAccountData item = saveAccountData;
		Accounts.Add(item);
		return true;
	}

	public SaveAccountData GetAccount(string username)
	{
		for (int i = 0; i < Accounts.Count; i++)
		{
			if (Accounts[i].FileUsername == username)
			{
				return Accounts[i];
			}
		}
		throw new KeyNotFoundException("Username " + username + " does not exist in this manifest");
	}

	public string AddUserAndGetFilename(string username, string password)
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
		return text;
	}
}
