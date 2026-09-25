using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework.Audio;

namespace Hacknet;

public class HackerScriptExecuter
{
	public const string splitDelimiter = " $#%#$\r\n";

	private static SoundEffect MusicStopSFX;

	public static void runScript(string scriptName, object os, string sourceCompReplacer = null, string targetCompReplacer = null)
	{
		string[] separator = new string[4] { " $#%#$\r\n", " $#%#$\n", "$#%#$\r\n", "$#%#$\n" };
		string localizedFilepath = LocalizedFileLoader.GetLocalizedFilepath(Utils.GetFileLoadPrefix() + scriptName);
		if (!File.Exists(localizedFilepath))
		{
			localizedFilepath = LocalizedFileLoader.GetLocalizedFilepath("Content/" + scriptName);
		}
		string text = Utils.readEntireFile(localizedFilepath);
		if (sourceCompReplacer != null)
		{
			text = text.Replace("[SOURCE_COMP]", sourceCompReplacer);
		}
		if (targetCompReplacer != null)
		{
			text = text.Replace("[TARGET_COMP]", targetCompReplacer);
		}
		string[] script = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		if (!OS.TestingPassOnly)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				executeThreadedScript(script, (OS)os);
			});
			thread.IsBackground = true;
			thread.CurrentCulture = Game1.culture;
			thread.CurrentUICulture = Game1.culture;
			Thread thread2 = thread;
			thread2.IsBackground = true;
			thread2.Name = "OpposingHackerThread";
			thread2.Start();
		}
		else
		{
			executeThreadedScript(script, (OS)os);
		}
	}

	private static void executeThreadedScript(string[] script, OS os)
	{
		KeyValuePair<string, string>? keyValuePair = null;
		bool flag = false;
		Computer computer = os.thisComputer;
		Computer computer2 = null;
		TimeSpan timeout = TimeSpan.FromSeconds(0.5);
		for (int i = 0; i < script.Length; i++)
		{
			if (computer2 != null && computer2.disabled)
			{
				Multiplayer.parseInputMessage(getBasicNetworkCommand("cDisconnect", computer, computer2), os);
				Console.WriteLine("Early Script Exit on Source Disable");
				return;
			}
			if (string.IsNullOrWhiteSpace(script[i]))
			{
				continue;
			}
			string[] array = script[i].Trim().Split(Utils.spaceDelim, StringSplitOptions.RemoveEmptyEntries);
			CultureInfo provider = new CultureInfo("en-au");
			bool flag2 = computer == os.thisComputer;
			try
			{
				switch (array[0])
				{
				case "config":
					computer = Programs.getComputer(os, array[1]);
					if (computer == null)
					{
						if (OS.DEBUG_COMMANDS)
						{
							os.write(" ");
							os.write("Error: ");
							os.write("Hack Script target " + array[1] + " not found! Aborting.");
							os.write("This error will not show up if debug commands are disabled.");
							os.write(" ");
						}
						return;
					}
					computer2 = Programs.getComputer(os, array[2]);
					if (computer2 == null)
					{
						if (OS.DEBUG_COMMANDS)
						{
							os.write(" ");
							os.write("Error: ");
							os.write("Hack Script source " + array[2] + " not found! Aborting.");
							os.write("This error will not show up if debug commands are disabled.");
							os.write(" ");
						}
						return;
					}
					timeout = TimeSpan.FromSeconds(Convert.ToDouble(array[3], provider));
					flag2 = false;
					keyValuePair = new KeyValuePair<string, string>(computer2.ip, computer.ip);
					os.ActiveHackers.Add(keyValuePair.Value);
					break;
				case "delay":
					if (!OS.TestingPassOnly)
					{
						Thread.Sleep(TimeSpan.FromSeconds(Convert.ToDouble(array[1], provider)));
					}
					flag2 = false;
					break;
				case "connect":
					Multiplayer.parseInputMessage(getBasicNetworkCommand("cConnection", computer, computer2), os);
					if (!flag && computer.ip == os.thisComputer.ip)
					{
						os.IncConnectionOverlay.Activate();
						flag = true;
					}
					break;
				case "openPort":
					Multiplayer.parseInputMessage(getBasicNetworkCommand("cPortOpen", computer, computer2) + " " + array[1], os);
					break;
				case "delete":
				{
					string pathString = getPathString(array[1], os, computer.files.root);
					string message = "cDelete #" + computer.ip + "#" + computer2.ip + "#" + array[2] + pathString;
					Multiplayer.parseInputMessage(message, os);
					break;
				}
				case "reboot":
					if (computer == os.thisComputer)
					{
						if (os.connectedComp == null || os.connectedComp == os.thisComputer)
						{
							os.runCommand("reboot");
						}
						else
						{
							os.rebootThisComputer();
						}
					}
					else
					{
						computer.reboot(computer2.ip);
					}
					break;
				case "forkbomb":
					Multiplayer.parseInputMessage(getBasicNetworkCommand("eForkBomb", computer, computer2), os);
					break;
				case "disconnect":
					computer.disconnecting(computer2.ip);
					break;
				case "systakeover":
					HostileHackerBreakinSequence.Execute(os, computer2, computer);
					break;
				case "clearTerminal":
					if (computer == os.thisComputer)
					{
						os.terminal.reset();
					}
					break;
				case "write":
				{
					string text = "";
					for (int k = 1; k < array.Length; k++)
					{
						text = text + array[k] + " ";
					}
					text = ComputerLoader.filter(text.Trim());
					if (computer == os.thisComputer)
					{
						os.terminal.write(" " + text);
						os.warningFlash();
					}
					break;
				}
				case "write_silent":
				{
					string text = "";
					for (int k = 1; k < array.Length; k++)
					{
						text = text + array[k] + " ";
					}
					text = ComputerLoader.filter(text.Trim());
					if (computer == os.thisComputer)
					{
						os.terminal.write(" " + text);
					}
					flag2 = false;
					break;
				}
				case "writel":
				{
					string text = "";
					for (int k = 1; k < array.Length; k++)
					{
						text = text + array[k] + " ";
					}
					text = ComputerLoader.filter(text.Trim());
					if (string.IsNullOrWhiteSpace(text))
					{
						flag2 = false;
					}
					if (computer == os.thisComputer)
					{
						os.terminal.writeLine(text);
						os.warningFlash();
					}
					break;
				}
				case "writel_silent":
				{
					string text = "";
					for (int k = 1; k < array.Length; k++)
					{
						text = text + array[k] + " ";
					}
					text = ComputerLoader.filter(text.Trim());
					if (string.IsNullOrWhiteSpace(text))
					{
						flag2 = false;
					}
					if (computer == os.thisComputer)
					{
						os.terminal.writeLine(text);
					}
					flag2 = false;
					break;
				}
				case "hideNetMap":
					if (computer == os.thisComputer)
					{
						os.netMap.visible = false;
					}
					break;
				case "hideRam":
					if (computer == os.thisComputer)
					{
						os.ram.visible = false;
					}
					break;
				case "hideDisplay":
					if (computer == os.thisComputer)
					{
						os.display.visible = false;
					}
					break;
				case "hideTerminal":
					if (computer == os.thisComputer)
					{
						os.terminal.visible = false;
					}
					break;
				case "showNetMap":
					if (computer == os.thisComputer)
					{
						os.netMap.visible = true;
					}
					break;
				case "showRam":
					if (computer == os.thisComputer)
					{
						os.ram.visible = true;
					}
					break;
				case "showTerminal":
					if (computer == os.thisComputer)
					{
						os.terminal.visible = true;
					}
					break;
				case "showDisplay":
					if (computer == os.thisComputer)
					{
						os.display.visible = true;
					}
					break;
				case "stopMusic":
					flag2 = false;
					if (computer != os.thisComputer)
					{
						break;
					}
					if (MusicStopSFX == null)
					{
						if (DLC1SessionUpgrader.HasDLC1Installed)
						{
							MusicStopSFX = os.content.Load<SoundEffect>("DLC/SFX/GlassBreak");
						}
						else
						{
							MusicStopSFX = os.content.Load<SoundEffect>("SFX/MeltImpact");
						}
					}
					MusicManager.stop();
					if (MusicStopSFX != null)
					{
						MusicStopSFX.Play();
					}
					break;
				case "startMusic":
					flag2 = false;
					if (!OS.TestingPassOnly && computer == os.thisComputer)
					{
						MusicManager.playSong();
					}
					break;
				case "trackseq":
					try
					{
						if (computer == os.thisComputer)
						{
							TrackerCompleteSequence.FlagNextForkbombCompletionToTrace(computer2?.ip);
						}
					}
					catch (Exception ex)
					{
						os.write(Utils.GenerateReportFromExceptionCompact(ex));
					}
					break;
				case "instanttrace":
					if (computer == os.thisComputer)
					{
						TrackerCompleteSequence.TriggerETAS(os);
					}
					break;
				case "flash":
					if (!OS.TestingPassOnly && computer == os.thisComputer)
					{
						os.warningFlash();
					}
					break;
				case "openCDTray":
					if (!OS.TestingPassOnly && computer == os.thisComputer)
					{
						computer.openCDTray(computer2.ip);
					}
					break;
				case "closeCDTray":
					if (!OS.TestingPassOnly && computer == os.thisComputer)
					{
						computer.closeCDTray(computer2.ip);
					}
					break;
				case "setAdminPass":
					computer.setAdminPassword(array[1]);
					break;
				case "makeFile":
				{
					string folderName = array[1];
					StringBuilder stringBuilder = new StringBuilder();
					for (int j = 3; j < array.Length; j++)
					{
						stringBuilder.Append(array[j]);
						if (j + 1 < array.Length)
						{
							stringBuilder.Append(" ");
						}
					}
					Folder folder = computer.files.root.searchForFolder(folderName);
					List<int> list = new List<int>();
					if (folder == null)
					{
						list.Add(0);
					}
					else
					{
						list.Add(computer.files.root.folders.IndexOf(folder));
					}
					computer.makeFile(computer2.ip, array[2], ComputerLoader.filter(stringBuilder.ToString()), list, isUpload: true);
					break;
				}
				}
			}
			catch (Exception ex)
			{
				if (OS.TestingPassOnly)
				{
					throw new FormatException("Error Parsing command " + array[0] + " in HackerScript:", ex);
				}
				if (OS.DEBUG_COMMANDS)
				{
					os.terminal.write(Utils.GenerateReportFromException(ex));
					os.write("HackScript error: " + array[0]);
					os.write("Report written to Warnings file");
					Utils.AppendToWarningsFile(Utils.GenerateReportFromException(ex));
				}
			}
			try
			{
				if (flag2 && !os.thisComputer.disabled && !OS.TestingPassOnly)
				{
					os.beepSound.Play();
				}
			}
			catch (Exception ex)
			{
				os.terminal.write(Utils.GenerateReportFromException(ex));
				Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex));
				return;
			}
			if (!OS.TestingPassOnly)
			{
				Thread.Sleep(timeout);
			}
		}
		if (keyValuePair.HasValue)
		{
			os.ActiveHackers.Remove(keyValuePair.Value);
		}
	}

	private static string getBasicNetworkCommand(string targetCommand, Computer target, Computer source)
	{
		return targetCommand + " " + target.ip + " " + source.ip;
	}

	private static string getPathString(string fPath, OS os, Folder f)
	{
		List<int> navigationPathAtPath = Programs.getNavigationPathAtPath(fPath, os, f);
		string text = "";
		for (int i = 0; i < navigationPathAtPath.Count; i++)
		{
			text = text + "#" + navigationPathAtPath[i];
		}
		return text;
	}
}
