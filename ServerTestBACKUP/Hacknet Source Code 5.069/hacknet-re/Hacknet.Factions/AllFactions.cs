using System.Collections.Generic;
using System.Xml;

namespace Hacknet.Factions;

internal class AllFactions
{
	public Dictionary<string, Faction> factions = new Dictionary<string, Faction>();

	public string currentFaction;

	public AllFactions()
	{
		currentFaction = null;
	}

	public void init()
	{
		if (!Settings.IsInExtensionMode)
		{
			currentFaction = "entropy";
			factions.Add("entropy", new EntropyFaction("Entropy", 5)
			{
				idName = "entropy"
			});
			factions.Add("lelzSec", new Faction("lelzSec", 1000)
			{
				idName = "lelzSec"
			});
			factions.Add("hub", new HubFaction("CSEC", 10)
			{
				idName = "hub"
			});
		}
	}

	public string getSaveString()
	{
		string text = "<AllFactions current=\"" + currentFaction + "\">\n";
		foreach (KeyValuePair<string, Faction> faction in factions)
		{
			text = text + "\t" + faction.Value.getSaveString();
		}
		return text + "</AllFactions>";
	}

	public void setCurrentFaction(string newFaction, OS os)
	{
		currentFaction = newFaction;
		os.currentFaction = factions[currentFaction];
	}

	public static AllFactions loadFromSave(XmlReader xmlRdr)
	{
		AllFactions allFactions = new AllFactions();
		while (xmlRdr.Name != "AllFactions")
		{
			xmlRdr.Read();
		}
		if (xmlRdr.MoveToAttribute("current"))
		{
			allFactions.currentFaction = xmlRdr.ReadContentAsString();
		}
		do
		{
			xmlRdr.Read();
		}
		while (string.IsNullOrWhiteSpace(xmlRdr.Name));
		while (!(xmlRdr.Name == "AllFactions") || xmlRdr.IsStartElement())
		{
			Faction faction = Faction.loadFromSave(xmlRdr);
			allFactions.factions.Add(faction.idName, faction);
			xmlRdr.Read();
		}
		return allFactions;
	}
}
