using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Hacknet.PlatformAPI.Storage;

namespace Hacknet.Misc;

public static class DLCExtendedTests
{
	public static string TesExtendedFunctionality(ScreenManager screenMan, out int errorsAdded)
	{
		string text = "";
		int errorsAdded2 = 0;
		int num = 0;
		text += TestConditionalActionSets(screenMan, out errorsAdded2);
		num += errorsAdded2;
		text += TestAdvancedConditionalActionSets(screenMan, out errorsAdded2);
		num += errorsAdded2;
		text += TestConditionalActionSetCollections(screenMan, out errorsAdded2);
		num += errorsAdded2;
		text += TestConditionalActionSetCollections2(screenMan, out errorsAdded2);
		num += errorsAdded2;
		text += TestConditionalActionSetCollectionsOnOS(screenMan, out errorsAdded2);
		num += errorsAdded2;
		text += TestObjectSerializer(screenMan, out errorsAdded2);
		num += errorsAdded2;
		text += TestDLCSessionUpgrader(screenMan, out errorsAdded2);
		num += errorsAdded2;
		object obj = text;
		text = string.Concat(obj, (errorsAdded2 > 0) ? "\r\n" : " ", "Complete - ", errorsAdded2, " errors found");
		errorsAdded = num;
		return text;
	}

	public static string TestConditionalActionSets(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		using (FileStream input = File.OpenRead("Content/Tests/DLCTests/TestConditionalActionSet.xml"))
		{
			XmlReader xmlReader = XmlReader.Create(input);
			SerializableConditionalActionSet serializableConditionalActionSet = SerializableConditionalActionSet.Deserialize(xmlReader);
			if (!Directory.Exists("Content/Tests/Output/"))
			{
				Directory.CreateDirectory("Content/Tests/Output");
			}
			File.WriteAllText("Content/Tests/Output/TestConditionalActionSetsOutput.txt", serializableConditionalActionSet.GetSaveString());
			using (FileStream input2 = File.OpenRead("Content/Tests/Output/TestConditionalActionSetsOutput.txt"))
			{
				XmlReader rdr = XmlReader.Create(input2);
				SerializableConditionalActionSet serializableConditionalActionSet2 = SerializableConditionalActionSet.Deserialize(rdr);
				if (serializableConditionalActionSet.Actions.Count != serializableConditionalActionSet2.Actions.Count)
				{
					num++;
					object obj = text;
					text = string.Concat(obj, "\r\n\r\nConditional Action Sets are Broken! Expected 2 actions, got ", serializableConditionalActionSet.Actions.Count, " and ", serializableConditionalActionSet2.Actions.Count);
				}
			}
			xmlReader.Close();
		}
		GitCommitEntry gitCommitEntry = new GitCommitEntry();
		gitCommitEntry.EntryNumber = 1;
		errorsAdded = num;
		return text;
	}

	public static string TestAdvancedConditionalActionSets(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		using (FileStream input = File.OpenRead("Content/Tests/DLCTests/TestAdvancedConditionalActionSet.xml"))
		{
			XmlReader xmlReader = XmlReader.Create(input);
			SerializableConditionalActionSet serializableConditionalActionSet = SerializableConditionalActionSet.Deserialize(xmlReader);
			if (serializableConditionalActionSet.Actions.Count != 3)
			{
				num++;
				text = text + "\r\n\r\nAdvanced Conditional Action Sets are Broken! Expected 3 actions, got " + serializableConditionalActionSet.Actions.Count;
			}
			if (!Directory.Exists("Content/Tests/Output/"))
			{
				Directory.CreateDirectory("Content/Tests/Output");
			}
			File.WriteAllText("Content/Tests/Output/TestAdvConditionalActionSetsOutput.txt", serializableConditionalActionSet.GetSaveString());
			using (FileStream input2 = File.OpenRead("Content/Tests/Output/TestAdvConditionalActionSetsOutput.txt"))
			{
				XmlReader rdr = XmlReader.Create(input2);
				SerializableConditionalActionSet serializableConditionalActionSet2 = SerializableConditionalActionSet.Deserialize(rdr);
				if (serializableConditionalActionSet.Actions.Count != serializableConditionalActionSet2.Actions.Count)
				{
					num++;
					object obj = text;
					text = string.Concat(obj, "\r\n\r\nAdvanced Conditional Action Sets are Broken! Expected 2 actions, got ", serializableConditionalActionSet.Actions.Count, " and ", serializableConditionalActionSet2.Actions.Count);
				}
			}
			xmlReader.Close();
		}
		using (FileStream input = File.OpenRead("Content/Tests/DLCTests/TestAdvancedConditionalActionSet2.xml"))
		{
			XmlReader xmlReader = XmlReader.Create(input);
			SerializableConditionalActionSet serializableConditionalActionSet = SerializableConditionalActionSet.Deserialize(xmlReader);
			if (serializableConditionalActionSet.Actions.Count != 21)
			{
				num++;
				text = text + "\r\n\r\nAdvanced Conditional Action Sets are Broken! Expected 21 actions, got " + serializableConditionalActionSet.Actions.Count;
			}
		}
		errorsAdded = num;
		return text;
	}

	public static string TestConditionalActionSetCollections(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		using (FileStream input = File.OpenRead("Content/Tests/DLCTests/TestConditionalActionSetCollection.xml"))
		{
			XmlReader xmlReader = XmlReader.Create(input);
			RunnableConditionalActions runnableConditionalActions = RunnableConditionalActions.Deserialize(xmlReader);
			if (!Directory.Exists("Content/Tests/Output/"))
			{
				Directory.CreateDirectory("Content/Tests/Output");
			}
			File.WriteAllText("Content/Tests/Output/TestConditionalActionSetsOutput2.xml", runnableConditionalActions.GetSaveString());
			using (FileStream input2 = File.OpenRead("Content/Tests/Output/TestConditionalActionSetsOutput2.xml"))
			{
				XmlReader rdr = XmlReader.Create(input2);
				RunnableConditionalActions runnableConditionalActions2 = RunnableConditionalActions.Deserialize(rdr);
				if (runnableConditionalActions.Actions.Count != runnableConditionalActions2.Actions.Count || runnableConditionalActions.Actions.Count != 3)
				{
					num++;
					object obj = text;
					text = string.Concat(obj, "\r\n\r\nConditional Action Set Collections are Broken! Expected 3 actions, got ", runnableConditionalActions.Actions.Count, " and ", runnableConditionalActions2.Actions.Count);
				}
				if (runnableConditionalActions.Actions[0].Actions.Count != 2 || runnableConditionalActions.Actions[1].Actions.Count != 0)
				{
					num++;
					text += "\nSave on OS COnditional actions failed! Incorrect action contents on original deserialization. ";
				}
				if (runnableConditionalActions2.Actions[0].Actions.Count != 2 || runnableConditionalActions2.Actions[1].Actions.Count != 0)
				{
					num++;
					text += "\nSave on OS COnditional actions failed! Incorrect action contents on realo.";
				}
			}
			xmlReader.Close();
		}
		errorsAdded = num;
		return text;
	}

	public static string TestConditionalActionSetCollections2(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		using (FileStream input = File.OpenRead("Content/Tests/DLCTests/TestConditionalActionSetCollection2.xml"))
		{
			XmlReader xmlReader = XmlReader.Create(input);
			RunnableConditionalActions runnableConditionalActions = RunnableConditionalActions.Deserialize(xmlReader);
			if (!Directory.Exists("Content/Tests/Output/"))
			{
				Directory.CreateDirectory("Content/Tests/Output");
			}
			File.WriteAllText("Content/Tests/Output/TestConditionalActionSetsOutput2.xml", runnableConditionalActions.GetSaveString());
			using (FileStream input2 = File.OpenRead("Content/Tests/Output/TestConditionalActionSetsOutput2.xml"))
			{
				XmlReader rdr = XmlReader.Create(input2);
				RunnableConditionalActions runnableConditionalActions2 = RunnableConditionalActions.Deserialize(rdr);
				if (runnableConditionalActions.Actions.Count != runnableConditionalActions2.Actions.Count || runnableConditionalActions.Actions.Count != 1)
				{
					num++;
					object obj = text;
					text = string.Concat(obj, "\r\n\r\nConditional Action Set Collections are Broken! Expected 1 actions, got ", runnableConditionalActions.Actions.Count, " and ", runnableConditionalActions2.Actions.Count);
				}
				if (runnableConditionalActions.Actions[0].Actions.Count != 1)
				{
					num++;
					text += "\n\nSave on OS Conditional actions v2 failed! Incorrect action contents on original deserialization. \n\n";
				}
			}
			xmlReader.Close();
		}
		errorsAdded = num;
		return text;
	}

	public static string TestConditionalActionSetCollectionsOnOS(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		string text2 = "__hacknettestaccount";
		string pass = "__testingpassword";
		SaveFileManager.AddUser(text2, pass);
		string saveFileNameForUsername = SaveFileManager.GetSaveFileNameForUsername(text2);
		OS.TestingPassOnly = true;
		OS oS = new OS();
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text2;
		screenMan.AddScreen(oS, screenMan.controllingPlayer);
		oS.delayer.RunAllDelayedActions();
		RunnableConditionalActions conditionalActions = oS.ConditionalActions;
		oS.ConditionalActions.Actions.Add(new SerializableConditionalActionSet
		{
			Condition = new SCOnAdminGained(),
			Actions = new List<SerializableAction>()
		});
		oS.ConditionalActions.Actions.Add(new SerializableConditionalActionSet
		{
			Condition = new SCOnAdminGained(),
			Actions = new List<SerializableAction>()
		});
		oS.threadedSaveExecute();
		List<Computer> nodes = oS.netMap.nodes;
		screenMan.RemoveScreen(oS);
		OS.WillLoadSave = true;
		oS = new OS();
		oS.SaveGameUserName = saveFileNameForUsername;
		oS.SaveUserAccountName = text2;
		screenMan.AddScreen(oS, screenMan.controllingPlayer);
		oS.delayer.RunAllDelayedActions();
		Game1.getSingleton().IsMouseVisible = true;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		if (oS.ConditionalActions.Actions.Count != conditionalActions.Actions.Count)
		{
			num++;
			text = text + "Save on OS COnditional actions failed! Expected 2, got " + oS.ConditionalActions.Actions.Count;
		}
		screenMan.RemoveScreen(oS);
		OS.TestingPassOnly = false;
		errorsAdded = num;
		return text;
	}

	public static string TestObjectSerializer(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		List<string> list = new List<string>();
		string s = ObjectSerializer.SerializeObject(list);
		List<string> list2 = (List<string>)ObjectSerializer.DeserializeObject(Utils.GenerateStreamFromString(s), list.GetType());
		if (list2 == null || list2.Count != 0)
		{
			num++;
			text += "\nError deserializing empty list";
		}
		list.Add("test 1");
		list.Add("12345");
		list2 = (List<string>)ObjectSerializer.DeepCopy(list);
		if (list2.Count != 2 || list2[0] != "test 1" || list2[1] != "12345")
		{
			num++;
			text += "\nError deserializing empty list";
		}
		VehicleRegistration vehicleRegistration = new VehicleRegistration();
		vehicleRegistration.licenceNumber = "1123-123";
		vehicleRegistration.licencePlate = "11-11";
		vehicleRegistration.vehicle = new VehicleType
		{
			maker = "asdf",
			model = "another asdf"
		};
		VehicleRegistration vehicleRegistration2 = vehicleRegistration;
		VehicleRegistration to = (VehicleRegistration)ObjectSerializer.DeepCopy(vehicleRegistration2);
		if (!Utils.PublicInstancePropertiesEqual(vehicleRegistration2, to))
		{
			num++;
			text += "\nError auto deserializing vehicle info\n";
		}
		errorsAdded = num;
		return text;
	}

	public static string TestDLCSessionUpgrader(ScreenManager screenMan, out int errorsAdded)
	{
		int num = 0;
		string text = "";
		string activeLocale = Settings.ActiveLocale;
		string text2 = "Content/Tests/DLCTests/save_preDLC.xml";
		OS.TestingPassOnly = true;
		OS oS = new OS();
		oS.SaveGameUserName = text2;
		oS.SaveUserAccountName = "preDLCAccountTest";
		OS.WillLoadSave = true;
		try
		{
			using FileStream forceLoadOverrideStream = File.OpenRead(text2);
			oS.ForceLoadOverrideStream = forceLoadOverrideStream;
			screenMan.AddScreen(oS, screenMan.controllingPlayer);
		}
		catch (Exception ex)
		{
			num++;
			text = text + "\r\nUnexpected error loading pre DLC save file for upgrade test!\r\n" + Utils.GenerateReportFromException(ex) + "\r\n";
		}
		if (oS.isLoaded)
		{
			Computer computer = Programs.getComputer(oS, "polarSnakeDest");
			if (oS.thisComputer.Memory == null)
			{
				num++;
				text += "\r\nPlayer computer memory dump still null after upgrade!\r\n";
			}
			computer = Programs.getComputer(oS, "polarSnakeDest");
			if (computer.Memory == null || computer.Memory.DataBlocks.Count != 1)
			{
				num++;
				text += "\r\nGibson Link memory dump did not get applied correctly!\r\n";
			}
			Computer computer2 = Programs.getComputer(oS, "dPets_MF");
			Folder folder = computer2.files.root.searchForFolder("Database");
			bool flag = false;
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i].name.Contains("minx"))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				num++;
				text += "\r\nSession upgrade does not add Minx file to DPets Database!\r\n";
				text = text + "PeopleWereGeneratedWithDLCAdditions=" + People.PeopleWereGeneratedWithDLCAdditions;
			}
		}
		screenMan.RemoveScreen(oS);
		OS.TestingPassOnly = false;
		errorsAdded = num;
		return text;
	}
}
