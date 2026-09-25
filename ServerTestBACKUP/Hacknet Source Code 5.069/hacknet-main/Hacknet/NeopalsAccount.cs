using System;
using System.Collections.Generic;

namespace Hacknet;

public class NeopalsAccount
{
	public string AccountName;

	public long NeoPoints;

	public long BankedPoints;

	public string InventoryID;

	public List<Neopal> Pets;

	public static NeopalsAccount GenerateAccount(string handle, bool isActiveUser = false)
	{
		NeopalsAccount neopalsAccount = new NeopalsAccount();
		neopalsAccount.AccountName = handle;
		neopalsAccount.NeoPoints = (isActiveUser ? Utils.random.Next(50000) : Utils.random.Next(5000));
		neopalsAccount.BankedPoints = (isActiveUser ? Utils.random.Next(10000) : Utils.random.Next(2000));
		neopalsAccount.InventoryID = Guid.NewGuid().ToString();
		neopalsAccount.Pets = new List<Neopal>();
		NeopalsAccount neopalsAccount2 = neopalsAccount;
		int num = 1 + Utils.random.Next(2);
		if (num == 2 && Utils.flipCoin() && Utils.flipCoin())
		{
			num++;
		}
		for (int i = 0; i < num; i++)
		{
			neopalsAccount2.Pets.Add(Neopal.GeneratePet(isActiveUser));
		}
		return neopalsAccount2;
	}

	public override string ToString()
	{
		return AccountName + "_x" + Pets.Count;
	}
}
