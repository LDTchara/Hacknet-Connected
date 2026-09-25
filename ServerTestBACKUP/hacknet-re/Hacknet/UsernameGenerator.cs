using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace Hacknet;

public static class UsernameGenerator
{
	public static string[] names;

	private static string[] delims = new string[1] { "\r\n\r\n" };

	private static int nameIndex;

	public static void init()
	{
		StreamReader streamReader = new StreamReader(TitleContainer.OpenStream("Content/Usernames.txt"));
		string text = streamReader.ReadToEnd();
		streamReader.Close();
		names = text.Split(delims, StringSplitOptions.RemoveEmptyEntries);
		nameIndex = (int)(Utils.random.NextDouble() * (double)(names.Length - 1));
	}

	public static string getName()
	{
		nameIndex = (nameIndex + 1) % (names.Length - 1);
		return names[nameIndex];
	}
}
