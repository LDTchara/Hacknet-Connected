using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class DLCIntroExe : ExeModule
{
	private enum IntroState
	{
		NotStarted,
		SpinningUp,
		Flickering,
		MailIconPhasingOut,
		AssignMission1,
		OnMission1,
		AssignMission2,
		OnMission2,
		Outro,
		Exiting
	}

	private const bool combineNodeExplodeAndMailBurst = false;

	private static float SpinUpTime = 13.8f;

	private static float FlickeringTime = 10f;

	private static float MailIconFlickerOutTime = 3.82f;

	private static float AssignMission1Time = 16f;

	private static float AssignMission2Time = 16f;

	private static float WindDownTimeAfterCompletingMission = 0.8f;

	private static float NodeImpactEffectTransOutTime = 3f;

	private static float NodeImpactEffectTransInTime = 2f;

	private Color themeColor = new Color(38, 201, 155, 220);

	private IntroState State = IntroState.NotStarted;

	private float TimeInThisState = 0f;

	private float percentageThroughThisState = 0f;

	private Texture2D circle;

	private Texture2D circleOutline;

	private OSTheme originalTheme = OSTheme.HacknetBlue;

	private List<TraceKillExe.PointImpactEffect> ImpactEffects = new List<TraceKillExe.PointImpactEffect>();

	private float timeBetweenNodeRemovals = 1f;

	private float timeSinceNodeRemoved = 0f;

	private Color originalTopBarIconsColor;

	private string assignment1MissionPath = "Content/DLC/Missions/Intro/KaguyaTrialMission1.xml";

	private string assignment2MissionPath = "Content/DLC/Missions/Intro/KaguyaTrialMission2.xml";

	private string Assignment1Text = "";

	private string Assignment2Text = "";

	private string AssignmentsCompleteText = "";

	private int charsRenderedSoFar = 0;

	private ActiveMission LoadedMission;

	private bool IsOnAssignment1 = true;

	private bool AllAssignmentsComplete = false;

	private bool MissionIsComplete = false;

	private string PhaseTitle = "";

	private string PhaseSubtitle = "";

	private HexGridBackground BackgroundEffect;

	private bool OSTraceTimerOverrideActive = false;

	private SoundEffect GlowSound;

	private SoundEffect BreakSound;

	private ExplodingUIElementEffect explosion = new ExplodingUIElementEffect();

	public DLCIntroExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "KaguyaTrial";
		ramCost = 190;
		IdentifierName = "KaguyaTrial";
		targetIP = os.thisComputer.ip;
		circle = os.content.Load<Texture2D>("Circle");
		circleOutline = os.content.Load<Texture2D>("CircleOutlineLarge");
		Assignment1Text = Utils.readEntireFile("Content/DLC/Docs/KaguyaTrial1.txt");
		Assignment2Text = Utils.readEntireFile("Content/DLC/Docs/KaguyaTrial2.txt");
		AssignmentsCompleteText = Utils.readEntireFile("Content/DLC/Docs/KaguyaTrialComplete.txt");
		BackgroundEffect = new HexGridBackground(os.content);
		explosion.Init(os.content);
		GlowSound = os.content.Load<SoundEffect>("SFX/Ending/PorthackSpindown");
		BreakSound = os.content.Load<SoundEffect>("SFX/DoomShock");
		if (MusicManager.currentSongName != "DLC\\Music\\snidelyWhiplash" && !os.Flags.HasFlag("KaguyaTrialComplete"))
		{
			MusicManager.transitionToSong("Music/Ambient/dark_drone_008");
		}
	}

	public override void Killed()
	{
		base.Killed();
		if (State == IntroState.NotStarted || State == IntroState.Exiting)
		{
			return;
		}
		os.delayer.Post(ActionDelayer.NextTick(), delegate
		{
			DLCIntroExe dLCIntroExe = new DLCIntroExe(bounds, os, new string[0])
			{
				State = State,
				TimeInThisState = TimeInThisState,
				LoadedMission = LoadedMission,
				IsOnAssignment1 = IsOnAssignment1,
				AllAssignmentsComplete = AllAssignmentsComplete,
				MissionIsComplete = MissionIsComplete,
				PhaseTitle = PhaseTitle,
				PhaseSubtitle = PhaseSubtitle
			};
			if (OSTraceTimerOverrideActive)
			{
				os.traceCompleteOverrideAction = null;
				OS oS = os;
				oS.traceCompleteOverrideAction = (Action)Delegate.Combine(oS.traceCompleteOverrideAction, new Action(dLCIntroExe.PlayerLostToTraceTimer));
			}
			os.addExe(dLCIntroExe);
		});
	}

	private void UpdateState(float t)
	{
		TimeInThisState += t;
		switch (State)
		{
		case IntroState.SpinningUp:
			if (TimeInThisState >= SpinUpTime)
			{
				PrepareForUIBreakdown();
				State = IntroState.Flickering;
				PhaseTitle = LocaleTerms.Loc("INITIALIZING");
				PhaseSubtitle = "---";
				TimeInThisState = 0f;
				os.execute("dc");
				os.execute("clear");
				os.warningFlash();
			}
			percentageThroughThisState = TimeInThisState / SpinUpTime;
			break;
		case IntroState.Flickering:
			if (TimeInThisState >= FlickeringTime)
			{
				State = IntroState.MailIconPhasingOut;
				TimeInThisState = 0f;
				SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed, 100f);
				os.execute("clear");
				os.warningFlash();
			}
			percentageThroughThisState = TimeInThisState / FlickeringTime;
			break;
		case IntroState.MailIconPhasingOut:
			if (TimeInThisState >= MailIconFlickerOutTime)
			{
				CompleteMailPhaseOut();
				State = IntroState.AssignMission1;
				TimeInThisState = 0f;
				os.warningFlash();
			}
			percentageThroughThisState = TimeInThisState / MailIconFlickerOutTime;
			break;
		case IntroState.AssignMission1:
			percentageThroughThisState = TimeInThisState / AssignMission1Time;
			break;
		case IntroState.OnMission1:
		case IntroState.OnMission2:
			CheckProgressOfCurrentAssignment();
			break;
		case IntroState.AssignMission2:
			percentageThroughThisState = TimeInThisState / AssignMission2Time;
			break;
		}
		explosion.Update(t);
	}

	private void PlayerLostToTraceTimer()
	{
		os.runCommand("dc");
		os.runCommand("clear");
		os.delayer.Post(ActionDelayer.NextTick(), delegate
		{
			os.write("-----------------\n\nKT_OVERRIDE_RECOVERY: " + LocaleTerms.Loc("Critical Error - resetting.") + "\n\n----------------");
			State = IntroState.AssignMission1;
			TimeInThisState = 0f;
		});
	}

	private void CompleteExecution()
	{
		PhaseTitle = LocaleTerms.Loc("COMPLETE");
		PhaseSubtitle = "---";
		State = IntroState.Exiting;
		isExiting = true;
		os.traceCompleteOverrideAction = null;
		if (!os.Flags.HasFlag("KaguyaTrialComplete"))
		{
			os.Flags.AddFlag("KaguyaTrialComplete");
			os.allFactions.setCurrentFaction("Bibliotheque", os);
			os.homeNodeID = "dhs";
			os.homeAssetServerID = "dhsDrop";
			MissionFunctions.runCommand(1, "addRankSilent");
			os.currentMission = null;
			Computer computer = Programs.getComputer(os, "dhs");
			DLCHubServer dLCHubServer = computer.getDaemon(typeof(DLCHubServer)) as DLCHubServer;
			dLCHubServer.AddAgent(os.defaultUser.name, "dnkA19ds", new Color(222, 153, 24));
			for (int i = 0; i < computer.users.Count; i++)
			{
				UserDetail value = computer.users[i];
				if (value.name == os.defaultUser.name)
				{
					value.known = true;
					computer.users[i] = value;
				}
			}
		}
		os.runCommand("connect 69.58.186.114");
		os.IsInDLCMode = true;
		os.DisableEmailIcon = false;
		isExiting = true;
	}

	private void UpdateMailePhaseOut(float t)
	{
		AddRadialMailLine();
		AddRadialMailLine();
		if (percentageThroughThisState > 0.2f)
		{
			AddRadialMailLine();
			AddRadialMailLine();
		}
		if (TimeInThisState % 0.6f <= t)
		{
			SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed, 100f + 200f * percentageThroughThisState);
		}
		os.topBarIconsColor = ((Utils.randm(1f) < percentageThroughThisState) ? Color.Red : originalTopBarIconsColor);
		Utils.FillEverywhereExcept(Utils.InsetRectangle(os.terminal.Bounds, 1), Utils.GetFullscreen(), spriteBatch, Color.Black * (0.8f * percentageThroughThisState));
	}

	private void CompleteMailPhaseOut()
	{
		os.DisableEmailIcon = true;
		SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed * 0.8f, 100f);
		for (int i = 0; i < 12; i++)
		{
			double time = (double)i / 7.0;
			os.delayer.Post(ActionDelayer.Wait(time), delegate
			{
				SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed * 0.8f, 400f);
			});
		}
		Vector2 mailIconPos = os.mailicon.pos;
		explosion.Explode(1500, new Vector2(-0.1f, 3.2415926f), mailIconPos, 1f, 8f, 100f, 1600f, 1000f, 1200f, 3f, 7f);
		os.delayer.Post(ActionDelayer.Wait(0.1), delegate
		{
			explosion.Explode(100, new Vector2(-0.1f, 3.2415926f), mailIconPos, 1f, 6f, 100f, 1300f, 1000f, 1300f, 3f, 7f);
		});
		BreakSound.Play();
		os.topBarIconsColor = originalTopBarIconsColor;
		PostProcessor.EndingSequenceFlashOutActive = false;
		PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
		os.terminal.reset();
	}

	private void PrepareForUIBreakdown()
	{
		originalTheme = ThemeManager.currentTheme;
		ThemeManager.setThemeOnComputer(os.thisComputer, "DLC/Themes/MiamiTheme.xml");
		ThemeManager.switchTheme(os, "DLC/Themes/MiamiTheme.xml");
		timeBetweenNodeRemovals = FlickeringTime / 2f / (float)os.netMap.visibleNodes.Count;
		os.netMap.CleanVisibleListofDuplicates();
		originalTopBarIconsColor = os.topBarIconsColor;
	}

	private void AddRadialMailLine()
	{
		SFX.AddRadialLine(os.mailicon.pos + new Vector2(20f, 10f), (float)(Math.PI + (double)Utils.rand((float)Math.PI)), 600f + Utils.randm(300f), 800f, 500f, 200f + Utils.randm(400f), 0.35f, Color.Lerp(Utils.makeColor(100, 0, 0, byte.MaxValue), Utils.AddativeRed, Utils.randm(1f)), 3f);
	}

	private void UpdateUIFlickerIn()
	{
		float num = 0.4f;
		float num2 = Math.Min(1f, percentageThroughThisState * (1f / num));
		OSTheme theme = OSTheme.Custom;
		if (percentageThroughThisState < 0.99f)
		{
			if (Utils.randm(1f) < num2)
			{
				ThemeManager.switchTheme(os, theme);
			}
			else
			{
				ThemeManager.switchThemeColors(os, originalTheme);
				ThemeManager.loadThemeBackground(os, originalTheme);
				ThemeManager.currentTheme = originalTheme;
			}
		}
		PostProcessor.EndingSequenceFlashOutActive = true;
		PostProcessor.EndingSequenceFlashOutPercentageComplete = 1f - num2;
		if (percentageThroughThisState > 0.7f)
		{
			AddRadialMailLine();
		}
	}

	private void UpdateUIBreaking(float t)
	{
		float num = 0.6f;
		float num2 = Math.Max(0f, Math.Min(1f, (percentageThroughThisState - (1f - num)) * (1f / num)));
		num2 = percentageThroughThisState;
		if (!(num2 > 0f) || os.netMap.visibleNodes.Count <= 1)
		{
			return;
		}
		timeSinceNodeRemoved += t;
		if (!(timeSinceNodeRemoved > timeBetweenNodeRemovals))
		{
			return;
		}
		timeSinceNodeRemoved -= timeBetweenNodeRemovals;
		int num3 = 0;
		Computer computer;
		do
		{
			num3 = Utils.random.Next(os.netMap.visibleNodes.Count);
			computer = os.netMap.nodes[os.netMap.visibleNodes[num3]];
		}
		while (computer == os.thisComputer);
		Vector2 screenSpacePosition = computer.getScreenSpacePosition();
		OS oS = os;
		oS.PreDLCVisibleNodesCache = oS.PreDLCVisibleNodesCache + ((os.PreDLCVisibleNodesCache.Length > 0) ? "," : "") + os.netMap.visibleNodes[num3];
		os.netMap.nodes[os.netMap.visibleNodes[num3]].adminIP = computer.ip;
		os.netMap.visibleNodes.RemoveAt(num3);
		ImpactEffects.Add(new TraceKillExe.PointImpactEffect
		{
			location = screenSpacePosition,
			scaleModifier = 3f + ((computer.securityLevel > 2) ? 1f : 0f),
			cne = new ConnectedNodeEffect(os, intense: true),
			timeEnabled = 0f,
			HasHighlightCircle = true
		});
		if (computer.securityLevel > 3)
		{
			for (int i = 0; i < computer.securityLevel && i < 6; i++)
			{
				ImpactEffects.Add(new TraceKillExe.PointImpactEffect
				{
					location = screenSpacePosition,
					scaleModifier = Math.Min(8, i),
					cne = new ConnectedNodeEffect(os, intense: true),
					timeEnabled = 0f,
					HasHighlightCircle = false
				});
			}
		}
	}

	private void DrawAssignmentPhase(float t)
	{
		Utils.FillEverywhereExcept(Utils.InsetRectangle(os.terminal.Bounds, 1), Utils.GetFullscreen(), spriteBatch, Color.Black * 0.8f);
		float point = 1f - Math.Min(2f, TimeInThisState / 2f) / 2f;
		point = Utils.CubicInCurve(point);
		float num = 200f;
		Rectangle rectangle = Utils.InsetRectangle(os.terminal.bounds, (int)(-1f * num * point));
		float num2 = 1f - point;
		if (num2 >= 0.8f)
		{
			num2 = (1f - (num2 - 0.8f) * 5f) * 0.8f;
		}
		RenderedRectangle.doRectangleOutline(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, (int)(60f * (0.06f + point)), os.highlightColor * num2);
		string text = AssignmentsCompleteText;
		if (!AllAssignmentsComplete)
		{
			text = (IsOnAssignment1 ? Assignment1Text : Assignment2Text);
		}
		charsRenderedSoFar = TextWriterTimed.WriteTextToTerminal(text, os, 0.04f, 1f, 20f, TimeInThisState, charsRenderedSoFar);
		if (charsRenderedSoFar >= text.Length)
		{
			StartAssignment();
		}
	}

	private void StartAssignment()
	{
		if (!AllAssignmentsComplete)
		{
			if (IsOnAssignment1)
			{
				PhaseTitle = "74.125.23.121";
				PhaseSubtitle = LocaleTerms.Loc("Download Tools") + "\n" + LocaleTerms.Loc("Delete System Files");
			}
			else
			{
				PhaseTitle = "216.239.32.181";
				PhaseSubtitle = LocaleTerms.Loc("Adapt") + "\n" + LocaleTerms.Loc("Advance");
			}
			State = (IsOnAssignment1 ? IntroState.OnMission1 : IntroState.OnMission2);
			TimeInThisState = 0f;
			LoadedMission = (ActiveMission)ComputerLoader.readMission(IsOnAssignment1 ? assignment1MissionPath : assignment2MissionPath);
		}
		else
		{
			CompleteExecution();
		}
	}

	private void CheckProgressOfCurrentAssignment()
	{
		if (MissionIsComplete)
		{
			if (TimeInThisState >= WindDownTimeAfterCompletingMission)
			{
				MissionWasCompleted();
			}
		}
		else if (os.connectedComp == null && LoadedMission.isComplete())
		{
			TimeInThisState = 0f;
			MissionIsComplete = true;
		}
	}

	private void MissionWasCompleted()
	{
		if (IsOnAssignment1)
		{
			State = IntroState.AssignMission2;
		}
		else
		{
			State = IntroState.Outro;
			AllAssignmentsComplete = true;
			GlowSound.Play();
			MusicManager.transitionToSong("Music/Ambient/AmbientDrone_Clipped");
			for (int i = 0; i < os.exes.Count; i++)
			{
				if (os.exes[i] is ShellExe)
				{
					(os.exes[i] as ShellExe).Killed();
					(os.exes[i] as ShellExe).isExiting = true;
				}
			}
		}
		charsRenderedSoFar = 0;
		TimeInThisState = 0f;
		MissionIsComplete = false;
		if (IsOnAssignment1)
		{
			IsOnAssignment1 = false;
		}
		for (int i = 0; i < os.exes.Count; i++)
		{
			if (os.exes[i] != this)
			{
				os.exes[i].isExiting = true;
			}
		}
		os.execute("clear");
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		UpdateState(t);
		Rectangle dest = new Rectangle(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - (Module.PANEL_HEIGHT + 4));
		Rectangle dest2 = new Rectangle(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 10, bounds.Width - 4, bounds.Height - (Module.PANEL_HEIGHT + 6));
		switch (State)
		{
		case IntroState.NotStarted:
		{
			BackgroundEffect.Update(t);
			BackgroundEffect.Draw(dest2, spriteBatch, Color.Black, themeColor * 0.2f);
			int num7 = 30;
			spriteBatch.Draw(Utils.white, new Rectangle(bounds.X + 10, bounds.Y + bounds.Height / 2 - num7 / 2, bounds.Width - 20, num7), Color.Black);
			if (!os.Flags.HasFlag("KaguyaTrialComplete"))
			{
				if (Button.doButton(8310101 + PID, bounds.X + 10, bounds.Y + bounds.Height / 2 - num7 / 2, bounds.Width - 20, num7, LocaleTerms.Loc("BEGIN TRIAL"), os.highlightColor))
				{
					State = IntroState.SpinningUp;
					TimeInThisState = 0f;
					MusicManager.stop();
					MusicManager.playSongImmediatley("DLC\\Music\\snidelyWhiplash");
					os.mailicon.isEnabled = false;
					os.thisComputer.links.Clear();
					OS oS = os;
					oS.traceCompleteOverrideAction = (Action)Delegate.Combine(oS.traceCompleteOverrideAction, new Action(PlayerLostToTraceTimer));
					OSTraceTimerOverrideActive = true;
				}
			}
			else
			{
				TextItem.doCenteredFontLabel(dest, LocaleTerms.Loc("Trials Locked"), GuiData.font, Color.White);
				if (Button.doButton(8310101 + PID, dest.X + 10, dest.Y + dest.Height - 22, dest.Width - 20, 18, "Exit", os.lockedColor))
				{
					isExiting = true;
				}
			}
			break;
		}
		case IntroState.SpinningUp:
		{
			Utils.LCG.reSeed(PID);
			Rectangle destinationRectangle = new Rectangle(dest.X, dest.Y, dest.Width, 1);
			for (int i = 0; i < dest.Height; i++)
			{
				float num2 = Utils.LCG.NextFloatScaled() * SpinUpTime;
				float num3 = Math.Min(1f, TimeInThisState / num2);
				if (Utils.LCG.NextFloatScaled() > 0.5f)
				{
					float num4 = 0.8f;
					float num5 = num3 * (1f - num4);
					if (num3 > num4)
					{
						float num6 = 1f - num5;
						float point = (num3 - num4) / (1f - num4);
						point = Utils.QuadraticOutCurve(point);
						num3 = num5 + num6 * point;
					}
					else
					{
						num3 = num5;
					}
				}
				else
				{
					num3 = Utils.QuadraticOutCurve(num3);
				}
				destinationRectangle.Y = dest.Y + i;
				destinationRectangle.Width = (int)(num3 * (float)dest.Width);
				Color color = Color.Lerp(Utils.AddativeWhite * 0.1f, themeColor, Utils.LCG.NextFloatScaled());
				spriteBatch.Draw(Utils.white, destinationRectangle, color);
			}
			break;
		}
		case IntroState.Flickering:
		{
			UpdateUIFlickerIn();
			UpdateUIBreaking(t);
			DrawPhaseTitle(t, dest2);
			bool flag = true;
			break;
		}
		case IntroState.MailIconPhasingOut:
			DrawPhaseTitle(t, dest2);
			UpdateMailePhaseOut(t);
			break;
		case IntroState.AssignMission1:
		case IntroState.AssignMission2:
		case IntroState.Outro:
			DrawPhaseTitle(t, dest2);
			DrawAssignmentPhase(t);
			break;
		case IntroState.OnMission1:
		case IntroState.OnMission2:
			DrawPhaseTitle(t, dest2);
			if (Settings.forceCompleteEnabled)
			{
				int num = 19;
				if (Button.doButton(8310102, bounds.X + 10, bounds.Y + num + 4, bounds.Width - 20, num, LocaleTerms.Loc("DEBUG: Skip"), os.highlightColor))
				{
					FileEntry item = new FileEntry(PortExploits.crackExeData[6881], PortExploits.cracks[6881]);
					os.thisComputer.files.root.searchForFolder("bin").files.Add(item);
					CompleteExecution();
				}
			}
			break;
		case IntroState.Exiting:
			DrawPhaseTitle(t, dest2);
			Utils.FillEverywhereExcept(Utils.InsetRectangle(os.terminal.Bounds, 1), Utils.GetFullscreen(), spriteBatch, Color.Black * 0.8f * (1f - Math.Min(1f, TimeInThisState)));
			break;
		}
		UpdateImpactEffects(t);
		DrawImpactEffects(ImpactEffects);
		explosion.Render(spriteBatch);
	}

	private void DrawPhaseTitle(float t, Rectangle dest)
	{
		BackgroundEffect.Update(t);
		BackgroundEffect.Draw(dest, spriteBatch, Color.Black, themeColor * 0.2f);
		int num = 40;
		if (dest.Height <= num)
		{
			return;
		}
		Rectangle rectangle = new Rectangle(dest.X, dest.Y + (int)((double)dest.Height / 2.8) - num / 2, dest.Width, num);
		spriteBatch.Draw(Utils.white, rectangle, Color.Black * 0.6f);
		TextItem.doFontLabelToSize(rectangle, PhaseTitle, Utils.GetTitleFontForLocalizedString(PhaseTitle), Color.White, doNotOversize: true);
		if (!isExiting)
		{
			string[] array = PhaseSubtitle.Split(Utils.newlineDelim);
			Rectangle rectangle2 = new Rectangle(rectangle.X, rectangle.Y + rectangle.Height + 2, rectangle.Width, 22);
			for (int i = 0; i < array.Length; i++)
			{
				spriteBatch.Draw(Utils.white, rectangle2, Color.Black * 0.4f);
				TextItem.doFontLabelToSize(rectangle2, array[i], GuiData.UISmallfont, Utils.AddativeWhite * 0.9f, doNotOversize: true);
				rectangle2.Y += rectangle2.Height + 2;
			}
		}
	}

	private void UpdateImpactEffects(float t)
	{
		for (int i = 0; i < ImpactEffects.Count; i++)
		{
			TraceKillExe.PointImpactEffect value = ImpactEffects[i];
			value.timeEnabled += t;
			if (value.timeEnabled > NodeImpactEffectTransInTime + NodeImpactEffectTransOutTime)
			{
				ImpactEffects.RemoveAt(i);
				i--;
			}
			else
			{
				ImpactEffects[i] = value;
			}
		}
	}

	private void DrawImpactEffects(List<TraceKillExe.PointImpactEffect> Effects)
	{
		Utils.LCG.reSeed(PID);
		for (int i = 0; i < Effects.Count; i++)
		{
			Color color = Color.Lerp(Utils.AddativeWhite, Utils.AddativeRed, 0.6f + 0.4f * Utils.LCG.NextFloatScaled()) * (0.6f + 0.4f * Utils.LCG.NextFloatScaled());
			TraceKillExe.PointImpactEffect pointImpactEffect = Effects[i];
			Vector2 location = pointImpactEffect.location;
			float num = Utils.QuadraticOutCurve(pointImpactEffect.timeEnabled / NodeImpactEffectTransInTime);
			float num2 = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(pointImpactEffect.timeEnabled / (NodeImpactEffectTransInTime + NodeImpactEffectTransOutTime)));
			float num3 = Utils.QuadraticOutCurve((pointImpactEffect.timeEnabled - NodeImpactEffectTransInTime) / NodeImpactEffectTransOutTime);
			pointImpactEffect.cne.color = color * num;
			pointImpactEffect.cne.ScaleFactor = num2 * pointImpactEffect.scaleModifier;
			if (pointImpactEffect.timeEnabled > NodeImpactEffectTransInTime)
			{
				pointImpactEffect.cne.color = color * (1f - num3);
			}
			if (num >= 0f && pointImpactEffect.HasHighlightCircle)
			{
				spriteBatch.Draw(circle, location, null, color * (1f - num - ((num3 >= 0f) ? (1f - num3) : 0f)), 0f, new Vector2(circle.Width / 2, circle.Height / 2), num / (float)circle.Width * 60f, SpriteEffects.None, 0.7f);
			}
			pointImpactEffect.cne.draw(spriteBatch, location);
		}
	}
}
