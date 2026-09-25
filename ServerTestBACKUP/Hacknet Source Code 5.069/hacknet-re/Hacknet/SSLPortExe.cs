using System;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class SSLPortExe : ExeModule
{
	private enum SSLMode
	{
		SSH,
		FTP,
		Web,
		RTSP
	}

	private const float RUN_TIME = 12f;

	private const float IDLE_TIME = 15f;

	private float elapsedTime = 0f;

	private SSLMode Mode = SSLMode.SSH;

	private Vector2 LastCentralRenderOffset = Vector2.Zero;

	private bool IsComplete = false;

	private SSLPortExe(Rectangle location, OS operatingSystem, SSLMode mode)
		: base(location, operatingSystem)
	{
		name = "SSLTrojan";
		ramCost = 220;
		IdentifierName = "SSLTrojan";
		Mode = mode;
		((os.connectedComp == null) ? os.thisComputer : os.connectedComp).hostileActionTaken();
	}

	public static SSLPortExe GenerateInstanceOrNullFromArguments(string[] args, Rectangle location, object osObj, Computer target)
	{
		OS oS = (OS)osObj;
		if (args.Length < 4)
		{
			oS.write("--------------------------------------");
			oS.write("SSLTrojan " + LocaleTerms.Loc("ERROR: Not enough arguments!"));
			oS.write(LocaleTerms.Loc("Usage:") + " SSLTrojan [" + LocaleTerms.Loc("PortNum") + "] [-" + LocaleTerms.Loc("option") + "] [" + LocaleTerms.Loc("option port num") + "]");
			oS.write(LocaleTerms.Loc("Valid Options:") + " [-s (SSH)] [-f (FTP)] [-w (HTTP)] [-r (RTSP)]");
			oS.write("--------------------------------------");
			return null;
		}
		try
		{
			int num = Convert.ToInt32(args[1]);
			int num2 = Convert.ToInt32(args[3]);
			string text = args[2].ToLower();
			SSLMode sSLMode = SSLMode.SSH;
			switch (text)
			{
			case "-s":
				sSLMode = SSLMode.SSH;
				break;
			case "-f":
				sSLMode = SSLMode.FTP;
				break;
			case "-w":
				sSLMode = SSLMode.Web;
				break;
			case "-r":
				sSLMode = SSLMode.RTSP;
				break;
			default:
				text = null;
				break;
			}
			if (text == null)
			{
				oS.write("--------------------------------------");
				oS.write("SSLTrojan " + string.Format(LocaleTerms.Loc("Error: Mode {0} is invalid."), args[2]));
				oS.write(LocaleTerms.Loc("Valid Options:") + " [-s (SSH)] [-f (FTP)] [-w (HTTP)] [-r (RTSP)]");
				oS.write("--------------------------------------");
				return null;
			}
			int num3 = -1;
			bool flag = false;
			switch (sSLMode)
			{
			case SSLMode.SSH:
				flag = target.isPortOpen(22);
				num3 = target.GetDisplayPortNumberFromCodePort(22);
				break;
			case SSLMode.FTP:
				flag = target.isPortOpen(21);
				num3 = target.GetDisplayPortNumberFromCodePort(21);
				break;
			case SSLMode.Web:
				flag = target.isPortOpen(80);
				num3 = target.GetDisplayPortNumberFromCodePort(80);
				break;
			case SSLMode.RTSP:
				flag = target.isPortOpen(554);
				num3 = target.GetDisplayPortNumberFromCodePort(554);
				break;
			}
			if (!flag)
			{
				oS.write("--------------------------------------");
				oS.write("SSLTrojan " + LocaleTerms.Loc("Error: Target bypass port is closed!"));
				return null;
			}
			int num4 = -1;
			try
			{
				num4 = Convert.ToInt32(num2);
			}
			catch (FormatException)
			{
				oS.write("--------------------------------------");
				oS.write("SSLTrojan " + string.Format(LocaleTerms.Loc("Error: Invalid tunnel port number : \"{0}\""), num2));
				return null;
			}
			if (num4 != num3)
			{
				oS.write("--------------------------------------");
				oS.write("SSLTrojan " + string.Format(LocaleTerms.Loc("Error: Tunnel port number {0} does not match expected service \"{1}"), num4, text));
				return null;
			}
			return new SSLPortExe(location, oS, sSLMode);
		}
		catch (Exception ex2)
		{
			oS.write("SSLTrojan " + LocaleTerms.Loc("Error:"));
			oS.write(ex2.Message);
			return null;
		}
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (elapsedTime < 12f && elapsedTime + t >= 12f)
		{
			Completed();
		}
		else if (elapsedTime < 15f && elapsedTime + t >= 15f)
		{
			isExiting = true;
		}
		elapsedTime += t;
	}

	public override void Completed()
	{
		base.Completed();
		Computer computer = Programs.getComputer(os, targetIP);
		if (computer != null)
		{
			computer.openPort(443, os.thisComputer.ip);
			os.write(":: " + LocaleTerms.Loc("HTTPS SSL Reverse Tunnel Opened"));
			IsComplete = true;
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle rectangle = Utils.InsetRectangle(new Rectangle(bounds.X, bounds.Y + Module.PANEL_HEIGHT, bounds.Width, bounds.Height - Module.PANEL_HEIGHT), 2);
		if (bounds.Height < Module.PANEL_HEIGHT + 4)
		{
			return;
		}
		float num = 1f;
		if (elapsedTime < 12f)
		{
			num = elapsedTime / 12f;
		}
		float num2 = 0.6f;
		float num3 = Utils.CubicInCurve(Utils.CubicInCurve(Math.Min(1f, num * (1f / num2))));
		float num4 = Math.Max(0f, Math.Min(1f, (num - num2) * (1f / (1f - num2))));
		int num5 = 7;
		bool flag = true;
		Vector2 vector = new Vector2(bounds.X + bounds.Width / 2, bounds.Y + Module.PANEL_HEIGHT + bounds.Height / 2 - 12);
		Vector2 vector2 = vector;
		float num6 = num2 * 12f;
		float num7 = (elapsedTime - num6) / (15f - num6);
		if (num3 < 0.99f)
		{
			num7 = 0f;
		}
		Vector2 left = vector;
		float num8 = (float)bounds.Width - (100f - num7 * 1500f);
		float num9 = num4 * 1f;
		Vector2 vector3 = new Vector2((float)Math.Sin(os.timer * 2f) * (num8 / 4f), (float)Math.Cos(os.timer * 2f) * (num8 / 4f));
		vector -= LastCentralRenderOffset * num4 * 1f;
		LastCentralRenderOffset = Vector2.Zero;
		for (int i = 0; i < num5; i++)
		{
			int num10 = 13;
			int subdivisions = num10;
			if (i == 0)
			{
				subdivisions = (int)((float)(num10 - 1) * Math.Min(1f, num3 + 0.0831f)) + 1;
			}
			float num11 = Math.Max(1f, (float)num5 - (float)i * 2f + 1f);
			num11 += num9;
			float num12 = os.timer * 2f;
			if (flag)
			{
				num12 *= ((i % 2 == 0) ? 1f : (-1f));
			}
			if ((float)i < 1f + (float)(int)(6f * num4))
			{
				TunnelingCircleEffect.Draw(spriteBatch, vector, num8, num11, subdivisions, num12, Color.Lerp(Color.White, Color.Transparent, Utils.QuadraticOutCurve((float)i / (float)num5)), Color.Gray * (0.12f * (1f - num) + 0.1f), os.highlightColor, rectangle);
				Utils.ClipLineSegmentsForRect(rectangle, left, vector, out var leftOut, out var rightOut);
				Utils.drawLineAlt(spriteBatch, leftOut, rightOut, Vector2.Zero, os.highlightColor * 0.3f, 0.6f, 1f, Utils.gradientLeftRight);
			}
			float num13 = num8 / 4f;
			left = vector;
			Vector2 vector4 = new Vector2((float)Math.Sin(num12) * num13, (float)Math.Cos(num12) * num13);
			LastCentralRenderOffset += vector4;
			vector += vector4;
			num8 = num8 / 2f - num11;
			if (i != num5 - 1 || !(num3 > 0.99f))
			{
				continue;
			}
			if (vector.X > (float)rectangle.X && vector.X < (float)(rectangle.X + rectangle.Width) && vector.Y > (float)rectangle.Y && vector.Y < (float)(rectangle.Y + rectangle.Height))
			{
				spriteBatch.Draw(Utils.white, vector, null, (num4 > 0.99f) ? os.unlockedColor : os.highlightColor, 0f, Vector2.Zero, Vector2.One * 2f, SpriteEffects.None, 0.7f);
			}
			if (IsComplete)
			{
				int num14 = 2;
				Rectangle destinationRectangle = new Rectangle((int)vector.X - num14 / 2, rectangle.Y, num14, rectangle.Height);
				spriteBatch.Draw(Utils.white, destinationRectangle, Utils.makeColorAddative(os.highlightColor));
				Rectangle rectangle2 = new Rectangle(rectangle.X, (int)vector.Y - num14 / 2, rectangle.Width, num14);
				if (rectangle2.Y > rectangle.Y + rectangle.Height - 30 || rectangle2.Y < rectangle.Y)
				{
					rectangle2.Y = rectangle.Y + rectangle.Height / 2 - num14;
				}
				spriteBatch.Draw(Utils.white, rectangle2, Utils.makeColorAddative(os.highlightColor));
				rectangle2.Height = Math.Min(24, rectangle.Height / 2);
				rectangle2.Y = rectangle.Y + rectangle.Height / 2 - rectangle2.Height / 2;
				if (Utils.randm(0.25f) + 0.75f < num7)
				{
					spriteBatch.Draw(Utils.white, rectangle2, Color.Black * 0.5f);
					TextItem.doFontLabelToSize(rectangle2, "   -  " + LocaleTerms.Loc("SSL TUNNEL COMPLETE") + "  -", GuiData.smallfont, Color.White, doNotOversize: true);
				}
			}
		}
	}
}
