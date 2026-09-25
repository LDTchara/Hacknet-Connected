using System;
using System.Collections.Generic;
using Hacknet.ExternalCounterparts;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class ServerScreen : GameScreen
{
	private string mainIP;

	private List<string> messages;

	private Color backgroundColor = new Color(6, 6, 6);

	private ExternalNetworkedServer server;

	private bool canCloseServer;

	public bool drawingWithEffects = false;

	public ServerScreen()
	{
		mainIP = MultiplayerLobby.getLocalIP();
		messages = new List<string>();
		server = new ExternalNetworkedServer();
		server.initializeListener();
		ExternalNetworkedServer externalNetworkedServer = server;
		externalNetworkedServer.messageReceived = (Action<string>)Delegate.Combine(externalNetworkedServer.messageReceived, new Action<string>(parseMessage));
	}

	public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
	{
		base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
		canCloseServer = !otherScreenHasFocus && !coveredByOtherScreen;
	}

	public override void HandleInput(InputState input)
	{
		base.HandleInput(input);
		GuiData.doInput(input);
	}

	public void parseMessage(string msg)
	{
		addDisplayMessage(msg);
		char[] separator = new char[2] { ' ', '\n' };
		string[] array = msg.Split(separator);
		if (array[0].Equals("cCDDrive"))
		{
			if (array[2].Equals("open"))
			{
				Programs.cdDrive(open: true);
			}
			else
			{
				Programs.cdDrive(open: false);
			}
		}
	}

	public void addDisplayMessage(string msg)
	{
		messages.Add(msg);
	}

	public override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);
		if (drawingWithEffects)
		{
			PostProcessor.begin();
		}
		GuiData.startDraw();
		Viewport viewport = base.ScreenManager.GraphicsDevice.Viewport;
		GuiData.spriteBatch.Draw(Utils.white, new Rectangle(0, 0, viewport.Width, viewport.Height), backgroundColor);
		int num = 80;
		int num2 = 80;
		TextItem.doFontLabel(new Vector2(num, num2), "HACKNET RELAY SERVER", GuiData.titlefont, null, 500f, 50f);
		num2 += 55;
		if (canCloseServer && Button.doButton(800, num, num2, 160, 30, "Shut Down Server", null))
		{
			server.closeServer();
			ExitScreen();
		}
		num2 += 35;
		for (int i = 0; i < MultiplayerLobby.allLocalIPs.Count; i++)
		{
			TextItem.doFontLabel(new Vector2(num, num2), "IP: " + MultiplayerLobby.allLocalIPs[i], GuiData.smallfont, null);
			num2 += 20;
		}
		num2 += 30;
		drawMessageLog(num, num2);
		GuiData.endDraw();
		if (drawingWithEffects)
		{
			PostProcessor.end();
		}
	}

	public void drawMessageLog(int x, int y)
	{
		float num = 1f;
		Vector2 pos = new Vector2(x, y);
		for (int i = 0; i < 20 && i < messages.Count; i++)
		{
			if (i > 10)
			{
				num = 1f - (float)(i - 10) / 10f;
			}
			TextItem.doTinyLabel(pos, messages[messages.Count - 1 - i], Color.White * num);
			pos.Y += 13f;
		}
	}
}
