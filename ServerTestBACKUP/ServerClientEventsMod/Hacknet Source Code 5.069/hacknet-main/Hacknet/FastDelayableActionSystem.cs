using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Hacknet;

public class FastDelayableActionSystem : DelayableActionSystem
{
	private static string EncryptionPass = "dasencrypt";

	private Folder folder;

	internal List<KeyValuePair<float, SerializableAction>> Actions = new List<KeyValuePair<float, SerializableAction>>();

	private int SeqNum = 0;

	public FastDelayableActionSystem(Folder sourceFolder, object osObj)
	{
		FastDelayableActionSystem fastDelayableActionSystem = this;
		folder = sourceFolder;
		OS obj = (OS)osObj;
		Action<float> updateSubscriptions = obj.UpdateSubscriptions;
		Action<float> b = delegate(float t)
		{
			fastDelayableActionSystem.Update(t, (OS)osObj);
		};
		obj.UpdateSubscriptions = (Action<float>)Delegate.Combine(updateSubscriptions, b);
	}

	private void Update(float t, object osObj)
	{
		for (int i = 0; i < Actions.Count; i++)
		{
			try
			{
				float key = Actions[i].Key;
				SerializableAction value = Actions[i].Value;
				key -= t;
				if (key <= 0f)
				{
					try
					{
						value.Trigger((OS)osObj);
					}
					catch (Exception ex)
					{
						((OS)osObj).write(Utils.GenerateReportFromException(ex));
						Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
					}
					Actions.RemoveAt(i);
					i--;
				}
				else
				{
					Actions[i] = new KeyValuePair<float, SerializableAction>(key, value);
				}
			}
			catch (Exception)
			{
			}
		}
	}

	public override void InstantlyResolveAllActions(object osObj)
	{
		for (int i = 0; i < Actions.Count; i++)
		{
			try
			{
				Actions[i].Value.Trigger(osObj);
				Actions.RemoveAt(i);
				i--;
			}
			catch (Exception ex)
			{
				((OS)osObj).write(Utils.GenerateReportFromException(ex));
				Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
			}
		}
	}

	public override void AddAction(SerializableAction action, float delay)
	{
		Actions.Add(new KeyValuePair<float, SerializableAction>(delay, action));
	}

	private FileEntry EncryptAction(SerializableAction action, float delay)
	{
		string text = FileEncrypter.EncryptString(action.GetSaveString(), "das", "UNKNOWN", EncryptionPass);
		string dataEntry = delay.ToString("0.0000000000", CultureInfo.InvariantCulture) + "\n" + text;
		FileEntry result = new FileEntry(dataEntry, SeqNum.ToString("000") + ".act");
		SeqNum++;
		return result;
	}

	public List<FileEntry> GetAllFilesForActions()
	{
		SeqNum = 0;
		List<FileEntry> list = new List<FileEntry>();
		for (int i = 0; i < Actions.Count; i++)
		{
			list.Add(EncryptAction(Actions[i].Value, Actions[i].Key));
		}
		return list;
	}

	public void DeserializeActions(List<FileEntry> files)
	{
		Actions.Clear();
		for (int i = 0; i < files.Count; i++)
		{
			try
			{
				string data = folder.files[i].data;
				int num = data.IndexOf('\n');
				string value = data.Substring(0, num);
				string data2 = data.Substring(num + 1);
				float key = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
				string s = FileEncrypter.DecryptString(data2, EncryptionPass)[2];
				using Stream input = Utils.GenerateStreamFromString(s);
				try
				{
					XmlReader rdr = XmlReader.Create(input);
					SerializableAction value2 = SerializableAction.Deserialize(rdr);
					Actions.Add(new KeyValuePair<float, SerializableAction>(key, value2));
				}
				catch (Exception ex)
				{
					if (OS.DEBUG_COMMANDS)
					{
						Utils.AppendToWarningsFile("Error deserializing action in fast action host :\n" + Utils.GenerateReportFromExceptionCompact(ex));
					}
				}
			}
			catch (Exception ex)
			{
				if (OS.DEBUG_COMMANDS)
				{
					Utils.AppendToWarningsFile("Error deserializing action " + i + " in fast action host :\n" + Utils.GenerateReportFromExceptionCompact(ex));
				}
			}
		}
	}
}
