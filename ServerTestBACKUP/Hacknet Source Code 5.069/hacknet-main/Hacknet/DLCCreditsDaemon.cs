using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Hacknet;

internal class DLCCreditsDaemon : Daemon
{
	private const float ResetSequenceTime = 5f;

	private string[] CreditsData;

	private bool showingCredits = false;

	private bool isInResetSequence = false;

	private float timeInReset = 0f;

	private float timeInCredits = 0f;

	private SoundEffect spindown;

	private SoundEffect spindownImpact;

	private SoundEffect buildup;

	private bool hasCuedBuildup = false;

	private bool hasCuedFinaleSong = false;

	public string OverrideTitle = null;

	public string OverrideButtonText = null;

	public string ConditionalActionsToLoadOnButtonPress = null;

	public DLCCreditsDaemon(Computer c, OS os)
		: base(c, LocaleTerms.Loc("Labyrinths project"), os)
	{
		LoadSounds();
	}

	public DLCCreditsDaemon(Computer c, OS os, string overrideTitle, string overrideButtonText)
		: base(c, overrideTitle, os)
	{
		LoadSounds();
		OverrideTitle = overrideTitle;
		OverrideButtonText = overrideButtonText;
	}

	private void LoadSounds()
	{
		if (DLC1SessionUpgrader.HasDLC1Installed)
		{
			spindown = os.content.Load<SoundEffect>("DLC/SFX/Spindown");
			spindownImpact = os.content.Load<SoundEffect>("DLC/SFX/RecoverImpact");
			buildup = os.content.Load<SoundEffect>("DLC/SFX/Kilmer_Woosh");
		}
		else
		{
			spindown = null;
			spindownImpact = os.content.Load<SoundEffect>("SFX/MeltImpact");
			buildup = os.content.Load<SoundEffect>("SFX/BrightFlash");
		}
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		Folder folder = comp.files.root.searchForFolder("home");
		FileEntry fileEntry = folder.searchForFile("CreditsData.txt");
		if (fileEntry == null)
		{
			CreditsData = new string[2]
			{
				"- " + LocaleTerms.Loc("Critical Error") + " -",
				LocaleTerms.Loc("Datafile not found")
			};
		}
		else
		{
			CreditsData = fileEntry.data.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
		}
		if (os.Flags.HasFlag("dlc_complete"))
		{
			showingCredits = true;
		}
	}

	private void EndDLC()
	{
		DLC1SessionUpgrader.EndDLCSection(os);
	}

	private void AddRadialMailLine()
	{
		SFX.AddRadialLine(os.mailicon.pos + new Vector2(20f, 10f), (float)(Math.PI + (double)Utils.rand((float)Math.PI)), 600f + Utils.randm(300f), 800f, 500f, 200f + Utils.randm(400f), 0.35f, Utils.AddativeWhite * Utils.randm(1f), 3f);
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		bounds = Utils.InsetRectangle(bounds, 1);
		int num = 90;
		int num2 = 30;
		Rectangle dest = new Rectangle(bounds.X, bounds.Y + num2, bounds.Width, num - num2);
		string text = ((OverrideTitle == null) ? LocaleTerms.Loc("Labyrinths Project") : OverrideTitle);
		TextItem.doCenteredFontLabel(dest, text, GuiData.font, Color.White);
		Rectangle rectangle = new Rectangle(bounds.X, bounds.Y + num + num2, bounds.Width, bounds.Height - (num + 2 * num2));
		if (showingCredits)
		{
			timeInCredits += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
			float num3 = timeInCredits;
			num3 = ((!(num3 <= 5f)) ? (num3 - 4f) : Utils.CubicInCurve(num3 / 5f));
			num3 %= 165f;
			Utils.FillEverywhereExcept(rectangle, Utils.GetFullscreen(), sb, Color.Black * 0.5f);
			float num4 = 20f;
			float num5 = (float)(rectangle.Y - num2 + rectangle.Height) - num3 * num4;
			float num6 = num5;
			for (int i = 0; i < CreditsData.Length; i++)
			{
				int num7 = 22;
				SpriteFont font = GuiData.smallfont;
				Color color = Color.LightGray * 0.9f;
				string text2 = CreditsData[i];
				if (text2.StartsWith("%"))
				{
					text2 = text2.Substring(1);
					num7 = 45;
					font = GuiData.font;
					color = Utils.AddativeWhite * 0.9f;
				}
				else if (text2.StartsWith("^"))
				{
					text2 = text2.Substring(1);
					num7 = 30;
					font = GuiData.font;
					color = Color.White;
				}
				if (num6 >= (float)(rectangle.Y - num2))
				{
					Rectangle dest2 = new Rectangle(rectangle.X, (int)num6, rectangle.Width, num7);
					TextItem.doCenteredFontLabel(dest2, text2, font, color);
				}
				num6 += (float)(num7 + 2);
				if (num6 > (float)(rectangle.Y + rectangle.Height))
				{
					break;
				}
			}
			if (timeInCredits > 40f && Button.doButton(18394902, rectangle.X + rectangle.Width / 4, rectangle.Y + rectangle.Height - 23, rectangle.Width / 2, 20, LocaleTerms.Loc("Proceed"), (timeInCredits > 65f) ? os.highlightColor : Color.Black))
			{
				os.display.command = "connect";
			}
		}
		else if (isInResetSequence)
		{
			timeInReset += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
			if (timeInReset >= 5f)
			{
				showingCredits = true;
				PostProcessor.EndingSequenceFlashOutActive = false;
				PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
				if (!Settings.IsInExtensionMode)
				{
					EndDLC();
					if (!hasCuedFinaleSong)
					{
						os.delayer.Post(ActionDelayer.Wait(2.0), delegate
						{
							MusicManager.playSongImmediatley("DLC\\Music\\DreamHead");
						});
						hasCuedFinaleSong = true;
					}
					MediaPlayer.IsRepeating = true;
					for (int i = 0; i < 9; i++)
					{
						double time = (double)i / 7.0;
						os.delayer.Post(ActionDelayer.Wait(time), delegate
						{
							SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeWhite * 0.8f, 400f);
						});
					}
				}
			}
			else
			{
				float num8 = Math.Min(timeInReset, 1f);
				Utils.FillEverywhereExcept(rectangle, Utils.GetFullscreen(), sb, Color.Black * 0.5f * num8);
				PatternDrawer.draw(rectangle, 0.3f, Color.Transparent, Color.Black * 0.7f, sb, PatternDrawer.binaryTile);
				AddRadialMailLine();
				PostProcessor.EndingSequenceFlashOutActive = true;
				PostProcessor.EndingSequenceFlashOutPercentageComplete = num8;
				if (!hasCuedBuildup && (double)timeInReset > 2.8)
				{
					buildup.Play();
					hasCuedBuildup = true;
				}
				TextItem.doCenteredFontLabel(rectangle, LocaleTerms.Loc("Disabling..."), GuiData.font, Color.White);
			}
		}
		else
		{
			PatternDrawer.draw(rectangle, 0.3f, Color.Transparent, Color.Black * 0.7f, sb, PatternDrawer.binaryTile);
			string text3 = ((OverrideButtonText == null) ? LocaleTerms.Loc("Disable Agent Monitoring") : OverrideButtonText);
			bool flag = OverrideButtonText != null;
			if (Button.doButton(38101920, rectangle.X + 50, rectangle.Y + rectangle.Height / 2 - 13, rectangle.Width - 100, 26, text3, os.highlightColor))
			{
				if (!flag)
				{
					isInResetSequence = true;
					timeInReset = 0f;
					if (MusicManager.currentSongName == "DLC/Music/RemiDrone")
					{
						MusicManager.stop();
					}
					spindownImpact.Play();
					if (spindown != null)
					{
						os.delayer.Post(ActionDelayer.Wait(1.1), delegate
						{
							spindown.Play();
						});
					}
					DLC1SessionUpgrader.ReDsicoverAllVisibleNodesInOSCache(os);
				}
				else
				{
					isInResetSequence = false;
					showingCredits = true;
					if (ConditionalActionsToLoadOnButtonPress != null)
					{
						RunnableConditionalActions.LoadIntoOS(ConditionalActionsToLoadOnButtonPress, os);
					}
				}
			}
		}
		Rectangle destinationRectangle = new Rectangle(bounds.X, bounds.Y + num, bounds.Width, num2);
		sb.Draw(Utils.white, destinationRectangle, Utils.VeryDarkGray);
		destinationRectangle.Y = bounds.Y + num + num2 + rectangle.Height;
		sb.Draw(Utils.white, destinationRectangle, Utils.VeryDarkGray);
	}

	public override string getSaveString()
	{
		string text = "";
		if (OverrideTitle != null)
		{
			text += $"Title=\"{OverrideTitle}\" ";
		}
		if (OverrideButtonText != null)
		{
			text += $"Button=\"{OverrideButtonText}\" ";
		}
		if (ConditionalActionsToLoadOnButtonPress != null)
		{
			text += $"Action=\"{ConditionalActionsToLoadOnButtonPress}\" ";
		}
		return "<DLCCredits " + text + "/>";
	}
}
