using System;
using System.IO;
using SDL2;

namespace Hacknet;

internal static class Program
{
	public static string GraphicsDeviceResetLog = "";

	[STAThread]
	private static void Main(string[] args)
	{
		for (int i = 0; i < args.Length; i++)
		{
			string text = args[i];
			if (text.Equals("-disableweb"))
			{
				WebRenderer.Enabled = false;
			}
			if (text.Equals("-disablebackground"))
			{
				Settings.DrawHexBackground = false;
			}
			if (text.Equals("-altmonitor"))
			{
				Settings.StartOnAltMonitor = true;
			}
			if (text.Equals("-enablefc") && Settings.emergencyForceCompleteEnabled)
			{
				Settings.forceCompleteEnabled = true;
			}
			if (text.Equals("-enabledebug") && Settings.emergencyDebugCommandsEnabled)
			{
				Settings.debugCommandsEnabled = true;
				OS.DEBUG_COMMANDS = true;
			}
			if (text.ToLower().Equals("-extstart") && Settings.AllowExtensionMode && i < args.Length - 1)
			{
				string path = args[i + 1];
				i++;
				path = Path.Combine("Extensions", path);
				Game1.AutoLoadExtensionPath = path;
			}
			if (text.ToLower().Equals("-allowextpublish"))
			{
				Settings.AllowExtensionPublish = true;
			}
		}
		using Game1 game = new Game1();
		try
		{
			game.Run();
		}
		catch (Exception ex)
		{
			SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "SAVE THIS MESSAGE!", ex.ToString(), IntPtr.Zero);
			string text2 = Utils.GenerateReportFromException(ex);
			try
			{
				if (OS.currentInstance != null)
				{
					text2 = text2 + "\r\n\r\n" + OS.currentInstance.Flags.GetSaveString() + "\r\n\r\n";
					object obj = text2;
					text2 = string.Concat(obj, "Timer : ", OS.currentInstance.timer / 60f, "mins\r\n");
					text2 = text2 + "Display cache : " + OS.currentInstance.displayCache + "\r\n";
					text2 = text2 + "String Cache : " + OS.currentInstance.getStringCache + "\r\n";
					text2 = text2 + "Terminal--------------\r\n" + OS.currentInstance.terminal.GetRecentTerminalHistoryString() + "-------------\r\n";
				}
				else
				{
					text2 += "\r\n\r\nOS INSTANCE NULL\r\n\r\n";
				}
				text2 = text2 + "\r\nMenuErrorCache: " + MainMenu.AccumErrors + "\r\n";
			}
			catch (Exception)
			{
			}
			text2 = text2 + "\r\n\r\nPlatform API: " + PlatformAPISettings.Report;
			text2 = text2 + "\r\n\r\nGraphics Device Reset Log: " + GraphicsDeviceResetLog;
			text2 = text2 + "\r\n\r\nCurrent Time: " + DateTime.Now.ToShortTimeString() + "\r\n";
			text2 = text2 + "\r\n\r\nVersion: " + MainMenu.OSVersion + "\r\n";
			text2 += "\r\n\r\nMode: FNA\r\n";
			string text3 = "";
			if (SDL.SDL_GetPlatform().Equals("Windows"))
			{
				text3 = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/My Games/Hacknet/Reports/";
				if (!Directory.Exists(text3))
				{
					Directory.CreateDirectory(text3);
				}
			}
			Utils.writeToFile(text2, text3 + "CrashReport_" + Guid.NewGuid().ToString().Replace(" ", "_") + ".txt");
			Utils.SendRealWorldEmail("Hackent " + MainMenu.OSVersion + " Crash " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString(), "hacknetbugs+Hacknet@gmail.com", text2);
			throw ex;
		}
	}
}
