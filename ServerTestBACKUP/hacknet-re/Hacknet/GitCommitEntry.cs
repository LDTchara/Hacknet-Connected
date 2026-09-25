using System.Collections.Generic;

namespace Hacknet;

public class GitCommitEntry
{
	public int EntryNumber = 0;

	public List<string> ChangedFiles = new List<string>();

	public string Message;

	public string UserName;

	public string SourceIP;

	public override string ToString()
	{
		return "Commit#" + EntryNumber.ToString("000") + (SourceIP.StartsWith("192.168.1.1") ? "" : "*");
	}
}
