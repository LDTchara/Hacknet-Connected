using System;

namespace Hacknet;

internal class FastBasicAdministrator : Administrator
{
	public override void disconnectionDetected(Computer c, OS os)
	{
		base.disconnectionDetected(c, os);
		for (int i = 0; i < c.ports.Count; i++)
		{
			c.closePort(c.ports[i], "LOCAL_ADMIN");
		}
		if (c.firewall != null)
		{
			c.firewall.resetSolutionProgress();
			c.firewall.solved = false;
		}
		if (c.hasProxy)
		{
			c.proxyActive = true;
			c.proxyOverloadTicks = c.startingOverloadTicks;
		}
		double time = 20.0 * Utils.random.NextDouble();
		Action action = delegate
		{
			if (os.connectedComp == null || os.connectedComp.ip != c.ip)
			{
				for (int j = 0; j < c.ports.Count; j++)
				{
					c.closePort(c.ports[j], "LOCAL_ADMIN");
				}
				if (ResetsPassword)
				{
					c.setAdminPassword(PortExploits.getRandomPassword());
				}
				c.adminIP = c.ip;
				if (c.firewall != null)
				{
					c.firewall.resetSolutionProgress();
				}
			}
		};
		if (IsSuper)
		{
			action();
		}
		else
		{
			os.delayer.Post(ActionDelayer.Wait(time), action);
		}
	}
}
