using System;
using System.IO;
using System.Text;
using Hacknet.Extensions;
using Steamworks;

namespace Hacknet.PlatformAPI.Storage;

public class SteamCloudStorageMethod : BasicStorageMethod
{
	private bool deserialized = false;

	private string PathPrefix
	{
		get
		{
			if (Settings.IsInExtensionMode)
			{
				return ExtensionLoader.ActiveExtensionInfo.GetFoldersafeName() + "/";
			}
			return "";
		}
	}

	public override bool ShouldWrite => true;

	public override bool DidDeserialize => deserialized;

	public override void Load()
	{
		if (PlatformAPISettings.Running)
		{
			try
			{
				manifest = SaveFileManifest.Deserialize(this);
			}
			catch (NullReferenceException)
			{
			}
			catch (FormatException)
			{
			}
			if (manifest == null)
			{
				manifest = new SaveFileManifest();
				manifest.Save(this);
			}
			else
			{
				deserialized = true;
			}
		}
	}

	public override bool FileExists(string filename)
	{
		if (!PlatformAPISettings.Running)
		{
			return false;
		}
		return SteamRemoteStorage.FileExists(PathPrefix + filename);
	}

	public override Stream GetFileReadStream(string filename)
	{
		if (!PlatformAPISettings.Running)
		{
			return null;
		}
		int num = 2000000;
		byte[] array = new byte[num];
		if (!SteamRemoteStorage.FileExists(PathPrefix + filename))
		{
			return null;
		}
		int count = SteamRemoteStorage.FileRead(PathPrefix + filename, array, num);
		string @string = Encoding.UTF8.GetString(array);
		return new MemoryStream(array, 0, count);
	}

	public override void WriteFileData(string filename, byte[] data)
	{
		if (PlatformAPISettings.Running && !SteamRemoteStorage.FileWrite(PathPrefix + filename, data, data.Length))
		{
			Console.WriteLine("Failed to write to steam");
		}
	}

	public override void WriteFileData(string filename, string data)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		WriteFileData(filename, bytes);
	}
}
