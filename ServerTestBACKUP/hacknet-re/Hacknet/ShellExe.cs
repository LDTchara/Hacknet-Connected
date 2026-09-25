using Hacknet.Gui;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class ShellExe : ExeModule
{
	private const int IDLE_STATE = 0;

	private const int CLOSING_STATE = -1;

	private const int PROXY_OVERLOAD_STATE = 1;

	private const int FORKBOMB_TRAP_STATE = 2;

	public static int INFOBAR_HEIGHT = 16;

	public static int BASE_RAM_COST = 40;

	public static float RAM_CHANGE_PS = 200f;

	public static int TRAP_RAM_USE = 100;

	private Rectangle infoBar;

	public string destinationIP = "";

	private Computer destComp = null;

	private Computer compThisShellIsRunningOn = null;

	private int destCompIndex = -1;

	private int state = 0;

	private int targetRamUse = BASE_RAM_COST;

	public ShellExe(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		ramCost = BASE_RAM_COST;
		IdentifierName = "Shell@" + targetIP;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		infoBar = new Rectangle(bounds.X, bounds.Y, bounds.Width, INFOBAR_HEIGHT);
		os.shells.Add(this);
		os.shellIPs.Add(targetIP);
		compThisShellIsRunningOn = Programs.getComputer(os, targetIP);
		compThisShellIsRunningOn.log(os.thisComputer.ip + "_Opened_#SHELL");
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (!Programs.getComputer(os, targetIP).adminIP.Equals(os.thisComputer.ip) || Programs.getComputer(os, targetIP).disabled)
		{
			if (state != -1)
			{
				os.write(">>");
				os.write(">> " + string.Format(LocaleTerms.Loc("SHELL ERROR: Administrator account lost on {0}"), compThisShellIsRunningOn.ip));
				os.write(">>");
			}
			Completed();
		}
		if (targetRamUse != ramCost)
		{
			if (targetRamUse < ramCost)
			{
				ramCost -= (int)(t * RAM_CHANGE_PS);
				if (ramCost < targetRamUse)
				{
					ramCost = targetRamUse;
				}
			}
			else
			{
				int num = (int)(t * RAM_CHANGE_PS);
				if (os.ramAvaliable >= num)
				{
					ramCost += num;
					if (ramCost > targetRamUse)
					{
						ramCost = targetRamUse;
					}
				}
			}
		}
		switch (state)
		{
		case 1:
			if (destComp.hasProxy)
			{
				destComp.proxyOverloadTicks -= t;
				if (destComp.proxyOverloadTicks <= 0f)
				{
					destComp.proxyOverloadTicks = 0f;
					destComp.proxyActive = false;
					completedAction(1);
				}
				else
				{
					destComp.hostileActionTaken();
				}
			}
			break;
		case 2:
			break;
		case -1:
		case 0:
			break;
		}
	}

	public override void Draw(float t)
	{
		string identifierName = IdentifierName;
		IdentifierName = "@" + targetIP;
		base.Draw(t);
		base.drawOutline();
		base.drawTarget("S");
		IdentifierName = identifierName;
		doGui();
	}

	public void doGui()
	{
		int num = state;
		if (num == 2 && ramCost == targetRamUse)
		{
			Color value = os.highlightColor;
			if (os.opponentLocation.Equals(targetIP))
			{
				value = os.lockedColor;
			}
			if (Button.doButton(95000 + os.exes.IndexOf(this), bounds.X + 10, bounds.Y + 20, bounds.Width - 20, 50, LocaleTerms.Loc("Trigger"), value))
			{
				destComp.forkBombClients(targetIP);
				completedAction(2);
				compThisShellIsRunningOn.log("#SHELL_TrapActivate_:_ConnectionsFlooded");
			}
		}
		doControlButtons();
	}

	public void StartOverload()
	{
		state = 1;
		targetRamUse = BASE_RAM_COST;
		destinationIP = ((os.connectedComp == null) ? os.thisComputer.ip : os.connectedComp.ip);
		if (destComp == null || destComp.ip != destinationIP)
		{
			compThisShellIsRunningOn.log("#SHELL_Overload_@_" + destinationIP);
		}
		destComp = Programs.getComputer(os, destinationIP);
		destCompIndex = os.netMap.nodes.IndexOf(destComp);
	}

	public void doControlButtons()
	{
		int num = 2;
		int num2 = 76;
		int num3 = bounds.Width - (num + 3 * num + num2 - 10);
		int num4 = (int)((double)num3 * 0.33333);
		int num5 = bounds.X + num + 1;
		int num6 = bounds.Height - Module.PANEL_HEIGHT - 8;
		int num7 = bounds.Y + bounds.Height - num6 - 6;
		if (bounds.Height - Module.PANEL_HEIGHT <= 5)
		{
			return;
		}
		Button.ForceNoColorTag = true;
		if (state == 2)
		{
			int num8 = 50;
			int num9 = 17;
			num7 += num8 + 9;
			num6 = num9;
		}
		if (Button.doButton(89200 + os.exes.IndexOf(this), num5, num7, (int)((double)num3 * 0.5), num6, LocaleTerms.Loc("Overload"), os.shellButtonColor * fade))
		{
			StartOverload();
		}
		if (Button.doButton(89300 + os.exes.IndexOf(this), num5 += (int)((double)num3 * 0.5) + num, num7, (int)((double)num3 * 0.36), num6, LocaleTerms.Loc("Trap"), os.shellButtonColor * fade))
		{
			state = 2;
			targetRamUse = TRAP_RAM_USE;
			destinationIP = ((os.connectedComp == null) ? os.thisComputer.ip : os.connectedComp.ip);
			if (destComp == null || destComp.ip != destinationIP)
			{
				compThisShellIsRunningOn.log("#SHELL_TrapAcive");
			}
			destComp = Programs.getComputer(os, destinationIP);
			destCompIndex = os.netMap.nodes.IndexOf(destComp);
		}
		if (Button.doButton(89101 + os.exes.IndexOf(this), bounds.X + bounds.Width - num2 - (num + 1), num7, num2, num6, LocaleTerms.Loc("Close"), os.lockedColor * fade))
		{
			Completed();
		}
		Button.ForceNoColorTag = false;
	}

	public void completedAction(int action)
	{
		cancelTarget();
	}

	public void cancelTarget()
	{
		state = 0;
		destinationIP = "";
		destComp = null;
		destCompIndex = -1;
		targetRamUse = BASE_RAM_COST;
	}

	public override void Completed()
	{
		base.Completed();
		cancelTarget();
		state = -1;
		os.shells.Remove(this);
		os.shellIPs.Remove(targetIP);
		if (!isExiting)
		{
			compThisShellIsRunningOn.log("#SHELL_Closed");
		}
		isExiting = true;
	}

	public void reportedTo(string data)
	{
	}
}
