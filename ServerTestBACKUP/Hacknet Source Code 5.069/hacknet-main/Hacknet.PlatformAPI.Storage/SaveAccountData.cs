using System;

namespace Hacknet.PlatformAPI.Storage;

public struct SaveAccountData
{
	private static string[] Delimiter = new string[2] { "\r\n__", "\n__" };

	public string Username;

	public string Password;

	public string FileUsername;

	public DateTime LastWriteTime;

	public static SaveAccountData ParseFromString(string input)
	{
		string[] array = input.Split(Delimiter, StringSplitOptions.None);
		string password = "";
		if (!string.IsNullOrEmpty(array[1]))
		{
			try
			{
				password = FileEncrypter.DecryptString(array[1])[2];
			}
			catch (FormatException)
			{
				Console.WriteLine("ACCOUNT AUTHENTICATION DETAILS CORRUPT : " + array[0]);
				password = "";
			}
			catch (NullReferenceException)
			{
				Console.WriteLine("ACCOUNT AUTHENTICATION DETAILS REMOVED OR DELETED : " + array[0]);
				password = "";
			}
		}
		if (array.Length <= 3)
		{
			SaveAccountData result = default(SaveAccountData);
			result.Username = array[0];
			result.Password = password;
			result.FileUsername = array[2];
			return result;
		}
		SaveAccountData result2 = default(SaveAccountData);
		result2.Username = array[0];
		result2.Password = password;
		result2.LastWriteTime = Utils.SafeParseDateTime(array[2]);
		result2.FileUsername = array[3];
		return result2;
	}

	public string Serialize()
	{
		string text = "";
		text = text + Username + Delimiter[0];
		text = text + FileEncrypter.EncryptString(Password, "", "") + Delimiter[0];
		text = text + Utils.SafeWriteDateTime(LastWriteTime) + Delimiter[0];
		return text + FileUsername;
	}
}
