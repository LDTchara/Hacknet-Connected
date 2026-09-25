using System.IO;
using System.Xml;

namespace Hacknet;

public static class MemoryDumpInjector
{
	public static void InjectMemory(string memoryFilepath, object computer)
	{
		using FileStream input = File.OpenRead(memoryFilepath);
		XmlReader rdr = XmlReader.Create(input);
		MemoryContents memory = MemoryContents.Deserialize(rdr);
		Computer computer2 = (Computer)computer;
		computer2.Memory = memory;
	}
}
