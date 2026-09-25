using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class Daemon
{
	public string name;

	public bool isListed;

	public Computer comp;

	public OS os;

	public Daemon(Computer computer, string serviceName, OS opSystem)
	{
		name = serviceName;
		isListed = true;
		comp = computer;
		os = opSystem;
	}

	public virtual void initFiles()
	{
	}

	public virtual void draw(Rectangle bounds, SpriteBatch sb)
	{
	}

	public virtual void navigatedTo()
	{
	}

	public virtual void userAdded(string name, string pass, byte type)
	{
	}

	public virtual string getSaveString()
	{
		return "";
	}

	public virtual void loadInit()
	{
	}

	public static bool validUser(byte type)
	{
		return type == 1 || type == 0;
	}

	public void registerAsDefaultBootDaemon()
	{
		if (!comp.AllowsDefaultBootModule)
		{
			return;
		}
		FileEntry fileEntry = comp.files.root.searchForFolder("sys").searchForFile(ComputerTypeInfo.getDefaultBootDaemonFilename(this));
		if (fileEntry != null)
		{
			if (fileEntry.data != "[Locked]")
			{
				fileEntry.data = LocaleTerms.Loc(name);
			}
		}
		else
		{
			fileEntry = new FileEntry(LocaleTerms.Loc(name), ComputerTypeInfo.getDefaultBootDaemonFilename(this));
			comp.files.root.searchForFolder("sys").files.Add(fileEntry);
		}
	}
}
