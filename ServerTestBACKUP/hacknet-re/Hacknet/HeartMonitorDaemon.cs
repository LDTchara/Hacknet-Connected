using System;
using System.Collections.Generic;
using Hacknet.Daemons.Helpers;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class HeartMonitorDaemon : Daemon
{
	private enum HeartMonitorState
	{
		Welcome,
		MainDisplay,
		SecondaryLogin,
		SecondaryLoginRunning,
		Error,
		FirmwareScreen,
		FirmwareScreenConfirm,
		FirmwareScreenLoading,
		FirmwareScreenComplete
	}

	private const string ActiveFirmwareFilename = "LiveFirmware.dll";

	private const string FolderName = "KBT_Pacemaker";

	private const string LiveFolderName = "Active";

	private const string SubLoginUsername = "EAdmin";

	private const string SubLoginPass = "tens86";

	private const float MinFirmwareLoadTime = 10f;

	private const float TimeToDeathInDanger = 21f;

	private const float DyingDangerTime = 6f;

	private const float DeadBeepSustainFadeOut = 16f;

	private const float DeadBeepSustainFadeoutStartDelay = 5f;

	private Texture2D Heart;

	private Texture2D OxyIcon;

	private Texture2D WarnIcon;

	private Effect blufEffect;

	private RenderTarget2D bloomTarget;

	private RenderTarget2D priorTarget;

	private RenderTarget2D secondaryBloomTarget;

	private List<Action<int, int, SpriteBatch>> PostBloomDrawCalls = new List<Action<int, int, SpriteBatch>>();

	private SpriteBatch BlurContentSpritebatch;

	private Color bloomColor = new Color(10, 10, 10, 0);

	private Color heartColor = new Color(247, 237, 125);

	private bool HasSecondaryLogin = false;

	public string PatientID = "UNKNOWN";

	private bool PatientInCardiacArrest = false;

	private bool PatientDead = false;

	private float PatientTimeInDanger = 0f;

	private float timeDead = 0f;

	private float timeTillNextHeartbeat = 0f;

	private float timeBetweenHeartbeats = 0.88235295f;

	private float beatTime = 0.18f;

	private SoundEffect beepSound;

	private SoundEffectInstance beepSustainSound;

	private float volume = 0.4f;

	private float opTransition = 0f;

	private bool opOpening = false;

	private string loginUsername = null;

	private string loginPass = null;

	private float firmwareLoadTime = 0f;

	private int selectedFirmwareIndex = 0;

	private bool isConfirmingSelection = false;

	private string selectedFirmwareName = "";

	private string selectedFirmwareData = "";

	private BasicMedicalMonitor HeartMonitor;

	private BasicMedicalMonitor BPMonitor;

	private BasicMedicalMonitor SPMonitor;

	private List<IMedicalMonitor> Monitors = new List<IMedicalMonitor>();

	private float projectionFowardsTime = 0.3f;

	private float timeSinceLastHeartBeat = float.MaxValue;

	private int HeartRate = 0;

	private float currentSPO2 = 0f;

	private float averageSPO2 = 0f;

	private int reportedSP02 = 95;

	private float timeSinceNormalHeartRate = 0f;

	private float alarmHeartOKTimer = 0f;

	private HeartMonitorState State = HeartMonitorState.Welcome;

	private float timeThisState = 0f;

	public HeartMonitorDaemon(Computer c, OS os)
		: base(c, LocaleTerms.Loc("Remote Monitor"), os)
	{
		blufEffect = os.content.Load<Effect>("Shaders/DOFBlur");
		blufEffect.CurrentTechnique = blufEffect.Techniques["SmoothGaussBlur"];
		Heart = os.content.Load<Texture2D>("Sprites/Icons/Heart");
		OxyIcon = os.content.Load<Texture2D>("Sprites/Icons/O2Img");
		WarnIcon = os.content.Load<Texture2D>("Sprites/Icons/CautionIcon");
		beepSound = os.content.Load<SoundEffect>("SFX/HeartMonitorBeep");
		beepSustainSound = os.content.Load<SoundEffect>("SFX/HeartMonitorSustain").CreateInstance();
		beepSustainSound.IsLooped = true;
		beepSustainSound.Volume = volume;
		SetUpMonitors();
	}

	public override string getSaveString()
	{
		return "<HeartMonitor patient=\"" + PatientID + "\" />";
	}

	public override void initFiles()
	{
		base.initFiles();
		Folder folder = comp.files.root.searchForFolder("KBT_Pacemaker");
		if (folder == null)
		{
			folder = new Folder("KBT_Pacemaker");
			comp.files.root.folders.Add(folder);
		}
		Folder folder2 = folder.searchForFolder("Active");
		if (folder2 == null)
		{
			folder2 = new Folder("Active");
		}
		folder.folders.Add(folder2);
		FileEntry item = new FileEntry(PortExploits.ValidPacemakerFirmware, "KBT_Firmware_v1.2.dll");
		folder.files.Add(item);
		FileEntry fileEntry = new FileEntry(PortExploits.ValidPacemakerFirmware, "LiveFirmware.dll");
		folder2.files.Add(item);
	}

	private void SetUpMonitors()
	{
		HeartMonitor = new BasicMedicalMonitor(delegate(float lastVal, float dt)
		{
			List<BasicMedicalMonitor.MonitorRecordKeypoint> list6 = new List<BasicMedicalMonitor.MonitorRecordKeypoint>();
			BasicMedicalMonitor.MonitorRecordKeypoint item11 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item11.timeOffset = dt;
			item11.value = lastVal;
			float num = (PatientDead ? 0.05f : 0.25f);
			if (lastVal > num)
			{
				item11.value -= dt * 0.5f;
			}
			else if (lastVal < -1f * num)
			{
				item11.value += dt * 0.5f * (PatientDead ? (0.5f * Math.Max(1f - timeDead / 16f, 0f)) : 1f);
			}
			else
			{
				item11.value += (Utils.randm(0.2f) - 0.1f) * 0.3f;
			}
			list6.Add(item11);
			return list6;
		}, delegate(float lastVal, float dt)
		{
			List<BasicMedicalMonitor.MonitorRecordKeypoint> list5 = new List<BasicMedicalMonitor.MonitorRecordKeypoint>();
			if (!PatientDead)
			{
				BasicMedicalMonitor.MonitorRecordKeypoint item8 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
				item8.timeOffset = dt / 3f;
				item8.value = -0.8f + (Utils.randm(0.2f) - 0.1f);
				list5.Add(item8);
				BasicMedicalMonitor.MonitorRecordKeypoint item9 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
				item9.timeOffset = dt / 3f;
				item9.value = 0.9f + (Utils.randm(0.1f) - 0.05f);
				list5.Add(item9);
				BasicMedicalMonitor.MonitorRecordKeypoint item10 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
				item10.timeOffset = dt / 3f;
				item10.value = Utils.randm(0.1f * dt) - 0.05f;
				list5.Add(item10);
			}
			return list5;
		});
		Monitors.Add(HeartMonitor);
		BPMonitor = new BasicMedicalMonitor(delegate(float lastVal, float dt)
		{
			List<BasicMedicalMonitor.MonitorRecordKeypoint> list4 = new List<BasicMedicalMonitor.MonitorRecordKeypoint>();
			BasicMedicalMonitor.MonitorRecordKeypoint item7 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item7.timeOffset = dt;
			item7.value = lastVal;
			if (lastVal > 0.25f)
			{
				item7.value -= dt * 0.5f;
			}
			else if (lastVal < -0.25f)
			{
				item7.value += dt * 0.5f;
			}
			else
			{
				item7.value += (Utils.randm(0.2f) - 0.1f) * 0.3f * (PatientDead ? (0.5f * Math.Max(1f - timeDead / 16f, 0f)) : 1f);
			}
			list4.Add(item7);
			return list4;
		}, delegate(float lastVal, float dt)
		{
			List<BasicMedicalMonitor.MonitorRecordKeypoint> list3 = new List<BasicMedicalMonitor.MonitorRecordKeypoint>();
			BasicMedicalMonitor.MonitorRecordKeypoint item4 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item4.timeOffset = dt / 3f;
			item4.value = -0.8f + (Utils.randm(0.2f) - 0.1f);
			list3.Add(item4);
			BasicMedicalMonitor.MonitorRecordKeypoint item5 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item5.timeOffset = dt / 3f;
			item5.value = 0.9f + (Utils.randm(0.1f) - 0.05f);
			list3.Add(item5);
			BasicMedicalMonitor.MonitorRecordKeypoint item6 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item6.timeOffset = dt / 3f;
			item6.value = Utils.randm(0.1f * dt) - 0.05f;
			list3.Add(item6);
			return list3;
		});
		Monitors.Add(BPMonitor);
		SPMonitor = new BasicMedicalMonitor(delegate(float lastVal, float dt)
		{
			List<BasicMedicalMonitor.MonitorRecordKeypoint> list2 = new List<BasicMedicalMonitor.MonitorRecordKeypoint>();
			BasicMedicalMonitor.MonitorRecordKeypoint item3 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item3.timeOffset = dt;
			item3.value = lastVal;
			if (lastVal > 0.8f)
			{
				item3.value += (Utils.randm(0.2f) - 0.1f) * 0.3f;
			}
			else
			{
				item3.value += 0.15f * (float)(Utils.random.NextDouble() * Utils.random.NextDouble()) * (PatientDead ? (0.5f * Math.Max(1f - timeDead / 16f, 0f)) : 1f);
			}
			list2.Add(item3);
			return list2;
		}, delegate(float lastVal, float dt)
		{
			List<BasicMedicalMonitor.MonitorRecordKeypoint> list = new List<BasicMedicalMonitor.MonitorRecordKeypoint>();
			BasicMedicalMonitor.MonitorRecordKeypoint item = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item.timeOffset = dt * 0.7f;
			item.value = -0.6f - Utils.randm(0.4f);
			list.Add(item);
			BasicMedicalMonitor.MonitorRecordKeypoint item2 = default(BasicMedicalMonitor.MonitorRecordKeypoint);
			item2.timeOffset = dt * 0.3f;
			item2.value = item.value - Utils.randm(0.15f) - 0.05f;
			list.Add(item2);
			return list;
		});
		Monitors.Add(SPMonitor);
	}

	private void UpdateReports(float dt)
	{
		timeSinceLastHeartBeat += dt;
		currentSPO2 = 1f - SPMonitor.GetCurrentValue(projectionFowardsTime);
		averageSPO2 = averageSPO2 * 0.96f + currentSPO2 * 0.04f;
		reportedSP02 = Math.Min(100, 90 + (int)(5f * averageSPO2 + 0.5f));
		if (HeartRate > 120 || HeartRate < 50)
		{
			timeSinceNormalHeartRate += dt;
			return;
		}
		alarmHeartOKTimer += dt;
		if (alarmHeartOKTimer > 10f)
		{
			timeSinceNormalHeartRate = 0f;
			alarmHeartOKTimer = 0f;
		}
	}

	private void UpdateReportsForHeartbeat()
	{
		HeartRate = (int)(60f / timeSinceLastHeartBeat + 0.5f);
		timeSinceLastHeartBeat = 0f;
	}

	private void ChangeState(HeartMonitorState newState)
	{
		if (State == HeartMonitorState.MainDisplay && newState == HeartMonitorState.MainDisplay)
		{
			return;
		}
		if (newState == HeartMonitorState.MainDisplay)
		{
			if (State != 0)
			{
				opOpening = false;
				opTransition = 0f;
			}
			else
			{
				opOpening = false;
				opTransition = 2f;
			}
		}
		else if (State == HeartMonitorState.MainDisplay)
		{
			opOpening = true;
			opTransition = 0f;
		}
		State = newState;
		timeThisState = 0f;
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		ChangeState(HeartMonitorState.Welcome);
		if (os.Flags.HasFlag(PatientID + ":DEAD"))
		{
			PatientDead = true;
		}
	}

	private void UpdateStates(float dt)
	{
		timeThisState += dt;
		switch (State)
		{
		case HeartMonitorState.MainDisplay:
			opTransition += (opOpening ? 1f : 2f) * dt;
			break;
		case HeartMonitorState.SecondaryLogin:
		case HeartMonitorState.SecondaryLoginRunning:
		case HeartMonitorState.Error:
		case HeartMonitorState.FirmwareScreen:
		case HeartMonitorState.FirmwareScreenConfirm:
			opTransition += dt;
			break;
		case HeartMonitorState.FirmwareScreenLoading:
			opTransition += dt;
			firmwareLoadTime += (float)((double)dt * Utils.random.NextDouble());
			if (firmwareLoadTime > 10f)
			{
				EnactFirmwareChange();
				ChangeState(HeartMonitorState.FirmwareScreenComplete);
				firmwareLoadTime = 0f;
			}
			break;
		case HeartMonitorState.FirmwareScreenComplete:
			opTransition += dt;
			firmwareLoadTime += dt;
			if (firmwareLoadTime > 3.3333333f)
			{
				ChangeState(HeartMonitorState.MainDisplay);
			}
			break;
		}
	}

	public void ForceStopBeepSustainSound()
	{
		if (beepSustainSound != null)
		{
			beepSustainSound.Stop();
		}
	}

	private void Update(float dt)
	{
		os.delayer.Post(ActionDelayer.Wait(os.lastGameTime.ElapsedGameTime.TotalSeconds * 1.999), delegate
		{
			if (os.display.command != name && PatientDead)
			{
				beepSustainSound.Stop(immediate: true);
			}
		});
		if (PatientInCardiacArrest)
		{
			PatientTimeInDanger += dt;
			if (PatientTimeInDanger > 21f)
			{
				PatientDead = true;
				HeartRate = 0;
				os.Flags.AddFlag(PatientID + ":DEAD");
				beepSustainSound.Play();
			}
			else
			{
				float num = 3.5f;
				if (PatientTimeInDanger - dt < num && PatientTimeInDanger >= num && os.currentMission.postingTitle == "Project Junebug")
				{
					MusicManager.FADE_TIME = 10f;
					MusicManager.transitionToSong("Music/Ambient/dark_drone_008");
				}
			}
		}
		else
		{
			PatientTimeInDanger = 0f;
		}
		timeTillNextHeartbeat -= dt;
		if (timeTillNextHeartbeat <= 0f && !PatientDead)
		{
			timeTillNextHeartbeat = timeBetweenHeartbeats + (Utils.randm(0.1f) - 0.05f);
			if (PatientInCardiacArrest)
			{
				if (PatientTimeInDanger > 15f)
				{
					timeTillNextHeartbeat = 0.36f - 0.25f * (PatientTimeInDanger / 21f);
				}
				else
				{
					timeTillNextHeartbeat = Utils.randm(timeBetweenHeartbeats * 2f);
				}
			}
			projectionFowardsTime += beatTime + dt;
			for (int i = 0; i < Monitors.Count; i++)
			{
				Monitors[i].HeartBeat(beatTime);
			}
			UpdateReportsForHeartbeat();
			if (State != 0)
			{
				os.delayer.Post(ActionDelayer.Wait(projectionFowardsTime + beatTime / 4f), delegate
				{
					beepSound.Play(volume, 0f, 0f);
				});
			}
		}
		else if (projectionFowardsTime > 0.3f)
		{
			projectionFowardsTime -= dt;
		}
		else
		{
			for (int i = 0; i < Monitors.Count; i++)
			{
				Monitors[i].Update(dt);
			}
		}
		if (PatientDead)
		{
			timeDead += dt;
			if (timeDead > 5f)
			{
				float num2 = 1f - (timeDead - 5f) / 16f;
				num2 *= volume;
				num2 = Math.Max(0f, num2);
				beepSustainSound.Volume = num2;
			}
		}
		UpdateStates(dt);
		UpdateReports(dt);
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		if (bloomTarget == null || bloomTarget.Width != bounds.Width || bloomTarget.Height != bounds.Height)
		{
			bloomTarget = new RenderTarget2D(sb.GraphicsDevice, bounds.Width, bounds.Height);
			secondaryBloomTarget = new RenderTarget2D(sb.GraphicsDevice, bounds.Width, bounds.Height);
		}
		if (BlurContentSpritebatch == null)
		{
			BlurContentSpritebatch = new SpriteBatch(sb.GraphicsDevice);
		}
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		PostBloomDrawCalls.Clear();
		StartBloomDraw(BlurContentSpritebatch);
		Rectangle rectangle = bounds;
		rectangle.X = (rectangle.Y = 0);
		SpriteBatch spriteBatch = GuiData.spriteBatch;
		GuiData.spriteBatch = BlurContentSpritebatch;
		DrawStates(rectangle, BlurContentSpritebatch);
		GuiData.spriteBatch = spriteBatch;
		EndBloomDraw(bounds, rectangle, sb, BlurContentSpritebatch);
		for (int i = 0; i < PostBloomDrawCalls.Count; i++)
		{
			PostBloomDrawCalls[i](bounds.X, bounds.Y, sb);
		}
		TextItem.DrawShadow = drawShadow;
	}

	private void DrawStates(Rectangle bounds, SpriteBatch sb)
	{
		switch (State)
		{
		case HeartMonitorState.Welcome:
			DrawWelcomeScreen(bounds, sb);
			break;
		default:
			DrawSegments(bounds, sb);
			break;
		}
		if (State != 0)
		{
			DrawOptionsPanel(bounds, sb);
		}
	}

	private void StartBloomDraw(SpriteBatch sb)
	{
		priorTarget = (RenderTarget2D)sb.GraphicsDevice.GetRenderTargets()[0].RenderTarget;
		sb.GraphicsDevice.SetRenderTarget(bloomTarget);
		sb.GraphicsDevice.Clear(Color.Transparent);
		sb.Begin();
	}

	private void EndBloomDraw(Rectangle bounds, Rectangle zeroedBounds, SpriteBatch mainSB, SpriteBatch bloomContentSpritebatch)
	{
		bloomContentSpritebatch.End();
		mainSB.GraphicsDevice.SetRenderTarget(secondaryBloomTarget);
		mainSB.GraphicsDevice.Clear(Color.Transparent);
		bloomContentSpritebatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullNone, blufEffect);
		zeroedBounds.X -= 2;
		bloomContentSpritebatch.Draw(bloomTarget, zeroedBounds, bloomColor);
		zeroedBounds.X += 4;
		bloomContentSpritebatch.Draw(bloomTarget, zeroedBounds, bloomColor);
		zeroedBounds.X -= 2;
		zeroedBounds.Y -= 2;
		bloomContentSpritebatch.Draw(bloomTarget, zeroedBounds, bloomColor);
		zeroedBounds.Y += 4;
		bloomContentSpritebatch.Draw(bloomTarget, zeroedBounds, bloomColor);
		bloomContentSpritebatch.End();
		mainSB.GraphicsDevice.SetRenderTarget(priorTarget);
		mainSB.Draw(bloomTarget, bounds, Color.White);
		mainSB.Draw(secondaryBloomTarget, bounds, Color.White);
	}

	public void DrawSegments(Rectangle bounds, SpriteBatch sb)
	{
		int num = 26;
		Rectangle rectangle = bounds;
		rectangle.Height = bounds.Height / 4 - 4;
		DrawGraph(HeartMonitor, rectangle, heartColor, sb);
		Rectangle rectangle2 = rectangle;
		rectangle2.Y += rectangle2.Height + 2;
		Rectangle HRM_DisplayBounds = rectangle2;
		HRM_DisplayBounds.Width = rectangle2.Width / 3;
		bool showingHeartIcon = timeSinceLastHeartBeat > beatTime * 1.6f && timeSinceLastHeartBeat < 4f * beatTime;
		PostBloomDrawCalls.Add(delegate(int x, int y, SpriteBatch sprBatch)
		{
			Rectangle bounds5 = HRM_DisplayBounds;
			bounds5.X += x;
			bounds5.Y += y;
			DrawMonitorNumericalDisplay(bounds5, "HR", string.Concat(HeartRate), sprBatch, heartColor, showingHeartIcon ? Heart : null);
		});
		Rectangle statusMonitorBounds = rectangle2;
		statusMonitorBounds.Width -= HRM_DisplayBounds.Width + 8 + num;
		statusMonitorBounds.X += HRM_DisplayBounds.Width + 4 + num;
		PostBloomDrawCalls.Add(delegate(int x, int y, SpriteBatch sprBatch)
		{
			Rectangle bounds4 = statusMonitorBounds;
			bounds4.X += x;
			bounds4.Y += y;
			string value = "OK";
			Color col = Color.Gray;
			Texture2D icon = null;
			if (timeSinceNormalHeartRate > 0.5f)
			{
				if (timeSinceNormalHeartRate > 4f)
				{
					value = "DANGER";
					col = Color.Red;
					if (os.timer % 0.3f < 0.1f)
					{
						icon = WarnIcon;
					}
				}
				else
				{
					value = "WARN";
					col = Color.Yellow;
					if (os.timer % 0.5f < 0.2f)
					{
						icon = WarnIcon;
					}
				}
			}
			DrawMonitorStatusPanelDisplay(bounds4, "ALARM", value, sprBatch, col, icon);
		});
		Color BPColor = default(Color);
		ref Color reference = ref BPColor;
		reference = new Color(148, 231, 243);
		Rectangle BP_DisplayBounds = HRM_DisplayBounds;
		BP_DisplayBounds.Y += BP_DisplayBounds.Height + 4;
		PostBloomDrawCalls.Add(delegate(int x, int y, SpriteBatch sprBatch)
		{
			Rectangle bounds3 = BP_DisplayBounds;
			bounds3.X += x;
			bounds3.Y += y;
			DrawMonitorNumericalDisplay(bounds3, "BP", "133 : 97\n\n  (109)", sprBatch, BPColor);
		});
		Rectangle rectangle3 = statusMonitorBounds;
		rectangle3.Y += rectangle3.Height + 4;
		DrawGraph(BPMonitor, rectangle3, BPColor, sb);
		Color SPColor = default(Color);
		ref Color reference2 = ref SPColor;
		reference2 = new Color(165, 241, 138);
		Rectangle SP_DisplayBounds = BP_DisplayBounds;
		SP_DisplayBounds.Y += SP_DisplayBounds.Height + 4;
		PostBloomDrawCalls.Add(delegate(int x, int y, SpriteBatch sprBatch)
		{
			Rectangle bounds2 = SP_DisplayBounds;
			bounds2.X += x;
			bounds2.Y += y;
			DrawMonitorNumericalDisplay(bounds2, "Sp02", string.Concat(reportedSP02), sprBatch, SPColor, (reportedSP02 >= 91) ? OxyIcon : null);
		});
		Rectangle dest = rectangle3;
		dest.Y += dest.Height + 4;
		DrawGraph(SPMonitor, dest, SPColor, sb);
	}

	private void DrawMonitorNumericalDisplay(Rectangle bounds, string display, string value, SpriteBatch sb, Color col, Texture2D icon = null)
	{
		float num = 45f;
		Vector2 vector = TextItem.doMeasuredFontLabel(new Vector2((float)bounds.X + 2f, (float)bounds.Y + 2f), display, GuiData.font, col, (float)bounds.Width - 12f, num);
		int num2 = 8;
		sb.Draw(destinationRectangle: new Rectangle((int)((float)bounds.X + vector.X + 9f), bounds.Y + num2, 8, (int)num - 2 * num2), texture: Utils.white, color: Color.DarkRed);
		if (icon != null)
		{
			sb.Draw(icon, new Rectangle(bounds.X + 2, (int)((float)bounds.Y + vector.Y + 2f), (int)num, (int)num), col);
		}
		TextItem.doFontLabelToSize(new Rectangle(bounds.X + bounds.Width / 3, bounds.Y + bounds.Height / 3, (int)((double)bounds.Width * 0.6), (int)((double)bounds.Height * 0.6)), value, GuiData.titlefont, col);
		sb.Draw(destinationRectangle: new Rectangle(bounds.X + 4, bounds.Y + bounds.Height + 2, bounds.Width - 8, 1), texture: Utils.white, color: Color.Gray);
	}

	private void DrawMonitorStatusPanelDisplay(Rectangle bounds, string display, string value, SpriteBatch sb, Color col, Texture2D icon = null)
	{
		float num = 45f;
		Vector2 vector = TextItem.doMeasuredFontLabel(new Vector2((float)bounds.X + 2f, (float)bounds.Y + 2f), display, GuiData.font, col, (float)bounds.Width - 12f, num);
		int num2 = 8;
		sb.Draw(destinationRectangle: new Rectangle((int)((float)bounds.X + vector.X + 9f), bounds.Y + num2, 8, (int)num - 2 * num2), texture: Utils.white, color: Color.DarkRed);
		if (icon != null)
		{
			sb.Draw(icon, new Rectangle(bounds.X + 2, (int)((float)bounds.Y + vector.Y + 2f), (int)num, (int)num), col);
		}
		TextItem.doFontLabelToSize(new Rectangle(bounds.X + (int)((double)num * 1.5), bounds.Y + bounds.Height / 3, (int)((double)bounds.Width * 0.3), (int)((double)bounds.Height * 0.3)), value, GuiData.titlefont, col);
		sb.Draw(destinationRectangle: new Rectangle(bounds.X + 4, bounds.Y + bounds.Height + 2, bounds.Width - 8, 1), texture: Utils.white, color: Color.Gray);
	}

	private void DrawGraph(IMedicalMonitor monitor, Rectangle dest, Color col, SpriteBatch sb, bool drawUnderline = true)
	{
		monitor.Draw(dest, sb, col, projectionFowardsTime);
		if (drawUnderline)
		{
			PostBloomDrawCalls.Add(delegate(int x, int y, SpriteBatch sprBatch)
			{
				sprBatch.Draw(destinationRectangle: new Rectangle(dest.X + 4 + x, dest.Y + dest.Height + 2 + y, dest.Width - 8, 1), texture: Utils.white, color: Color.Gray);
			});
		}
	}

	private void EnactFirmwareChange()
	{
		PatientInCardiacArrest = selectedFirmwareData == PortExploits.DangerousPacemakerFirmware || selectedFirmwareData == PortExploits.DangerousPacemakerFirmwareLRNG;
	}

	public void DrawWelcomeScreen(Rectangle bounds, SpriteBatch sb)
	{
		bool hasAdmin = comp.adminIP == os.thisComputer.ip;
		Color graphColor = Color.Red;
		graphColor.A = 100;
		if (hasAdmin)
		{
			ref Color reference = ref graphColor;
			reference = new Color(20, 200, 20, 100);
		}
		Rectangle heartRateDisplay = default(Rectangle);
		ref Rectangle reference2 = ref heartRateDisplay;
		reference2 = new Rectangle(bounds.X + 10, bounds.Y + bounds.Height / 4, bounds.Width - 20, bounds.Height / 4);
		DrawGraph(HeartMonitor, heartRateDisplay, graphColor, sb, drawUnderline: false);
		heartRateDisplay.Y += heartRateDisplay.Height + 10;
		string adminMsg = (hasAdmin ? LocaleTerms.Loc("Admin Access Granted") : LocaleTerms.Loc("Admin Access Required"));
		adminMsg = adminMsg.ToUpper();
		heartRateDisplay.Height = 20;
		DrawLinedMessage(adminMsg, graphColor * 0.2f, heartRateDisplay, sb);
		Rectangle nextToButonsLineThing = default(Rectangle);
		ref Rectangle reference3 = ref nextToButonsLineThing;
		reference3 = new Rectangle(heartRateDisplay.X + heartRateDisplay.Width / 4 - 18, heartRateDisplay.Y + heartRateDisplay.Height + 10, 14, bounds.Height / 3);
		sb.Draw(Utils.white, nextToButonsLineThing, graphColor * 0.2f);
		PostBloomDrawCalls.Add(delegate(int x, int y, SpriteBatch sprBatch)
		{
			Rectangle dest = heartRateDisplay;
			dest.X += x;
			dest.Y += y;
			DrawLinedMessage(adminMsg, graphColor, dest, sprBatch);
			nextToButonsLineThing.X += x;
			nextToButonsLineThing.Y += y;
			sprBatch.Draw(Utils.white, nextToButonsLineThing, graphColor);
			dest.Y += dest.Height + 10;
			dest.Width /= 2;
			dest.X += dest.Width / 2;
			dest.Height = 28;
			dest.Height *= 2;
			sprBatch.DrawString(GuiData.font, comp.name, new Vector2(dest.X, dest.Y), Color.White);
			sprBatch.DrawString(GuiData.detailfont, "Kellis Biotech\nB-Type Pacemaker v2.44", new Vector2(dest.X + 2, dest.Y + 30), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0.5f);
			dest.Y += dest.Height + 6;
			dest.Height /= 2;
			if (Button.doButton(686868001, dest.X, dest.Y, dest.Width, dest.Height + 6, LocaleTerms.Loc("View Monitor"), hasAdmin ? os.highlightColor : Color.Gray) && hasAdmin)
			{
				ChangeState(HeartMonitorState.MainDisplay);
			}
			dest.Y += dest.Height + 10 + 6;
			if (Button.doButton(686868003, dest.X, dest.Y, dest.Width, dest.Height, LocaleTerms.Loc("Admin Login"), (!hasAdmin) ? os.highlightColor : Color.Gray) && !hasAdmin)
			{
				os.runCommand("login");
			}
			dest.Y += dest.Height + 10;
			if (Button.doButton(686868005, dest.X, dest.Y, dest.Width, dest.Height, LocaleTerms.Loc("Exit"), os.brightLockedColor))
			{
				os.display.command = "connect";
			}
		});
	}

	private void DrawLinedMessage(string message, Color col, Rectangle dest, SpriteBatch sb)
	{
		int num = 16;
		SpriteFont smallfont = GuiData.smallfont;
		Vector2 vector = smallfont.MeasureString(message);
		vector.X += num;
		dest.Height = (int)(vector.Y + 0.5f);
		Rectangle destinationRectangle = new Rectangle(dest.X, dest.Y + dest.Height / 2 - 1, dest.Width / 2 - (int)(vector.X / 2f), 2);
		sb.Draw(Utils.white, destinationRectangle, col);
		sb.DrawString(smallfont, message, new Vector2(destinationRectangle.X + destinationRectangle.Width + num / 2, destinationRectangle.Y - dest.Height / 2), col);
		destinationRectangle.X = dest.X + dest.Width - destinationRectangle.Width;
		sb.Draw(Utils.white, destinationRectangle, col);
	}

	private void DrawOptionsPanel(Rectangle bounds, SpriteBatch spritebatch)
	{
		int buttonWidth = 120;
		float num = (float)(bounds.Width - buttonWidth) * 0.7f;
		float num2 = Math.Min(1f, opTransition);
		if (!opOpening)
		{
			num2 = 1f - num2;
		}
		num2 = Utils.QuadraticOutCurve(num2);
		bool ShowingContent = num2 >= 0.98f;
		float contentFade = 0f;
		float num3 = 0.5f;
		if (ShowingContent)
		{
			contentFade = Math.Min(1f + num3, opTransition) - num3;
		}
		int num4 = (int)(num2 * num) + buttonWidth;
		Rectangle panelArea = default(Rectangle);
		ref Rectangle reference = ref panelArea;
		reference = new Rectangle(bounds.X + bounds.Width - num4, bounds.Y + bounds.Height / 10, num4, bounds.Height - bounds.Height / 5);
		Rectangle buttonSourceRect = panelArea;
		PostBloomDrawCalls.Add(delegate(int x, int y, SpriteBatch sprBatch)
		{
			Rectangle rectangle = buttonSourceRect;
			rectangle.X += x;
			rectangle.Y += y;
			int num5 = bounds.Height / 4 + 12;
			rectangle.Y += num5 - bounds.Height / 10;
			int num6 = 30;
			Rectangle destinationRectangle = rectangle;
			destinationRectangle.Width = buttonWidth;
			destinationRectangle.Height = num6;
			destinationRectangle.X -= 2;
			sprBatch.Draw(Utils.white, destinationRectangle, Utils.VeryDarkGray);
			if (Button.doButton(83838001, destinationRectangle.X, destinationRectangle.Y, buttonWidth, num6, LocaleTerms.Loc("Login"), (State != HeartMonitorState.SecondaryLogin) ? os.highlightColor : Color.Black))
			{
				ChangeState(HeartMonitorState.SecondaryLogin);
			}
			destinationRectangle.Y += num6 + 2;
			sprBatch.Draw(Utils.white, destinationRectangle, Utils.VeryDarkGray);
			if (Button.doButton(83838003, destinationRectangle.X, destinationRectangle.Y, buttonWidth, num6, LocaleTerms.Loc("Firmware"), (State != HeartMonitorState.FirmwareScreen) ? os.highlightColor : Color.Black))
			{
				ChangeState(HeartMonitorState.FirmwareScreen);
			}
			destinationRectangle.Y += num6 + 2;
			sprBatch.Draw(Utils.white, destinationRectangle, Utils.VeryDarkGray);
			if (Button.doButton(83838006, destinationRectangle.X, destinationRectangle.Y, buttonWidth, num6, LocaleTerms.Loc("Monitor"), (State != HeartMonitorState.MainDisplay) ? os.highlightColor : Color.Black))
			{
				ChangeState(HeartMonitorState.MainDisplay);
			}
			destinationRectangle.Y += num6 + 2;
			sprBatch.Draw(Utils.white, destinationRectangle, Utils.VeryDarkGray);
			if (Button.doButton(83838009, destinationRectangle.X, destinationRectangle.Y, buttonWidth, num6, LocaleTerms.Loc("Exit"), os.lockedColor))
			{
				ChangeState(HeartMonitorState.Welcome);
			}
			destinationRectangle.Y += num6 + 2;
			panelArea.X += x;
			panelArea.Y += y;
			panelArea.X += buttonWidth - 3;
			panelArea.Width -= buttonWidth;
			if (opOpening || opTransition < 1f)
			{
				panelArea.Width += 2;
			}
			sprBatch.Draw(Utils.white, panelArea, os.outlineColor);
			int num7 = 3;
			panelArea.X += num7;
			panelArea.Width -= 2 * num7;
			panelArea.Y += num7;
			panelArea.Height -= 2 * num7;
			sprBatch.Draw(Utils.white, panelArea, os.indentBackgroundColor);
			if (ShowingContent)
			{
				DrawOptionsPanelContent(panelArea, sprBatch, contentFade);
			}
		});
	}

	private Rectangle DrawOptionsPanelHeaders(Rectangle bounds, SpriteBatch sb, float contentFade)
	{
		Rectangle rectangle = bounds;
		rectangle.Height = 30;
		rectangle.X += 2;
		rectangle.Width -= 4;
		rectangle.Y += 2;
		string message = LocaleTerms.Loc("Firmware Config");
		DrawLinedMessage(message, os.highlightColor * contentFade, rectangle, sb);
		rectangle.X -= 2;
		DrawLinedMessage(message, os.highlightColor * contentFade * 0.2f, rectangle, sb);
		rectangle.X += 4;
		DrawLinedMessage(message, os.highlightColor * contentFade * 0.2f, rectangle, sb);
		rectangle.X -= 2;
		rectangle.Y += rectangle.Height - 10;
		rectangle.Height = 54;
		string text = LocaleTerms.Loc("Firmware Administration") + "\n" + LocaleTerms.Loc("Access") + " : ";
		Color color = os.brightLockedColor;
		if (HasSecondaryLogin)
		{
			text = text + " " + LocaleTerms.Loc("GRANTED");
			color = os.brightUnlockedColor;
		}
		else
		{
			text = text + " " + LocaleTerms.Loc("DENIED");
		}
		color *= contentFade;
		sb.DrawString(GuiData.font, text, new Vector2(rectangle.X, rectangle.Y), color, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0.6f);
		rectangle.Y += rectangle.Height;
		sb.DrawString(GuiData.tinyfont, LocaleTerms.Loc("Secondary security layer for firmware read/write access"), new Vector2(rectangle.X, rectangle.Y), color * 0.7f, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0.6f);
		rectangle.Y += 15;
		rectangle.Height = 1;
		sb.Draw(Utils.white, rectangle, Color.Gray * contentFade * (float)(Utils.random.NextDouble() * 0.15 + 0.2800000011920929));
		return new Rectangle(bounds.X, rectangle.Y, bounds.Width, bounds.Y + bounds.Height - rectangle.Y - rectangle.Height);
	}

	private void DrawOptionsPanelContent(Rectangle bounds, SpriteBatch sb, float contentFade)
	{
		Rectangle bounds2 = DrawOptionsPanelHeaders(bounds, sb, contentFade);
		switch (State)
		{
		default:
			DrawOptionsPanelLoginContent(bounds2, sb, contentFade);
			break;
		case HeartMonitorState.Error:
			break;
		case HeartMonitorState.FirmwareScreen:
		case HeartMonitorState.FirmwareScreenConfirm:
		case HeartMonitorState.FirmwareScreenLoading:
		case HeartMonitorState.FirmwareScreenComplete:
			DrawOptionsPanelFirmwareContent(bounds2, sb, contentFade);
			break;
		}
	}

	private void DrawOptionsPanelFirmwareContent(Rectangle bounds, SpriteBatch sb, float contentFade)
	{
		Color color = heartColor;
		color.A = 0;
		if (!HasSecondaryLogin)
		{
			Rectangle dest = new Rectangle(bounds.X + bounds.Width / 4, bounds.Y, bounds.Width / 2, bounds.Height);
			TextItem.doFontLabelToSize(dest, LocaleTerms.Loc("Firmware Administration") + "\n" + LocaleTerms.Loc("Access Required") + "\n" + LocaleTerms.Loc("Log In First"), GuiData.font, color);
			return;
		}
		switch (State)
		{
		case HeartMonitorState.FirmwareScreenConfirm:
			return;
		case HeartMonitorState.FirmwareScreenLoading:
		{
			Rectangle bounds2 = bounds;
			bounds2.X++;
			bounds2.Width -= 2;
			bounds2.Height = 110;
			DrawSelectedFirmwareFileDetails(bounds2, sb, selectedFirmwareData, selectedFirmwareName);
			bounds2.Y += bounds2.Height + 2;
			Rectangle rectangle2 = bounds;
			int num = bounds2.Height + 10;
			rectangle2.Height -= num;
			rectangle2.Y += num;
			Rectangle destinationRectangle = rectangle2;
			destinationRectangle.Height = 1;
			float num2 = firmwareLoadTime / 10f;
			color.A = 0;
			for (int i = 0; i < rectangle2.Height; i += 3)
			{
				float num3 = (float)i / (float)rectangle2.Height;
				if (num2 > num3)
				{
					destinationRectangle.Y = rectangle2.Y + i;
					sb.Draw(Utils.white, destinationRectangle, color);
				}
			}
			return;
		}
		case HeartMonitorState.FirmwareScreenComplete:
		{
			Rectangle rectangle = bounds;
			rectangle.X += 2;
			rectangle.Width -= 4;
			rectangle.Y += 2;
			rectangle.Height -= 4;
			sb.Draw(Utils.white, rectangle, os.brightLockedColor * 0.3f * contentFade);
			rectangle.X += 40;
			rectangle.Width -= 80;
			TextItem.doFontLabelToSize(rectangle, LocaleTerms.Loc("FIRMWARE UPDATE COMPLETE"), GuiData.font, Color.White);
			return;
		}
		}
		Folder folder = comp.files.root.searchForFolder("KBT_Pacemaker");
		int num4 = folder.files.Count + 1;
		string[] array = new string[num4];
		array[0] = LocaleTerms.Loc("Currently Active Firmware");
		if (folder.files.Count > 0)
		{
			for (int i = 0; i < folder.files.Count; i++)
			{
				array[i + 1] = folder.files[i].name;
			}
		}
		selectedFirmwareIndex = SelectableTextList.doFancyList(8937001, bounds.X + 2, bounds.Y + 10, (int)((float)bounds.Width - 4f), bounds.Height / 3, array, selectedFirmwareIndex, os.topBarColor);
		Rectangle rectangle3 = new Rectangle(bounds.X + 2, bounds.Y + 10 + bounds.Height / 3 + 4, bounds.Width - 4, bounds.Height / 4);
		string data = ((selectedFirmwareIndex != 0) ? folder.files[selectedFirmwareIndex - 1].data : null);
		string filename = ((selectedFirmwareIndex != 0) ? folder.files[selectedFirmwareIndex - 1].name : LocaleTerms.Loc("Currently Active Firmware"));
		bool flag = DrawSelectedFirmwareFileDetails(rectangle3, sb, data, filename);
		rectangle3.Y += rectangle3.Height + 6;
		if (!flag || selectedFirmwareIndex == 0)
		{
			return;
		}
		rectangle3.Height = 30;
		if (!isConfirmingSelection)
		{
			if (Button.doButton(8937004, rectangle3.X, rectangle3.Y, rectangle3.Width, rectangle3.Height, LocaleTerms.Loc("Activate This Firmware"), os.highlightColor))
			{
				isConfirmingSelection = true;
			}
			return;
		}
		DrawLinedMessage(LocaleTerms.Loc("Confirm Firmware Activation"), os.brightLockedColor, rectangle3, sb);
		rectangle3.Y += rectangle3.Height;
		if (Button.doButton(8937008, rectangle3.X, rectangle3.Y, rectangle3.Width, rectangle3.Height, LocaleTerms.Loc("Confirm Activation"), os.highlightColor))
		{
			selectedFirmwareName = filename;
			selectedFirmwareData = data;
			ChangeState(HeartMonitorState.FirmwareScreenLoading);
			firmwareLoadTime = 0f;
			isConfirmingSelection = false;
		}
		rectangle3.Y += rectangle3.Height + 4;
		rectangle3.Height = 20;
		if (Button.doButton(8937009, rectangle3.X, rectangle3.Y, rectangle3.Width, rectangle3.Height, LocaleTerms.Loc("Cancel"), os.lockedColor))
		{
			ChangeState(HeartMonitorState.FirmwareScreen);
			isConfirmingSelection = false;
		}
	}

	private bool DrawSelectedFirmwareFileDetails(Rectangle bounds, SpriteBatch sb, string data, string filename)
	{
		bool flag = data == null || IsValidFirmwareData(data);
		Rectangle dest = bounds;
		dest.Height = 28;
		DrawLinedMessage(flag ? LocaleTerms.Loc("Valid Firmware File") : LocaleTerms.Loc("Invalid Firmware File"), flag ? heartColor : os.brightLockedColor, dest, sb);
		dest.Y += dest.Height - 4;
		Color color = Color.White;
		if (!flag)
		{
			color = Color.Gray;
		}
		TextItem.doFontLabel(new Vector2((float)dest.X + 6f, dest.Y), filename, GuiData.font, color, dest.Width, dest.Height);
		dest.Y += dest.Height + 2;
		dest.Height = 14;
		string text = LocaleTerms.Loc("Invalid binary package") + "\n" + LocaleTerms.Loc("Valid firmware packages must be") + "\n" + LocaleTerms.Loc("digitally signed by an authorized manufacturer");
		if (flag)
		{
			text = LocaleTerms.Loc("Valid binary package") + "\n" + LocaleTerms.Loc("Signed by") + " : KELLIS BIOTECH\n" + LocaleTerms.Loc("Compiled by") + " : EIDOLON SOFT";
		}
		TextItem.doFontLabel(new Vector2((float)dest.X + 6f, dest.Y), text, GuiData.detailfont, color * 0.7f, dest.Width - 12);
		Rectangle destinationRectangle = bounds;
		destinationRectangle.Y += destinationRectangle.Height - 1;
		destinationRectangle.Height = 1;
		sb.Draw(Utils.white, destinationRectangle, Color.Gray);
		return flag;
	}

	private bool IsValidFirmwareData(string data)
	{
		return data == PortExploits.DangerousPacemakerFirmware || data == PortExploits.ValidPacemakerFirmware;
	}

	private void DrawOptionsPanelLoginContent(Rectangle bounds, SpriteBatch sb, float contentFade)
	{
		string data = LocaleTerms.Loc("A secondary login is required to review and modify running firmware. Personal login details are provided for each chip. If you have lost your login details, connect your support program to our content server") + " (111.105.22.1)";
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + 10, bounds.Width - 4, 24);
		data = Utils.SuperSmartTwimForWidth(data, rectangle.Width, GuiData.detailfont);
		sb.DrawString(GuiData.detailfont, data, new Vector2(rectangle.X, rectangle.Y), Color.Gray * contentFade);
		rectangle.Y += (int)(GuiData.detailfont.MeasureString(data).Y + 10f);
		if (State == HeartMonitorState.SecondaryLoginRunning || HasSecondaryLogin)
		{
			Rectangle rectangle2 = rectangle;
			rectangle2.Height = bounds.Height / 4;
			int height = rectangle2.Height;
			if (loginUsername != null)
			{
				rectangle2.Height = 60;
				GetStringUIControl.DrawGetStringControlInactive(LocaleTerms.Loc("Username") + " : ", loginUsername, rectangle2, sb, os, "");
				rectangle2.Y += rectangle2.Height + 2;
			}
			if (loginPass != null)
			{
				rectangle2.Height = 60;
				GetStringUIControl.DrawGetStringControlInactive(LocaleTerms.Loc("Password") + " : ", loginPass, rectangle2, sb, os, "");
				rectangle2.Y += rectangle2.Height + 2;
			}
			rectangle2.Height = height;
			if (loginPass == null || loginUsername == null)
			{
				string text = GetStringUIControl.DrawGetStringControl((loginUsername == null) ? (LocaleTerms.Loc("Username") + " : ") : (LocaleTerms.Loc("Password") + " : "), rectangle2, delegate
				{
					loginUsername = (loginPass = "");
				}, delegate
				{
					loginUsername = (loginPass = "");
				}, sb, os, os.highlightColor, os.lockedColor, "");
				if (text != null)
				{
					if (loginUsername == null)
					{
						loginUsername = text;
						GetStringUIControl.StartGetString(LocaleTerms.Loc("Password"), os);
					}
					else
					{
						loginPass = text;
					}
				}
				rectangle2.Y += rectangle2.Height + 10;
				return;
			}
			rectangle2.Y += 20;
			if (loginUsername == "EAdmin" && loginPass == "tens86")
			{
				HasSecondaryLogin = true;
			}
			else
			{
				HasSecondaryLogin = false;
			}
			rectangle2.Height = 20;
			Color col = (HasSecondaryLogin ? os.brightUnlockedColor : os.brightLockedColor);
			DrawLinedMessage(HasSecondaryLogin ? LocaleTerms.Loc("Login Complete") : LocaleTerms.Loc("Login Failed"), col, rectangle2, sb);
			rectangle2.Y += rectangle2.Height + 20;
			if (!HasSecondaryLogin)
			{
				if (Button.doButton(92923008, rectangle2.X + 4, rectangle2.Y, rectangle2.Width - 80, 24, LocaleTerms.Loc("Retry Login"), null))
				{
					ChangeState(HeartMonitorState.SecondaryLogin);
				}
			}
			else if (Button.doButton(92923009, rectangle2.X + 4, rectangle2.Y, rectangle2.Width - 80, 24, LocaleTerms.Loc("Administrate Firmware"), os.highlightColor))
			{
				ChangeState(HeartMonitorState.FirmwareScreen);
			}
		}
		else if (!HasSecondaryLogin && ButtonFlashForContentFade(contentFade) && Button.doButton(92923001, rectangle.X, rectangle.Y, (int)((float)rectangle.Width * 0.7f), 30, LocaleTerms.Loc("Login"), os.highlightColor))
		{
			loginUsername = (loginPass = null);
			GetStringUIControl.StartGetString(LocaleTerms.Loc("Username"), os);
			ChangeState(HeartMonitorState.SecondaryLoginRunning);
		}
	}

	private bool ButtonFlashForContentFade(float contentFade)
	{
		if (contentFade > 0.95f)
		{
			return true;
		}
		return Utils.random.NextDouble() > (double)contentFade;
	}
}
