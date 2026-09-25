using System.Collections.Generic;
using System.Text;

namespace Hacknet;

public static class VehicleInfo
{
	public static List<VehicleType> vehicleTypes;

	public static void init()
	{
		string text = Utils.readEntireFile("Content/files/VehicleTypes.txt");
		string[] array = text.Split(Utils.newlineDelim);
		char[] separator = new char[1] { '#' };
		vehicleTypes = new List<VehicleType>(array.Length);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(separator);
			vehicleTypes.Add(new VehicleType(array2[0], array2[1]));
		}
	}

	public static VehicleRegistration getRandomRegistration()
	{
		int index = Utils.random.Next(vehicleTypes.Count);
		VehicleType vehicleType = vehicleTypes[index];
		string plate = Utils.getRandomLetter() + Utils.getRandomLetter() + Utils.getRandomLetter() + "-" + Utils.getRandomLetter() + Utils.getRandomLetter() + Utils.getRandomLetter();
		StringBuilder stringBuilder = new StringBuilder();
		int num = 12;
		int num2 = 4;
		for (int i = 0; i < num; i++)
		{
			if (i % num2 == 0 && i > 0)
			{
				stringBuilder.Append('-');
			}
			else
			{
				stringBuilder.Append(Utils.getRandomChar());
			}
		}
		string regNumber = stringBuilder.ToString();
		return new VehicleRegistration(vehicleType, plate, regNumber);
	}
}
