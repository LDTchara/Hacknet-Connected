using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class WhitelistConnectionDaemon : Daemon
{
	private const string SystemFilename = "authenticator.dll";

	private const string ListFilename = "list.txt";

	private const string SourceFilename = "source.txt";

	private Folder folder;

	public string RemoteSourceIP = null;

	public bool AuthenticatesItself = true;

	private bool HasAllowedSingleTimeAdminPassthroughThisSession = false;

	public WhitelistConnectionDaemon(Computer c, OS os)
		: base(c, "Whitelist Authenticator", os)
	{
	}

	public override void initFiles()
	{
		folder = comp.files.root.searchForFolder("Whitelist");
		if (folder == null)
		{
			folder = new Folder("Whitelist");
			comp.files.root.folders.Add(folder);
		}
		if (!folder.containsFile("authenticator.dll"))
		{
			folder.files.Add(new FileEntry(Computer.generateBinaryString(500), "authenticator.dll"));
		}
		if (RemoteSourceIP == null)
		{
			if (!folder.containsFile("list.txt"))
			{
				folder.files.Add(new FileEntry(comp.adminIP, "list.txt"));
			}
			return;
		}
		ComputerLoader.postAllLoadedActions = (Action)Delegate.Combine(ComputerLoader.postAllLoadedActions, (Action)delegate
		{
			Computer computer = Programs.getComputer(os, RemoteSourceIP);
			if (computer != null)
			{
				RemoteSourceIP = computer.ip;
			}
			if (!folder.containsFile("source.txt"))
			{
				folder.files.Add(new FileEntry(RemoteSourceIP, "source.txt"));
			}
		});
	}

	public override void loadInit()
	{
		base.loadInit();
		folder = comp.files.root.searchForFolder("Whitelist");
		if (folder.containsFile("source.txt"))
		{
			RemoteSourceIP = folder.searchForFile("source.txt").data.Trim();
		}
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		if (!IPCanPassWhitelist(os.thisComputer.ip, isFromRemote: false))
		{
			DisconnectTarget();
		}
	}

	public void DisconnectTarget()
	{
		os.execute("disconnect");
		os.display.command = "connectiondenied";
		os.delayer.Post(ActionDelayer.NextTick(), delegate
		{
			os.display.command = "connectiondenied";
		});
		os.write(" ");
		os.write(" ");
		os.write("------------------------------");
		os.write("------------------------------");
		os.write(" ");
		os.write("---  " + LocaleTerms.Loc("CONNECTION ERROR") + "  ---");
		os.write(" ");
		os.write(LocaleTerms.Loc("Message from Server:"));
		os.write(string.Format(LocaleTerms.Loc("Whitelist Authenticator denied connection from IP {0}"), os.thisComputer.ip));
		os.write(" ");
		os.write("------------------------------");
		os.write("------------------------------");
		os.write(" ");
	}

	private bool RemoteCompCanBeAccessed()
	{
		if (RemoteSourceIP == null)
		{
			return false;
		}
		Computer computer = Programs.getComputer(os, RemoteSourceIP);
		if (computer != null && computer.bootTimer <= 0f)
		{
			WhitelistConnectionDaemon whitelistConnectionDaemon = (WhitelistConnectionDaemon)computer.getDaemon(typeof(WhitelistConnectionDaemon));
			if (whitelistConnectionDaemon == null)
			{
				return true;
			}
			if (whitelistConnectionDaemon.comp.files.root.searchForFolder("Whitelist").searchForFile("authenticator.dll") == null)
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public bool IPCanPassWhitelist(string ip, bool isFromRemote)
	{
		if (!AuthenticatesItself && !isFromRemote)
		{
			return true;
		}
		if (RemoteSourceIP != null)
		{
			Computer computer = Programs.getComputer(os, RemoteSourceIP);
			if (!RemoteCompCanBeAccessed())
			{
				return true;
			}
			if (folder.searchForFile("authenticator.dll") == null)
			{
				return true;
			}
			return ((WhitelistConnectionDaemon)computer.getDaemon(typeof(WhitelistConnectionDaemon)))?.IPCanPassWhitelist(ip, isFromRemote: true) ?? true;
		}
		if (folder.searchForFile("authenticator.dll") == null)
		{
			return true;
		}
		FileEntry fileEntry = folder.searchForFile("list.txt");
		if (fileEntry == null)
		{
			return true;
		}
		string[] array = fileEntry.data.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			if (os.thisComputer.ip == array[i].Trim())
			{
				return true;
			}
		}
		return false;
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		if (comp.adminIP == os.thisComputer.ip && !HasAllowedSingleTimeAdminPassthroughThisSession)
		{
			os.display.command = "connect";
			HasAllowedSingleTimeAdminPassthroughThisSession = true;
		}
		Rectangle dest = Utils.InsetRectangle(bounds, 2);
		bool flag = RemoteCompCanBeAccessed();
		bool flag2 = IPCanPassWhitelist(os.thisComputer.ip, isFromRemote: false) && flag;
		bool flag3 = folder.searchForFile("authenticator.dll") != null;
		bool flag4 = RemoteSourceIP != null || folder.searchForFile("list.txt") != null;
		bool flag5 = flag2 && flag4 && flag3;
		Color color = ((!AuthenticatesItself) ? os.highlightColor : (flag5 ? os.unlockedColor : os.brightLockedColor));
		PatternDrawer.draw(dest, 1f, Color.Black * 0.1f, color * 0.4f, sb, flag5 ? PatternDrawer.thinStripe : PatternDrawer.errorTile);
		Rectangle dest2 = new Rectangle(dest.X + 10, dest.Y + dest.Height / 4, dest.Width - 20, 40);
		if (Button.doButton(8711133, dest2.X, dest2.Y - 30, dest.Width / 4, 24, LocaleTerms.Loc("Proceed"), os.highlightColor))
		{
			os.display.command = "connect";
		}
		string text = string.Format(LocaleTerms.Loc("Whitelist Authentication Successful : {0}"), os.thisComputer.ip);
		if (!AuthenticatesItself)
		{
			text = LocaleTerms.Loc("Whitelist Server Active for remote nodes...");
		}
		else if (!flag)
		{
			text = string.Format(LocaleTerms.Loc("Whitelist Authenticator Critical Error:") + "\n" + LocaleTerms.Loc("Source Whitelist server at {0} cannot be accessed"), RemoteSourceIP);
		}
		else if (!flag3)
		{
			text = string.Format(LocaleTerms.Loc("Whitelist Authenticator Critical Error:") + "\n" + LocaleTerms.Loc("System File {0} not found"), "authenticator.dll");
		}
		else if (!flag4)
		{
			text = string.Format(LocaleTerms.Loc("Whitelist Authenticator Critical Error:") + "\n" + LocaleTerms.Loc("Whitelist File {0} not found"), "list.txt");
		}
		sb.Draw(Utils.white, new Rectangle(bounds.X + 1, dest2.Y - 3, bounds.Width - 2, dest2.Height + 6), Color.Black * 0.7f);
		TextItem.doFontLabelToSize(dest2, text, GuiData.font, color, doNotOversize: true, offsetToTopLeft: true);
		Rectangle dest3 = new Rectangle(dest2.X, dest2.Y + dest2.Height + 6, dest2.Width, dest.Height / 2);
		string text2 = LocaleTerms.Loc("Connection established");
		if (!AuthenticatesItself)
		{
			text2 = LocaleTerms.Loc("Processing connections as authentication server.");
		}
		else if (!flag)
		{
			text2 = string.Format(LocaleTerms.Loc("Could not establish connection to whitelist server {0}.") + "\n" + LocaleTerms.Loc("Aborting Execution."), RemoteSourceIP);
		}
		else if (!flag3)
		{
			text2 = LocaleTerms.Loc("Unhanded FileNotFoundException") + "\n" + string.Format(LocaleTerms.Loc("File /Whitelist/{0} Could not be read."), "authenticator.dll") + "\n" + LocaleTerms.Loc("Aborting Execution");
		}
		else if (!flag4)
		{
			text2 = LocaleTerms.Loc("Unhanded FileNotFoundException") + "\n" + string.Format(LocaleTerms.Loc("File /Whitelist/{0} Could not be read."), "list.txt") + "\n" + LocaleTerms.Loc("Aborting Execution");
		}
		TextItem.doFontLabelToSize(dest3, text2, GuiData.smallfont, Color.White * 0.8f, doNotOversize: true, offsetToTopLeft: true);
	}

	public override string getSaveString()
	{
		return "<WhitelistAuthenticatorDaemon SelfAuthenticating=\"" + AuthenticatesItself + "\"/>";
	}
}
