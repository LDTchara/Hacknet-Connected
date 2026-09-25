namespace Hacknet;

internal class Administrator
{
	public bool ResetsPassword = false;

	public bool IsSuper = false;

	public virtual void disconnectionDetected(Computer c, OS os)
	{
	}

	public virtual void traceEjectionDetected(Computer c, OS os)
	{
	}
}
