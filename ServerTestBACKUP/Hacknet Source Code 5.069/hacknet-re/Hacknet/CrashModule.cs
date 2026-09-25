using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class CrashModule : Module
{
	private const float BOOT_FAIL_CRASH_TIME = 15f;

	public static float BLUESCREEN_TIME = 8f;

	public static float BOOT_TIME = (Settings.isConventionDemo ? 5f : (Settings.FastBootText ? 1.2f : 14.5f));

	public static float BLACK_TIME = 2f;

	public static float POST_BLACK_TIME = 1f;

	public string BootLoadErrors = "";

	public float elapsedTime = 0f;

	public int state = 0;

	public Color bluescreenBlue = new Color(0, 0, 170);

	public Color bluescreenGrey = new Color(167, 167, 167);

	public Color textColor = new Color(0, 0, 255);

	private SpriteFont bsodFont;

	private static SoundEffect beep;

	private string bsodText = "";

	private string originalBootText;

	private string[] bootText;

	private int bootTextCount = 0;

	private float bootTextDelay = 1f;

	private float bootTextTimer = 0f;

	private float bootTextErrorDelay = 0f;

	private bool graphicsErrorsDetected = false;

	private bool hasPlayedBeep = false;

	private bool IsInHostileFileCrash = false;

	private int extraErrors = 0;

	public CrashModule(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		bounds = location;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		bsodFont = os.ScreenManager.Game.Content.Load<SpriteFont>("BSODFont");
		beep = os.content.Load<SoundEffect>("SFX/beep");
		char[] array = new char[1] { '\n' };
		StreamReader streamReader = new StreamReader(TitleContainer.OpenStream("Content/BSOD.txt"));
		bsodText = streamReader.ReadToEnd();
		streamReader.Close();
		streamReader = new StreamReader(TitleContainer.OpenStream("Content/OSXBoot.txt"));
		originalBootText = streamReader.ReadToEnd();
		streamReader.Close();
		loadBootText();
		bootTextDelay = BOOT_TIME / ((float)(bootText.Length - 1) * 2f);
	}

	private void loadBootText()
	{
		char[] separator = new char[1] { '\n' };
		bootText = checkOSBootFiles(originalBootText).Split(separator);
	}

	public override void Update(float t)
	{
		base.Update(t);
		elapsedTime += t;
		if (IsInHostileFileCrash)
		{
			if (elapsedTime % 0.5f < 1f / 30f)
			{
				extraErrors++;
			}
			if (elapsedTime >= 15f)
			{
				if (Settings.EnableDLC && DLC1SessionUpgrader.HasDLC1Installed)
				{
					os.BootAssitanceModule.IsActive = true;
				}
				reset();
				os.canRunContent = false;
				os.bootingUp = false;
			}
		}
		else if (elapsedTime < BLUESCREEN_TIME / 4f)
		{
			state = 0;
		}
		else if (elapsedTime < BLUESCREEN_TIME)
		{
			state = 1;
		}
		else if (elapsedTime < BLUESCREEN_TIME + BLACK_TIME)
		{
			state = 2;
		}
		else if (elapsedTime < BLUESCREEN_TIME + BLACK_TIME + BOOT_TIME + bootTextErrorDelay)
		{
			state = 3;
			bootTextTimer -= t;
			if (bootTextTimer <= 0f)
			{
				float num = bootTextDelay;
				float num2 = (float)Utils.random.NextDouble() * bootTextDelay;
				num -= num2;
				num2 = (float)Utils.random.NextDouble() * bootTextDelay;
				num += num2;
				bootTextTimer = num;
				bootTextCount++;
				if (bootTextCount >= bootText.Length - 1)
				{
					bootTextCount = bootText.Length - 1;
				}
				if (bootText[bootTextCount].Equals(" "))
				{
					bootTextTimer = bootTextDelay * 12f;
				}
				if (bootText[bootTextCount].StartsWith("ERROR:"))
				{
					bootTextTimer = bootTextDelay * 29f;
					os.thisComputer.bootupTick(0f - bootTextDelay * 42f);
					bootTextErrorDelay += bootTextDelay * 42f;
				}
				if (bootTextCount == 50 && HostileHackerBreakinSequence.IsInBlockingHostileFileState(os))
				{
					bootTextTimer = 999999f;
					os.thisComputer.bootTimer = 9999f;
					IsInHostileFileCrash = true;
					elapsedTime = 0.2f;
				}
			}
			if (hasPlayedBeep)
			{
				return;
			}
			if (!Settings.soundDisabled)
			{
				beep.Play(0.5f, 0.5f, 0f);
				os.delayer.Post(ActionDelayer.Wait(0.1), delegate
				{
					beep.Play(0.5f, 0.5f, 0f);
				});
			}
			hasPlayedBeep = true;
		}
		else
		{
			state = 2;
		}
	}

	public override void Draw(float t)
	{
		switch (state)
		{
		case 0:
			spriteBatch.Draw(Utils.white, bounds, bluescreenBlue);
			drawString((elapsedTime % 0.8f > 0.5f) ? "" : "_", new Vector2(bounds.X, bounds.Y + 10), bsodFont);
			break;
		case 1:
			spriteBatch.Draw(Utils.white, bounds, bluescreenBlue);
			drawString(bsodText, new Vector2(bounds.X, bounds.Y + 10), bsodFont);
			break;
		default:
			spriteBatch.Draw(Utils.white, bounds, os.darkBackgroundColor);
			break;
		case 3:
		{
			float num = GuiData.ActiveFontConfig.tinyFontCharHeight + 1f;
			spriteBatch.Draw(Utils.white, bounds, os.darkBackgroundColor);
			int num2 = Math.Min((int)(((float)(bounds.Height - 10) - num) / num), bootTextCount);
			num2 = bootTextCount;
			Vector2 dpos = new Vector2(bounds.X + 10, (float)num2 * num + 10f);
			float y = dpos.Y;
			if (dpos.Y > (float)(bounds.Y + bounds.Height - 14))
			{
				dpos.Y = bounds.Y + bounds.Height - 14;
			}
			while (num2 >= 0 && dpos.Y > num)
			{
				drawString(bootText[num2], dpos, GuiData.tinyfont);
				dpos.Y -= num;
				num2--;
			}
			if (IsInHostileFileCrash)
			{
				dpos.Y = y + num;
				drawString("ERROR: Critical boot error loading \"VMBootloaderTrap.dll\"", dpos, GuiData.tinyfont);
				for (int i = 0; i < extraErrors; i++)
				{
					dpos.Y += num;
					drawString("ERROR:", dpos, GuiData.tinyfont);
				}
			}
			break;
		}
		}
	}

	private Vector2 drawString(string text, Vector2 dpos, SpriteFont font)
	{
		if (IsInHostileFileCrash)
		{
			float point = elapsedTime / 15f;
			point = Utils.QuadraticOutCurve(point);
			text = Utils.FlipRandomChars(text, (double)point * 0.2 * (double)point * (double)point);
			string text2 = "";
			for (int i = 0; i < text.Length; i++)
			{
				text2 += ((Utils.randm(1f) < point * point * point) ? ' ' : text[i]);
			}
			text = text2;
		}
		Vector2 result = font.MeasureString(text);
		bool flag = text.StartsWith("ERROR:");
		spriteBatch.DrawString(font, text, dpos, flag ? Color.Red : Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		return result;
	}

	public string checkOSBootFiles(string bootString)
	{
		BootLoadErrors = "";
		Folder folder = os.thisComputer.files.root.searchForFolder("sys");
		bool flag = true;
		string text = "ERROR: " + LocaleTerms.Loc("Unable to Load System file os-config.sys") + "\n";
		if (folder.containsFile("os-config.sys"))
		{
			text = "Loaded os-config.sys : System Config Initialized";
		}
		else
		{
			os.failBoot();
			flag = false;
			BootLoadErrors = BootLoadErrors + text + " \n";
		}
		bootString = bootString.Replace("[OSBoot1]", text);
		text = "ERROR: " + LocaleTerms.Loc("Unable to Load System file bootcfg.dll") + "\n";
		if (folder.containsFile("bootcfg.dll"))
		{
			text = "Loaded bootcfg.dll : Boot Config Module Loaded";
		}
		else
		{
			os.failBoot();
			flag = false;
			BootLoadErrors = BootLoadErrors + text + " \n";
		}
		bootString = bootString.Replace("[OSBoot2]", text);
		text = "ERROR: " + LocaleTerms.Loc("Unable to Load System file netcfgx.dll") + "\n";
		if (folder.containsFile("netcfgx.dll"))
		{
			text = "Loaded netcfgx.dll : Network Config Module Loaded";
		}
		else
		{
			os.failBoot();
			flag = false;
			BootLoadErrors = BootLoadErrors + text + " \n";
		}
		bootString = bootString.Replace("[OSBoot3]", text);
		text = "ERROR: " + LocaleTerms.Loc("Unable to Load System file x-server.sys") + "\nERROR: " + LocaleTerms.Loc("Locate and restore a valid x-server file in ~/sys/ folder to restore UX functionality") + "\nERROR: " + LocaleTerms.Loc("Consider examining reports in ~/log/ for problem cause and source") + "\nERROR: " + LocaleTerms.Loc("System UX resources unavailable -- defaulting to terminal mode") + "\n .\n .\n .\n";
		if (folder.containsFile("x-server.sys"))
		{
			text = "Loaded x-server.sys : UX Graphics Module Loaded";
			ThemeManager.switchTheme(os, ThemeManager.getThemeForDataString(folder.searchForFile("x-server.sys").data));
			graphicsErrorsDetected = false;
		}
		else
		{
			os.graphicsFailBoot();
			flag = false;
			graphicsErrorsDetected = true;
			BootLoadErrors = BootLoadErrors + text + " \n";
		}
		bootString = bootString.Replace("[OSBootTheme]", text);
		if (flag)
		{
			if (os.Flags.HasFlag("BootFailure") && !os.Flags.HasFlag("BootFailureThemeSongChange") && ThemeManager.currentTheme != OSTheme.HacknetBlue)
			{
				os.Flags.AddFlag("BootFailureThemeSongChange");
				if (MusicManager.isPlaying)
				{
					MusicManager.stop();
				}
				MusicManager.loadAsCurrentSong("Music\\The_Quickening");
			}
			os.sucsesfulBoot();
		}
		else
		{
			os.Flags.AddFlag("BootFailure");
		}
		return bootString;
	}

	public void reset()
	{
		elapsedTime = 0f;
		state = 0;
		bootTextCount = 0;
		bootTextTimer = 0f;
		bootTextErrorDelay = 0f;
		hasPlayedBeep = false;
		IsInHostileFileCrash = false;
		extraErrors = 0;
		loadBootText();
		MusicManager.stop();
	}

	public void completeReboot()
	{
		os.terminal.reset();
		os.execute("FirstTimeInitdswhupwnemfdsiuoewnmdsmffdjsklanfeebfjkalnbmsdakj Init");
		os.inputEnabled = false;
		if (!graphicsErrorsDetected)
		{
			os.setMouseVisiblity(mouseIsVisible: true);
			MusicManager.playSong();
		}
		os.connectedComp = null;
		Folder folder = os.thisComputer.files.root.searchForFolder("sys");
		if (folder.searchForFile("Notes_Reopener.bat") != null)
		{
			os.runCommand("notes");
		}
		try
		{
			os.threadedSaveExecute();
			os.gameSavedTextAlpha = -1f;
		}
		catch (Exception)
		{
			int num = -1;
			num++;
		}
	}
}
