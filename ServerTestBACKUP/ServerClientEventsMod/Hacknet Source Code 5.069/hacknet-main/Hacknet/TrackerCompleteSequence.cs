namespace Hacknet;

public static class TrackerCompleteSequence
{
	public static float MinTrackTime = 10f;

	public static float MaxTrackTime = 20f;

	public static bool NextCompleteForkbombShouldTrace;

	internal static string ForkbombCompleteTraceIP = null;

	internal static void TrackComplete(object osobj, Computer source)
	{
		OS oS = (OS)osobj;
		oS.TrackersInProgress.Clear();
		Folder folder = source.files.root.searchForFolder("log");
		for (int i = 0; i < folder.files.Count; i++)
		{
			string data = folder.files[i].data;
			if (data.Contains(oS.thisComputer.ip) && (data.Contains("FileCopied") || data.Contains("FileDeleted") || data.Contains("FileMoved")))
			{
				folder.files.RemoveAt(i);
				i--;
			}
		}
		HackerScriptExecuter.runScript("HackerScripts/TrackSequence.txt", oS, source.ip);
	}

	internal static bool CompShouldStartTrackerFromLogs(object osobj, Computer c, string targetIP = null)
	{
		OS oS = (OS)osobj;
		Folder folder = c.files.root.searchForFolder("log");
		if (targetIP == null)
		{
			targetIP = oS.thisComputer.ip;
		}
		for (int i = 0; i < folder.files.Count; i++)
		{
			string data = folder.files[i].data;
			if (data.Contains(targetIP) && (data.Contains("FileCopied") || data.Contains("FileDeleted") || data.Contains("FileMoved")))
			{
				return true;
			}
		}
		return false;
	}

	internal static void TriggerETAS(object osobj)
	{
		OS oS = (OS)osobj;
		oS.timerExpired();
	}

	internal static void FlagNextForkbombCompletionToTrace(string source)
	{
		NextCompleteForkbombShouldTrace = true;
		ForkbombCompleteTraceIP = source;
	}
}
