using System;

namespace Hacknet;

public class Neopal
{
	public enum PetType
	{
		Blundo,
		Chisha,
		Jubdub,
		Kachici,
		Kyrill,
		Myncl,
		Pageri,
		Psybunny,
		Scorchum,
		Unisam
	}

	public PetType Type;

	public string Name;

	public int DaysSinceFed;

	public byte CombatRating;

	public float Happiness;

	public string Identifier;

	private static string[] PossibleNames;

	private static string GenerateName()
	{
		if (PossibleNames == null)
		{
			string text = Utils.readEntireFile("Content/DLC/Docs/Untranslated/NeopalNames.txt");
			PossibleNames = text.Split(new string[5] { "\r\n", "\n", ", ", " ,", "," }, StringSplitOptions.RemoveEmptyEntries);
		}
		return PossibleNames[Utils.random.Next(PossibleNames.Length)];
	}

	public static Neopal GeneratePet(bool isActiveUser = false)
	{
		isActiveUser = isActiveUser || Utils.randm(1f) < 0.02f;
		Array values = Enum.GetValues(typeof(PetType));
		PetType type = (PetType)values.GetValue(Utils.random.Next(values.Length));
		Neopal neopal = new Neopal();
		neopal.CombatRating = (byte)Utils.random.Next(255);
		neopal.DaysSinceFed = (isActiveUser ? Utils.random.Next(2) : Utils.random.Next(3650));
		neopal.Happiness = (isActiveUser ? (1f - Utils.randm(0.15f)) : Utils.randm(0.05f));
		neopal.Name = GenerateName();
		neopal.Type = type;
		neopal.Identifier = Guid.NewGuid().ToString().Substring(0, 13);
		return neopal;
	}
}
