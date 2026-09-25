using System;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class RTSPPortExe : ExeModule
{
	private static float RUN_TIME = 6.3f;

	private static float IDLE_TIME = 30.5f;

	private float elapsedTime = 0f;

	private float completionFlashDuration = 3.2f;

	private float completionFlashTimer = 0f;

	private bool isComplete = false;

	private TrailLoadingSpinnerEffect spinner;

	private int completeRamUse = 220;

	private float preciceRamCost = 0f;

	public RTSPPortExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "RTSPCrack";
		ramCost = 360;
		IdentifierName = "RTSPCrack";
		((os.connectedComp == null) ? os.thisComputer : os.connectedComp).hostileActionTaken();
		spinner = new TrailLoadingSpinnerEffect(operatingSystem);
		preciceRamCost = ramCost;
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (elapsedTime < RUN_TIME && elapsedTime + t >= RUN_TIME)
		{
			Completed();
		}
		else if (elapsedTime < IDLE_TIME && elapsedTime + t >= IDLE_TIME)
		{
			isExiting = true;
		}
		if (isComplete)
		{
			completionFlashTimer -= t;
			if (completionFlashTimer <= 0f)
			{
				completionFlashTimer = 0f;
			}
			if (ramCost > completeRamUse)
			{
				float num = 1f;
				float num2 = 2f;
				if (elapsedTime - RUN_TIME < 2f)
				{
					num = (elapsedTime - RUN_TIME) / num2;
				}
				preciceRamCost -= num * 0.5f;
				ramCost = (int)preciceRamCost;
				if (ramCost < completeRamUse)
				{
					ramCost = completeRamUse;
				}
			}
		}
		elapsedTime += t;
	}

	public override void Completed()
	{
		base.Completed();
		Computer computer = Programs.getComputer(os, targetIP);
		if (computer != null)
		{
			computer.openPort(554, os.thisComputer.ip);
			os.write("RTSPCrack Complete");
			isComplete = true;
			completionFlashTimer = completionFlashDuration;
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		float num = 1f;
		if (elapsedTime < RUN_TIME)
		{
			num = elapsedTime / RUN_TIME;
		}
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + bounds.Height / 2 - 16, bounds.Width - 4, Math.Min(30, Math.Max(0, bounds.Height - 50)));
		rectangle.Width = (int)((float)rectangle.Width * num);
		Color highlightColor = os.highlightColor;
		highlightColor = Color.Lerp(highlightColor, Utils.AddativeWhite, completionFlashTimer / completionFlashDuration);
		spinner.Draw(GetContentAreaDest(), spriteBatch, RUN_TIME, (float)Math.Max(0.0, RUN_TIME - elapsedTime), Math.Max(0f, elapsedTime - RUN_TIME));
	}
}
