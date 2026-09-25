using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Hacknet;

public class MedicalRecord
{
	public List<string> Visits = new List<string>();

	public List<string> Perscriptions = new List<string>();

	public List<string> Allergies = new List<string>();

	public int Height = 172;

	public string Notes = "N/A";

	public string BloodType = "AB";

	public DateTime DateofBirth;

	private static List<string> Allergants = null;

	public MedicalRecord()
	{
	}

	public MedicalRecord(WorldLocation location, DateTime dob)
	{
		DateofBirth = dob;
		int days = (DateTime.Now - dob).Days;
		int num = 155;
		int num2 = 220;
		Height = num + (int)(Utils.random.NextDouble() * Utils.random.NextDouble() * (double)(num2 - num));
		AddRandomVists(days, location);
		char c = (Utils.flipCoin() ? 'A' : (Utils.flipCoin() ? 'B' : 'O'));
		char c2 = (Utils.flipCoin() ? 'A' : (Utils.flipCoin() ? 'B' : 'O'));
		BloodType = string.Concat(c, c2);
		if (Allergants == null)
		{
			LoadStatics();
		}
		AddAllergies();
	}

	private void LoadStatics()
	{
		Allergants = new List<string>();
		Allergants.AddRange(Utils.readEntireFile("Content/PersonData/Allergies.txt").Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries));
	}

	private void AddAllergies()
	{
		int num = 4;
		int num2 = Math.Max(0, Utils.random.Next(0, num + 2) - 2);
		for (int i = 0; i < num2; i++)
		{
			string text = null;
			do
			{
				text = Allergants[Utils.random.Next(0, Allergants.Count - 1)];
			}
			while (Allergies.Contains(text));
			Allergies.Add(text);
		}
	}

	private void AddRandomVists(int daysOld, WorldLocation loc)
	{
		int num = (int)(Utils.random.NextDouble() * Utils.random.NextDouble() * 10.0);
		int maxValue = daysOld / 2;
		int num2 = daysOld;
		int num3 = 0;
		for (int i = 0; i < num; i++)
		{
			int num4 = Utils.random.Next(1, maxValue);
			DateTime dateTime = DateofBirth + TimeSpan.FromDays((double)(num3 + num4) - Utils.random.NextDouble());
			string item = dateTime.ToString() + " - " + ((Utils.random.NextDouble() < 0.7) ? loc.name : loc.country) + (Utils.flipCoin() ? " Public" : " Private") + " Hospital";
			Visits.Add(item);
			num2 -= num4;
			num3 += num4;
			maxValue = (int)((double)num2 * 0.7);
		}
	}

	public override string ToString()
	{
		CultureInfo cultureInfo = new CultureInfo("en-au");
		string text = "Medical Record\n";
		text = text + "Date of Birth :: " + Utils.SafeWriteDateTime(DateofBirth);
		TimeSpan timeSpan = DateTime.Now - DateofBirth;
		text = text + "\nBlood Type :: " + BloodType;
		double num = 0.032808399 * (double)Height;
		object obj = text;
		text = string.Concat(obj, "\nHeight :: ", Height, "cm (", (int)num, "\"", (int)(num % 1.0 * 12.0), "')");
		text = text + "\nAllergies :: " + GetCSVFromList(Allergies);
		text += "\nActive Prescriptions :: ";
		if (Perscriptions.Count == 0)
		{
			text += "NONE";
		}
		else
		{
			obj = text;
			text = string.Concat(obj, "x", Perscriptions.Count, " Active");
			for (int i = 0; i < Perscriptions.Count; i++)
			{
				text = text + "\n" + Perscriptions[i];
			}
		}
		text += "\nRecorded Visits ::";
		if (Visits.Count == 0)
		{
			text += "NONE RECORDED\n";
		}
		else
		{
			for (int i = 0; i < Visits.Count; i++)
			{
				text = text + Visits[i] + "\n";
			}
		}
		return text + "Notes :: " + Notes + "\n";
	}

	public static MedicalRecord Load(XmlReader rdr, WorldLocation location, DateTime dob)
	{
		MedicalRecord medicalRecord = new MedicalRecord(location, dob);
		while (rdr.Name != "Medical")
		{
			rdr.Read();
		}
		rdr.Read();
		while (!(rdr.Name == "Medical") || rdr.IsStartElement())
		{
			if (rdr.IsStartElement())
			{
				switch (rdr.Name)
				{
				case "Blood":
					medicalRecord.BloodType = rdr.ReadElementContentAsString();
					break;
				case "Height":
					medicalRecord.Height = rdr.ReadElementContentAsInt();
					break;
				case "Allergies":
					medicalRecord.Allergies.Clear();
					medicalRecord.Allergies.AddRange(rdr.ReadElementContentAsString().Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries));
					break;
				case "Perscription":
					medicalRecord.Perscriptions.Add(rdr.ReadElementContentAsString());
					break;
				case "Notes":
					medicalRecord.Notes = rdr.ReadElementContentAsString();
					break;
				}
			}
			rdr.Read();
		}
		return medicalRecord;
	}

	private string GetCSVFromList(List<string> list)
	{
		if (list.Count <= 0)
		{
			return "NONE";
		}
		string text = "";
		for (int i = 0; i < list.Count; i++)
		{
			text = text + list[i] + ",";
		}
		return text.Substring(0, text.Length - 1);
	}
}
