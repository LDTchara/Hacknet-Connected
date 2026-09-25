using System;
using System.Xml;
using Hacknet.Factions;

namespace Hacknet;

public class Faction
{
	public int playerValue;

	public int neededValue;

	public string name = "unknown";

	public string idName = "";

	public bool playerHasPassedValue = false;

	public bool PlayerLosesValueOnAbandon = false;

	public Faction(string _name, int _neededValue)
	{
		playerValue = 0;
		neededValue = _neededValue;
		name = _name;
	}

	public virtual void addValue(int value, object os)
	{
		int num = playerValue;
		playerValue += value;
		if (playerValue >= neededValue && !playerHasPassedValue)
		{
			playerPassedValue(os);
		}
	}

	public void contractAbbandoned(object osIn)
	{
		OS oS = (OS)osIn;
		if (PlayerLosesValueOnAbandon)
		{
			playerValue -= 10;
			if (playerValue < 0)
			{
				playerValue = 0;
			}
		}
		string subject = LocaleTerms.Loc("Contract Abandoned");
		string body = string.Format(Utils.readEntireFile("Content/LocPost/ContractAbandonedEmail.txt"), getRank(), getMaxRank(), name);
		string sender = name + " ReplyBot";
		string mail = MailServer.generateEmail(subject, body, sender);
		MailServer mailServer = (MailServer)oS.netMap.mailServer.getDaemon(typeof(MailServer));
		mailServer.addMail(mail, oS.defaultUser.name);
	}

	public int getRank()
	{
		float num = Math.Min((float)playerValue, (float)neededValue);
		return Math.Max(1, Math.Abs((int)((1f - num / (float)neededValue) * (float)getMaxRank())));
	}

	public virtual string getSaveString()
	{
		string text = "Faction";
		if (this is EntropyFaction)
		{
			text = "EntropyFaction";
		}
		if (this is HubFaction)
		{
			text = "HubFaction";
		}
		if (this is CustomFaction)
		{
			text = "CustomFaction";
		}
		return "<" + text + " name=\"" + name + "\" id=\"" + idName + "\" neededVal=\"" + neededValue + "\" playerVal=\"" + playerValue + "\" playerHasPassed=\"" + playerHasPassedValue + "\" />";
	}

	public int getMaxRank()
	{
		return 100;
	}

	public virtual void playerPassedValue(object os)
	{
		playerHasPassedValue = true;
	}

	public static Faction loadFromSave(XmlReader xmlRdr)
	{
		string text = "UNKNOWN";
		string id = "";
		bool playerHasPassed = false;
		int num = 100;
		int playerVal = 0;
		while (xmlRdr.Name != "Faction" && xmlRdr.Name != "CustomFaction" && xmlRdr.Name != "EntropyFaction" && xmlRdr.Name != "HubFaction")
		{
			xmlRdr.Read();
		}
		string text2 = xmlRdr.Name;
		if (xmlRdr.MoveToAttribute("name"))
		{
			text = xmlRdr.ReadContentAsString();
		}
		if (xmlRdr.MoveToAttribute("id"))
		{
			id = xmlRdr.ReadContentAsString();
		}
		if (xmlRdr.MoveToAttribute("neededVal"))
		{
			num = xmlRdr.ReadContentAsInt();
		}
		if (xmlRdr.MoveToAttribute("playerVal"))
		{
			playerVal = xmlRdr.ReadContentAsInt();
		}
		if (xmlRdr.MoveToAttribute("playerHasPassed"))
		{
			playerHasPassed = xmlRdr.ReadContentAsString().ToLower() == "true";
		}
		Faction faction = null;
		faction = text2 switch
		{
			"HubFaction" => new HubFaction(text, num), 
			"EntropyFaction" => new EntropyFaction(text, num), 
			"CustomFaction" => CustomFaction.DeserializeFromXmlReader(xmlRdr, text, id, playerVal, playerHasPassed), 
			_ => new Faction(text, num), 
		};
		faction.playerValue = playerVal;
		faction.idName = id;
		faction.playerHasPassedValue = playerHasPassed;
		return faction;
	}

	public bool valuePassedPoint(int oldValue, int neededValue)
	{
		return playerValue >= neededValue && oldValue < neededValue;
	}
}
