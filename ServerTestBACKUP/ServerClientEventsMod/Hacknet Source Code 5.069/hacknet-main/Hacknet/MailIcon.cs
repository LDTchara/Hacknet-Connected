using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MailIcon : MailResponder
{
	private static float ALERT_TIME = 2.4f;

	private static float SCALE = 1f;

	public bool isEnabled = true;

	private Texture2D tex;

	private Texture2D texBig;

	private float bigTexScaleMod = 1f;

	private float firstEverAlertMod = 2.7f;

	public Vector2 pos;

	private SoundEffect newMailSound;

	private float alertTimer;

	private bool mailUnchecked;

	private static Color uncheckedMailPulseColor = new Color(110, 110, 110, 0);

	private OS os;

	private MailServer targetServer = null;

	public MailIcon(OS operatingSystem, Vector2 position)
	{
		os = operatingSystem;
		pos = position;
		alertTimer = 0f;
		tex = os.content.Load<Texture2D>("UnopenedMail");
		texBig = os.content.Load<Texture2D>("MailIconBig");
		bigTexScaleMod = (float)tex.Width / (float)texBig.Width;
		newMailSound = os.content.Load<SoundEffect>("SFX/EmailSound");
		if (!os.multiplayer)
		{
			MailServer mailServer = (MailServer)os.netMap.mailServer.daemons[0];
			mailServer.addResponder(this);
		}
		mailUnchecked = false;
	}

	public void UpdateTargetServer(MailServer server)
	{
		if (targetServer != null)
		{
			targetServer.removeResponder(this);
		}
		else
		{
			(os.netMap.mailServer.daemons[0] as MailServer).removeResponder(this);
		}
		targetServer = server;
		targetServer.addResponder(this);
	}

	public void Update(float t)
	{
		float num = alertTimer;
		if (!os.Flags.HasFlag("FirstAlertComplete"))
		{
			alertTimer -= t * 0.65f;
		}
		else
		{
			alertTimer -= t;
		}
		if (alertTimer <= 0f)
		{
			alertTimer = 0f;
		}
		if (num > 0f && alertTimer <= 0f)
		{
			float num2 = 280f;
			if (!os.Flags.HasFlag("FirstAlertComplete"))
			{
				os.Flags.AddFlag("FirstAlertComplete");
				num2 *= firstEverAlertMod;
			}
			SFX.addCircle(pos + new Vector2((float)tex.Width * SCALE / 2f, (float)tex.Height * SCALE / 2f), uncheckedMailPulseColor, num2);
		}
	}

	public void Draw()
	{
		if (Button.doButton(45687, (int)pos.X, (int)pos.Y, (int)((float)tex.Width * SCALE), (int)((float)tex.Height * SCALE), "", os.topBarIconsColor, tex) && isEnabled)
		{
			connectToMail();
		}
		float percent = alertTimer / ALERT_TIME;
		percent *= percent;
		percent *= percent;
		percent = (float)Math.Pow(percent, 1f - percent);
		Vector2 iconPositionOffset = default(Vector2);
		ref Vector2 reference = ref iconPositionOffset;
		reference = new Vector2(-100f, 20f);
		float scaleMod = bigTexScaleMod;
		if (!os.Flags.HasFlag("FirstAlertComplete"))
		{
			scaleMod += firstEverAlertMod * percent;
			iconPositionOffset *= firstEverAlertMod * percent;
		}
		if (alertTimer > 0f)
		{
			OS oS = os;
			oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
			{
				Vector2 vector = new Vector2((float)texBig.Width * SCALE / 2f, (float)texBig.Height * SCALE / 2f);
				GuiData.spriteBatch.Draw(texBig, pos + vector * scaleMod + iconPositionOffset * percent, null, Color.White * (1f - percent), 0f, vector, (Vector2.One + Vector2.One * percent * 25f) * scaleMod, SpriteEffects.None, 0.5f);
			});
		}
		else if (mailUnchecked && os.timer % 2f < 1f / 31f)
		{
			SFX.addCircle(pos + new Vector2((float)tex.Width * SCALE / 2f, (float)tex.Height * SCALE / 2f), uncheckedMailPulseColor, 40f);
		}
	}

	public int getWidth()
	{
		return (int)((float)tex.Width * SCALE);
	}

	public void connectToMail()
	{
		if (!os.terminal.preventingExecution)
		{
			if (!os.netMap.visibleNodes.Contains(os.netMap.nodes.IndexOf(os.netMap.mailServer)))
			{
				os.netMap.discoverNode(os.netMap.mailServer);
			}
			MailServer mailServer = (MailServer)os.netMap.mailServer.daemons[0];
			if (targetServer != null)
			{
				mailServer = targetServer;
			}
			os.connectedComp = mailServer.comp;
			os.terminal.prompt = os.connectedComp.ip + "@> ";
			mailServer.comp.userLoggedIn = true;
			os.display.command = mailServer.name;
			mailServer.viewInbox(os.defaultUser);
			mailUnchecked = false;
		}
	}

	public void mailSent(string mail, string userTo)
	{
	}

	public void mailReceived(string mail, string userTo)
	{
		if (userTo.Equals(os.defaultUser.name))
		{
			alertTimer = ALERT_TIME;
			if (!Settings.soundDisabled)
			{
				newMailSound.Play();
			}
			if (os.connectedComp == null || !os.connectedComp.Equals(os.netMap.mailServer))
			{
				mailUnchecked = true;
			}
		}
	}
}
