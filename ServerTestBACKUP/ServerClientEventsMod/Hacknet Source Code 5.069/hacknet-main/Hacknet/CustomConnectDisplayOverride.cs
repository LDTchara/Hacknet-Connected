namespace Hacknet;

internal abstract class CustomConnectDisplayOverride : Daemon
{
	public CustomConnectDisplayOverride(Computer c, string name, OS os)
		: base(c, name, os)
	{
		isListed = false;
	}
}
