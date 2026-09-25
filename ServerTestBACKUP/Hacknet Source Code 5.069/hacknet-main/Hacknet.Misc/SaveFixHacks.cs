using System;
using System.Collections.Generic;
using Hacknet.Mission;

namespace Hacknet.Misc;

public static class SaveFixHacks
{
	public static void FixSavesWithTerribleHacks(object osObj)
	{
		OS oS = (OS)osObj;
		Computer computer = Programs.getComputer(oS, "mainHubAssets");
		if (computer != null)
		{
			Folder folder = computer.files.root.searchForFolder("bin");
			if (folder != null)
			{
				folder = folder.searchForFolder("Sequencer");
				if (folder != null)
				{
					FileEntry fileEntry = folder.searchForFile("Sequencer.exe");
					if (fileEntry == null)
					{
						folder.files.Add(new FileEntry(PortExploits.crackExeData[17], "Sequencer.exe"));
					}
					else
					{
						fileEntry.data = PortExploits.crackExeData[17];
					}
				}
			}
		}
		Computer computer2 = Programs.getComputer(oS, "pacemakerSW_BE");
		if (computer2 != null)
		{
			Console.WriteLine("Searching for pacemaker comp");
			Folder folder = computer2.files.root.searchForFolder("projects");
			if (folder != null)
			{
				Console.WriteLine("Searching for pacemaker projects");
				folder = folder.searchForFolder("KellisBT");
				if (folder != null)
				{
					folder = folder.searchForFolder("Tests");
					if (folder != null)
					{
						Console.WriteLine("Searching for pacemaker file");
						FileEntry fileEntry = folder.searchForFile("PacemakerFirmware_Cycle_Test.dll");
						if (fileEntry == null)
						{
							folder.files.Add(new FileEntry(PortExploits.DangerousPacemakerFirmware, "PacemakerFirmware_Cycle_Test.dll"));
						}
						else
						{
							fileEntry.data = PortExploits.DangerousPacemakerFirmware;
						}
					}
				}
			}
		}
		if (!oS.HasLoadedDLCContent)
		{
			return;
		}
		List<Computer> list = new List<Computer>();
		Computer computer3 = Programs.getComputer(oS, "dPets_MF");
		if (computer3.links.Count == 0)
		{
			ComputerLoader.postAllLoadedActions = null;
			List<string> dLCList = BootLoadList.getDLCList();
			for (int i = 0; i < dLCList.Count; i++)
			{
				Computer item = (Computer)ComputerLoader.loadComputer(dLCList[i], preventAddingToNetmap: true, preventInitDaemons: true);
				list.Add(item);
			}
			ComputerLoader.postAllLoadedActions();
		}
		for (int j = 0; j < list.Count; j++)
		{
			Computer computer4 = Programs.getComputer(oS, list[j].idName);
			computer4.links = list[j].links;
		}
		Computer computer5 = Programs.getComputer(oS, "dPets_MF");
		Folder folder2 = computer5.files.root.searchForFolder("Database");
		bool flag = false;
		if (folder2.files.Count > 0 && folder2.files[0].data.Contains("DigiPet"))
		{
			for (int j = 0; j < folder2.files.Count; j++)
			{
				folder2.files[j].data = folder2.files[j].data.Replace("DigiPet", "Neopal");
				if (folder2.files[j].data.Contains("Minx"))
				{
					flag = true;
				}
			}
			if (!flag)
			{
			}
			Computer computer6 = Programs.getComputer(oS, "dhs");
			DLCHubServer dLCHubServer = (DLCHubServer)computer6.getDaemon(typeof(DLCHubServer));
			dLCHubServer.navigatedTo();
			if (dLCHubServer.ActiveMissions.Count == 0)
			{
				dLCHubServer.AddMission(oS.currentMission, oS.defaultUser.name);
			}
		}
		if (oS.Flags.HasFlag("KaguyaTrialComplete") && !oS.netMap.visibleNodes.Contains(oS.netMap.nodes.IndexOf(Programs.getComputer(oS, "dhs"))))
		{
			oS.Flags.RemoveFlag("KaguyaTrialComplete");
		}
	}

	public static string GetReportOnHashCodeOfEmptyStringForOtherCLRVersion()
	{
		string data = Utils.readEntireFile("DebugTest.txt");
		string data2 = FileEncrypter.EncryptString("test content", "headercontent", "1.1.1.1.1", "12345asdf");
		Console.WriteLine("password 12345asdf hashes to: " + (ushort)"12345asdf".GetHashCode());
		for (ushort num = 0; num < ushort.MaxValue; num++)
		{
			string[] array = FileEncrypter.TestingDecryptString(data, num);
			if (array[0].Contains("Firmware"))
			{
				Console.WriteLine("Match Found: " + num + " is valid match, current CLR produces: " + (ushort)"".GetHashCode());
			}
			else
			{
				if (num % 1000 == 0)
				{
					Console.Write(".");
				}
				if (num % 50000 == 0)
				{
					Console.WriteLine("");
				}
			}
			string[] array2 = FileEncrypter.TestingDecryptString(data2, num);
			if (array2[2] != null && array2[2].Contains("test") && array2[4] != null && array2[4] == "1" && array2[5] != null && array2[5] == "ENCODED")
			{
				Console.WriteLine("Alt Match Found: " + num + " is valid match, current CLR produces: " + (ushort)"".GetHashCode());
			}
		}
		Console.WriteLine("Operation Complete");
		return "";
	}
}
