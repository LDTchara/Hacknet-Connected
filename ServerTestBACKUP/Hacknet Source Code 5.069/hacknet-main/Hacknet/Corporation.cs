using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class Corporation
{
	private static float COMPUTER_SEPERATION = 0.066f;

	private static float COMPUTER_SEPERATION_ADD_PER_CYCLE = 0.04f;

	private static float Y_ASPECT_RATIO_BIAS = 1.9f;

	public static List<Vector2> TestedPositions = new List<Vector2>();

	private OS os;

	public List<Computer> servers;

	private string name;

	private string postfix;

	private string ipSubstring;

	private string baseID;

	public Computer mainframe;

	public Computer mailServer;

	public Computer webServer;

	public Computer internalServices;

	public Computer fileServer;

	public Computer backupServer;

	private Vector2 basePosition;

	private int baseSecurityLevel;

	private int serverCount;

	private bool altRotation;

	public Corporation(OS _os)
	{
		os = _os;
		baseSecurityLevel = 3;
		serverCount = 5;
		baseID = "corp" + GenerationStatics.CorportationsGenerated + "#";
		GenerationStatics.CorportationsGenerated++;
		servers = new List<Computer>();
		altRotation = Utils.flipCoin();
		do
		{
			basePosition = os.netMap.getRandomPosition();
		}
		while (locationCollides(basePosition));
		generate();
	}

	private void generate()
	{
		generateName();
		generateServers();
	}

	private void generateName()
	{
		string[] array = NameGenerator.generateCompanyName();
		name = array[0];
		postfix = array[1];
		ipSubstring = NetworkMap.generateRandomIP();
		ipSubstring = ipSubstring.Substring(ipSubstring.Length - ipSubstring.LastIndexOf('.'));
		ipSubstring += ".";
	}

	private void generateServers()
	{
		generateMainframe();
		os.netMap.nodes.Add(servers[servers.Count - 1]);
		generateMailServer();
		os.netMap.nodes.Add(servers[servers.Count - 1]);
		generateWebServer();
		os.netMap.nodes.Add(servers[servers.Count - 1]);
		generateInternalServices();
		os.netMap.nodes.Add(servers[servers.Count - 1]);
		generateFileServer();
		os.netMap.nodes.Add(servers[servers.Count - 1]);
		generateBackupMachine();
		os.netMap.nodes.Add(servers[servers.Count - 1]);
		linkServers();
	}

	private void generateMainframe()
	{
		mainframe = new Computer(getFullName() + " Central Mainframe", getAddress(), getLocation(), baseSecurityLevel + 2, 3, os);
		mainframe.idName = baseID + "MF";
		servers.Add(mainframe);
	}

	private void generateMailServer()
	{
		mailServer = new Computer(name + " Mail Server", getAddress(), getLocation(), baseSecurityLevel, 3, os);
		MailServer item = new MailServer(mailServer, name + " Mail", os);
		mailServer.daemons.Add(item);
		mailServer.initDaemons();
		mailServer.idName = baseID + "MS";
		servers.Add(mailServer);
	}

	private void generateWebServer()
	{
		webServer = new Computer(name + " Web Server", getAddress(), getLocation(), baseSecurityLevel, 3, os);
		WebServerDaemon webServerDaemon = new WebServerDaemon(webServer, name + " Web Server", os);
		webServer.daemons.Add(webServerDaemon);
		webServer.initDaemons();
		webServerDaemon.generateBaseCorporateSite(getFullName());
		webServer.idName = baseID + "WS";
		servers.Add(webServer);
	}

	private void generateInternalServices()
	{
		internalServices = new Computer(name + " Internal Services Machine", getAddress(), getLocation(), baseSecurityLevel - 1, 1, os);
		internalServices.idName = baseID + "IS";
		servers.Add(internalServices);
	}

	private void generateFileServer()
	{
		fileServer = new Computer(name + " File Server", getAddress(), getLocation(), baseSecurityLevel, 3, os);
		MissionListingServer item = new MissionListingServer(fileServer, "Mission Board", name, os);
		fileServer.daemons.Add(item);
		fileServer.initDaemons();
		fileServer.idName = baseID + "FS";
		servers.Add(fileServer);
	}

	private void generateBackupMachine()
	{
		backupServer = new Computer(getFullName() + " Backup Server", getAddress(), getLocation(), baseSecurityLevel - 1, 1, os);
		backupServer.idName = baseID + "BU";
		servers.Add(backupServer);
	}

	private void linkServers()
	{
		for (int i = 0; i < servers.Count; i++)
		{
			for (int j = 0; j < servers.Count; j++)
			{
				if (j != i)
				{
					servers[i].links.Add(os.netMap.nodes.IndexOf(servers[j]) + 1);
				}
			}
		}
	}

	private void addServersToInternet()
	{
		for (int i = 0; i < servers.Count; i++)
		{
			os.netMap.nodes.Add(servers[i]);
		}
	}

	private string getAddress()
	{
		string text;
		while (true)
		{
			text = ipSubstring + (Utils.random.Next() % 253 + 1);
			bool flag = true;
			for (int i = 0; i < servers.Count; i++)
			{
				if (servers[i].ip.Equals(text))
				{
					flag = false;
				}
			}
			if (flag)
			{
				break;
			}
			bool flag2 = true;
		}
		return text;
	}

	private Vector2 getLocation()
	{
		Vector2 vector = basePosition;
		Vector2 nearbyNodeOffset = getNearbyNodeOffset(basePosition, servers.Count, serverCount, os.netMap);
		return vector + nearbyNodeOffset;
	}

	public static Vector2 GetOffsetPositionFromCycle(int pos, int total, float ExtraDistance = 0f)
	{
		int val = pos / total - 1;
		val = Math.Max(0, val);
		int num = pos % total;
		float magnitude = ExtraDistance + COMPUTER_SEPERATION + (float)val * COMPUTER_SEPERATION_ADD_PER_CYCLE;
		float angle = (float)num / (float)total * ((float)Math.PI * 2f);
		Vector2 result = Utils.PolarToCartesian(angle, magnitude);
		result.Y *= Y_ASPECT_RATIO_BIAS;
		return result;
	}

	private static bool GeneratedPositionIsValid(Vector2 position, NetworkMap netMap, bool ignoreNetmap = false)
	{
		if (position.X < 0f || position.X > 1f || position.Y < 0f || position.Y > 1f)
		{
			return false;
		}
		return ignoreNetmap || !netMap.collides(position);
	}

	public static Vector2 getNearbyNodeOffset(Vector2 basePos, int positionNumber, int total, NetworkMap map, float extraDistance = 0f, bool forceUseThisPosition = false)
	{
		int total2 = total;
		int num = positionNumber;
		if (total < 20)
		{
			int num2 = 30;
			float num3 = (float)positionNumber / (float)total;
			total2 = num2;
			num = (int)(num3 * (float)num2);
		}
		int num4 = 300;
		for (int i = 0; i < num4; i++)
		{
			Vector2 offsetPositionFromCycle = GetOffsetPositionFromCycle(num + i, total2, extraDistance);
			if (GeneratedPositionIsValid(offsetPositionFromCycle + basePos, map, forceUseThisPosition))
			{
				return offsetPositionFromCycle;
			}
			TestedPositions.Add(offsetPositionFromCycle + basePos);
		}
		Vector2 randomPosition = map.getRandomPosition();
		return randomPosition - basePos;
	}

	public static Vector2 getNearbyNodeOffsetOld(Vector2 basePos, int positionNumber, int total, NetworkMap map, float ExtraSeperationDistance = 0f)
	{
		int num = 60;
		int num2 = 0;
		Vector2 vector = Vector2.Zero;
		bool flag = false;
		Vector2 location;
		do
		{
			int num3 = positionNumber + num2;
			int i;
			for (i = total; num3 >= i; i += total)
			{
				num3 -= i;
			}
			if (num3 > 0)
			{
				Vector2 vector2 = Utils.PolarToCartesian((float)num3 / (float)i * ((float)Math.PI * 2f), 1f);
				vector = vector2;
			}
			else
			{
				vector.X = 1f;
			}
			vector.Y *= Y_ASPECT_RATIO_BIAS;
			float num4 = COMPUTER_SEPERATION + ExtraSeperationDistance;
			vector = new Vector2(vector.X * COMPUTER_SEPERATION, vector.Y * COMPUTER_SEPERATION);
			location = basePos + vector;
			num2++;
			TestedPositions.Add(vector);
		}
		while ((location.X < 0f || location.X > 1f || location.Y < 0f || location.Y > 1f || map.collides(location, 0.075f)) && num2 < num);
		if (num2 >= num)
		{
			if (ExtraSeperationDistance <= 0f)
			{
				return getNearbyNodeOffsetOld(basePos, positionNumber, total, map, COMPUTER_SEPERATION);
			}
			if (ExtraSeperationDistance <= COMPUTER_SEPERATION)
			{
				return getNearbyNodeOffsetOld(basePos, positionNumber, total, map, COMPUTER_SEPERATION + COMPUTER_SEPERATION);
			}
			Vector2 randomPosition = map.getRandomPosition();
			vector = randomPosition - basePos;
		}
		return vector;
	}

	private bool locationCollides(Vector2 loc)
	{
		for (int i = 0; i < servers.Count; i++)
		{
			if (Vector2.Distance(loc, servers[i].location) <= 0.08f)
			{
				return true;
			}
		}
		if (loc.X < 0f || loc.X > 1f)
		{
			return true;
		}
		if (loc.Y < 0f || loc.Y > 1f)
		{
			return true;
		}
		return false;
	}

	public string getName()
	{
		return name;
	}

	public string getFullName()
	{
		return name + postfix;
	}
}
