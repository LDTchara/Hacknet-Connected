using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Hacknet.Extensions;
using SDL2;

namespace Hacknet;

public static class HostileHackerBreakinSequence
{
	private static readonly string BaseDirectory = GetBaseDirectory();

	private static readonly string HelpFilePath = Path.Combine(BaseDirectory, "VM_Recovery_Guide.txt");

	private static readonly string Win32_BatchFilePath = Path.Combine(BaseDirectory, "OpenCMD.bat");

	private static readonly string HostileDirectory = Path.Combine(BaseDirectory, "Libs", "Injected");

	private static readonly string HostileFilePath = Path.Combine(HostileDirectory, "VMBootloaderTrap.dll");

	private static string GetBaseDirectory()
	{
		string text = SDL.SDL_GetPlatform();
		string text2;
		if (text.Equals("Linux"))
		{
			text2 = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
			if (string.IsNullOrEmpty(text2))
			{
				text2 = Environment.GetEnvironmentVariable("HOME");
				text2 = ((!string.IsNullOrEmpty(text2)) ? Path.Combine(text2, ".local/share") : ".");
			}
		}
		else if (text.Equals("Mac OS X"))
		{
			text2 = Environment.GetEnvironmentVariable("HOME");
			text2 = ((!string.IsNullOrEmpty(text2)) ? Path.Combine(text2, "Library/Application Support") : ".");
		}
		else
		{
			if (!text.Equals("Windows"))
			{
				throw new NotSupportedException("Unhandled SDL2 platform!");
			}
			text2 = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/My Games/";
		}
		if (Settings.IsInExtensionMode)
		{
			text2 = Path.Combine(text2, ExtensionLoader.ActiveExtensionInfo.GetFoldersafeName());
		}
		text2 = Path.Combine(text2, "Hacknet");
		Console.WriteLine("HHBS-Folderpath: " + text2);
		if (!Directory.Exists(text2))
		{
			Directory.CreateDirectory(text2);
		}
		return text2;
	}

	internal static void Execute(object osobj, Computer source, Computer target)
	{
		Console.WriteLine("Breakin Started!");
		PostProcessor.EndingSequenceFlashOutActive = true;
		PostProcessor.EndingSequenceFlashOutPercentageComplete = 1f;
		OS os = (OS)osobj;
		DateTime now = DateTime.Now;
		os.Flags.AddFlag("startupBreakinTrapActivated");
		os.threadedSaveExecute(preventSaveText: true);
		CopyHostileFileToLocalSystem();
		Console.WriteLine("Copied files to local system...");
		double totalSeconds = (DateTime.Now - now).TotalSeconds;
		totalSeconds = ((!(totalSeconds > 3.0)) ? (4.0 - totalSeconds) : 1.5);
		os.delayer.Post(ActionDelayer.Wait(totalSeconds), delegate
		{
			PostProcessor.EndingSequenceFlashOutActive = false;
			PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
			os.thisComputer.crash(source.ip);
		});
	}

	public static bool IsFirstSuccessfulBootAfterBlockingState(object osobj)
	{
		OS oS = (OS)osobj;
		if (oS.Flags.HasFlag("startupBreakinTrapActivated") && !oS.Flags.HasFlag("startupBreakinTrapPassed"))
		{
			if (File.Exists(HostileFilePath))
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public static void ReactToFirstSuccesfulBoot(object osobj)
	{
		OS oS = (OS)osobj;
		oS.Flags.AddFlag("startupBreakinTrapPassed");
		MusicManager.loadAsCurrentSong("DLC\\Music\\World_Chase");
	}

	public static bool IsInBlockingHostileFileState(object osobj)
	{
		Console.WriteLine("Booting up -- checking libs");
		OS oS = (OS)osobj;
		if (oS.Flags.HasFlag("startupBreakinTrapActivated") && !oS.Flags.HasFlag("startupBreakinTrapPassed"))
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			{
				return File.Exists(HostileFilePath);
			}
			if (Directory.Exists(HostileDirectory))
			{
				FileAttributes attributes = new DirectoryInfo(HostileDirectory).Attributes;
				if (attributes.HasFlag(FileAttributes.ReadOnly))
				{
					return true;
				}
				return File.Exists(HostileFilePath);
			}
		}
		Console.WriteLine("Libs loaded successfully");
		return false;
	}

	private static void CopyHostileFileToLocalSystem()
	{
		if (!Directory.Exists(HostileDirectory))
		{
			Directory.CreateDirectory(HostileDirectory);
		}
		try
		{
			if (!File.Exists(HostileFilePath))
			{
				File.Copy("Content/DLC/Misc/VMBootloaderTrap.dll", HostileFilePath);
				if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				{
					LockFileToPreventDeletionWin32(HostileFilePath);
				}
				else
				{
					LockFileToPreventDeletionUnix(HostileDirectory, HostileFilePath);
				}
			}
		}
		catch (UnauthorizedAccessException ex)
		{
			Utils.AppendToErrorFile("HHBreakinSequence error : Insufficient permissions for folder access.\r\n" + Utils.GenerateReportFromException(ex));
			Console.WriteLine("HHSequence Error " + Utils.GenerateReportFromException(ex));
		}
	}

	public static string GetHelpText()
	{
		return File.ReadAllText(LocalizedFileLoader.GetLocalizedFilepath("Content/DLC/Misc/" + ((Environment.OSVersion.Platform == PlatformID.Win32NT) ? "Win_AllyHelpFile.txt" : "Unix_AllyHelpFile.txt")));
	}

	public static void CopyHelpFile()
	{
		try
		{
			if (!File.Exists(HelpFilePath))
			{
				File.Copy(LocalizedFileLoader.GetLocalizedFilepath("Content/DLC/Misc/" + ((Environment.OSVersion.Platform == PlatformID.Win32NT) ? "Win_AllyHelpFile.txt" : "Unix_AllyHelpFile.txt")), HelpFilePath);
				if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				{
					Process.Start("chmod", " -x " + HelpFilePath);
				}
			}
		}
		catch (UnauthorizedAccessException ex)
		{
			Utils.AppendToErrorFile("HHBreakinSequence error : Insufficient permissions for folder access.\r\n" + Utils.GenerateReportFromException(ex));
			Console.WriteLine("HHSequence Error " + Utils.GenerateReportFromException(ex));
		}
		catch (Exception ex2)
		{
			Console.WriteLine("HHSequence Error " + Utils.GenerateReportFromException(ex2));
		}
	}

	public static void OpenWindowsHelpDocument()
	{
		MinimizeAllOpenWindows();
		Process.Start("notepad.exe", HelpFilePath);
	}

	public static string OpenTerminal()
	{
		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			try
			{
				if (!File.Exists(Win32_BatchFilePath))
				{
					File.Copy("Content/DLC/Misc/Win_OpenCMD.bat", Win32_BatchFilePath);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			Process.Start(Win32_BatchFilePath);
		}
		else
		{
			string text = SDL.SDL_GetPlatform();
			if (text.Equals("Linux"))
			{
				try
				{
					if (File.Exists("/usr/bin/gsettings"))
					{
						ProcessStartInfo processStartInfo = new ProcessStartInfo("/usr/bin/gsettings", " get org.gnome.desktop.default-applications.terminal exec");
						processStartInfo.RedirectStandardOutput = true;
						processStartInfo.UseShellExecute = false;
						Process process = Process.Start(processStartInfo);
						process.WaitForExit();
						string text2 = process.StandardOutput.ReadLine();
						if (!string.IsNullOrEmpty(text2))
						{
							text2 = text2.Replace("'", "");
						}
						if (!string.IsNullOrEmpty(text2))
						{
							processStartInfo = new ProcessStartInfo(text2);
							processStartInfo.WorkingDirectory = BaseDirectory;
							Process.Start(processStartInfo);
						}
					}
				}
				catch (Exception)
				{
				}
			}
			else if (text.Equals("Mac OS X"))
			{
				Process.Start("/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal", BaseDirectory);
			}
			else
			{
				Console.WriteLine(text);
			}
		}
		return BaseDirectory.Replace("\\", "/");
	}

	public static void CrashProgram()
	{
		MusicManager.stop();
		Game1.threadsExiting = true;
		Game1.getSingleton().Exit();
	}

	private static void LockFileToPreventDeletionWin32(string filepath)
	{
		FileSecurity accessControl = File.GetAccessControl(filepath);
		AuthorizationRuleCollection accessRules = accessControl.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(NTAccount));
		accessControl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
		foreach (FileSystemAccessRule item in accessRules)
		{
			accessControl.RemoveAccessRule(item);
		}
		accessControl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Delete, AccessControlType.Deny));
		File.SetAccessControl(filepath, accessControl);
	}

	private static void LockFileToPreventDeletionUnix(string dir, string file)
	{
		Process.Start("chmod", " 000 " + file).WaitForExit();
		Process.Start("chmod", " 000 " + dir).WaitForExit();
	}

	private static void MinimizeWindow(IntPtr handle)
	{
	}

	private static void MinimizeAllOpenWindows()
	{
		Process currentProcess = Process.GetCurrentProcess();
		Process[] processes = Process.GetProcesses();
		Process[] array = processes;
		foreach (Process process in array)
		{
			if (process != currentProcess)
			{
				IntPtr mainWindowHandle = process.MainWindowHandle;
				if (!(mainWindowHandle == IntPtr.Zero))
				{
					MinimizeWindow(mainWindowHandle);
				}
			}
		}
	}
}
