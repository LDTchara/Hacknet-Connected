using System;
using Hacknet.Effects;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class TorrentPortExe : ExeModule
{
	private const float RUN_TIME = 4.8f;

	private const float IDLE_TIME = 16.5f;

	private float elapsedTime = 0f;

	private float completionFlashDuration = 3.2f;

	private float completionFlashTimer = 0f;

	private bool isComplete = false;

	private RaindropsEffect RainEffect = new RaindropsEffect();

	private RaindropsEffect BackgroundRainEffect = new RaindropsEffect();

	public TorrentPortExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "TorrentStreamInjector";
		ramCost = 360;
		IdentifierName = "TorrentInjector";
		BackgroundRainEffect.Init(os.content);
		BackgroundRainEffect.MaxVerticalLandingVariane += 0.04f;
		BackgroundRainEffect.FallRate = 0.5f;
		RainEffect.Init(os.content);
		RainEffect.ForceSpawnDrop(new Vector3(0.5f, 0f, 0f));
		((os.connectedComp == null) ? os.thisComputer : os.connectedComp).hostileActionTaken();
	}

	public override void Update(float t)
	{
		base.Update(t);
		float num = 20f;
		if (elapsedTime < 1f)
		{
			num *= elapsedTime * 0.2f;
		}
		RainEffect.Update(t, isComplete ? 2f : num);
		BackgroundRainEffect.Update(t, num * 3f);
		if (elapsedTime < 4.8f && elapsedTime + t >= 4.8f)
		{
			Completed();
		}
		else if (elapsedTime < 16.5f && elapsedTime + t >= 16.5f)
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
		}
		elapsedTime += t;
	}

	public override void Completed()
	{
		base.Completed();
		Computer computer = Programs.getComputer(os, targetIP);
		if (computer != null)
		{
			computer.openPort(6881, os.thisComputer.ip);
			os.write(" - " + LocaleTerms.Loc("Torrent Stream Injection Complete") + " - ");
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
		if (elapsedTime < 4.8f)
		{
			num = elapsedTime / 4.8f;
		}
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + bounds.Height / 2 - 16, bounds.Width - 4, Math.Min(30, Math.Max(0, bounds.Height - 50)));
		rectangle.Width = (int)((float)rectangle.Width * num);
		if (isComplete)
		{
			Color value = os.highlightColor * 0.2f;
			value = Color.Lerp(value, Utils.AddativeWhite, completionFlashTimer / completionFlashDuration);
			BackgroundRainEffect.Render(GetContentAreaDest(), spriteBatch, value, 20f, 40f);
		}
		Color color = os.highlightColor;
		if (isComplete)
		{
			color = Color.Lerp(color, Utils.AddativeWhite, completionFlashTimer / completionFlashDuration);
		}
		RainEffect.Render(GetContentAreaDest(), spriteBatch, color, 50f, 100f);
	}
}
