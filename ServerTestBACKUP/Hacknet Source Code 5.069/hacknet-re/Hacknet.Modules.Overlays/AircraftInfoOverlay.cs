using System;
using Hacknet.Daemons.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Hacknet.Modules.Overlays;

public class AircraftInfoOverlay
{
	private AircraftDaemon CrashingAircraft;

	private AircraftDaemon SecondaryAircraft;

	public bool IsActive = false;

	private float timeElapsed = 0f;

	private float flashInTimeLeft = 1f;

	private OS os;

	public bool IsMonitoringDLCEndingCases = false;

	private bool TargetHasStartedCrashing = false;

	private bool IsInPostSaveState = false;

	private SoundEffect AircraftSaveSound;

	public AircraftInfoOverlay(object OSobj)
	{
		os = (OS)OSobj;
		Computer computer = Programs.getComputer(os, "dair_crash");
		CrashingAircraft = (AircraftDaemon)computer.getDaemon(typeof(AircraftDaemon));
		AircraftDaemon crashingAircraft = CrashingAircraft;
		Action crashAction = crashingAircraft.CrashAction;
		Action b = delegate
		{
			CrashingAircraft = null;
		};
		crashingAircraft.CrashAction = (Action)Delegate.Combine(crashAction, b);
		Computer computer2 = Programs.getComputer(os, "dair_secondary");
		SecondaryAircraft = (AircraftDaemon)computer2.getDaemon(typeof(AircraftDaemon));
		AircraftDaemon secondaryAircraft = SecondaryAircraft;
		secondaryAircraft.CrashAction = (Action)Delegate.Combine(secondaryAircraft.CrashAction, (Action)delegate
		{
			SecondaryAircraft = null;
		});
		AircraftSaveSound = os.content.Load<SoundEffect>("SFX/TraceKill");
	}

	public void Activate()
	{
		IsActive = true;
		timeElapsed = 0f;
		flashInTimeLeft = 1f;
		CrashingAircraft.StartUpdating();
		SecondaryAircraft.StartUpdating();
		os.Flags.AddFlag("AircraftInfoOverlayActivated");
	}

	public void Update(float dt)
	{
		flashInTimeLeft = Math.Max(0f, flashInTimeLeft - dt);
		timeElapsed += dt;
		if (!IsMonitoringDLCEndingCases)
		{
			return;
		}
		if (CrashingAircraft != null)
		{
			if (CrashingAircraft.IsInCriticalFirmwareFailure)
			{
				TargetHasStartedCrashing = true;
				IsInPostSaveState = false;
				double totalSeconds = MediaPlayer.PlayPosition.TotalSeconds;
				double num = 131.0;
				double num2 = 1.0 / (num / 60.0);
				double num3 = num2 * 4.0;
				double num4 = num2 * 2.0;
				double num5 = ((totalSeconds < 58.0) ? 999.0 : ((totalSeconds < 117.0) ? num3 : num4));
				if ((totalSeconds + num2 / 2.0) % num5 < num2 / 4.0)
				{
					os.warningFlash();
				}
			}
			else if (TargetHasStartedCrashing)
			{
				if (!os.Flags.HasFlag("DLC_PlaneResult"))
				{
					RunnableConditionalActions.LoadIntoOS("DLC/ActionScripts/FinaleSaveActions.xml", os);
					os.Flags.AddFlag("DLC_PlaneSaveResponseTriggered");
					os.Flags.AddFlag("DLC_PlaneResult");
				}
				if (!CrashingAircraft.IsInCriticalDescent() && !MediaPlayer.IsRepeating)
				{
					MusicManager.FADE_TIME = 6f;
					MusicManager.transitionToSong("DLC/Music/RemiDrone");
					MediaPlayer.IsRepeating = true;
					os.delayer.Post(ActionDelayer.Wait(2.0), delegate
					{
						AircraftSaveSound.Play();
					});
					IsInPostSaveState = true;
				}
			}
		}
		else if (TargetHasStartedCrashing)
		{
			if (SecondaryAircraft == null || SecondaryAircraft.IsInCriticalFirmwareFailure)
			{
				if (!os.Flags.HasFlag("DLC_PlaneResult"))
				{
					RunnableConditionalActions.LoadIntoOS("DLC/ActionScripts/FinaleDoubleCrashActions.xml", os);
					os.Flags.AddFlag("DLC_DoubleCrashResponseTriggered");
					os.Flags.AddFlag("DLC_PlaneResult");
				}
			}
			else if (!os.Flags.HasFlag("DLC_PlaneResult"))
			{
				RunnableConditionalActions.LoadIntoOS("DLC/ActionScripts/FinaleCrashActions.xml", os);
				os.Flags.AddFlag("DLC_PlaneCrashedResponseTriggered");
				os.Flags.AddFlag("DLC_PlaneResult");
			}
			if (MusicManager.currentSongName != "DLC\\Music\\CrashTrack")
			{
				MusicManager.playSongImmediatley("DLC\\Music\\CrashTrack");
				MediaPlayer.IsRepeating = false;
			}
		}
		if (!MediaPlayer.IsRepeating && MediaPlayer.State != MediaState.Playing && !IsInPostSaveState)
		{
			MusicManager.FADE_TIME = 6f;
			MissionFunctions.runCommand(7, "changeSongDLC");
			MediaPlayer.IsRepeating = true;
		}
	}

	public void Draw(Rectangle dest, SpriteBatch sb)
	{
		if (IsActive && Utils.randm(1f) >= flashInTimeLeft)
		{
			if (CrashingAircraft != null)
			{
				AircraftAltitudeIndicator.RenderAltitudeIndicator(dest, sb, (int)CrashingAircraft.CurrentAltitude, CrashingAircraft.IsInCriticalDescent(), AircraftAltitudeIndicator.GetFlashRateFromTimer(os.timer));
			}
			else
			{
				AircraftAltitudeIndicator.RenderAltitudeIndicator(dest, sb, 0, IsInCriticalDescenet: true, AircraftAltitudeIndicator.GetFlashRateFromTimer(os.timer));
			}
		}
	}
}
