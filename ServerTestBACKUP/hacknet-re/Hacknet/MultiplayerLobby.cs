using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Hacknet.Gui;
using Hacknet.Magic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MultiplayerLobby : GameScreen
{
	private Rectangle fullscreen;

	private string destination = "192.168.1.1";

	private string myIP = "?";

	private string externalIP = "Loading...";

	public static List<string> allLocalIPs = new List<string>();

	private TcpListener listener;

	private Thread listenerThread;

	private byte[] buffer;

	private ASCIIEncoding encoder;

	private bool shouldAddServer = false;

	private TcpClient client;

	private NetworkStream clientStream;

	private bool isConnecting = false;

	private List<string> messages;

	private string chatIP = "";

	private string messageString = "Test Message";

	private TcpClient chatclient;

	private NetworkStream chatclientStream;

	private Color darkgrey = new Color(9, 9, 9);

	private Color dark_ish_gray = new Color(20, 20, 20);

	private bool connectingToServer = false;

	public MultiplayerLobby()
	{
		base.TransitionOnTime = TimeSpan.FromSeconds(0.1);
		base.TransitionOffTime = TimeSpan.FromSeconds(0.3);
	}

	public override void LoadContent()
	{
		base.LoadContent();
		try
		{
			listener = new TcpListener(IPAddress.Any, Multiplayer.PORT);
			listener.Start();
			listener.BeginAcceptTcpClient(DoAcceptTcpClientCallback, listener);
			buffer = new byte[4096];
			encoder = new ASCIIEncoding();
			if (allLocalIPs.Count == 0)
			{
				myIP = getLocalIP();
				Thread thread = new Thread(getExternalIP);
				thread.Start();
				Console.WriteLine("Started Multiplayer IP Getter Thread");
			}
			else
			{
				myIP = allLocalIPs[0];
			}
			messages = new List<string>();
			Viewport viewport = base.ScreenManager.GraphicsDevice.Viewport;
			fullscreen = new Rectangle(0, 0, viewport.Width, viewport.Height);
		}
		catch (Exception)
		{
			base.ScreenManager.RemoveScreen(this);
			base.ScreenManager.AddScreen(new MainMenu(), base.ScreenManager.controllingPlayer);
		}
	}

	public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
	{
		base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
		if (shouldAddServer && base.ScreenState != ScreenState.TransitionOff)
		{
			ExitScreen();
			base.ScreenManager.AddScreen(new OS(client, clientStream, actingServer: true, base.ScreenManager), base.ScreenManager.controllingPlayer);
			if (chatclient != null)
			{
				chatclient.Close();
			}
		}
	}

	public override void HandleInput(InputState input)
	{
		base.HandleInput(input);
		GuiData.doInput(input);
	}

	public override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);
		GuiData.startDraw();
		base.ScreenManager.SpriteBatch.Draw(Utils.white, fullscreen, isConnecting ? Color.Gray : Color.Black);
		doGui();
		GuiData.endDraw();
		base.ScreenManager.FadeBackBufferToBlack(255 - base.TransitionAlpha);
	}

	public void drawConnectingGui()
	{
		PatternDrawer.draw(new Rectangle(100, 200, 600, 250), 1.4f, Color.DarkGreen * 0.3f, Color.DarkGreen, GuiData.spriteBatch);
		TextItem.doLabel(new Vector2(110f, 210f), "Connecting...", null);
	}

	public void doGui()
	{
		PatternDrawer.draw(new Rectangle(180, 160, 500, 85), 1f, darkgrey, dark_ish_gray, GuiData.spriteBatch);
		TextItem.doSmallLabel(new Vector2(200f, 170f), "IP To Connect to:", null);
		destination = TextBox.doTextBox(100, 200, 200, 300, 1, destination, GuiData.smallfont);
		if (Button.doButton(123, 510, 201, 120, 23, "Connect", null) || TextBox.BoxWasActivated)
		{
			ConnectToServer(destination);
			return;
		}
		if (isConnecting)
		{
			drawConnectingGui();
		}
		TextItem.doLabel(new Vector2(200f, 300f), "Local IPs: " + myIP, null);
		float num = 340f;
		for (int i = 0; i < allLocalIPs.Count - 1; i++)
		{
			TextItem.doLabel(new Vector2(351f, num), allLocalIPs[i], null);
			num += 40f;
		}
		num += 40f;
		TextItem.doLabel(new Vector2(200f, num), "Extrn IP: " + externalIP, null);
		Vector2 pos = new Vector2(610f, 280f);
		TextItem.doLabel(pos, "Info:", null);
		pos.Y += 40f;
		string s = "To Begin a multiplayer session, type in the IP of the computer you want to connect to and press enter or connect. Both players must be on this screen. To connect over the internet, use the extern IP address and ensure port 3030 is open.";
		s = DisplayModule.cleanSplitForWidth(s, 400);
		TextItem.doFontLabel(pos, s, GuiData.tinyfont, Color.DarkGray);
		if (Button.doButton(999, 10, 10, 200, 30, "<- Back to Menu", Color.Gray))
		{
			ExitScreen();
			base.ScreenManager.AddScreen(new MainMenu(), base.ScreenManager.controllingPlayer);
		}
	}

	public void ConnectToServer(string ip)
	{
		connectingToServer = true;
		try
		{
			TcpClient tcpClient = new TcpClient();
			IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ip), Multiplayer.PORT);
			tcpClient.Connect(remoteEP);
			NetworkStream stream = tcpClient.GetStream();
			buffer = encoder.GetBytes("connect Client Connecting");
			stream.Write(buffer, 0, buffer.Length);
			stream.Flush();
			stream.Read(buffer, 0, buffer.Length);
			ExitScreen();
			base.ScreenManager.AddScreen(new OS(tcpClient, stream, actingServer: false, base.ScreenManager), base.ScreenManager.controllingPlayer);
		}
		catch (Exception ex)
		{
			DebugLog.add(ex.ToString());
			isConnecting = false;
		}
	}

	public void chatToServer(string ip, string msg)
	{
		try
		{
			if (chatclientStream == null)
			{
				chatclient = new TcpClient();
				IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ip), Multiplayer.PORT);
				chatclient.Connect(remoteEP);
				chatclientStream = chatclient.GetStream();
			}
			buffer = encoder.GetBytes("Chat " + msg);
			messages.Add("Me: " + msg);
			chatclientStream.Write(buffer, 0, buffer.Length);
			chatclientStream.Flush();
			messageString = "";
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
		}
	}

	public void DoAcceptTcpClientCallback(IAsyncResult ar)
	{
		TcpListener tcpListener = (TcpListener)ar.AsyncState;
		try
		{
			client = tcpListener.EndAcceptTcpClient(ar);
			clientStream = client.GetStream();
			bool flag = true;
			while (flag)
			{
				int num = 0;
				try
				{
					num = clientStream.Read(buffer, 0, 4096);
				}
				catch (Exception value)
				{
					Console.WriteLine(value);
					if (Game1.threadsExiting)
					{
						break;
					}
				}
				if (num == 0)
				{
					client.Close();
					if (Game1.threadsExiting)
					{
						break;
					}
				}
				string @string = encoder.GetString(buffer);
				char[] separator = new char[1] { ' ' };
				string[] array = @string.Split(separator);
				if (array[0].Equals("Chat"))
				{
					char[] trimChars = new char[3] { ' ', '\n', '\0' };
					messages.Add(@string.Substring(4).Trim(trimChars).Replace("\0", ""));
					if (Game1.threadsExiting)
					{
						break;
					}
					continue;
				}
				buffer = encoder.GetBytes("Replying - Hello World");
				clientStream.Write(buffer, 0, buffer.Length);
				clientStream.Flush();
				shouldAddServer = true;
				flag = false;
				break;
			}
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
		}
	}

	private void listenForConnections()
	{
		try
		{
			listener = new TcpListener(IPAddress.Any, Multiplayer.PORT);
			listener.Start();
			client = listener.AcceptTcpClient();
			clientStream = client.GetStream();
			bool flag = true;
			while (flag)
			{
				int num = 0;
				try
				{
					num = clientStream.Read(buffer, 0, 4096);
				}
				catch (Exception value)
				{
					Console.WriteLine(value);
					if (Game1.threadsExiting)
					{
						break;
					}
				}
				if (num == 0)
				{
					client.Close();
					if (Game1.threadsExiting)
					{
						break;
					}
				}
				string @string = encoder.GetString(buffer);
				char[] separator = new char[1] { ' ' };
				string[] array = @string.Split(separator);
				if (array[0].Equals("Chat"))
				{
					char[] trimChars = new char[3] { ' ', '\n', '\0' };
					messages.Add(@string.Substring(4).Trim(trimChars).Replace("\0", ""));
					if (Game1.threadsExiting)
					{
						break;
					}
					continue;
				}
				buffer = encoder.GetBytes("Replying - Hello World");
				clientStream.Write(buffer, 0, buffer.Length);
				clientStream.Flush();
				shouldAddServer = true;
				flag = false;
				break;
			}
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
		}
	}

	public static string getLocalIP()
	{
		string result = "?";
		IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
		IPAddress[] addressList = hostEntry.AddressList;
		foreach (IPAddress iPAddress in addressList)
		{
			if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
			{
				result = iPAddress.ToString();
				allLocalIPs.Add(iPAddress.ToString());
			}
		}
		return result;
	}

	public void getExternalIPwithPF()
	{
		try
		{
			externalIP = "Attempting automated port-fowarding...";
			try
			{
				if (NAT.Discover())
				{
					Console.WriteLine("Attempting port foward");
					NAT.ForwardPort(Multiplayer.PORT, ProtocolType.Tcp, "Hacknet (TCP)");
					externalIP = NAT.GetExternalIP().ToString();
				}
				else
				{
					base.ScreenManager.ShowPopup("You dont have UPNP enabled - Internet play will not work");
				}
			}
			catch (Exception value)
			{
				Console.WriteLine(value);
			}
			if (externalIP.Equals("Attempting automated port-fowarding..."))
			{
				externalIP = "Automated port-fowarding Failed - Internet Play Disabled";
			}
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
			externalIP = "Automated port-fowarding Failed - Internet Play Disabled";
		}
	}

	public void getExternalIP()
	{
		try
		{
			externalIP = "Unknown...";
			WebClient webClient = new WebClient();
			externalIP = webClient.DownloadString("http://icanhazip.com/");
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
			externalIP = "Could not Find Conection";
		}
	}
}
