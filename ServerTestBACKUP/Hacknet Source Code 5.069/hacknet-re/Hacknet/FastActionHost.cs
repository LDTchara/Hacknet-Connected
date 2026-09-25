using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class FastActionHost : Daemon
{
	public FastDelayableActionSystem DelayedActions;

	public bool RequiresLogin = false;

	private Folder folder;

	public FastActionHost(Computer c, OS os, string name)
		: base(c, name, os)
	{
		isListed = true;
	}

	public override void initFiles()
	{
		base.initFiles();
		folder = comp.files.root.searchForFolder("runtime");
		if (folder == null)
		{
			folder = new Folder("runtime");
			comp.files.root.folders.Add(folder);
		}
		DelayedActions = new FastDelayableActionSystem(folder, os);
	}

	public override void loadInit()
	{
		base.loadInit();
		folder = comp.files.root.searchForFolder("runtime");
		DelayedActions = new FastDelayableActionSystem(folder, os);
		DelayedActions.DeserializeActions(folder.files);
	}

	public string GetName()
	{
		return name;
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		Vector2 pos = new Vector2((float)bounds.X + 20f, (float)bounds.Y + 20f);
		TextItem.doFontLabel(pos, "Active Actions : " + DelayedActions.Actions.Count, GuiData.smallfont, Color.White);
		pos.Y += 30f;
		if (Button.doButton(38391101, (int)pos.X, (int)pos.Y, 300, 25, "Back", null))
		{
			os.display.command = "connect";
		}
	}

	public override string getSaveString()
	{
		folder.files = DelayedActions.GetAllFilesForActions();
		return "<FastActionHost />";
	}
}
