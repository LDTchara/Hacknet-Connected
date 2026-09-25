using System;
using System.Collections.Generic;
using System.IO;
using Hacknet.Factions;
using Hacknet.Mission;

namespace Hacknet;

internal class DLC1SessionUpgrader
{
	public static bool HasDLC1Installed = false;

	public static void CheckForDLCFiles()
	{
		if (File.Exists("Content/DLC/DLCFaction.xml"))
		{
			HasDLC1Installed = true;
		}
		else
		{
			HasDLC1Installed = false;
		}
	}

	public static void UpgradeSession(object osobj, bool needsNodeInjection)
	{
		OS oS = (OS)osobj;
		MemoryDumpInjector.InjectMemory(LocalizedFileLoader.GetLocalizedFilepath("Content/DLC/Missions/Injections/MemoryDumps/GibsonLink.xml"), Programs.getComputer(oS, "polarSnakeDest"));
		MemoryDumpInjector.InjectMemory(LocalizedFileLoader.GetLocalizedFilepath("Content/DLC/Missions/Injections/MemoryDumps/NaixHome.xml"), Programs.getComputer(oS, "naixGateway"));
		MemoryDumpInjector.InjectMemory(LocalizedFileLoader.GetLocalizedFilepath("Content/DLC/Missions/Injections/MemoryDumps/BitDropBox.xml"), Programs.getComputer(oS, "BitWorkServer"));
		MemoryDumpInjector.InjectMemory(LocalizedFileLoader.GetLocalizedFilepath("Content/DLC/Missions/Injections/MemoryDumps/ExpandKeysInjection.xml"), Programs.getComputer(oS, "portcrack01"));
		if (oS.thisComputer.Memory == null)
		{
			oS.thisComputer.Memory = new MemoryContents();
		}
		oS.allFactions.factions.Add("Bibliotheque", CustomFaction.ParseFromFile("Content/DLC/DLCFaction.xml"));
		if (needsNodeInjection)
		{
			if (People.all == null)
			{
				People.init();
			}
			else if (!People.PeopleWereGeneratedWithDLCAdditions)
			{
				People.LoadInDLCPeople();
			}
			List<string> dLCList = BootLoadList.getDLCList();
			for (int i = 0; i < dLCList.Count; i++)
			{
				Computer.loadFromFile(dLCList[i]);
			}
			ComputerLoader.postAllLoadedActions();
		}
		Computer computer = Programs.getComputer(oS, "ispComp");
		if (!computer.ports.Contains(443))
		{
			computer.ports.Add(443);
		}
		if (!computer.ports.Contains(6881))
		{
			computer.ports.Add(6881);
		}
	}

	public static void EndDLCSection(object osobj)
	{
		OS oS = (OS)osobj;
		oS.IsInDLCMode = false;
		oS.mailicon.isEnabled = true;
		oS.Flags.AddFlag("dlc_complete");
		if (oS.Flags.HasFlag("dlc_start_csec"))
		{
			oS.Flags.AddFlag("dlc_complete_FromCSEC");
			ComputerLoader.loadMission("Content/DLC/Missions/BaseGameConnectors/Missions/CSEC_DLC_EndEmail.xml");
			oS.allFactions.setCurrentFaction("hub", oS);
			oS.homeNodeID = "mainHub";
			oS.homeAssetServerID = "mainHubAssets";
			oS.Flags.AddFlag("dlc_csec_end_facval:" + oS.currentFaction.playerValue);
		}
		else if (oS.Flags.HasFlag("dlc_start_entropy"))
		{
			oS.Flags.AddFlag("dlc_complete_FromEntropy");
			ComputerLoader.loadMission("Content/DLC/Missions/BaseGameConnectors/Missions/Entropy_DLC_EndEmail.xml");
			oS.allFactions.setCurrentFaction("entropy", oS);
			oS.homeNodeID = "entropy00";
			oS.homeAssetServerID = "entropy01";
		}
		else
		{
			oS.Flags.AddFlag("dlc_complete_FromUnknown");
			ComputerLoader.loadMission("Content/DLC/Missions/BaseGameConnectors/Missions/Entropy_DLC_EndEmail.xml");
			oS.allFactions.setCurrentFaction("entropy", oS);
			oS.homeNodeID = "entropy00";
			oS.homeAssetServerID = "entropy01";
		}
		ReDsicoverAllVisibleNodesInOSCache(oS);
	}

	public static void ReDsicoverAllVisibleNodesInOSCache(object osobj)
	{
		OS os = (OS)osobj;
		float num = 0f;
		float num2 = 8f;
		float num3 = num2 / (float)os.netMap.nodes.Count;
		string[] array = os.PreDLCVisibleNodesCache.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			try
			{
				int nIndex = Convert.ToInt32(array[i]);
				os.delayer.Post(ActionDelayer.Wait(num), delegate
				{
					os.netMap.visibleNodes.Add(nIndex);
					os.netMap.nodes[nIndex].highlightFlashTime = 1f;
					SFX.addCircle(os.netMap.nodes[nIndex].getScreenSpacePosition(), Utils.AddativeWhite * 0.4f, 70f);
				});
				os.delayer.Post(ActionDelayer.Wait(num2), delegate
				{
					SFX.addCircle(os.netMap.nodes[nIndex].getScreenSpacePosition(), Utils.AddativeWhite * 0.3f, 30f);
				});
			}
			catch (FormatException)
			{
				Console.WriteLine("Error restoring node " + i);
			}
			num += num3;
			os.PreDLCVisibleNodesCache = "";
		}
	}
}
