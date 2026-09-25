namespace Hacknet;

public class OnlineAccount
{
	public int ID = 0;

	public string Username;

	public string BanStatus;

	public string Notes;

	public override string ToString()
	{
		return Username + "#" + ID;
	}
}
