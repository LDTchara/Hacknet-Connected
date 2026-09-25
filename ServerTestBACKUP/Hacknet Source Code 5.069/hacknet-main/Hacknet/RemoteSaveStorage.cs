using System;
using System.IO;
using System.Text;
using Steamworks;

namespace Hacknet;

public static class RemoteSaveStorage
{
	public static string BASE_SAVE_FILE_NAME = "save";

	public static string SAVE_FILE_EXT = ".xml";

	public static string Standalone_FolderPath = "Accounts/";

	public static bool FileExists(string playerID, bool forceLocal = false)
	{
		string text = BASE_SAVE_FILE_NAME + playerID + SAVE_FILE_EXT;
		if (!forceLocal && PlatformAPISettings.Running)
		{
			return SteamRemoteStorage.FileExists(text);
		}
		return File.Exists(Standalone_FolderPath + text);
	}

	public static Stream GetSaveReadStream(string playerID, bool forceLocal = false)
	{
		string text = BASE_SAVE_FILE_NAME + playerID + SAVE_FILE_EXT;
		if (!forceLocal && PlatformAPISettings.Running)
		{
			int num = 2000000;
			byte[] array = new byte[num];
			if (!SteamRemoteStorage.FileExists(text))
			{
				return null;
			}
			int num2 = SteamRemoteStorage.FileRead(text, array, num);
			if (num2 == 0)
			{
				return null;
			}
			string @string = Encoding.UTF8.GetString(array);
			return new MemoryStream(array, 0, num2);
		}
		if (File.Exists(Standalone_FolderPath + text))
		{
			return File.OpenRead(Standalone_FolderPath + text);
		}
		return null;
	}

	public static void WriteSaveData(string saveData, string playerID, bool forcelocal = false)
	{
		string text = BASE_SAVE_FILE_NAME + playerID + SAVE_FILE_EXT;
		if (!forcelocal || !PlatformAPISettings.Running)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(saveData);
			if (!SteamRemoteStorage.FileWrite(text, bytes, bytes.Length))
			{
				Console.WriteLine("Failed to write to steam");
			}
			try
			{
				Utils.SafeWriteToFile(saveData, Standalone_FolderPath + text);
				return;
			}
			catch (Exception ex)
			{
				Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
				return;
			}
		}
		Utils.SafeWriteToFile(saveData, Standalone_FolderPath + text);
	}

	public static void Delete(string playerID)
	{
		string text = BASE_SAVE_FILE_NAME + playerID + SAVE_FILE_EXT;
		if (PlatformAPISettings.Running)
		{
			SteamRemoteStorage.FileDelete(text);
		}
		else if (Directory.Exists(Standalone_FolderPath) && File.Exists(Standalone_FolderPath + text))
		{
			File.Delete(Standalone_FolderPath + text);
		}
	}

	public static bool CanLoad(string playerID)
	{
		string text = BASE_SAVE_FILE_NAME + playerID + SAVE_FILE_EXT;
		if (PlatformAPISettings.Running)
		{
			return SteamRemoteStorage.FileExists(text);
		}
		return File.Exists(Standalone_FolderPath + text);
	}
}
