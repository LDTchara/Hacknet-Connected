using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Hacknet.Factions;

public class CustomFaction : Faction
{
	internal List<CustomFactionAction> CustomActions = new List<CustomFactionAction>();

	public CustomFaction(string _name, int _neededValue)
		: base(_name, _neededValue)
	{
		PlayerLosesValueOnAbandon = false;
	}

	public static CustomFaction ParseFromFile(string filepath)
	{
		string localizedFilepath = LocalizedFileLoader.GetLocalizedFilepath(filepath);
		using FileStream input = File.OpenRead(localizedFilepath);
		XmlReader xmlRdr = XmlReader.Create(input);
		Faction faction = Faction.loadFromSave(xmlRdr);
		return faction as CustomFaction;
	}

	public void CheckForAllCustomActionsToRun(object os_obj)
	{
		OS oS = (OS)os_obj;
		for (int i = 0; i < CustomActions.Count; i++)
		{
			if (playerValue < CustomActions[i].ValueRequiredForTrigger)
			{
				continue;
			}
			bool flag = true;
			if (!string.IsNullOrWhiteSpace(CustomActions[i].FlagsRequiredForTrigger))
			{
				string[] array = CustomActions[i].FlagsRequiredForTrigger.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
				for (int j = 0; j < array.Length; j++)
				{
					if (!oS.Flags.HasFlag(array[j]))
					{
						flag = false;
					}
				}
			}
			if (flag)
			{
				CustomFactionAction customFactionAction = CustomActions[i];
				CustomActions.RemoveAt(i);
				i--;
				customFactionAction.Trigger(oS);
			}
		}
	}

	public override void addValue(int value, object os_obj)
	{
		int num = playerValue;
		base.addValue(value, os_obj);
		CheckForAllCustomActionsToRun(os_obj);
	}

	private void SendNotification(object osIn, string body, string subject)
	{
	}

	public override string getSaveString()
	{
		string saveString = base.getSaveString();
		string text = ">\n";
		for (int i = 0; i < CustomActions.Count; i++)
		{
			text += CustomActions[i].GetSaveString();
			text += "\n";
		}
		text += "</CustomFaction>";
		saveString = saveString.Replace("/>", text);
		return "\r\n" + saveString;
	}

	public static CustomFaction DeserializeFromXmlReader(XmlReader rdr, string name, string id, int playerVal, bool playerHasPassed)
	{
		CustomFaction customFaction = new CustomFaction(name, 100);
		rdr.MoveToElement();
		while (!rdr.EOF)
		{
			if (rdr.Name != "CustomFaction")
			{
				customFaction.CustomActions.Add(CustomFactionAction.Deserialize(rdr));
				rdr.Read();
			}
			else
			{
				if (rdr.Name == "CustomFaction" && !rdr.IsStartElement())
				{
					break;
				}
				rdr.Read();
			}
			while (!rdr.EOF && string.IsNullOrWhiteSpace(rdr.Name))
			{
				rdr.Read();
			}
		}
		return customFaction;
	}
}
