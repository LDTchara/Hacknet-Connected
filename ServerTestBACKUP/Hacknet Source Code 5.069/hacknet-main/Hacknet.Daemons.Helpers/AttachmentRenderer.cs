using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Hacknet.Daemons.Helpers;

public static class AttachmentRenderer
{
	private static string[] spaceDelim = new string[1] { "#%#" };

	public static bool RenderAttachment(string data, object osObj, Vector2 dpos, int startingButtonIndex, SoundEffect buttonSound)
	{
		OS oS = (OS)osObj;
		string[] array = data.Split(spaceDelim, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 1)
		{
			return false;
		}
		if (array[0].Equals("link"))
		{
			Vector2 labelSize = TextItem.doMeasuredTinyLabel(dpos, LocaleTerms.Loc("LINK") + " : " + array[1] + "@" + array[2], null);
			Computer computer = Programs.getComputer(oS, array[2]);
			if (!oS.netMap.visibleNodes.Contains(oS.netMap.nodes.IndexOf(computer)))
			{
				DrawButtonGlow(dpos, labelSize, oS);
			}
			if (Button.doButton(800009 + startingButtonIndex, (int)(dpos.X + labelSize.X + 5f), (int)dpos.Y, 20, 17, "+", null))
			{
				if (computer == null)
				{
					oS.write("ERROR: Linked target not found");
				}
				else
				{
					computer.highlightFlashTime = 1f;
					oS.netMap.discoverNode(computer);
					SFX.addCircle(Programs.getComputer(oS, array[2]).getScreenSpacePosition(), Color.White, 32f);
					if (buttonSound != null && !Settings.soundDisabled)
					{
						buttonSound.Play();
					}
				}
			}
		}
		else if (array[0].Equals("account"))
		{
			Vector2 labelSize = TextItem.doMeasuredTinyLabel(dpos, LocaleTerms.Loc("ACCOUNT") + " : " + array[1] + " : User=" + array[3] + " Pass=" + array[4], null);
			DrawButtonGlow(dpos, labelSize, oS);
			if (Button.doButton(801009 + startingButtonIndex, (int)(dpos.X + labelSize.X + 5f), (int)dpos.Y, 20, 17, "+", null))
			{
				Computer computer2 = Programs.getComputer(oS, array[2]);
				computer2.highlightFlashTime = 1f;
				oS.netMap.discoverNode(computer2);
				computer2.highlightFlashTime = 1f;
				SFX.addCircle(computer2.getScreenSpacePosition(), Color.White, 32f);
				for (int i = 0; i < computer2.users.Count; i++)
				{
					UserDetail value = computer2.users[i];
					if (value.name.Equals(array[3]))
					{
						value.known = true;
						computer2.users[i] = value;
						break;
					}
				}
				if (buttonSound != null && !Settings.soundDisabled)
				{
					buttonSound.Play();
				}
			}
		}
		else
		{
			if (!array[0].Equals("note"))
			{
				return false;
			}
			Vector2 labelSize = TextItem.doMeasuredTinyLabel(dpos, LocaleTerms.Loc("NOTE") + " : " + array[1], null);
			string data2 = array[2];
			data2 = LocalizedFileLoader.SafeFilterString(data2);
			if (!NotesExe.NoteExists(data2, oS))
			{
				DrawButtonGlow(dpos, labelSize, oS);
			}
			if (Button.doButton(800009 + startingButtonIndex, (int)(dpos.X + labelSize.X + 5f), (int)dpos.Y, 20, 17, "+", null))
			{
				NotesExe.AddNoteToOS(data2, oS);
			}
		}
		return true;
	}

	private static void DrawButtonGlow(Vector2 dpos, Vector2 labelSize, OS os)
	{
		Rectangle rectangle = new Rectangle((int)(dpos.X + labelSize.X + 5f), (int)dpos.Y, 20, 17);
		float num = Utils.QuadraticOutCurve(1f - os.timer % 1f);
		float num2 = 8.5f;
		Rectangle destinationRectangle = Utils.InsetRectangle(rectangle, (int)(-1f * (num2 * (1f - num))));
		GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, Utils.AddativeWhite * num * 0.32f);
		GuiData.spriteBatch.Draw(Utils.white, rectangle, Color.Black * 0.7f);
	}
}
