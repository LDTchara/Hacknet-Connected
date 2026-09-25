using System;
using System.Text;

namespace Hacknet;

public static class FileEncrypter
{
	private static string[] HeaderSplitDelimiters = new string[1] { "::" };

	public static string EncryptString(string data, string header, string ipLink, string pass = "", string fileExtension = null)
	{
		if (string.IsNullOrWhiteSpace(data))
		{
			data = "";
		}
		ushort passCodeFromString = GetPassCodeFromString(pass);
		ushort passCodeFromString2 = GetPassCodeFromString("");
		StringBuilder stringBuilder = new StringBuilder();
		string text = "#DEC_ENC::" + Encrypt(header, passCodeFromString2) + "::" + Encrypt(ipLink, passCodeFromString2) + "::" + Encrypt("ENCODED", passCodeFromString);
		if (fileExtension != null)
		{
			text = text + "::" + Encrypt(fileExtension, passCodeFromString2);
		}
		stringBuilder.Append(text);
		stringBuilder.Append("\r\n");
		stringBuilder.Append(Encrypt(data, passCodeFromString));
		return stringBuilder.ToString();
	}

	private static ushort GetPassCodeFromString(string code)
	{
		return (ushort)code.GetHashCode();
	}

	private static string Encrypt(string data, ushort passcode)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < data.Length; i++)
		{
			int num = data[i] * 1822 + 32767 + passcode;
			stringBuilder.Append(num + " ");
		}
		return stringBuilder.ToString().Trim();
	}

	private static string Decrypt(string data, ushort passcode)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string[] array = data.Split(Utils.spaceDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			int num = Convert.ToInt32(array[i]);
			int num2 = 32767;
			int num3 = num - num2 - passcode;
			num3 /= 1822;
			stringBuilder.Append((char)num3);
		}
		return stringBuilder.ToString().Trim();
	}

	public static string[] DecryptString(string data, string pass = "")
	{
		if (string.IsNullOrEmpty(data))
		{
			throw new NullReferenceException("String to decrypt cannot be null or empty");
		}
		string[] array = new string[6];
		ushort passCodeFromString = GetPassCodeFromString(pass);
		ushort passCodeFromString2 = GetPassCodeFromString("");
		string[] array2 = data.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
		if (array2.Length < 2)
		{
			throw new FormatException("Tried to decrypt an invalid valid DEC ENC file \"" + data + "\" - not enough elements. Need 2 lines, had " + array2.Length);
		}
		string[] array3 = array2[0].Split(HeaderSplitDelimiters, StringSplitOptions.None);
		if (array3.Length < 4)
		{
			throw new FormatException("Tried to decrypt an invalid valid DEC ENC file \"" + data + "\" - not enough headers");
		}
		string text = Decrypt(array3[1], passCodeFromString2);
		string text2 = Decrypt(array3[2], passCodeFromString2);
		string text3 = Decrypt(array3[3], passCodeFromString);
		string text4 = null;
		if (array3.Length > 4)
		{
			text4 = Decrypt(array3[4], passCodeFromString2);
		}
		string text5 = null;
		string text6 = "1";
		if (text3 == "ENCODED")
		{
			text5 = Decrypt(array2[1], passCodeFromString);
		}
		else
		{
			text6 = "0";
		}
		array[0] = text;
		array[1] = text2;
		array[2] = text5;
		array[3] = text4;
		array[4] = text6;
		array[5] = text3;
		return array;
	}

	internal static string[] TestingDecryptString(string data, ushort pass)
	{
		if (string.IsNullOrEmpty(data))
		{
			throw new NullReferenceException("String to decrypt cannot be null or empty");
		}
		string[] array = new string[6];
		ushort passcode = pass;
		ushort passCodeFromString = GetPassCodeFromString("");
		string[] array2 = data.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
		if (array2.Length < 2)
		{
			throw new FormatException("Tried to decrypt an invalid valid DEC ENC file \"" + data + "\" - not enough elements. Need 2 lines, had " + array2.Length);
		}
		string[] array3 = array2[0].Split(HeaderSplitDelimiters, StringSplitOptions.None);
		if (array3.Length < 4)
		{
			throw new FormatException("Tried to decrypt an invalid valid DEC ENC file \"" + data + "\" - not enough headers");
		}
		string text = Decrypt(array3[1], passCodeFromString);
		string text2 = Decrypt(array3[2], passCodeFromString);
		string text3 = Decrypt(array3[3], passcode);
		string text4 = null;
		if (array3.Length > 4)
		{
			text4 = Decrypt(array3[4], passCodeFromString);
		}
		string text5 = null;
		string text6 = "1";
		if (text3 == "ENCODED")
		{
			text5 = Decrypt(array2[1], passcode);
		}
		else
		{
			text6 = "0";
		}
		array[0] = text;
		array[1] = text2;
		array[2] = text5;
		array[3] = text4;
		array[4] = text6;
		array[5] = text3;
		return array;
	}

	public static string[] DecryptHeaders(string data, string pass = "")
	{
		string[] array = new string[3];
		ushort passCodeFromString = GetPassCodeFromString(pass);
		string[] array2 = data.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
		if (array2.Length < 2)
		{
			throw new FormatException("Tried to decrypt an invalid valid DEC ENC file");
		}
		string[] array3 = array2[0].Split(HeaderSplitDelimiters, StringSplitOptions.None);
		if (array3.Length < 4)
		{
			throw new FormatException("Tried to decrypt an invalid valid DEC ENC file");
		}
		string text = Decrypt(array3[1], passCodeFromString);
		string text2 = Decrypt(array3[2], passCodeFromString);
		string text3 = null;
		if (array3.Length > 4)
		{
			text3 = Decrypt(array3[4], passCodeFromString);
		}
		array[0] = text;
		array[1] = text2;
		array[2] = text3;
		return array;
	}

	public static int FileIsEncrypted(string data, string pass = "")
	{
		if (data.StartsWith("#DEC_ENC::"))
		{
			string[] array = DecryptString(data, pass);
			if (array[5] != "ENCODED")
			{
				if (array[4] == "0")
				{
					return 2;
				}
			}
			else
			{
				if (array[4] == "0")
				{
					return 2;
				}
				if (array[4] == "1")
				{
					return 1;
				}
			}
		}
		return 0;
	}

	public static string MakeReplacementsForDisplay(string input)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < input.Length; i++)
		{
			if (GuiData.tinyfont.Characters.Contains(input[i]))
			{
				stringBuilder.Append(input[i]);
			}
			else
			{
				stringBuilder.Append("_");
			}
		}
		return stringBuilder.ToString();
	}
}
