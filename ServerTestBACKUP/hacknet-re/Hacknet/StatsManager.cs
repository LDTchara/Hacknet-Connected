using Steamworks;

namespace Hacknet;

public static class StatsManager
{
	private struct StatData
	{
		public string Name;

		public int IntVal;

		public float FloatVal;
	}

	private static bool HasReceivedUserStats = false;

	private static Callback<UserStatsReceived_t> UserStatsCallback;

	private static StatData[] StatDefinitions = new StatData[1]
	{
		new StatData
		{
			Name = "commands_run"
		}
	};

	public static void InitStats()
	{
		UserStatsCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
	}

	private static void OnUserStatsReceived(UserStatsReceived_t pCallback)
	{
		for (int i = 0; i < StatDefinitions.Length; i++)
		{
		}
	}

	public static void IncrementStat(string statName, int valueChange)
	{
		if (HasReceivedUserStats)
		{
			SteamUserStats.SetStat(statName, valueChange);
		}
	}

	public static void SaveStatProgress()
	{
		if (HasReceivedUserStats)
		{
			SteamUserStats.StoreStats();
		}
	}
}
