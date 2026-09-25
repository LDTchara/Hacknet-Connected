using System;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class PorthackHeartDaemon : Daemon
{
	private RenderTarget2D rendertarget;

	private SpriteBatch rtSpritebatch;

	private float playTimeExpended = 0f;

	private bool PlayingHeartbreak = false;

	private PortHackCubeSequence pcs = new PortHackCubeSequence();

	private float FadeoutDelay = 1f;

	private float FadeoutDuration = 10f;

	private bool IsFlashingOut = false;

	private float flashOutTime = 0f;

	private SoundEffect SpinDownEffect;

	private SoundEffect glowSoundEffect;

	public PorthackHeartDaemon(Computer c, OS os)
		: base(c, "Porthack.Heart", os)
	{
		name = "Porthack.Heart";
		SpinDownEffect = os.content.Load<SoundEffect>("SFX/TraceKill");
		glowSoundEffect = os.content.Load<SoundEffect>("SFX/Ending/PorthackSpindown");
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
	}

	private void UpdateForTime(Rectangle bounds, SpriteBatch sb)
	{
		if (playTimeExpended > FadeoutDelay)
		{
			float fade = Math.Min(1f, (playTimeExpended - FadeoutDelay) / FadeoutDuration);
			Rectangle correctedbounds = default(Rectangle);
			ref Rectangle reference = ref correctedbounds;
			reference = new Rectangle(bounds.X, bounds.Y - Module.PANEL_HEIGHT, bounds.Width, bounds.Height + Module.PANEL_HEIGHT);
			OS oS = os;
			oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
			{
				Utils.FillEverywhereExcept(correctedbounds, os.fullscreen, sb, Color.Black * fade * 0.8f);
			});
		}
		if (pcs.HeartFadeSequenceComplete)
		{
			IsFlashingOut = true;
			flashOutTime += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
			float num = 3.8f;
			if (flashOutTime > num)
			{
				flashOutTime = num;
				os.canRunContent = false;
				os.endingSequence.IsActive = true;
				PostProcessor.EndingSequenceFlashOutActive = false;
				PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
				return;
			}
			PostProcessor.EndingSequenceFlashOutPercentageComplete = flashOutTime / num;
		}
		else
		{
			IsFlashingOut = false;
		}
		PostProcessor.EndingSequenceFlashOutActive = IsFlashingOut;
	}

	public void BreakHeart()
	{
		if (os.TraceDangerSequence.IsActive)
		{
			os.TraceDangerSequence.CancelTraceDangerSequence();
		}
		os.RequestRemovalOfAllPopups();
		PlayingHeartbreak = true;
		os.terminal.inputLocked = true;
		os.netMap.inputLocked = true;
		os.ram.inputLocked = true;
		os.DisableTopBarButtons = true;
		MusicManager.transitionToSong("Music/Ambient/AmbientDrone_Clipped");
		SpinDownEffect.Play();
		os.delayer.Post(ActionDelayer.Wait(18.0), delegate
		{
			glowSoundEffect.Play();
		});
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		try
		{
			int width = bounds.Width;
			int height = bounds.Height;
			if (rendertarget == null || rendertarget.Width != width || rendertarget.Height != height)
			{
				if (rtSpritebatch == null)
				{
					rtSpritebatch = new SpriteBatch(sb.GraphicsDevice);
				}
				if (rendertarget != null)
				{
					rendertarget.Dispose();
				}
				rendertarget = new RenderTarget2D(sb.GraphicsDevice, width, height);
			}
			if (!PlayingHeartbreak)
			{
				TextItem.DrawShadow = false;
				TextItem.doFontLabel(new Vector2(bounds.X + 6, bounds.Y + 2), Utils.FlipRandomChars("PortHack.Heart", 0.003), GuiData.font, Utils.AddativeWhite * 0.6f, bounds.Width - 10, 100f);
				TextItem.doFontLabel(new Vector2(bounds.X + 6, bounds.Y + 2), Utils.FlipRandomChars("PortHack.Heart", 0.1), GuiData.font, Utils.AddativeWhite * 0.2f, bounds.Width - 10, 100f);
			}
			if (PlayingHeartbreak)
			{
				playTimeExpended += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
			}
			UpdateForTime(bounds, sb);
			RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
			sb.GraphicsDevice.SetRenderTarget(rendertarget);
			sb.GraphicsDevice.Clear(Color.Transparent);
			rtSpritebatch.Begin();
			Rectangle dest = new Rectangle(0, 0, bounds.Width, bounds.Height);
			Vector3 value = new Vector3(MathHelper.ToRadians(35.4f), MathHelper.ToRadians(45f), MathHelper.ToRadians(0f));
			Vector3 vector = new Vector3(1f, 1f, 0f) * os.timer * 0.2f + new Vector3(os.timer * 0.1f, os.timer * -0.4f, 0f);
			float num = 2.5f;
			if (PlayingHeartbreak)
			{
				if (playTimeExpended < num)
				{
					vector = Vector3.Lerp(Utils.NormalizeRotationVector(vector), value, Utils.QuadraticOutCurve(playTimeExpended / num));
					Cube3D.RenderWireframe(Vector3.Zero, 2.6f, vector, Color.White);
				}
				else
				{
					pcs.DrawHeartSequence(dest, (float)os.lastGameTime.ElapsedGameTime.TotalSeconds, 30f);
				}
			}
			else
			{
				Cube3D.RenderWireframe(new Vector3(0f, 0f, 0f), 2.6f, vector, Color.White);
			}
			rtSpritebatch.End();
			sb.GraphicsDevice.SetRenderTarget(currentRenderTarget);
			Rectangle rectangle = new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
			float rarity = Math.Min(1f, playTimeExpended / num * 0.8f + 0.2f);
			FlickeringTextEffect.DrawFlickeringSprite(sb, rectangle, rendertarget, 4f, rarity, os, Color.White);
			sb.Draw(rendertarget, rectangle, Utils.AddativeWhite * 0.7f);
		}
		catch (Exception ex)
		{
			Console.WriteLine(Utils.GenerateReportFromException(ex));
		}
	}

	public override string getSaveString()
	{
		return "<porthackheart name=\"" + name + "\"/>";
	}
}
