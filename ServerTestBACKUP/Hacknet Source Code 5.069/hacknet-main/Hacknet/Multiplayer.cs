using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class Multiplayer
{
	private static int generatedComputerCount = 0;

	public static int PORT = 3020;

	public static char[] delims = new char[2] { ' ', '\n' };

	public static char[] specSplitDelims = new char[1] { '#' };

	public static void parseInputMessage(string message, OS os)
	{
		if (message.Equals(""))
		{
			return;
		}
		string[] array = message.Split(delims);
		if (array.Length == 0)
		{
			return;
		}
		if (os.thisComputer != null && os.thisComputer.ip.Equals(array[1]))
		{
			os.warningFlash();
		}
		if (array[0].Equals("init"))
		{
			int num = Convert.ToInt32(array[1]);
			Utils.random = new Random(num);
			os.canRunContent = true;
			os.LoadContent();
			os.write("Seed Established :" + num);
		}
		else if (array[0].Equals("chat"))
		{
			string text = "";
			for (int i = 2; i < array.Length; i++)
			{
				text = text + array[i] + " ";
			}
			os.write(array[1] + ": " + DisplayModule.splitForWidth(text, 350));
		}
		else if (array[0].Equals("clientConnect"))
		{
			os.write("Connection Established");
		}
		else if (array[0].Equals("cConnection"))
		{
			Computer comp = getComp(array[1], os);
			if (comp == null)
			{
				os.write("Error in Message : " + message);
				return;
			}
			comp.silent = true;
			comp.connect(array[2]);
			comp.silent = false;
			os.opponentLocation = array[1];
		}
		else if (array[0].Equals("cDisconnect"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			comp.disconnecting(array[2]);
			comp.silent = false;
			os.opponentLocation = "";
		}
		else if (array[0].Equals("cAdmin"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			comp.giveAdmin(array[2]);
			comp.silent = false;
		}
		else if (array[0].Equals("cPortOpen"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			comp.openPort(Convert.ToInt32(array[3]), array[2]);
			comp.silent = false;
		}
		else if (array[0].Equals("cPortClose"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			comp.closePort(Convert.ToInt32(array[3]), array[2]);
			comp.silent = false;
		}
		else if (array[0].Equals("cFile"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			FileEntry f = new FileEntry("", array[3]);
			comp.canReadFile(array[2], f, Convert.ToInt32(array[4]));
			comp.silent = false;
		}
		else if (array[0].Equals("newComp"))
		{
			array = message.Split(specSplitDelims);
			Computer comp = new Computer(compLocation: new Vector2(Convert.ToInt32(array[2]), Convert.ToInt32(array[3])), compName: array[5], compIP: array[1], seclevel: Convert.ToInt32(array[4]), compType: 1, opSystem: os);
			comp.idName = "opponent#" + generatedComputerCount;
			generatedComputerCount++;
			comp.addMultiplayerTargetFile();
			os.netMap.nodes.Add(comp);
			os.opponentComputer = comp;
		}
		else if (array[0].Equals("cDelete"))
		{
			array = message.Split(specSplitDelims);
			Computer comp = getComp(array[1], os);
			List<int> list = new List<int>();
			for (int i = 4; i < array.Length; i++)
			{
				if (array[i] != "")
				{
					list.Add(Convert.ToInt32(array[i]));
				}
			}
			comp.silent = true;
			comp.deleteFile(array[2], array[3], list);
			comp.silent = false;
		}
		else if (array[0].Equals("cMake"))
		{
			array = message.Split(specSplitDelims);
			Computer comp = getComp(array[1], os);
			List<int> list = new List<int>();
			for (int i = 4; i < array.Length; i++)
			{
				if (array[i] != "")
				{
					list.Add(Convert.ToInt32(array[i]));
				}
			}
			comp.silent = true;
			comp.makeFile(array[2], array[3], array[4], list);
			comp.silent = false;
		}
		else if (array[0].Equals("cMove"))
		{
			array = message.Split(specSplitDelims);
			Computer comp = getComp(array[1], os);
			char[] separator = new char[1] { '%' };
			List<int> list = new List<int>();
			string[] array2 = array[5].Split(separator, 500, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array2.Length; i++)
			{
				if (array[i] != "")
				{
					list.Add(Convert.ToInt32(array[i]));
				}
			}
			List<int> list2 = new List<int>();
			array2 = array[6].Split(separator, 500, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array2.Length; i++)
			{
				if (array[i] != "")
				{
					list2.Add(Convert.ToInt32(array[i]));
				}
			}
			comp.silent = true;
			comp.moveFile(array[2], array[3], array[4], list, list2);
			comp.silent = false;
		}
		else if (array[0].Equals("cMkDir"))
		{
			array = message.Split(specSplitDelims);
			Computer comp = getComp(array[1], os);
			List<int> list = new List<int>();
			for (int i = 4; i < array.Length; i++)
			{
				if (array[i] != "")
				{
					list.Add(Convert.ToInt32(array[i]));
				}
			}
			comp.silent = true;
			comp.makeFolder(array[2], array[3], list);
			comp.silent = false;
		}
		else if (array[0].Equals("cAddUser"))
		{
			array = message.Split(specSplitDelims);
			Computer comp = getComp(array[1], os);
			string name = array[3];
			string pass = array[4];
			byte type = Convert.ToByte(array[5]);
			comp.silent = true;
			comp.addNewUser(array[2], name, pass, type);
			comp.silent = false;
		}
		else if (array[0].Equals("cCopy"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			comp.canCopyFile(array[2], array[3]);
			comp.silent = false;
			FileEntry f = null;
			for (int i = 0; i < comp.files.root.folders[2].files.Count; i++)
			{
				if (comp.files.root.folders[2].files[i].name.Equals(array[3]))
				{
					f = comp.files.root.folders[2].files[i];
				}
			}
			FileEntry item = new FileEntry(f.data, f.name);
			Computer comp2 = getComp(array[2], os);
			comp2.files.root.folders[2].files.Add(item);
		}
		else if (array[0].Equals("cCDDrive"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			if (array[2].Equals("open"))
			{
				comp.openCDTray(array[1]);
			}
			else
			{
				comp.closeCDTray(array[1]);
			}
			comp.silent = false;
		}
		else if (array[0].Equals("cCrash"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			comp.crash(array[2]);
			comp.silent = false;
		}
		else if (array[0].Equals("cReboot"))
		{
			Computer comp = getComp(array[1], os);
			comp.silent = true;
			comp.reboot(array[2]);
			comp.silent = false;
		}
		else if (array[0].Equals("cFBClients"))
		{
			Computer comp = getComp(array[1], os);
			if (os.connectedComp != null && os.connectedComp.ip.Equals(array[1]))
			{
				os.exes.Add(new ForkBombExe(os.getExeBounds(), os));
			}
			comp.silent = true;
			comp.forkBombClients(array[2]);
			comp.silent = false;
		}
		else if (array[0].Equals("eForkBomb"))
		{
			if (os.thisComputer.ip.Equals(array[1]))
			{
				ForkBombExe forkBombExe = new ForkBombExe(os.getExeBounds(), os);
				forkBombExe.LoadContent();
				os.exes.Add(forkBombExe);
			}
		}
		else if (array[0].Equals("mpOpponentWin"))
		{
			os.endMultiplayerMatch(won: false);
		}
		else if (!array[0].Equals("stayAlive"))
		{
			os.write("MSG: " + message);
		}
	}

	private static Computer getComp(string ip, OS os)
	{
		for (int i = 0; i < os.netMap.nodes.Count; i++)
		{
			if (os.netMap.nodes[i].ip.Equals(ip))
			{
				return os.netMap.nodes[i];
			}
		}
		return null;
	}
}
