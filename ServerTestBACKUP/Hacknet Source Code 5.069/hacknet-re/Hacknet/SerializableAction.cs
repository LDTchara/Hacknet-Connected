using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Hacknet;

public abstract class SerializableAction
{
	public abstract void Trigger(object os_obj);

	public string GetSaveString()
	{
		Type type = GetType();
		string text = type.Name;
		if (text.StartsWith("Hacknet."))
		{
			text = text.Substring("Hacknet.".Length);
		}
		if (text.StartsWith("SA"))
		{
			text = text.Substring("SA".Length);
		}
		StringBuilder stringBuilder = new StringBuilder("<" + text + " ");
		string text2 = null;
		FieldInfo[] fields = type.GetFields();
		for (int i = 0; i < fields.Length; i++)
		{
			if (Utils.FieldContainsAttributeOfType(fields[i], typeof(XMLContentAttribute)))
			{
				if (text2 != null)
				{
					throw new InvalidOperationException("More than one field in object " + ToString() + " is a content serializable type!");
				}
				text2 = string.Format(CultureInfo.InvariantCulture, "{0}", new object[1] { fields[i].GetValue(this) });
			}
			else
			{
				stringBuilder.Append(fields[i].Name + "=\"");
				stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}", new object[1] { fields[i].GetValue(this) });
				stringBuilder.Append("\" ");
			}
		}
		if (text2 == null)
		{
			stringBuilder.Append("/>");
		}
		else
		{
			stringBuilder.Append(">");
			stringBuilder.Append(text2);
			stringBuilder.Append("</" + text + ">");
		}
		return stringBuilder.ToString();
	}

	public static SerializableAction Deserialize(XmlReader rdr)
	{
		Dictionary<string, Func<XmlReader, SerializableAction>> dictionary = new Dictionary<string, Func<XmlReader, SerializableAction>>();
		dictionary.Add("LoadMission", SALoadMission.DeserializeFromReader);
		dictionary.Add("RunFunction", SARunFunction.DeserializeFromReader);
		dictionary.Add("AddAsset", SAAddAsset.DeserializeFromReader);
		dictionary.Add("AddMissionToHubServer", SAAddMissionToHubServer.DeserializeFromReader);
		dictionary.Add("RemoveMissionFromHubServer", SARemoveMissionFromHubServer.DeserializeFromReader);
		dictionary.Add("AddThreadToMissionBoard", SAAddThreadToMissionBoard.DeserializeFromReader);
		dictionary.Add("AddIRCMessage", SAAddIRCMessage.DeserializeFromReader);
		dictionary.Add("AddConditionalActions", SAAddConditionalActions.DeserializeFromReader);
		dictionary.Add("CopyAsset", SACopyAsset.DeserializeFromReader);
		dictionary.Add("CrashComputer", SACrashComputer.DeserializeFromReader);
		dictionary.Add("DeleteFile", SADeleteFile.DeserializeFromReader);
		dictionary.Add("LaunchHackScript", SALaunchHackScript.DeserializeFromReader);
		dictionary.Add("SwitchToTheme", SASwitchToTheme.DeserializeFromReader);
		dictionary.Add("StartScreenBleedEffect", SAStartScreenBleedEffect.DeserializeFromReader);
		dictionary.Add("CancelScreenBleedEffect", SACancelScreenBleedEffect.DeserializeFromReader);
		dictionary.Add("AppendToFile", SAAppendToFile.DeserializeFromReader);
		dictionary.Add("KillExe", SAKillExe.DeserializeFromReader);
		dictionary.Add("ChangeAlertIcon", SAChangeAlertIcon.DeserializeFromReader);
		dictionary.Add("HideNode", SAHideNode.DeserializeFromReader);
		dictionary.Add("GivePlayerUserAccount", SAGivePlayerUserAccount.DeserializeFromReader);
		dictionary.Add("ChangeIP", SAChangeIP.DeserializeFromReader);
		dictionary.Add("ChangeNetmapSortMethod", SAChangeNetmapSortMethod.DeserializeFromReader);
		dictionary.Add("SaveGame", SASaveGame.DeserializeFromReader);
		dictionary.Add("HideAllNodes", SAHideAllNodes.DeserializeFromReader);
		dictionary.Add("ShowNode", SAShowNode.DeserializeFromReader);
		dictionary.Add("SetLock", SASetLock.DeserializeFromReader);
		while (!rdr.EOF && (!rdr.IsStartElement() || !dictionary.ContainsKey(rdr.Name)))
		{
			rdr.Read();
		}
		if (rdr.EOF)
		{
			throw new FormatException("Unexpected end of file!");
		}
		return dictionary[rdr.Name](rdr);
	}
}
