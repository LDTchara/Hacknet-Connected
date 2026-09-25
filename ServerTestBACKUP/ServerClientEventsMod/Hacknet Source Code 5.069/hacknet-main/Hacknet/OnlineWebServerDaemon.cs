using System;
using System.Net;

namespace Hacknet;

internal class OnlineWebServerDaemon : WebServerDaemon
{
	private static string DEFAULT_PAGE_URL = "http://www.google.com";

	public string webURL = DEFAULT_PAGE_URL;

	private string lastRequestedURL = null;

	public OnlineWebServerDaemon(Computer computer, string serviceName, OS opSystem)
		: base(computer, serviceName, opSystem)
	{
	}

	public void setURL(string url)
	{
		webURL = url;
	}

	public override void LoadWebPage(string url = null)
	{
		if (url == null || url == "index.html")
		{
			url = webURL;
		}
		WebClient webClient = new WebClient();
		webClient.DownloadStringAsync(new Uri(url));
		webClient.DownloadStringCompleted += web_DownloadStringCompleted;
		lastRequestedURL = url;
	}

	private void web_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
	{
		if (!e.Cancelled && e.Error == null)
		{
			instantiateWebPage(e.Result);
			return;
		}
		string body = FileSanitiser.purifyStringForDisplay(Utils.readEntireFile("Content/Web/404Page.html"));
		instantiateWebPage(body);
	}

	private void instantiateWebPage(string body)
	{
		string data = FileSanitiser.purifyStringForDisplay(body);
		string text = lastRequestedURL;
		FileEntry fileEntry = root.searchForFile(text);
		if (fileEntry == null)
		{
			fileEntry = root.searchForFile("index.html");
			text = "index.html";
		}
		if (fileEntry != null)
		{
			fileEntry.data = data;
		}
		base.LoadWebPage(text);
	}

	public override string getSaveString()
	{
		return "<OnlineWebServer name=\"" + name + "\" url=\"" + webURL + "\" />";
	}
}
