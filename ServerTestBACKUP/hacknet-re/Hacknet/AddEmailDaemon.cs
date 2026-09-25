using System;
using System.Linq;
using System.Net;
using System.Threading;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class AddEmailDaemon : Daemon
{
	private const string SEND_SCRIPT_URL = "http://www.tijital-games.com/hacknet/SendVictoryEmail.php?mail=[EMAIL]";

	private const int WAITING = 0;

	private const int ENTERING = 1;

	private const int CONFIRM = 2;

	private const int ERROR = 3;

	private static string lastSentEmail;

	private int state;

	private string email;

	public AddEmailDaemon(Computer computer, string serviceName, OS opSystem)
		: base(computer, serviceName, opSystem)
	{
		state = 0;
		email = "";
	}

	private void saveEmail()
	{
		try
		{
			Utils.appendToFile(email + "\r\n", "Emails.txt");
			lastSentEmail = email;
			SFX.addCircle(os.netMap.GetNodeDrawPos(comp) + new Vector2(os.netMap.bounds.X, os.netMap.bounds.Y) + new Vector2(NetworkMap.NODE_SIZE / 2), os.thisComputerNode, 100f);
			state = 0;
			os.display.command = "connect";
		}
		catch (Exception)
		{
			state = 3;
		}
	}

	public void sendEmail()
	{
		if (lastSentEmail == null || !lastSentEmail.Equals(email))
		{
			if (email != null && email.Contains('@'))
			{
				Thread thread = new Thread(makeWebRequest);
				thread.IsBackground = true;
				thread.Start();
				lastSentEmail = email;
				SFX.addCircle(comp.location + new Vector2(os.netMap.bounds.X, os.netMap.bounds.Y) + new Vector2(NetworkMap.NODE_SIZE / 2), os.thisComputerNode, 100f);
				state = 0;
				os.display.command = "connect";
			}
			else
			{
				state = 3;
			}
		}
	}

	public override string getSaveString()
	{
		return "<AddEmailServer name=\"" + name + "\"/>";
	}

	public void makeWebRequest()
	{
		try
		{
			string address = "http://www.tijital-games.com/hacknet/SendVictoryEmail.php?mail=[EMAIL]".Replace("[EMAIL]", email);
			WebClient webClient = new WebClient();
			string value = webClient.DownloadString(address);
			Console.WriteLine(value);
		}
		catch (Exception)
		{
		}
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		int num = bounds.X + 10;
		int num2 = bounds.Y + 10;
		TextItem.doFontLabel(new Vector2(num, num2), "Email Verification", GuiData.font, null, bounds.Width - 20, bounds.Height);
		num2 += 50;
		switch (state)
		{
		default:
			num2 += bounds.Height / 2 - 60;
			if (state == 3)
			{
				TextItem.doSmallLabel(new Vector2(num, num2 - 60), "Error - Invalid Email Address", null);
			}
			if (Button.doButton(10, num, num2, 300, 50, "Add Email", os.highlightColor))
			{
				state = 1;
				os.execute("getString Email");
			}
			if (Button.doButton(12, num, num2 + 55, 300, 20, LocaleTerms.Loc("Exit"), os.lockedColor))
			{
				os.display.command = "connect";
			}
			break;
		case 1:
		{
			num2 += (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), "Enter a secure email address :\nEnsure that you are the only one with access to it", null).Y + 10;
			string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
			string[] array = os.getStringCache.Split(separator, StringSplitOptions.None);
			if (array.Length > 1)
			{
				email = array[1];
				if (email.Equals(""))
				{
					email = os.terminal.currentLine;
				}
			}
			Rectangle destinationRectangle = new Rectangle(num, num2, bounds.Width - 20, 200);
			sb.Draw(Utils.white, destinationRectangle, os.darkBackgroundColor);
			num2 += 80;
			destinationRectangle.X = num + (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), "Email: " + email, null).X + 2;
			destinationRectangle.Y = num2;
			destinationRectangle.Width = 7;
			destinationRectangle.Height = 20;
			if (os.timer % 1f < 0.3f)
			{
				sb.Draw(Utils.white, destinationRectangle, os.outlineColor);
			}
			num2 += 122;
			if (array.Length > 2 || Button.doButton(30, num, num2, 300, 22, "Confirm", os.highlightColor))
			{
				if (array.Length <= 2)
				{
					os.terminal.executeLine();
				}
				state = 2;
			}
			break;
		}
		case 2:
			num2 += 20;
			TextItem.doSmallLabel(new Vector2(num, num2), "Confirm this Email Address :\n" + email, null);
			num2 += 60;
			if (Button.doButton(21, num, num2, 200, 50, "Confirm Email", os.highlightColor))
			{
				if (!Settings.isDemoMode)
				{
					sendEmail();
				}
				else
				{
					saveEmail();
				}
			}
			num += 220;
			if (Button.doButton(20, num, num2, 200, 50, "Re-Enter Email", null))
			{
				state = 1;
				email = "";
				os.execute("getString Email");
			}
			break;
		}
	}
}
