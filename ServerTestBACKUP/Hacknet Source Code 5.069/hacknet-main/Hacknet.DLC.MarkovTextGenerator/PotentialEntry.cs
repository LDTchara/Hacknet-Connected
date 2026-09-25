namespace Hacknet.DLC.MarkovTextGenerator;

public class PotentialEntry
{
	public string word;

	public double weighting;

	public override string ToString()
	{
		return word + " %" + weighting.ToString("0.00");
	}
}
