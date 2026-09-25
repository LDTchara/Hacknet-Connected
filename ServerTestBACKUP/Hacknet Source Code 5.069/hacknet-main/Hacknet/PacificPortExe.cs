using Hacknet.Effects;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class PacificPortExe : ExeModule
{
	private const float RUN_TIME = 6f;

	private const float IDLE_TIME = 6.2f;

	private float elapsedTime = 0f;

	private int framesFlashed = 0;

	private FlyoutEffect effect;

	public PacificPortExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		needsProxyAccess = true;
		name = "Pacific_Portcrusher";
		ramCost = 190;
		IdentifierName = "PacificPortcrusher";
		Computer computer = Programs.getComputer(os, targetIP);
		computer.hostileActionTaken();
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (elapsedTime < 6f && elapsedTime + t >= 6f)
		{
			Completed();
		}
		else if (elapsedTime < 6.2f && elapsedTime + t >= 6.2f)
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
			computer.openPort(192, os.thisComputer.ip);
			os.write("Pacific Portcrusher  >>" + LocaleTerms.Loc("SUCCESS") + "<<");
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		if (effect == null)
		{
			effect = new FlyoutEffect(spriteBatch.GraphicsDevice, os.content, bounds.Width - 2, bounds.Height - 2);
		}
		float num = 1f;
		if (elapsedTime < 6f)
		{
			num = elapsedTime / 6f;
		}
		Rectangle contentAreaDest = GetContentAreaDest();
		if (contentAreaDest.Height > 2)
		{
			if (isExiting)
			{
				framesFlashed++;
			}
			effect.Draw((float)os.lastGameTime.ElapsedGameTime.TotalSeconds, contentAreaDest, spriteBatch, 50f * num, 3, 100f * Utils.QuadraticOutCurve(1f - num), os.highlightColor, drawFlashFromMiddle: false, isExiting && framesFlashed % 30 <= 3);
		}
	}
}
