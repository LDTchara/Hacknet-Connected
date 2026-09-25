namespace Hacknet.Misc;

public static class SessionAccelerator
{
	public static void AccelerateSessionToDLCHA(object osObj)
	{
		OS oS = (OS)osObj;
		oS.Flags.AddFlag("TutorialComplete");
		oS.delayer.RunAllDelayedActions();
		oS.allFactions.setCurrentFaction("Bibliotheque", oS);
		oS.IsInDLCMode = true;
		ThemeManager.setThemeOnComputer(oS.thisComputer, "DLC/Themes/RiptideThemeStandard.xml");
		ThemeManager.switchTheme(oS, "DLC/Themes/RiptideThemeStandard.xml");
		oS.netMap.discoverNode(Programs.getComputer(oS, "dhs"));
		MissionFunctions.runCommand(0, "setFaction:Bibliotheque");
		Computer computer = Programs.getComputer(oS, "dhs");
		DLCHubServer dLCHubServer = (DLCHubServer)computer.getDaemon(typeof(DLCHubServer));
		int num = 11;
		for (int i = 0; i < num; i++)
		{
			MissionFunctions.runCommand(1, "addRankSilent");
			if (i + 1 < num)
			{
				oS.delayer.RunAllDelayedActions();
				dLCHubServer.DelayedActions.InstantlyResolveAllActions(oS);
				dLCHubServer.ClearAllActiveMissions();
			}
		}
		AddProgramToComputer(oS.thisComputer, 6881);
		AddProgramToComputer(oS.thisComputer, 211);
		AddProgramToComputer(oS.thisComputer, 31);
		AddProgramToComputer(oS.thisComputer, 443);
		MissionFunctions.runCommand(9, "changeSongDLC");
	}

	public static void AccelerateSessionToDLCEND(object osObj)
	{
		OS oS = (OS)osObj;
		oS.Flags.AddFlag("TutorialComplete");
		oS.delayer.RunAllDelayedActions();
		oS.allFactions.setCurrentFaction("Bibliotheque", oS);
		oS.IsInDLCMode = true;
		ThemeManager.setThemeOnComputer(oS.thisComputer, "DLC/Themes/RiptideThemeStandard.xml");
		ThemeManager.switchTheme(oS, "DLC/Themes/RiptideThemeStandard.xml");
		oS.netMap.discoverNode(Programs.getComputer(oS, "dhs"));
		MissionFunctions.runCommand(0, "setFaction:Bibliotheque");
		Computer computer = Programs.getComputer(oS, "dhs");
		DLCHubServer dLCHubServer = (DLCHubServer)computer.getDaemon(typeof(DLCHubServer));
		int num = 11;
		for (int i = 0; i < num; i++)
		{
			MissionFunctions.runCommand(1, "addRankSilent");
			if (i + 1 < num)
			{
				oS.delayer.RunAllDelayedActions();
				dLCHubServer.DelayedActions.InstantlyResolveAllActions(oS);
				dLCHubServer.ClearAllActiveMissions();
			}
		}
		AddProgramToComputer(oS.thisComputer, 6881);
		AddProgramToComputer(oS.thisComputer, 211);
		AddProgramToComputer(oS.thisComputer, 31);
		AddProgramToComputer(oS.thisComputer, 443);
	}

	public static void AccelerateSessionToDLCStart(object osObj)
	{
		OS os = (OS)osObj;
		os.Flags.AddFlag("TutorialComplete");
		os.delayer.RunAllDelayedActions();
		ThemeManager.setThemeOnComputer(os.thisComputer, "DLC/Themes/RiptideClassicTheme.xml");
		ThemeManager.switchTheme(os, "DLC/Themes/RiptideClassicTheme.xml");
		for (int i = 0; i < 60; i++)
		{
			int num = 0;
			do
			{
				num = Utils.random.Next(os.netMap.nodes.Count);
			}
			while (os.netMap.nodes[num].idName == "mainHub" || os.netMap.nodes[num].idName == "entropy00" || os.netMap.nodes[num].idName == "entropy01");
			os.netMap.discoverNode(os.netMap.nodes[num]);
		}
		os.netMap.lastAddedNode = os.thisComputer;
		os.delayer.Post(ActionDelayer.Wait(0.15), delegate
		{
			Game1.getSingleton().IsMouseVisible = true;
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[22], "SSHCrack.exe"));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[21], "FTPBounce.exe"));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[13], "eosDeviceScan.exe"));
			MissionFunctions.runCommand(7, "changeSong");
			MusicManager.stop();
		});
		os.delayer.Post(ActionDelayer.Wait(56.0), delegate
		{
			ComputerLoader.loadMission("Content/DLC/Missions/Demo/DLCDemoIntroMission1.xml");
		});
	}

	internal static void AddProgramToComputer(Computer c, int portnum)
	{
		FileEntry item = new FileEntry(PortExploits.crackExeData[portnum], PortExploits.cracks[portnum]);
		c.files.root.searchForFolder("bin").files.Add(item);
	}
}
