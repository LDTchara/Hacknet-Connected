using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Hacknet.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class BootCrashAssistanceModule : Module
{
	private const float TimePerBlock = 9.2f;

	private const float TimeSubForEarlyLines = -8.3f;

	private const int NumberOfFastBlocks = 11;

	public bool IsActive = false;

	private float elapsedTime = 0f;

	private List<string> SequenceBlocks = new List<string>();

	private int blocksComplete = 0;

	private bool AwaitingInput = false;

	internal bool ShouldSkipDialogueTypeout = false;

	public BootCrashAssistanceModule(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		bounds = location;
		SequenceBlocks.Add("");
		SequenceBlocks.Add("");
		SequenceBlocks.Add("");
		SequenceBlocks.Add("");
		SequenceBlocks.Add("");
		SequenceBlocks.Add(Utils.readEntireFile("Content/DLC/Docs/Untranslated/BootFailSequence0.txt"));
		SequenceBlocks.Add("");
		SequenceBlocks.Add(Utils.readEntireFile("Content/DLC/Docs/Untranslated/BootFailSequence1.txt"));
		SequenceBlocks.Add("");
		SequenceBlocks.Add(Utils.readEntireFile("Content/DLC/Docs/Untranslated/BootFailSequence2.txt"));
		SequenceBlocks.Add("");
		SequenceBlocks.Add(Utils.readEntireFile("Content/DLC/Docs/Untranslated/BootFailSequence3.txt"));
		SequenceBlocks.Add(">");
		SequenceBlocks.Add(Utils.readEntireFile("Content/DLC/Docs/BootFailSequenceCoel1.txt"));
		SequenceBlocks.Add(">");
		SequenceBlocks.Add(Utils.readEntireFile("Content/DLC/Docs/BootFailSequenceCoel2.txt"));
		SequenceBlocks.Add(">");
		SequenceBlocks.Add(Utils.readEntireFile("Content/DLC/Docs/BootFailSequenceCoel3.txt"));
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (!IsActive)
		{
			return;
		}
		elapsedTime += t;
		float num = ((blocksComplete < 11) ? (-8.3f) : 0f);
		if (!AwaitingInput && elapsedTime > num + 9.2f)
		{
			elapsedTime = 0f;
			blocksComplete++;
			if (blocksComplete >= SequenceBlocks.Count || (ShouldSkipDialogueTypeout && blocksComplete == 12))
			{
				AwaitingInput = true;
				Game1.getSingleton().IsMouseVisible = true;
				blocksComplete = SequenceBlocks.Count - 1;
			}
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		spriteBatch.Draw(Utils.white, bounds, os.darkBackgroundColor);
		Vector2 pos = new Vector2((float)bounds.X + 10f, (float)(bounds.Y + bounds.Height) - (GuiData.ActiveFontConfig.tinyFontCharHeight + 16f));
		Rectangle destinationRectangle = new Rectangle((int)pos.X, (int)pos.Y, 10, 14);
		spriteBatch.Draw(Utils.white, destinationRectangle, (os.timer % 0.3f < 0.15f) ? Color.White : (Color.White * 0.05f));
		List<string> list = new List<string>();
		Color c = Color.Lerp(Utils.AddativeWhite, Utils.makeColor(204, byte.MaxValue, 249, 0), 0.5f);
		float num = ((blocksComplete < 11) ? (-8.3f) : 0f);
		pos.Y -= 6f;
		if (AwaitingInput)
		{
			pos.Y -= 46f;
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			{
				if (Button.doButton(464191001, (int)pos.X, (int)pos.Y - 4, 220, 30, LocaleTerms.Loc("Proceed"), Color.White))
				{
					HostileHackerBreakinSequence.CopyHelpFile();
					HostileHackerBreakinSequence.OpenWindowsHelpDocument();
					HostileHackerBreakinSequence.OpenTerminal();
					HostileHackerBreakinSequence.CrashProgram();
				}
			}
			else
			{
				if (Button.doButton(464191002, (int)pos.X, (int)pos.Y - 4, 220, 30, LocaleTerms.Loc("README"), Color.White))
				{
					SequenceBlocks.Add("");
					SequenceBlocks.Add("");
					SequenceBlocks.Add("");
					SequenceBlocks.Add(HostileHackerBreakinSequence.GetHelpText());
					SequenceBlocks.Add("");
					blocksComplete = SequenceBlocks.Count - 1;
				}
				if (Button.doButton(464191003, (int)pos.X + 230, (int)pos.Y - 4, 220, 30, LocaleTerms.Loc("Terminal"), Color.White))
				{
					SequenceBlocks.Add("---------------------------------------");
					SequenceBlocks.Add("TERMINAL SHOULD BE OPEN ON YOUR SYSTEM.");
					SequenceBlocks.Add("");
					SequenceBlocks.Add("IF NOT, OPEN A TERMINAL TO");
					SequenceBlocks.Add(HostileHackerBreakinSequence.OpenTerminal());
					SequenceBlocks.Add("---------------------------------------");
					SequenceBlocks.Add("");
					blocksComplete = SequenceBlocks.Count - 1;
				}
				if (Button.doButton(464191001, (int)pos.X + 460, (int)pos.Y - 4, 220, 30, LocaleTerms.Loc("Crash VM"), Color.White))
				{
					HostileHackerBreakinSequence.CopyHelpFile();
					HostileHackerBreakinSequence.CrashProgram();
				}
			}
		}
		if (blocksComplete + 1 < SequenceBlocks.Count)
		{
			string[] array = SequenceBlocks[blocksComplete + 1].Split(Utils.robustNewlineDelim, StringSplitOptions.None);
			float num2 = Math.Min(1f, elapsedTime / (num + 9.2f)) * (float)(array.Length - 1);
			int num3 = (int)num2;
			for (int num4 = num3; num4 >= 0; num4--)
			{
				string text = array[num4];
				if (num4 == num3 && blocksComplete + 1 >= 11)
				{
					float num5 = (num + 9.2f) / (float)Math.Max(1, array.Length - 1);
					float num6 = Math.Min(num5, elapsedTime - (float)num3 * num5);
					int length = (int)((float)text.Length * (num6 / num5));
					text = text.Substring(0, length);
				}
				list.Add(text);
			}
		}
		for (int num7 = blocksComplete; num7 >= 0; num7--)
		{
			string[] array2 = SequenceBlocks[num7].Split(Utils.robustNewlineDelim, StringSplitOptions.None);
			for (int num8 = array2.Length - 1; num8 >= 0; num8--)
			{
				list.Add(array2[num8]);
			}
		}
		for (int num7 = 0; num7 < list.Count; num7++)
		{
			pos.Y -= GuiData.ActiveFontConfig.tinyFontCharHeight + 6f;
			DrawMonospace(list[num7], GuiData.smallfont, pos, c, LocaleActivator.ActiveLocaleIsCJK() ? 13 : 10);
		}
	}

	private void DrawMonospace(string text, SpriteFont font, Vector2 pos, Color c, float charWidth)
	{
		for (int i = 0; i < text.Length; i++)
		{
			spriteBatch.DrawString(font, string.Concat(text[i]), Utils.ClipVec2ForTextRendering(pos), c);
			pos.X += charWidth;
		}
	}
}
