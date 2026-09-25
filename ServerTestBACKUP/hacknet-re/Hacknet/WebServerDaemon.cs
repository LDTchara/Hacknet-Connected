using System;
using System.IO;
using Hacknet.Extensions;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class WebServerDaemon : Daemon
{
	public const string ROOT_FOLDERNAME = "web";

	public const string DEFAULT_PAGE_FILE = "index.html";

	public const string TEMP_WEBPAGE_CACHE_FILENAME = "/Content/Web/Cache/HN_OS_WebCache.html";

	private const string DEFAULT_PAGE_DATA_LOCATION = "Content/Web/BaseImageWebPage.html";

	private const string COMPANY_NAME_SENTINAL = "#$#COMPANYNAME#$#";

	private const string COMPANY_NAME_COMPACT_SENTINAL = "#$#LC_COMPANYNAME#$#";

	public const int BASE_BAR_HEIGHT = 16;

	private static string BaseWebpageData;

	private static string BaseComnayPageData;

	private string webPageFileLocation;

	public Folder root;

	public FileEntry lastLoadedFile;

	private bool shouldShow404 = false;

	private string saveURL = "";

	public WebServerDaemon(Computer computer, string serviceName, OS opSystem, string pageFileLocation = "Content/Web/BaseImageWebPage.html")
		: base(computer, serviceName, opSystem)
	{
		if (pageFileLocation != null)
		{
			webPageFileLocation = pageFileLocation;
			if (webPageFileLocation != "Content/Web/BaseImageWebPage.html")
			{
				BaseWebpageData = null;
			}
		}
	}

	public override void initFiles()
	{
		root = comp.files.root.searchForFolder("web");
		if (root == null)
		{
			root = new Folder("web");
			comp.files.root.folders.Add(root);
		}
		if (BaseWebpageData == null)
		{
			string text = null;
			if (Settings.IsInExtensionMode)
			{
				webPageFileLocation = Utils.GetFileLoadPrefix() + webPageFileLocation;
			}
			text = Utils.readEntireFile(webPageFileLocation);
			BaseWebpageData = ((Settings.ActiveLocale == "en-us") ? FileSanitiser.purifyStringForDisplay(text) : text);
		}
		FileEntry item = new FileEntry(BaseWebpageData, "index.html");
		root.files.Add(item);
		lastLoadedFile = item;
	}

	public override void loadInit()
	{
		root = comp.files.root.searchForFolder("web");
	}

	public void generateBaseCorporateSite(string companyName, string targetBaseFile = "Content/Web/BaseCorporatePage.html")
	{
		if (BaseComnayPageData == null || targetBaseFile != "Content/Web/BaseCorporatePage.html")
		{
			BaseComnayPageData = FileSanitiser.purifyStringForDisplay(Utils.readEntireFile(targetBaseFile));
		}
		string text = BaseComnayPageData.Replace("#$#COMPANYNAME#$#", companyName).Replace("#$#LC_COMPANYNAME#$#", companyName.Replace(' ', '_'));
		FileEntry fileEntry = root.searchForFile("index.html");
		if (fileEntry == null)
		{
			fileEntry = new FileEntry(text, "index.html");
		}
		else
		{
			fileEntry.data = text;
		}
		FileEntry item = new FileEntry(fileEntry.data, "index_BACKUP.html");
		Folder folder = comp.files.root.searchForFolder("home");
		folder.files.Add(item);
	}

	public virtual void LoadWebPage(string url = "index.html")
	{
		saveURL = url;
		FileEntry fileEntry = root.searchForFile(url);
		if (fileEntry != null)
		{
			shouldShow404 = false;
			ShowPage(fileEntry.data);
		}
		else
		{
			shouldShow404 = true;
		}
		lastLoadedFile = fileEntry;
	}

	public virtual void ShowPage(string pageData)
	{
		string text = Directory.GetCurrentDirectory() + "/Content/Web/Cache/HN_OS_WebCache.html";
		if (Settings.IsInExtensionMode)
		{
			string text2 = Path.Combine(Directory.GetCurrentDirectory(), ExtensionLoader.ActiveExtensionInfo.FolderPath, "Web", "Cache");
			if (!Directory.Exists(text2))
			{
				Directory.CreateDirectory(text2);
			}
			text = text2 + "/HN_OS_WebCache.html";
		}
		Utils.writeToFile(pageData, text);
		WebRenderer.navigateTo(text);
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		Rectangle webRect = bounds;
		webRect.Height -= 16;
		webRect.X++;
		webRect.Width -= 2;
		if (shouldShow404)
		{
			Vector2 vector = new Vector2(webRect.X + 10, webRect.Y + webRect.Height / 4);
			sb.Draw(Utils.white, new Rectangle(webRect.X, (int)vector.Y, (int)((double)webRect.Width * 0.7), 80), os.highlightColor * 0.3f);
			TextItem.doFontLabel(vector + new Vector2(0f, 10f), LocaleTerms.Loc("Error 404"), GuiData.font, Color.White);
			TextItem.doFontLabel(vector + new Vector2(0f, 42f), LocaleTerms.Loc("Page not found"), GuiData.smallfont, Color.White);
		}
		else
		{
			OS oS = os;
			oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
			{
				WebRenderer.drawTo(webRect, sb);
			});
		}
		Rectangle rectangle = bounds;
		rectangle.Y += webRect.Height;
		rectangle.Height = 16;
		bool smallButtonDraw = Button.smallButtonDraw;
		Button.smallButtonDraw = true;
		int num = 200;
		if (Button.doButton(83801, rectangle.X + 1, rectangle.Y + 1, num, rectangle.Height - 2, "HN: " + LocaleTerms.Loc("Exit Web View"), null))
		{
			os.display.command = "connect";
		}
		if (os.hasConnectionPermission(admin: false) && Button.doButton(83805, rectangle.X + rectangle.Width - (num + 1), rectangle.Y + 1, num, rectangle.Height - 2, LocaleTerms.Loc("View Source"), null))
		{
			showSourcePressed();
		}
		Button.smallButtonDraw = smallButtonDraw;
	}

	public virtual void showSourcePressed()
	{
		string text = "";
		int count = Programs.getNavigationPathAtPath("", os, root).Count;
		for (int i = 0; i < count; i++)
		{
			text += "../";
		}
		text += "web";
		os.runCommand("cd " + text);
		if (lastLoadedFile != null)
		{
			os.delayer.Post(ActionDelayer.Wait(0.1), delegate
			{
				os.runCommand("cat " + lastLoadedFile.name);
			});
		}
	}

	public override void navigatedTo()
	{
		LoadWebPage();
	}

	public override string getSaveString()
	{
		return "<WebServer name=\"" + name + "\" url=\"" + saveURL + "\" />";
	}
}
