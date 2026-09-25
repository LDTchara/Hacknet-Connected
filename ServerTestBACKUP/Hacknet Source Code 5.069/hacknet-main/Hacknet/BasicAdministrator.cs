namespace Hacknet;

internal class BasicAdministrator : Administrator
{
	public override void disconnectionDetected(Computer c, OS os)
	{
		base.disconnectionDetected(c, os);
		double time = 20.0 * Utils.random.NextDouble();
		os.delayer.Post(ActionDelayer.Wait(time), delegate
		{
			if (os.connectedComp == null || os.connectedComp.ip != c.ip)
			{
				for (int i = 0; i < c.ports.Count; i++)
				{
					c.closePort(c.ports[i], "LOCAL_ADMIN");
				}
				if (ResetsPassword)
				{
					c.setAdminPassword(PortExploits.getRandomPassword());
				}
				c.adminIP = c.ip;
				if (c.firewall != null)
				{
					c.firewall.solved = false;
					c.firewall.resetSolutionProgress();
				}
			}
		});
	}
}
