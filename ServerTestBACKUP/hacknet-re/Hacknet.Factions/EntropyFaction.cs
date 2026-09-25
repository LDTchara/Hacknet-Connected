using System;

namespace Hacknet.Factions;

internal class EntropyFaction : Faction
{
	public EntropyFaction(string _name, int _neededValue)
		: base(_name, _neededValue)
	{
	}

	public override void addValue(int value, object os)
	{
		int oldValue = playerValue;
		base.addValue(value, os);
		if (valuePassedPoint(oldValue, 3))
		{
			((OS)os).Flags.AddFlag("eosPathStarted");
			ComputerLoader.loadMission("Content/Missions/Entropy/StartingSet/eosMissions/eosIntroDelayer.xml");
		}
		if (!valuePassedPoint(oldValue, 4) || !Settings.EnableDLC || !DLC1SessionUpgrader.HasDLC1Installed || !((OS)os).HasLoadedDLCContent)
		{
			return;
		}
		try
		{
			Computer computer = Programs.getComputer((OS)os, "entropy00");
			if (computer == null)
			{
			}
			MissionListingServer missionListingServer = (MissionListingServer)computer.getDaemon(typeof(MissionListingServer));
			missionListingServer.addMisison((ActiveMission)ComputerLoader.readMission("Content/DLC/Missions/BaseGameConnectors/Missions/EntropyDLCConnectorIntro.xml"), injectToTop: true);
			Console.WriteLine("Injected Labyrinths transition mission to Entropy");
		}
		catch (Exception ex)
		{
			Utils.AppendToErrorFile("Could not add in Labyrinths upgrade mission to entropy!\r\n\r\n" + Utils.GenerateReportFromException(ex));
		}
	}

	public override void playerPassedValue(object os)
	{
		base.playerPassedValue(os);
		if (Settings.isAlphaDemoMode)
		{
			ComputerLoader.loadMission("Content/Missions/Entropy/EntropyMission3.xml");
			return;
		}
		((OS)os).delayer.Post(ActionDelayer.Wait(1.7), delegate
		{
			ComputerLoader.loadMission("Content/Missions/Entropy/ThemeHackTransitionMission.xml");
			((OS)os).saveGame();
		});
	}
}
