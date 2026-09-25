using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Hacknet;

public static class People
{
	private const int NUMBER_OF_PEOPLE = 200;

	private const int NUMBER_OF_HACKERS = 10;

	private const int NUMBER_OF_HUB_AGENTS = 22;

	public static List<Person> all;

	public static List<Person> hackers;

	public static List<Person> hubAgents;

	public static string[] maleNames;

	public static string[] femaleNames;

	public static string[] surnames;

	public static bool PeopleWereGeneratedWithDLCAdditions = false;

	public static void init()
	{
		string text = Utils.readEntireFile("Content/PersonData/MaleNames.txt").Replace("\r", "");
		maleNames = text.Split(Utils.newlineDelim);
		text = Utils.readEntireFile("Content/PersonData/FemaleNames.txt").Replace("\r", "");
		femaleNames = text.Split(Utils.newlineDelim);
		text = Utils.readEntireFile("Content/PersonData/Surnames.txt").Replace("\r", "");
		surnames = text.Split(Utils.newlineDelim);
		all = new List<Person>(200);
		int num = 0;
		DirectoryInfo directoryInfo = new DirectoryInfo("Content/People");
		FileInfo[] files = directoryInfo.GetFiles("*.xml");
		List<string> list = new List<string>();
		for (int i = 0; i < files.Length; i++)
		{
			list.Add("Content/People/" + Path.GetFileName(files[i].Name));
		}
		if (Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed)
		{
			directoryInfo = new DirectoryInfo("Content/DLC/People");
			files = directoryInfo.GetFiles("*.xml");
			for (int i = 0; i < files.Length; i++)
			{
				list.Add("Content/DLC/People/" + Path.GetFileName(files[i].Name));
			}
			PeopleWereGeneratedWithDLCAdditions = true;
		}
		for (int i = 0; i < list.Count; i++)
		{
			string filepath = list[i];
			filepath = LocalizedFileLoader.GetLocalizedFilepath(filepath);
			Person person = loadPersonFromFile(filepath);
			if (person != null)
			{
				all.Add(person);
				num++;
			}
			else
			{
				Console.WriteLine("Person Load Error: " + list[i]);
			}
		}
		for (int i = num; i < 200; i++)
		{
			bool flag = Utils.flipCoin();
			string fName = (flag ? maleNames[Utils.random.Next(maleNames.Length)] : femaleNames[Utils.random.Next(femaleNames.Length)]);
			string lName = surnames[Utils.random.Next(surnames.Length)];
			all.Add(new Person(fName, lName, flag, isHacker: false, UsernameGenerator.getName()));
		}
		hackers = new List<Person>();
		generatePeopleForList(hackers, 10, areHackers: true);
		hubAgents = new List<Person>();
		generatePeopleForList(hubAgents, 22, areHackers: true);
	}

	public static void LoadInDLCPeople()
	{
		if (!Settings.EnableDLC || !DLC1SessionUpgrader.HasDLC1Installed)
		{
			return;
		}
		DirectoryInfo directoryInfo = new DirectoryInfo("Content/DLC/People");
		FileInfo[] files = directoryInfo.GetFiles("*.xml");
		for (int i = 0; i < files.Length; i++)
		{
			string filepath = "Content/DLC/People/" + Path.GetFileName(files[i].Name);
			filepath = LocalizedFileLoader.GetLocalizedFilepath(filepath);
			Person person = loadPersonFromFile(filepath);
			if (person != null)
			{
				all.Insert(0, person);
			}
		}
	}

	public static void ReInitPeopleForExtension()
	{
		all.Clear();
		hackers.Clear();
		hubAgents.Clear();
		int num = 0;
		string text = Path.Combine(Utils.GetFileLoadPrefix(), "People");
		if (Directory.Exists(text))
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			FileInfo[] files = directoryInfo.GetFiles("*.xml");
			for (int i = 0; i < files.Length; i++)
			{
				string filepath = text + "/" + Path.GetFileName(files[i].Name);
				filepath = LocalizedFileLoader.GetLocalizedFilepath(filepath);
				Person person = loadPersonFromFile(filepath);
				if (person != null)
				{
					all.Insert(0, person);
					num++;
				}
			}
		}
		for (int i = num; i < 200; i++)
		{
			bool flag = Utils.flipCoin();
			string fName = (flag ? maleNames[Utils.random.Next(maleNames.Length)] : femaleNames[Utils.random.Next(femaleNames.Length)]);
			string lName = surnames[Utils.random.Next(surnames.Length)];
			all.Add(new Person(fName, lName, flag, isHacker: false, UsernameGenerator.getName()));
		}
		hackers = new List<Person>();
		generatePeopleForList(hackers, 10, areHackers: true);
		hubAgents = new List<Person>();
		generatePeopleForList(hubAgents, 22, areHackers: true);
	}

	private static void generatePeopleForList(List<Person> list, int numberToGenerate, bool areHackers = false)
	{
		for (int i = 0; i < numberToGenerate; i++)
		{
			bool flag = Utils.flipCoin();
			if (areHackers)
			{
				flag = !flag || !Utils.flipCoin();
			}
			string fName = (flag ? maleNames[Utils.random.Next(maleNames.Length)] : femaleNames[Utils.random.Next(femaleNames.Length)]);
			string lName = surnames[Utils.random.Next(surnames.Length)];
			Person item = new Person(fName, lName, flag, areHackers, UsernameGenerator.getName());
			list.Add(item);
			all.Add(item);
		}
	}

	public static Person loadPersonFromFile(string path)
	{
		try
		{
			using FileStream input = new FileStream(path, FileMode.Open);
			XmlReader xmlReader = XmlReader.Create(input, new XmlReaderSettings());
			while (xmlReader.Name != "Person")
			{
				xmlReader.Read();
			}
			string fName;
			string lName;
			string handle = (fName = (lName = "unknown"));
			bool male = true;
			bool isHacker = false;
			bool flag = false;
			if (xmlReader.MoveToAttribute("id"))
			{
				string text = xmlReader.ReadContentAsString();
			}
			if (xmlReader.MoveToAttribute("handle"))
			{
				handle = xmlReader.ReadContentAsString();
			}
			if (xmlReader.MoveToAttribute("firstName"))
			{
				fName = xmlReader.ReadContentAsString();
			}
			if (xmlReader.MoveToAttribute("lastName"))
			{
				lName = xmlReader.ReadContentAsString();
			}
			if (xmlReader.MoveToAttribute("isMale"))
			{
				male = xmlReader.ReadContentAsBoolean();
			}
			if (xmlReader.MoveToAttribute("isHacker"))
			{
				isHacker = xmlReader.ReadContentAsBoolean();
			}
			if (xmlReader.MoveToAttribute("forceHasNeopals"))
			{
				flag = xmlReader.ReadContentAsBoolean();
			}
			Person person = new Person(fName, lName, male, isHacker, handle);
			if (person.NeopalsAccount == null && flag && DLC1SessionUpgrader.HasDLC1Installed)
			{
				person.NeopalsAccount = NeopalsAccount.GenerateAccount(person.handle, Utils.flipCoin());
			}
			xmlReader.Read();
			while (!(xmlReader.Name == "Person") || xmlReader.IsStartElement())
			{
				switch (xmlReader.Name)
				{
				case "Degrees":
				{
					List<Degree> list = new List<Degree>();
					xmlReader.Read();
					while (!(xmlReader.Name == "Degrees") || xmlReader.IsStartElement())
					{
						if (xmlReader.Name == "Degree")
						{
							string text3;
							string uniName = (text3 = "UNKNOWN");
							double num = 3.0;
							if (xmlReader.MoveToAttribute("uni"))
							{
								uniName = xmlReader.ReadContentAsString();
							}
							if (xmlReader.MoveToAttribute("gpa"))
							{
								num = xmlReader.ReadContentAsDouble();
							}
							xmlReader.MoveToContent();
							text3 = xmlReader.ReadElementContentAsString();
							Degree item = new Degree(text3, uniName, (float)num);
							list.Add(item);
						}
						xmlReader.Read();
					}
					if (list.Count > 0)
					{
						person.degrees = list;
					}
					break;
				}
				case "Birthplace":
				{
					string text2 = null;
					if (xmlReader.MoveToAttribute("name"))
					{
						text2 = xmlReader.ReadContentAsString();
					}
					if (text2 == null)
					{
						text2 = WorldLocationLoader.getRandomLocation().name;
					}
					person.birthplace = WorldLocationLoader.getClosestOrCreate(text2);
					break;
				}
				case "DOB":
				{
					CultureInfo cultureInfo = new CultureInfo("en-au");
					xmlReader.MoveToContent();
					string input2 = xmlReader.ReadElementContentAsString();
					DateTime dateOfBirth = Utils.SafeParseDateTime(input2);
					if (dateOfBirth.Hour == 0 && dateOfBirth.Second == 0)
					{
						TimeSpan timeSpan = TimeSpan.FromHours(Utils.random.NextDouble() * 23.99);
						dateOfBirth += timeSpan;
					}
					person.DateOfBirth = dateOfBirth;
					break;
				}
				case "Medical":
					person.medicalRecord = MedicalRecord.Load(xmlReader, person.birthplace, person.DateOfBirth);
					break;
				}
				xmlReader.Read();
			}
			if (DLC1SessionUpgrader.HasDLC1Installed)
			{
				if (person.handle == "Minx" && person.NeopalsAccount == null)
				{
					person.NeopalsAccount = NeopalsAccount.GenerateAccount("Minx");
				}
				if (person.handle == "Orann" && person.NeopalsAccount == null && DLC1SessionUpgrader.HasDLC1Installed && PeopleWereGeneratedWithDLCAdditions)
				{
					person.NeopalsAccount = NeopalsAccount.GenerateAccount("Orann");
				}
			}
			return person;
		}
		catch (FileNotFoundException)
		{
			return null;
		}
	}

	public static void printAllPeople()
	{
		for (int i = 0; i < all.Count; i++)
		{
			Console.WriteLine("------------------------------------------\n" + all[i].ToString());
		}
	}
}
