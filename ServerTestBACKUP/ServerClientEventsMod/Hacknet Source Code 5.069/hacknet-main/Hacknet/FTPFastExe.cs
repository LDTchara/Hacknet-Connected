using System;
using Hacknet.Effects;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class FTPFastExe : ExeModule
{
	private const float RUN_TIME = 7f;

	private const float IDLE_TIME = 7.5f;

	private float elapsedTime = 0f;

	private PointGatherEffect points = new PointGatherEffect();

	public FTPFastExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		needsProxyAccess = true;
		name = "FTP_SprintBreak";
		ramCost = 190;
		IdentifierName = "FTPSprint";
		points.Init(os.content);
		points.AllowDoubleLines = true;
		points.Explode(70);
		Computer computer = Programs.getComputer(os, targetIP);
		computer.hostileActionTaken();
	}

	public override void Update(float t)
	{
		base.Update(t);
		points.attractionToCentreMass = elapsedTime;
		points.Update(t);
		points.Update(t);
		points.Update(t);
		if (elapsedTime < 7f && elapsedTime + t >= 7f)
		{
			Completed();
		}
		else if (elapsedTime < 7.5f && elapsedTime + t >= 7.5f)
		{
			isExiting = true;
			points.NodeColor = Color.White;
		}
		else
		{
			points.LineLengthPercentage = Math.Min(0.5f, elapsedTime / 7f * 0.7f);
		}
		elapsedTime += t;
	}

	public override void Completed()
	{
		base.Completed();
		Computer computer = Programs.getComputer(os, targetIP);
		if (computer != null)
		{
			computer.openPort(21, os.thisComputer.ip);
			os.write(">> " + LocaleTerms.Loc("FTP Sprint Crack Successful"));
		}
		points.FlashComplete();
		points.Explode(30);
		points.timeRemainingWithoutAttract = 10f;
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		float num = 1f;
		if (elapsedTime < 7f)
		{
			num = elapsedTime / 7f;
		}
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + bounds.Height / 2 - 16, bounds.Width - 4, Math.Min(30, Math.Max(0, bounds.Height - 50)));
		Rectangle contentAreaDest = GetContentAreaDest();
		if (contentAreaDest.Height > 2)
		{
			points.Render(contentAreaDest, spriteBatch);
		}
	}
}
