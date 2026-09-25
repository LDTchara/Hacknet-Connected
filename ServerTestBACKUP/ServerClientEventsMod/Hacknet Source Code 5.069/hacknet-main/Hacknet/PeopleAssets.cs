using System;

namespace Hacknet;

internal class PeopleAssets
{
	private static string[] degreeTitles = new string[3] { "Bachelor of ", "Masters in ", "PHD in " };

	private static string[] degreeNames = new string[12]
	{
		"Computer Science", "Business", "Electrical Engineering", "Finance", "Marketing", "The Arts", "Computer Graphics", "Design", "Medicine", "Pharmacy",
		"Information Technology", "Psychology"
	};

	private static string[] hackerDegreeNames = new string[5] { "Computer Science", "Digital Security", "Computer Networking", "Information Technology", "Computer Graphics" };

	public static Degree getRandomDegree(WorldLocation origin)
	{
		Degree degree = new Degree();
		degree.name = randOf(degreeTitles) + randOf(degreeNames);
		degree.GPA = 3f + 3f * (float)(Utils.random.NextDouble() - 0.5) * 0.5f;
		while (degree.GPA > 4f)
		{
			degree.GPA -= Utils.randm(0.4f);
		}
		degree.uni = "University of " + origin.name;
		if (Utils.flipCoin())
		{
			degree.uni = origin.name + " University";
		}
		return degree;
	}

	public static Degree getRandomHackerDegree(WorldLocation origin)
	{
		Degree degree = new Degree();
		degree.name = randOf(degreeTitles) + randOf(hackerDegreeNames);
		degree.GPA = 3f + 5f * (float)(Utils.random.NextDouble() - 0.5) * 0.5f;
		degree.uni = "University of " + origin.name;
		if (Utils.flipCoin())
		{
			degree.uni = origin.name + " University";
		}
		return degree;
	}

	public static string randOf(string[] array)
	{
		int num = (int)(Math.Max(0.0001, Utils.random.NextDouble()) * (double)array.Length - 1.0);
		return array[num];
	}
}
