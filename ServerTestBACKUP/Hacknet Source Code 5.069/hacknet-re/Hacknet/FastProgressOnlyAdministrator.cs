namespace Hacknet;

internal class FastProgressOnlyAdministrator : Administrator
{
	public override void traceEjectionDetected(Computer c, OS os)
	{
		base.traceEjectionDetected(c, os);
		disconnectionDetected(c, os);
	}

	public override void disconnectionDetected(Computer c, OS os)
	{
		base.disconnectionDetected(c, os);
		if (c.adminIP != os.thisComputer.adminIP)
		{
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
		}
	}
}
