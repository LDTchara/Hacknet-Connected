using Steamworks;

namespace Hacknet;

public static class AchievementsManager
{
	public static bool Unlock(string name, bool recordAndCheckFlag = false)
	{
		try
		{
			string flag = name + "_Unlocked";
			if (!recordAndCheckFlag || !OS.currentInstance.Flags.HasFlag(flag))
			{
				SteamUserStats.SetAchievement(name);
				if (SteamUserStats.StoreStats())
				{
					OS.currentInstance.Flags.AddFlag(flag);
					return true;
				}
				return false;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}
}
