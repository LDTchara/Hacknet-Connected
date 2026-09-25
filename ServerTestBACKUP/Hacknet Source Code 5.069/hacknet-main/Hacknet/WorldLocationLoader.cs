using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hacknet;

public static class WorldLocationLoader
{
	public static List<WorldLocation> locations;

	public static void init()
	{
		CultureInfo provider = new CultureInfo("en-au");
		string text = Utils.readEntireFile("Content/PersonData/LocationData.txt");
		string[] array = text.Split(Utils.newlineDelim);
		char[] separator = new char[1] { '#' };
		locations = new List<WorldLocation>(array.Length);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(separator);
			locations.Add(new WorldLocation(array2[1], array2[0], (float)Convert.ToDouble(array2[2], provider), (float)Convert.ToDouble(array2[3], provider), (float)Convert.ToDouble(array2[4], provider), (float)Convert.ToDouble(array2[5], provider)));
		}
	}

	public static WorldLocation getRandomLocation()
	{
		return locations[Utils.random.Next(locations.Count)];
	}

	public static WorldLocation getClosestOrCreate(string name)
	{
		for (int i = 0; i < locations.Count; i++)
		{
			if (locations[i].name.ToLower().Equals(name.ToLower()))
			{
				return locations[i];
			}
		}
		WorldLocation worldLocation = new WorldLocation(name, name, (float)Utils.random.NextDouble(), (float)Utils.random.NextDouble(), (float)Utils.random.NextDouble(), (float)Utils.random.NextDouble());
		locations.Add(worldLocation);
		return worldLocation;
	}
}
