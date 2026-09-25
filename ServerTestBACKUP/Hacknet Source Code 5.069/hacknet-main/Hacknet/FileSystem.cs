using System;
using System.Xml;

namespace Hacknet;

internal class FileSystem
{
	public Folder root;

	public FileSystem(bool empty)
	{
	}

	public FileSystem()
	{
		root = new Folder("/");
		root.folders.Add(new Folder("home"));
		root.folders.Add(new Folder("log"));
		root.folders.Add(new Folder("bin"));
		root.folders.Add(new Folder("sys"));
		generateSystemFiles();
	}

	public void generateSystemFiles()
	{
		Folder folder = root.searchForFolder("sys");
		folder.files.Add(new FileEntry(ThemeManager.getThemeDataString(OSTheme.HacknetTeal), "x-server.sys"));
		folder.files.Add(new FileEntry(Computer.generateBinaryString(500), "os-config.sys"));
		folder.files.Add(new FileEntry(Computer.generateBinaryString(500), "bootcfg.dll"));
		folder.files.Add(new FileEntry(Computer.generateBinaryString(500), "netcfgx.dll"));
	}

	public string getSaveString()
	{
		string text = "<filesystem>\n";
		text += root.getSaveString();
		return text + "</filesystem>\n";
	}

	public static FileSystem load(XmlReader reader)
	{
		FileSystem fileSystem = new FileSystem(empty: true);
		while (reader.Name != "filesystem")
		{
			reader.Read();
		}
		fileSystem.root = Folder.load(reader);
		return fileSystem;
	}

	public string TestEquals(object obj)
	{
		if (!(obj is FileSystem fileSystem))
		{
			throw new ArgumentNullException();
		}
		return root.TestEqualsFolder(fileSystem.root);
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
