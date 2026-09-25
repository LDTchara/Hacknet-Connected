using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Hacknet;

public class DelayableActionSystem
{
	private static string EncryptionPass = "dasencrypt";

	private Folder folder;

	public DelayableActionSystem(Folder sourceFolder, object osObj)
	{
		DelayableActionSystem delayableActionSystem = this;
		folder = sourceFolder;
		OS obj = (OS)osObj;
		Action<float> updateSubscriptions = obj.UpdateSubscriptions;
		Action<float> b = delegate(float t)
		{
			delayableActionSystem.Update(t, (OS)osObj);
		};
		obj.UpdateSubscriptions = (Action<float>)Delegate.Combine(updateSubscriptions, b);
	}

	internal DelayableActionSystem()
	{
	}

	private void Update(float t, object osObj)
	{
		for (int i = 0; i < folder.files.Count; i++)
		{
			try
			{
				string data = folder.files[i].data;
				int num = data.IndexOf('\n');
				string value = data.Substring(0, num);
				string text = data.Substring(num + 1);
				float num2 = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
				num2 -= t;
				if (num2 <= 0f)
				{
					string s = FileEncrypter.DecryptString(text, EncryptionPass)[2];
					using (Stream input = Utils.GenerateStreamFromString(s))
					{
						try
						{
							XmlReader xmlReader = XmlReader.Create(input);
							SerializableAction serializableAction = SerializableAction.Deserialize(xmlReader);
							serializableAction.Trigger((OS)osObj);
							xmlReader.Close();
						}
						catch (Exception ex)
						{
							((OS)osObj).write(Utils.GenerateReportFromException(ex));
							Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
						}
					}
					folder.files.RemoveAt(i);
					i--;
				}
				else
				{
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.Append(num2.ToString("0.0000000000", CultureInfo.InvariantCulture));
					stringBuilder.Append('\n');
					stringBuilder.Append(text);
					folder.files[i].data = stringBuilder.ToString();
				}
			}
			catch (Exception)
			{
			}
		}
	}

	public virtual void InstantlyResolveAllActions(object osObj)
	{
		int num;
		for (num = 0; num < folder.files.Count; num++)
		{
			string data = folder.files[num].data;
			int num2 = data.IndexOf('\n');
			string data2 = data.Substring(num2 + 1);
			string s = FileEncrypter.DecryptString(data2, EncryptionPass)[2];
			using (Stream input = Utils.GenerateStreamFromString(s))
			{
				XmlReader xmlReader = XmlReader.Create(input);
				SerializableAction serializableAction = SerializableAction.Deserialize(xmlReader);
				serializableAction.Trigger((OS)osObj);
				xmlReader.Close();
			}
			folder.files.RemoveAt(num);
			num--;
		}
	}

	public virtual void AddAction(SerializableAction action, float delay)
	{
		string text = FileEncrypter.EncryptString(action.GetSaveString(), "das", "UNKNOWN", EncryptionPass);
		string dataEntry = delay.ToString("0.0000000000", CultureInfo.InvariantCulture) + "\n" + text;
		FileEntry item = new FileEntry(dataEntry, GetNextSeqNumber() + ".act");
		folder.files.Add(item);
	}

	internal string GetNextSeqNumber()
	{
		int num = 0;
		if (folder.files.Count > 0)
		{
			string name = folder.files[folder.files.Count - 1].name;
			string value = name.Substring(0, name.IndexOf('.'));
			try
			{
				int num2 = Convert.ToInt32(value, CultureInfo.InvariantCulture);
				num2++;
				num = num2;
			}
			catch (Exception)
			{
			}
		}
		return num.ToString("000", CultureInfo.InvariantCulture);
	}

	internal static DelayableActionSystem FindDelayableActionSystemOnComputer(Computer c)
	{
		if (c.getDaemon(typeof(IRCDaemon)) is IRCDaemon { DelayedActions: var delayedActions })
		{
			return delayedActions;
		}
		if (!(c.getDaemon(typeof(DLCHubServer)) is DLCHubServer { DelayedActions: var delayedActions2 }))
		{
			if (c.getDaemon(typeof(FastActionHost)) is FastActionHost fastActionHost)
			{
				return fastActionHost.DelayedActions;
			}
			throw new InvalidOperationException("Target computer " + c.name + " does not contain a Daemon that supports delayable actions");
		}
		return delayedActions2;
	}
}
