using System;
using System.Threading;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class AuthenticatingDaemon : Daemon
{
	public UserDetail user;

	public AuthenticatingDaemon(Computer computer, string serviceName, OS opSystem)
		: base(computer, serviceName, opSystem)
	{
	}

	public virtual void loginGoBack()
	{
	}

	public virtual void userLoggedIn()
	{
	}

	public void startLogin()
	{
		os.displayCache = "";
		os.execute("login");
		while (os.displayCache.Equals(""))
		{
		}
		os.display.command = name;
	}

	public void doLoginDisplay(Rectangle bounds, SpriteBatch sb)
	{
		int num = bounds.X + 20;
		int num2 = bounds.Y + 100;
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		string[] array = os.displayCache.Split(separator, StringSplitOptions.None);
		string text = "";
		string text2 = "";
		int num3 = -1;
		int num4 = 0;
		if (array[0].Equals("loginData"))
		{
			text = ((!(array[1] != "")) ? os.terminal.currentLine : array[1]);
			if (array.Length > 2)
			{
				num4 = 1;
				text2 = array[2];
				if (text2.Equals(""))
				{
					for (int i = 0; i < os.terminal.currentLine.Length; i++)
					{
						text2 += "*";
					}
				}
				else
				{
					string text3 = "";
					for (int i = 0; i < text2.Length; i++)
					{
						text3 += "*";
					}
					text2 = text3;
				}
			}
			if (array.Length > 3)
			{
				num4 = 2;
				num3 = Convert.ToInt32(array[3]);
			}
		}
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = bounds.X + 2;
		tmpRect.Y = num2;
		tmpRect.Height = 200;
		tmpRect.Width = bounds.Width - 4;
		sb.Draw(Utils.white, tmpRect, (num3 == 0) ? os.lockedColor : os.indentBackgroundColor);
		if (num3 != 0 && num3 != -1)
		{
			for (int i = 0; i < comp.users.Count; i++)
			{
				if (comp.users[i].name.Equals(text))
				{
					user = comp.users[i];
				}
			}
			userLoggedIn();
		}
		tmpRect.Height = 22;
		num2 += 30;
		Vector2 vector = TextItem.doMeasuredLabel(new Vector2(num, num2), LocaleTerms.Loc("Login") + " ", Color.White);
		if (num3 == 0)
		{
			num += (int)vector.X;
			TextItem.doLabel(new Vector2(num, num2), LocaleTerms.Loc("Failed"), os.brightLockedColor);
			num -= (int)vector.X;
		}
		num2 += 60;
		if (num4 == 0)
		{
			tmpRect.Y = num2;
			sb.Draw(Utils.white, tmpRect, os.subtleTextColor);
		}
		int num5 = (int)(6f + Math.Max(GuiData.smallfont.MeasureString(LocaleTerms.Loc("username :")).X, GuiData.smallfont.MeasureString(LocaleTerms.Loc("password :")).X));
		sb.DrawString(GuiData.smallfont, LocaleTerms.Loc("username :"), new Vector2(num, num2), Color.White);
		num += num5;
		sb.DrawString(GuiData.smallfont, text, new Vector2(num, num2), Color.White);
		num -= num5;
		num2 += 30;
		if (num4 == 1)
		{
			tmpRect.Y = num2;
			sb.Draw(Utils.white, tmpRect, os.subtleTextColor);
		}
		sb.DrawString(GuiData.smallfont, LocaleTerms.Loc("password :"), new Vector2(num, num2), Color.White);
		num += num5;
		sb.DrawString(GuiData.smallfont, text2, new Vector2(num, num2), Color.White);
		num2 += 30;
		num -= num5;
		if (num3 != -1)
		{
			if (Button.doButton(12345, num, num2, 160, 30, LocaleTerms.Loc("Back"), os.indentBackgroundColor))
			{
				loginGoBack();
			}
			if (Button.doButton(123456, num + 165, num2, 160, 30, LocaleTerms.Loc("Retry"), os.indentBackgroundColor))
			{
				os.displayCache = "";
				os.execute("login");
				while (os.displayCache.Equals(""))
				{
				}
				os.display.command = name;
			}
			return;
		}
		num2 += 65;
		for (int i = 0; i < comp.users.Count; i++)
		{
			if (comp.users[i].known && validUser(comp.users[i].type))
			{
				if (Button.doButton(123457 + i, num, num2, 300, 25, LocaleTerms.Loc("User") + ": " + comp.users[i].name + " " + LocaleTerms.Loc("Pass") + ": " + comp.users[i].pass, os.darkBackgroundColor))
				{
					forceLogin(comp.users[i].name, comp.users[i].pass);
				}
				num2 += 27;
			}
		}
	}

	public new static bool validUser(byte type)
	{
		return Daemon.validUser(type) || type == 3;
	}

	public void forceLogin(string username, string pass)
	{
		string prompt = os.terminal.prompt;
		os.terminal.currentLine = username;
		os.terminal.NonThreadedInstantExecuteLine();
		while (os.terminal.prompt.Equals(prompt))
		{
			Thread.Sleep(0);
		}
		os.terminal.currentLine = pass;
		os.terminal.NonThreadedInstantExecuteLine();
	}
}
