using System;
using System.IO;

namespace Hacknet.PlatformAPI.Storage;

public class BasicStorageMethod : IStorageMethod
{
	protected SaveFileManifest manifest;

	public virtual bool ShouldWrite
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool DidDeserialize
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual void Load()
	{
		throw new NotImplementedException();
	}

	public virtual SaveFileManifest GetSaveManifest()
	{
		return manifest;
	}

	public virtual Stream GetFileReadStream(string filename)
	{
		throw new NotImplementedException();
	}

	public virtual bool FileExists(string filename)
	{
		throw new NotImplementedException();
	}

	public virtual void WriteFileData(string filename, string data)
	{
		throw new NotImplementedException();
	}

	public virtual void WriteFileData(string filename, byte[] data)
	{
		throw new NotImplementedException();
	}

	public virtual void WriteSaveFileData(string filename, string username, string data, DateTime utcSaveFileTime)
	{
		WriteFileData(filename, data);
		for (int i = 0; i < manifest.Accounts.Count; i++)
		{
			SaveAccountData lastLoggedInUser = manifest.Accounts[i];
			if (lastLoggedInUser.Username == username)
			{
				lastLoggedInUser.LastWriteTime = utcSaveFileTime;
				manifest.LastLoggedInUser = lastLoggedInUser;
				break;
			}
		}
		manifest.Save(this);
	}

	public virtual void UpdateDataFromOtherManager(IStorageMethod otherMethod)
	{
		string username = manifest.LastLoggedInUser.Username;
		SaveFileManifest saveManifest = otherMethod.GetSaveManifest();
		for (int i = 0; i < saveManifest.Accounts.Count; i++)
		{
			SaveAccountData saveAccountData = saveManifest.Accounts[i];
			bool flag = false;
			for (int j = 0; j < manifest.Accounts.Count; j++)
			{
				SaveAccountData saveAccountData2 = manifest.Accounts[j];
				if (!(saveAccountData2.Username == saveAccountData.Username))
				{
					continue;
				}
				flag = true;
				TimeSpan timeSpan = saveAccountData.LastWriteTime - saveAccountData2.LastWriteTime;
				if (!(saveAccountData.LastWriteTime > saveAccountData2.LastWriteTime) || !(timeSpan.TotalSeconds > 5.0))
				{
					break;
				}
				Stream fileReadStream = otherMethod.GetFileReadStream(saveAccountData.FileUsername);
				if (fileReadStream != null)
				{
					string text = Utils.ReadEntireContentsOfStream(fileReadStream);
					if (text.Length > 100)
					{
						WriteFileData(saveAccountData2.FileUsername, text);
					}
				}
				break;
			}
			if (!flag)
			{
				Stream fileReadStream2 = otherMethod.GetFileReadStream(saveAccountData.FileUsername);
				if (fileReadStream2 != null)
				{
					string saveFileNameForUsername = SaveFileManager.GetSaveFileNameForUsername(saveAccountData.Username);
					manifest.AddUser(saveAccountData.Username, saveAccountData.Password, DateTime.UtcNow, saveFileNameForUsername);
					string text = Utils.ReadEntireContentsOfStream(fileReadStream2);
					WriteFileData(saveFileNameForUsername, text);
				}
			}
		}
		for (int j = 0; j < manifest.Accounts.Count; j++)
		{
			if (manifest.Accounts[j].Username == username)
			{
				manifest.LastLoggedInUser = manifest.Accounts[j];
			}
		}
		if (manifest.LastLoggedInUser.Username == null && manifest.Accounts.Count > 0)
		{
			manifest.LastLoggedInUser = manifest.Accounts[0];
		}
		manifest.Save(this);
	}
}
