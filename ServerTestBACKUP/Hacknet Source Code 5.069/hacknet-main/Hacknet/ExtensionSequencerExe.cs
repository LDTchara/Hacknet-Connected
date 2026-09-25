using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;

namespace Hacknet;

internal class ExtensionSequencerExe : ExeModule
{
	private enum SequencerExeState
	{
		Unavaliable,
		AwaitingActivation,
		SpinningUp,
		Active
	}

	public static int ACTIVATING_RAM_COST = 170;

	public static int BASE_RAM_COST = 60;

	public static float RAM_CHANGE_PS = 100f;

	public static double Song_Length = 186.0;

	private static float TimeBetweenBeats = 1.832061f;

	private MovingBarsEffect bars = new MovingBarsEffect();

	private string targetID;

	private string flagForProgressionName;

	private string oldSongName = null;

	private int targetRamUse = ACTIVATING_RAM_COST;

	private float stateTimer = 0f;

	private float beatHits = 0.15f;

	private double beatDropTime = 16.64;

	private List<ConnectedNodeEffect> nodeeffects = new List<ConnectedNodeEffect>();

	private SequencerExeState state = SequencerExeState.Unavaliable;

	private Computer targetComp;

	private bool HasBeenKilled = false;

	public ExtensionSequencerExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "ESequencer";
		ramCost = ACTIVATING_RAM_COST;
		IdentifierName = "ESequencer";
		targetIP = os.thisComputer.ip;
		bars.MinLineChangeTime = 1f;
		bars.MaxLineChangeTime = 3f;
		beatDropTime = ExtensionLoader.ActiveExtensionInfo.SequencerSpinUpTime;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		nodeeffects.Add(new ConnectedNodeEffect(os, intense: true));
		nodeeffects.Add(new ConnectedNodeEffect(os, intense: true));
		nodeeffects.Add(new ConnectedNodeEffect(os, intense: true));
		nodeeffects.Add(new ConnectedNodeEffect(os, intense: true));
		nodeeffects.Add(new ConnectedNodeEffect(os, intense: true));
		targetID = ExtensionLoader.ActiveExtensionInfo.SequencerTargetID;
		flagForProgressionName = ExtensionLoader.ActiveExtensionInfo.SequencerFlagRequiredForStart;
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (HasBeenKilled)
		{
			return;
		}
		bars.Update(t);
		UpdateRamCost(t);
		stateTimer += t;
		if (isExiting)
		{
			os.netMap.DimNonConnectedNodes = false;
		}
		bool flag = true;
		if (!string.IsNullOrWhiteSpace(flagForProgressionName))
		{
			string[] array = flagForProgressionName.Split(Utils.newlineDelim, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				flag &= os.Flags.HasFlag(flagForProgressionName) || Settings.debugCommandsEnabled;
			}
		}
		switch (state)
		{
		case SequencerExeState.Unavaliable:
			if (flag)
			{
				state = SequencerExeState.AwaitingActivation;
				break;
			}
			bars.MinLineChangeTime = 1f;
			bars.MaxLineChangeTime = 3f;
			break;
		case SequencerExeState.AwaitingActivation:
			if (!flag)
			{
				state = SequencerExeState.Unavaliable;
			}
			break;
		case SequencerExeState.SpinningUp:
			if (MediaPlayer.State == MediaState.Playing)
			{
				if (MediaPlayer.PlayPosition.TotalSeconds >= beatDropTime && (double)stateTimer > beatDropTime - 0.5)
				{
					MoveToActiveState();
				}
			}
			else if (stateTimer > ExtensionLoader.ActiveExtensionInfo.SequencerSpinUpTime)
			{
				MoveToActiveState();
			}
			break;
		case SequencerExeState.Active:
		{
			float num = 2.5f;
			if (stateTimer < num)
			{
				double num2 = MediaPlayer.PlayPosition.TotalSeconds - beatDropTime;
				if (num2 % (double)beatHits < 0.009999999776482582)
				{
					os.warningFlash();
				}
			}
			ActiveStateUpdate(t);
			break;
		}
		}
	}

	private void ActiveStateUpdate(float t)
	{
		double num = stateTimer;
		double num2 = num - beatDropTime;
		if (num2 % (double)TimeBetweenBeats < (double)t)
		{
			os.warningFlash();
		}
	}

	private void UpdateRamCost(float t)
	{
		if (targetRamUse == ramCost)
		{
			return;
		}
		if (targetRamUse < ramCost)
		{
			ramCost -= (int)(t * RAM_CHANGE_PS);
			if (ramCost < targetRamUse)
			{
				ramCost = targetRamUse;
			}
			return;
		}
		int num = (int)(t * RAM_CHANGE_PS);
		if (os.ramAvaliable >= num)
		{
			ramCost += num;
			if (ramCost > targetRamUse)
			{
				ramCost = targetRamUse;
			}
		}
	}

	public override void Killed()
	{
		base.Killed();
		if (state == SequencerExeState.Active)
		{
			os.delayer.Post(ActionDelayer.NextTick(), delegate
			{
				ExtensionSequencerExe exe = new ExtensionSequencerExe(bounds, os, new string[0])
				{
					state = state,
					stateTimer = stateTimer,
					bounds = bounds,
					oldSongName = oldSongName,
					nodeeffects = nodeeffects,
					ramCost = ramCost
				};
				os.addExe(exe);
			});
		}
		else
		{
			os.netMap.DimNonConnectedNodes = false;
			os.runCommand("disconnect");
			if (targetComp != null)
			{
				os.netMap.visibleNodes.Remove(os.netMap.nodes.IndexOf(targetComp));
			}
		}
	}

	private void MoveToActiveState()
	{
		state = SequencerExeState.Active;
		stateTimer = 0f;
		targetRamUse = BASE_RAM_COST;
		os.warningFlashTimer = OS.WARNING_FLASH_TIME;
		os.netMap.DimNonConnectedNodes = true;
		os.netMap.discoverNode(targetComp);
		os.runCommand("connect " + targetComp.ip);
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle contentAreaDest = GetContentAreaDest();
		contentAreaDest = Utils.InsetRectangle(contentAreaDest, 1);
		float num = os.warningFlashTimer / OS.WARNING_FLASH_TIME;
		float num2 = 2f;
		if (num > 0f)
		{
			num2 += num * ((float)contentAreaDest.Height - num2);
		}
		Color drawColor = Color.Lerp(Utils.AddativeWhite * 0.5f, Utils.AddativeRed, num);
		bars.Draw(spriteBatch, GetContentAreaDest(), num2, 4f, 1f, drawColor);
		switch (state)
		{
		case SequencerExeState.Unavaliable:
		{
			spriteBatch.Draw(Utils.white, contentAreaDest, Color.Black * 0.5f);
			Rectangle rectangle2 = Utils.InsetRectangle(contentAreaDest, 6);
			if (!isExiting)
			{
				TextItem.doFontLabelToSize(rectangle2, "LINK UNAVAILABLE", GuiData.titlefont, Utils.AddativeWhite);
			}
			Rectangle destinationRectangle2 = rectangle2;
			destinationRectangle2.Y += destinationRectangle2.Height - 20;
			destinationRectangle2.Height = 20;
			if (!isExiting)
			{
				GuiData.spriteBatch.Draw(Utils.white, destinationRectangle2, Color.Black * 0.5f);
				if (Button.doButton(32711803, destinationRectangle2.X, destinationRectangle2.Y, destinationRectangle2.Width, destinationRectangle2.Height, LocaleTerms.Loc("Exit"), os.lockedColor))
				{
					isExiting = true;
				}
			}
			break;
		}
		case SequencerExeState.AwaitingActivation:
		{
			int num3 = 30;
			Rectangle destinationRectangle = new Rectangle(bounds.X + 1, bounds.Y + bounds.Height / 2 - num3, bounds.Width - 2, num3 * 2);
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Black * 0.92f);
			if (Button.doButton(8310101, bounds.X + 10, bounds.Y + bounds.Height / 2 - num3 / 2, bounds.Width - 20, num3, LocaleTerms.Loc("ACTIVATE"), os.highlightColor))
			{
				if (os.TraceDangerSequence.IsActive)
				{
					os.write("SEQUENCER ERROR: OS reports critical action already in progress.");
					break;
				}
				stateTimer = 0f;
				state = SequencerExeState.SpinningUp;
				bars.MinLineChangeTime = 0.1f;
				bars.MaxLineChangeTime = 1f;
				MusicManager.FADE_TIME = 0.6f;
				oldSongName = MusicManager.currentSongName;
				targetComp = Programs.getComputer(os, targetID);
				((WebServerDaemon)targetComp.getDaemon(typeof(WebServerDaemon)))?.LoadWebPage();
				RunnableConditionalActions.LoadIntoOS(ExtensionLoader.ActiveExtensionInfo.ActionsToRunOnSequencerStart, os);
			}
			break;
		}
		case SequencerExeState.SpinningUp:
		{
			Rectangle rectangle = contentAreaDest;
			rectangle.Height = (int)((float)rectangle.Height * (stateTimer / ExtensionLoader.ActiveExtensionInfo.SequencerSpinUpTime));
			rectangle.Y = contentAreaDest.Y + contentAreaDest.Height - rectangle.Height + 1;
			rectangle.Width += 4;
			bars.Draw(spriteBatch, rectangle, num2, 4f, 1f, os.brightLockedColor);
			break;
		}
		case SequencerExeState.Active:
			spriteBatch.Draw(Utils.white, GetContentAreaDest(), Color.Black * 0.5f);
			TextItem.doFontLabelToSize(GetContentAreaDest(), " G O   G O   G O ", GuiData.titlefont, Color.Lerp(Utils.AddativeRed, os.brightLockedColor, Math.Min(1f, stateTimer / 2f)));
			DrawActiveState();
			break;
		}
	}

	private void DrawActiveState()
	{
		float num = 5.2f;
		float point = Math.Min(num, stateTimer) / num;
		float num2 = 30f;
		Vector2 vector = new Vector2(os.netMap.bounds.X, os.netMap.bounds.Y) + new Vector2((float)NetworkMap.NODE_SIZE / 2f);
		for (int i = 0; i < nodeeffects.Count; i++)
		{
			float num3 = (float)(i + 1) / (float)(nodeeffects.Count + 1);
			float num4 = 3f * num3;
			float num5 = 1f - Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(point)));
			nodeeffects[i].ScaleFactor = num3 * (num2 * num5) + num4;
			nodeeffects[i].draw(spriteBatch, vector + os.netMap.GetNodeDrawPos(targetComp));
		}
	}
}
