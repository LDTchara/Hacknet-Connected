using System;
using System.Collections.Generic;
using Hacknet.Daemons.Helpers;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class IRCDaemon : Daemon, IMonitorableDaemon
{
	public IRCSystem System;

	public List<KeyValuePair<string, string>> StartingMessages = new List<KeyValuePair<string, string>>();

	public Dictionary<string, Color> UserColors = new Dictionary<string, Color>();

	public Color ThemeColor;

	public DelayableActionSystem DelayedActions;

	public bool RequiresLogin = false;

	public IRCDaemon(Computer c, OS os, string name)
		: base(c, name, os)
	{
		isListed = true;
	}

	public override void initFiles()
	{
		base.initFiles();
		Folder folder = comp.files.root.searchForFolder("IRC");
		if (folder == null)
		{
			folder = new Folder("IRC");
			comp.files.root.folders.Add(folder);
		}
		System = new IRCSystem(folder);
		for (int i = 0; i < StartingMessages.Count; i++)
		{
			System.AddLog(StartingMessages[i].Key, StartingMessages[i].Value);
		}
		string text = name + "\nRequireAuth: " + (RequiresLogin ? "true" : "false") + "\n" + Utils.convertColorToParseableString(ThemeColor) + "\n";
		foreach (KeyValuePair<string, Color> userColor in UserColors)
		{
			string text2 = text;
			text = text2 + userColor.Key + "#%#" + Utils.convertColorToParseableString(userColor.Value) + "\n";
		}
		folder.files.Add(new FileEntry(text.Trim(), "users.cfg"));
		Folder folder2 = folder.searchForFolder("runtime");
		if (folder2 == null)
		{
			folder2 = new Folder("runtime");
			folder.folders.Add(folder2);
		}
		DelayedActions = new DelayableActionSystem(folder2, os);
	}

	public override void loadInit()
	{
		base.loadInit();
		Folder folder = comp.files.root.searchForFolder("IRC");
		System = new IRCSystem(folder);
		Folder sourceFolder = folder.searchForFolder("runtime");
		DelayedActions = new DelayableActionSystem(sourceFolder, os);
		ReloadUserColors();
	}

	public void SubscribeToAlertActionFroNewMessage(Action<string, string> act)
	{
		IRCSystem system = System;
		system.LogAdded = (Action<string, string>)Delegate.Combine(system.LogAdded, act);
	}

	public void UnSubscribeToAlertActionFroNewMessage(Action<string, string> act)
	{
		IRCSystem system = System;
		system.LogAdded = (Action<string, string>)Delegate.Remove(system.LogAdded, act);
	}

	public bool ShouldDisplayNotifications()
	{
		return true;
	}

	public string GetName()
	{
		return name;
	}

	private void ReloadUserColors()
	{
		Folder folder = comp.files.root.searchForFolder("IRC");
		UserColors.Clear();
		FileEntry fileEntry = folder.searchForFile("users.cfg");
		if (fileEntry == null)
		{
			return;
		}
		string[] array = fileEntry.data.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
		try
		{
			name = array[0];
			RequiresLogin = array[1].Substring("RequireAuth: ".Length).Trim().ToLower() == "true";
			ThemeColor = Utils.convertStringToColor(array[2]);
		}
		catch (Exception)
		{
		}
		for (int i = 3; i < array.Length; i++)
		{
			try
			{
				string[] array2 = array[i].Split(new string[1] { "#%#" }, StringSplitOptions.RemoveEmptyEntries);
				UserColors.Add(array2[0], Utils.convertStringToColor(array2[1]));
			}
			catch (Exception)
			{
			}
		}
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		ReloadUserColors();
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		bool flag = !RequiresLogin || comp.adminIP == os.thisComputer.ip || comp.userLoggedIn;
		Rectangle dest = Utils.InsetRectangle(bounds, 2);
		Rectangle destinationRectangle = new Rectangle(dest.X, dest.Y - 1, 18, dest.Height + 2);
		sb.Draw(Utils.white, destinationRectangle, ThemeColor);
		destinationRectangle.X += destinationRectangle.Width / 2;
		destinationRectangle.Width /= 2;
		sb.Draw(Utils.white, destinationRectangle, Color.Black * 0.2f);
		dest.X += 20;
		dest.Width -= 25;
		Rectangle rectangle = new Rectangle(dest.X + 4, dest.Y, dest.Width, 35);
		TextItem.doFontLabelToSize(rectangle, name, GuiData.font, Color.White, doNotOversize: true, offsetToTopLeft: true);
		int num = dest.Width / 4;
		int num2 = 22;
		if (Button.doButton(37849102, rectangle.X + rectangle.Width - 6 - num, rectangle.Y + rectangle.Height - rectangle.Height / 2 - num2 / 2, num, num2, LocaleTerms.Loc("Exit IRC View"), ThemeColor))
		{
			os.display.command = "connect";
		}
		rectangle.Y += rectangle.Height;
		rectangle.X -= 6;
		dest.Y += rectangle.Height;
		dest.Height -= rectangle.Height;
		rectangle.Height = 2;
		sb.Draw(Utils.white, rectangle, ThemeColor);
		dest.Y += rectangle.Height + 2;
		dest.Height -= rectangle.Height + 2;
		dest.Height -= 6;
		PatternDrawer.draw(dest, 0.22f, Color.Black * 0.5f, flag ? (ThemeColor * 0.12f) : (Utils.AddativeRed * 0.2f), sb, flag ? PatternDrawer.thinStripe : PatternDrawer.warningStripe);
		dest.X += 2;
		dest.Width -= 4;
		dest.Height -= 4;
		if (flag)
		{
			System.Draw(dest, sb, CanWrite: false, LocaleTerms.Loc("UNKNOWN"), UserColors);
			return;
		}
		int num3 = dest.Height / 4;
		Rectangle rectangle2 = new Rectangle(dest.X - 4, dest.Y + dest.Height / 2 - num3 / 2, dest.Width + 6, num3);
		sb.Draw(Utils.white, rectangle2, os.lockedColor);
		rectangle2.Height -= 35;
		TextItem.doCenteredFontLabel(rectangle2, LocaleTerms.Loc("Login To Server"), GuiData.font, Color.White);
		if (Button.doButton(84109551, rectangle2.X + rectangle2.Width / 2 - rectangle2.Width / 4, rectangle2.Y + rectangle2.Height - 32, rectangle2.Width / 2, 28, "Login", null))
		{
			os.runCommand("login");
		}
	}

	public override string getSaveString()
	{
		return "<IRCDaemon />";
	}
}
