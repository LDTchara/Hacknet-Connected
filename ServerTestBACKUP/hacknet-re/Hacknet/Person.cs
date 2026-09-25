using System;
using System.Collections.Generic;

namespace Hacknet;

public class Person
{
	public DateTime DateOfBirth = DateTime.Now;

	public bool isMale = true;

	public bool isHacker = false;

	public string firstName;

	public string lastName;

	public string handle;

	public List<Degree> degrees;

	public List<VehicleRegistration> vehicles;

	public WorldLocation birthplace;

	public MedicalRecord medicalRecord;

	public NeopalsAccount NeopalsAccount;

	public string FullName => firstName + " " + lastName;

	public Person()
	{
	}

	public Person(string fName, string lName, bool male, bool isHacker = false, string handle = null)
	{
		firstName = fName;
		lastName = lName;
		isMale = male;
		if (handle == null)
		{
			handle = UsernameGenerator.getName();
		}
		if (handle == null)
		{
			throw new InvalidOperationException();
		}
		this.handle = handle;
		this.isHacker = isHacker;
		birthplace = WorldLocationLoader.getRandomLocation();
		vehicles = new List<VehicleRegistration>();
		degrees = new List<Degree>();
		addRandomDegrees();
		addRandomVehicles();
		int num = 18;
		int num2 = 72;
		if (isHacker)
		{
			num2 = 45;
		}
		int num3 = num * 365 + (int)(Utils.random.NextDouble() * (double)(num2 - num) * 365.0);
		DateOfBirth = DateTime.Now - TimeSpan.FromDays(num3);
		medicalRecord = new MedicalRecord(birthplace, DateOfBirth);
		if (Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed && (isHacker || Utils.randm(1f) < 0.8f))
		{
			NeopalsAccount = NeopalsAccount.GenerateAccount(handle, Utils.flipCoin() && Utils.flipCoin() && isHacker && handle.ToLower() != "bit");
		}
	}

	public void addRandomDegrees()
	{
		double num = 0.6;
		if (isHacker)
		{
			num = 0.9;
		}
		while (Utils.random.NextDouble() < num)
		{
			if (isHacker)
			{
				degrees.Add(PeopleAssets.getRandomHackerDegree(birthplace));
			}
			else
			{
				degrees.Add(PeopleAssets.getRandomDegree(birthplace));
			}
			num *= num;
			if (isHacker)
			{
				num *= 0.36;
			}
		}
	}

	public void addRandomVehicles()
	{
		double num = 0.7;
		while (Utils.random.NextDouble() < num)
		{
			vehicles.Add(VehicleInfo.getRandomRegistration());
			num *= num;
			if (isHacker)
			{
				num *= num;
			}
		}
	}

	public override string ToString()
	{
		return firstName + " " + lastName + "\n Gender: " + (isMale ? "Male  " : "Female  ") + "Born: " + birthplace.ToString() + "\n" + getDegreeString() + " " + getVehicleRegString() + "\n" + medicalRecord.ToString();
	}

	private string getDegreeString()
	{
		string text = "Degrees:\n";
		for (int i = 0; i < degrees.Count; i++)
		{
			text = text + " -" + degrees[i].ToString() + "\n";
		}
		return text;
	}

	private string getVehicleRegString()
	{
		string text = "Vehicle Registrations:\n";
		for (int i = 0; i < vehicles.Count; i++)
		{
			text = text + " -" + vehicles[i].ToString() + "\n";
		}
		return text;
	}
}
