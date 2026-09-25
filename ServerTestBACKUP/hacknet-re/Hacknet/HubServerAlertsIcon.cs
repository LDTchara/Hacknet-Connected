using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public class HubServerAlertsIcon
{
	public bool IsEnabled = true;

	private Texture2D icon;

	private Texture2D circle;

	private SoundEffect alertSound;

	private string syncServerName;

	private IMonitorableDaemon HubServer;

	private Computer HubServerComp;

	private string[] TagsToAlertFor;

	private OS os;

	private bool PendingAlerts = false;

	private List<float> RadiusCircles = new List<float>();

	public HubServerAlertsIcon(ContentManager content, string serverToSyncTo, string[] tagsToAlertFor)
	{
		if (DLC1SessionUpgrader.HasDLC1Installed)
		{
			icon = content.Load<Texture2D>("DLC/Icons/ChatWideIcon");
			alertSound = content.Load<SoundEffect>("DLC/SFX/NotificationSound");
		}
		else
		{
			icon = content.Load<Texture2D>("Sprites/Misc/ChatWideIcon");
			alertSound = content.Load<SoundEffect>("SFX/EmailSound");
		}
		circle = content.Load<Texture2D>("CircleOutlineLarge");
		syncServerName = serverToSyncTo;
		TagsToAlertFor = tagsToAlertFor;
	}

	public void Init(object OSobj)
	{
		os = (OS)OSobj;
		HubServerComp = Programs.getComputer(os, syncServerName);
		if (HubServerComp != null || !Settings.IsInExtensionMode)
		{
			HubServer = HubServerComp.getDaemon(typeof(DLCHubServer)) as DLCHubServer;
			if (HubServer == null)
			{
				throw new NullReferenceException("DLCHubServer not found on AlertsIcon sync destination computer");
			}
			HubServer.SubscribeToAlertActionFroNewMessage(ProcessNewLog);
		}
	}

	public void UpdateTarget(object monitor, object comp)
	{
		if (HubServer != null)
		{
			HubServer.UnSubscribeToAlertActionFroNewMessage(ProcessNewLog);
		}
		HubServer = (IMonitorableDaemon)monitor;
		HubServerComp = (Computer)comp;
		HubServer.SubscribeToAlertActionFroNewMessage(ProcessNewLog);
	}

	private void ProcessNewLog(string Author, string Message)
	{
		for (int i = 0; i < TagsToAlertFor.Length; i++)
		{
			if (Message.Contains(TagsToAlertFor[i]))
			{
				if (HubServer.ShouldDisplayNotifications())
				{
					SendAlert();
				}
				break;
			}
		}
	}

	private void SendAlert()
	{
		if (!Settings.soundDisabled)
		{
			alertSound.Play();
		}
		RadiusCircles.Add(0f);
		if (os.connectedComp != HubServerComp)
		{
			PendingAlerts = true;
		}
	}

	public void Update(float dt)
	{
		for (int i = 0; i < RadiusCircles.Count; i++)
		{
			RadiusCircles[i] += dt * 0.2f;
			if (RadiusCircles[i] > 1f)
			{
				RadiusCircles.RemoveAt(i);
				i--;
			}
		}
	}

	private void ConnectToServer()
	{
		if (!os.terminal.preventingExecution)
		{
			if (!os.netMap.visibleNodes.Contains(os.netMap.nodes.IndexOf(HubServerComp)))
			{
				os.netMap.discoverNode(HubServerComp);
			}
			os.connectedComp = HubServerComp;
			HubServerComp.userLoggedIn = true;
			HubServer.navigatedTo();
			os.display.command = HubServer.GetName();
			PendingAlerts = false;
		}
	}

	public void Draw(Rectangle dest, SpriteBatch sb)
	{
		if (Button.doButton(4568702, dest.X, dest.Y, dest.Width, dest.Height, "", os.topBarIconsColor, icon) && IsEnabled)
		{
			ConnectToServer();
		}
		if (RadiusCircles.Count > 0)
		{
			for (int i = 0; i < RadiusCircles.Count; i++)
			{
				float num = 320f - RadiusCircles[i] * 240f;
				float renderSize = num * Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(RadiusCircles[i]));
				int activeIndex = i;
				OS oS = os;
				oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
				{
					sb.Draw(circle, Utils.InsetRectangle(new Rectangle(dest.X + dest.Width / 2, dest.Y + dest.Height / 2, 1, 1), (int)renderSize), os.highlightColor * (1f - RadiusCircles[activeIndex]));
				});
			}
		}
		else if (PendingAlerts && os.timer % 2f < 1f / 31f)
		{
			SFX.addCircle(new Vector2(dest.X, dest.Y) + new Vector2(dest.Width / 2, dest.Height / 2), os.highlightColor, 40f);
		}
	}
}
