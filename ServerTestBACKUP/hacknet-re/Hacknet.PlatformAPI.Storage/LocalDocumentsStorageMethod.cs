using System;
using System.IO;
using Hacknet.Extensions;
using SDL2;

namespace Hacknet.PlatformAPI.Storage;

public class LocalDocumentsStorageMethod : BasicStorageMethod
{
	private string FolderPath;

	private bool deserialized = false;

	private string FullFolderPath
	{
		get
		{
			if (Settings.IsInExtensionMode)
			{
				return FolderPath + ExtensionLoader.ActiveExtensionInfo.GetFoldersafeName() + "/";
			}
			return FolderPath;
		}
	}

	public override bool ShouldWrite => true;

	public override bool DidDeserialize => deserialized;

	public override void Load()
	{
		string text = SDL.SDL_GetPlatform();
		if (text.Equals("Linux"))
		{
			FolderPath = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
			if (string.IsNullOrEmpty(FolderPath))
			{
				FolderPath = Environment.GetEnvironmentVariable("HOME");
				if (string.IsNullOrEmpty(FolderPath))
				{
					FolderPath = "./";
				}
				else
				{
					FolderPath += "/.local/share/Hacknet/Accounts/";
				}
			}
			else
			{
				FolderPath += "/Hacknet/Accounts/";
			}
		}
		else if (text.Equals("Mac OS X"))
		{
			FolderPath = Environment.GetEnvironmentVariable("HOME");
			if (string.IsNullOrEmpty(FolderPath))
			{
				FolderPath = "./";
			}
			else
			{
				FolderPath += "/Library/Application Support/Hacknet/Accounts/";
			}
		}
		else
		{
			if (!text.Equals("Windows"))
			{
				throw new NotSupportedException("Unhandled SDL2 platform!");
			}
			FolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/My Games/Hacknet/Accounts/";
		}
		try
		{
			if (!Directory.Exists(FullFolderPath))
			{
				Directory.CreateDirectory(FullFolderPath);
			}
		}
		catch (UnauthorizedAccessException ex)
		{
			Utils.AppendToErrorFile("Local Documents Storage load error : Insufficient permissions for folder access.\r\n" + Utils.GenerateReportFromException(ex));
			FolderPath = "Accounts/";
			MainMenu.AccumErrors = MainMenu.AccumErrors + "\r\nERROR: Local Documents Storage Folder is refusing access. Saving accounts to:\r\n" + Path.GetFullPath(FolderPath);
		}
		try
		{
			manifest = SaveFileManifest.Deserialize(this);
		}
		catch (FormatException ex2)
		{
			manifest = null;
			Console.WriteLine("Local Save Manifest Corruption: " + Utils.GenerateReportFromException(ex2));
		}
		catch (NullReferenceException ex3)
		{
			manifest = null;
			Console.WriteLine("Local Save Manifest Corruption: " + Utils.GenerateReportFromException(ex3));
		}
		if (manifest == null)
		{
			manifest = new SaveFileManifest();
			manifest.Save(this, ExpectNoAccounts: true);
		}
		else
		{
			deserialized = true;
		}
	}

	public override bool FileExists(string filename)
	{
		return File.Exists(FullFolderPath + filename);
	}

	public override Stream GetFileReadStream(string filename)
	{
		return File.OpenRead(FullFolderPath + filename);
	}

	public override void WriteFileData(string filename, byte[] data)
	{
		if (!Directory.Exists(FullFolderPath))
		{
			Directory.CreateDirectory(FullFolderPath);
		}
		Utils.SafeWriteToFile(data, FullFolderPath + filename);
	}

	public override void WriteFileData(string filename, string data)
	{
		if (!Directory.Exists(FullFolderPath))
		{
			Directory.CreateDirectory(FullFolderPath);
		}
		Utils.SafeWriteToFile(data, FullFolderPath + filename);
	}
}
