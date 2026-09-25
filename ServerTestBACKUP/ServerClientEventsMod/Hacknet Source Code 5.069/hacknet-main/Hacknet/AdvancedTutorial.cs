using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hacknet;

internal class AdvancedTutorial : ExeModule
{
	private static List<string> commandSequence;

	private static List<string[]> altCommandequence;

	private static List<string> feedbackSequence;

	private static string loadedLocale = "en-us";

	private int state;

	private string lastCommand;

	private string[] renderText;

	private string[] hintButtonText = null;

	private float hintTextFadeTimer = 0f;

	private string[] hintButtonDelimiter = new string[1] { "|" };

	private float flashTimer;

	private SoundEffect startSound;

	private SoundEffect advanceFlash;

	private List<Action> stepCompletionSequence;

	private NodeBounceEffect bounceEffect;

	public bool CanActivateFirstStep = true;

	public AdvancedTutorial(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		state = 0;
		lastCommand = "";
		ramCost = 500;
		IdentifierName = "Tutorial v16.2";
		flashTimer = 1f;
		startSound = os.content.Load<SoundEffect>("SFX/DoomShock");
		advanceFlash = os.content.Load<SoundEffect>("SFX/BrightFlash");
		commandSequence = null;
	}

	public override void LoadContent()
	{
		base.LoadContent();
		bounceEffect = new NodeBounceEffect
		{
			NodeHitDelay = 0.02f,
			TimeBetweenBounces = 0.2f
		};
		string text = "\n\n%&%&%&%\n";
		string text2 = LocalizedFileLoader.Read("Content/Post/AdvancedTutorialData.txt").Replace("\r\n", "\n");
		string[] array = text2.Split(new string[1] { text }, StringSplitOptions.None);
		if (feedbackSequence != null && loadedLocale != Settings.ActiveLocale)
		{
			feedbackSequence = null;
		}
		loadedLocale = Settings.ActiveLocale ?? "";
		if (commandSequence == null)
		{
			altCommandequence = new List<string[]>();
			for (int i = 0; i < 15; i++)
			{
				altCommandequence.Add(new string[0]);
			}
			commandSequence = new List<string>();
			commandSequence.Add("&#*@(&#@(&#@&)@&#)(@&)@#");
			commandSequence.Add("connect " + os.thisComputer.ip);
			commandSequence.Add("scan");
			commandSequence.Add("dc");
			altCommandequence[3] = new string[1] { "disconnect" };
			commandSequence.Add("connect");
			commandSequence.Add("probe");
			altCommandequence[5] = new string[1] { "nmap" };
			commandSequence.Add("porthack");
			commandSequence.Add("scan");
			commandSequence.Add("ls");
			altCommandequence[8] = new string[1] { "dir" };
			commandSequence.Add("cd bin");
			altCommandequence[9] = new string[3] { "cd ../bin", "cd ../../bin", "cd /bin" };
			commandSequence.Add("cat config.txt");
			altCommandequence[10] = new string[1] { "less config.txt" };
			commandSequence.Add("cd ..");
			altCommandequence[11] = new string[2] { "cd..", "cd /" };
			commandSequence.Add("cd log");
			altCommandequence[12] = new string[3] { "cd ../log", "cd ../../log", "cd /log" };
			commandSequence.Add("rm *");
			commandSequence.Add("dc");
			altCommandequence[14] = new string[1] { "disconnect" };
		}
		if (feedbackSequence == null)
		{
			feedbackSequence = new List<string>();
			feedbackSequence.Add(array[0]);
			feedbackSequence.Add(array[1]);
			feedbackSequence.Add(array[2]);
			feedbackSequence.Add(array[3]);
			feedbackSequence.Add(array[4]);
			feedbackSequence.Add(array[5]);
			feedbackSequence.Add(array[6]);
			feedbackSequence.Add(array[7]);
			feedbackSequence.Add(array[8]);
			feedbackSequence.Add(array[9]);
			feedbackSequence.Add(array[10]);
			feedbackSequence.Add(array[11]);
			feedbackSequence.Add(array[12]);
			feedbackSequence.Add(array[13]);
			feedbackSequence.Add(array[14]);
			feedbackSequence.Add(array[15]);
		}
		stepCompletionSequence = new List<Action>();
		for (int i = 0; i < feedbackSequence.Count; i++)
		{
			stepCompletionSequence.Add(null);
		}
		stepCompletionSequence[1] = delegate
		{
			os.netMap.visible = true;
			os.netMap.inputLocked = false;
		};
		stepCompletionSequence[2] = delegate
		{
			os.display.visible = true;
			os.display.inputLocked = false;
		};
		stepCompletionSequence[3] = delegate
		{
			os.netMap.inputLocked = true;
		};
		stepCompletionSequence[4] = delegate
		{
			os.netMap.inputLocked = false;
		};
		stepCompletionSequence[5] = delegate
		{
			os.display.inputLocked = true;
			os.ram.inputLocked = true;
			os.netMap.inputLocked = true;
			os.terminal.visible = true;
			os.terminal.inputLocked = false;
			os.terminal.clearCurrentLine();
		};
		stepCompletionSequence[8] = delegate
		{
			os.display.inputLocked = false;
			os.ram.inputLocked = false;
		};
		state = 0;
		getRenderText();
	}

	public override void Update(float t)
	{
		base.Update(t);
		string lastRunCommand = os.terminal.getLastRunCommand();
		if (lastCommand != lastRunCommand)
		{
			lastCommand = lastRunCommand;
			parseCommand();
		}
		else if (GuiData.getKeyboadState().IsKeyDown(Keys.F8))
		{
			lastCommand = "";
		}
		flashTimer -= t;
		flashTimer = Math.Max(flashTimer, 0f);
		bounceEffect.Update(t, delegate
		{
			bounceEffect.NodeHitDelay = Math.Max(0f, -0.2f + Utils.randm(0.5f));
			bounceEffect.TimeBetweenBounces = 0.15f + Utils.randm(0.7f);
		});
		hintTextFadeTimer = Math.Max(0f, hintTextFadeTimer - t);
	}

	public void parseCommand()
	{
		try
		{
			if (state < commandSequence.Count)
			{
				bool flag = lastCommand.ToLower().StartsWith(commandSequence[state]);
				if (!flag)
				{
					for (int i = 0; i < altCommandequence[state].Length; i++)
					{
						flag |= lastCommand.ToLower().StartsWith(altCommandequence[state][i]);
					}
				}
				if (flag)
				{
					advanceState();
				}
			}
			else
			{
				lastCommand = null;
			}
		}
		catch (Exception)
		{
			lastCommand = "";
		}
	}

	private void advanceState()
	{
		state++;
		getRenderText();
		if (state > 0)
		{
			printCurrentCommandToTerminal();
		}
		flashTimer = 1f;
		if (stepCompletionSequence[state] != null)
		{
			stepCompletionSequence[state]();
		}
		if (!Settings.soundDisabled && !MusicManager.isMuted && state > 1)
		{
			advanceFlash.Play(0.6f, 1f, 1f);
		}
	}

	public void printCurrentCommandToTerminal()
	{
		os.terminal.writeLine("\n--------------------------------------------------");
		os.terminal.writeLine(" ");
		for (int i = 0; i < renderText.Length; i++)
		{
			if (!renderText[i].Contains("<#") && !renderText[i].Contains("#>"))
			{
				os.terminal.writeLine(renderText[i]);
			}
		}
		os.terminal.writeLine(" ");
		os.terminal.writeLine("--------------------------------------------------\n");
	}

	public void getRenderText()
	{
		string text = feedbackSequence[state];
		char[] separator = new char[1] { '\n' };
		string[] array = text.Split(hintButtonDelimiter, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length > 1)
		{
			hintButtonText = DisplayModule.cleanSplitForWidth(array[1], 178).Split(separator);
			text = array[0];
			hintTextFadeTimer = 0f;
		}
		else
		{
			hintButtonText = null;
		}
		string data = LocalizedFileLoader.SafeFilterString(text);
		renderText = Utils.SuperSmartTwimForWidth(data, bounds.Width - 10, GuiData.tinyfont).Split(separator);
	}

	public override void Killed()
	{
		base.Killed();
		if (!os.multiplayer && os.initShowsTutorial)
		{
			os.currentMission.sendEmail(os);
			os.Flags.AddFlag("TutorialComplete");
			os.initShowsTutorial = false;
		}
		if (os.hubServerAlertsIcon != null)
		{
			os.hubServerAlertsIcon.IsEnabled = true;
		}
		os.mailicon.isEnabled = true;
		os.terminal.visible = true;
		os.terminal.inputLocked = false;
		os.netMap.visible = true;
		os.netMap.inputLocked = false;
		os.ram.visible = true;
		os.ram.inputLocked = false;
		os.display.visible = true;
		os.display.inputLocked = false;
		if (state < feedbackSequence.Count - 1)
		{
			AchievementsManager.Unlock("kill_tutorial");
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		Rectangle rectangle = bounds;
		rectangle.X++;
		rectangle.Y++;
		rectangle.Width -= 2;
		rectangle.Height -= 2;
		rectangle.Height -= Module.PANEL_HEIGHT;
		rectangle.Y += Module.PANEL_HEIGHT;
		PatternDrawer.draw(rectangle, 1f, os.highlightColor * 0.2f, os.highlightColor, spriteBatch);
		rectangle.X += 2;
		rectangle.Y += 2;
		rectangle.Width -= 4;
		rectangle.Height -= 4;
		spriteBatch.Draw(Utils.white, rectangle, os.darkBackgroundColor * 0.99f);
		drawTarget();
		int num = 260;
		Rectangle rectangle2 = new Rectangle(bounds.X + 1, bounds.Y + bounds.Height - num, bounds.Width - 2, num);
		bounceEffect.Draw(spriteBatch, rectangle2, Utils.AdditivizeColor(os.highlightColor) * 0.35f, Utils.AddativeWhite * 0.5f);
		int num2 = 210;
		Rectangle rectangle3 = new Rectangle(rectangle2.X, rectangle2.Y + (num - num2), rectangle2.Width, num2);
		spriteBatch.Draw(Utils.gradient, rectangle3, null, os.darkBackgroundColor, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0.7f);
		Rectangle destinationRectangle = rectangle3;
		destinationRectangle.Y = rectangle2.Y;
		destinationRectangle.Height = num - num2;
		spriteBatch.Draw(Utils.white, destinationRectangle, os.darkBackgroundColor);
		spriteBatch.Draw(Utils.white, rectangle, os.highlightColor * flashTimer);
		spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars("Tutorial", 0.024), new Vector2(bounds.X + 5, bounds.Y + 22), os.subtleTextColor * 0.6f);
		spriteBatch.DrawString(GuiData.font, "Tutorial", new Vector2(bounds.X + 5, bounds.Y + 22), os.subtleTextColor);
		float num3 = GuiData.ActiveFontConfig.tinyFontCharHeight + 1f;
		Vector2 dpos2 = RenderText(dpos: new Vector2(bounds.X + 5, bounds.Y + 67), stringData: renderText, charHeight: num3);
		dpos2.Y += num3;
		if (state == 0 && CanActivateFirstStep)
		{
			if (Button.doButton(2933201, bounds.X + 10, (int)(dpos2.Y + 20f), bounds.Width - 20, 30, LocaleTerms.Loc("Continue"), null) || (!os.terminal.visible && Utils.keyPressed(GuiData.lastInput, Keys.Enter, null)))
			{
				advanceState();
				if (!Settings.soundDisabled)
				{
					startSound.Play(1f, 0f, 0f);
					startSound.Play(1f, 0f, 0f);
				}
			}
		}
		else if (hintButtonText != null)
		{
			if (Button.doButton(2933202, bounds.X + 10, (int)(dpos2.Y + 6f), bounds.Width - 20, 20, LocaleTerms.Loc("Hint"), null))
			{
				hintTextFadeTimer = 9f;
			}
			dpos2.Y += 30f;
			dpos2.X += 10f;
			float num4 = Math.Min(1f, hintTextFadeTimer / 3f);
			string text = "";
			for (int i = 0; i < hintButtonText.Length; i++)
			{
				text = text + hintButtonText[i] + "\n";
			}
			text = Utils.SuperSmartTwimForWidth(text.Trim(), bounds.Width, GuiData.tinyfont);
			string[] array = text.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
			Rectangle destinationRectangle2 = new Rectangle((int)dpos2.X, (int)dpos2.Y, bounds.Width, (int)(num3 * (float)array.Length));
			spriteBatch.Draw(Utils.white, destinationRectangle2, Color.Black * 0.5f * num4);
			RenderText(array, dpos2, num3, num4);
		}
	}

	private Vector2 RenderText(string[] stringData, Vector2 dpos, float charHeight, float opacityMod = 1f)
	{
		string text = "";
		bool flag = false;
		for (int i = 0; i < stringData.Length; i++)
		{
			text = stringData[i];
			text = text.Replace("\r", "");
			if (text.Length > 0)
			{
				if (text.Trim() == "<#")
				{
					flag = true;
					continue;
				}
				if (text.Trim() == "#>")
				{
					flag = false;
					continue;
				}
				spriteBatch.DrawString(GuiData.tinyfont, text, dpos, (flag ? os.highlightColor : Color.White) * opacityMod);
				dpos.Y += charHeight;
			}
		}
		return dpos;
	}

	private Vector2 RenderTextOld(string[] stringData, Vector2 dpos, float charHeight, float opacityMod = 1f)
	{
		string text = "";
		bool flag = false;
		for (int i = 0; i < stringData.Length; i++)
		{
			text = stringData[i];
			if (text.Length > 0)
			{
				flag = false;
				if (text[0] == '#')
				{
					text = text.Substring(1, text.Length - 2);
					flag = true;
				}
				spriteBatch.DrawString(GuiData.tinyfont, text, dpos, (flag ? os.highlightColor : Color.White) * opacityMod);
				dpos.Y += charHeight;
			}
		}
		return dpos;
	}
}
