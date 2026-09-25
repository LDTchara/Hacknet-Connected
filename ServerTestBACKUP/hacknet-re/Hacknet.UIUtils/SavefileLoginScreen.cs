using System;
using System.Collections.Generic;
using System.Text;
using Hacknet.Gui;
using Hacknet.PlatformAPI.Storage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hacknet.UIUtils;

public class SavefileLoginScreen
{
	public Action<string, string> StartNewGameForUsernameAndPass;

	public Action<string, string> LoadGameForUserFileAndUsername;

	public Action RequestGoBack;

	private string terminalString = "";

	private List<string> History = new List<string>();

	private string currentPrompt = "USERNAME :";

	private int promptIndex = 0;

	public string ProjectName = "Hacknet";

	private List<string> PromptSequence = new List<string>();

	private List<string> Answers = new List<string>();

	private bool IsReady = false;

	private bool IsNewAccountMode = true;

	private bool InPasswordMode = false;

	private bool CanReturnEnter = false;

	private string userPathCache = null;

	public bool DrawFromTop = false;

	private bool HasOverlayScreen = false;

	private static Color CancelColor = new Color(125, 82, 82);

	private bool PreventAdvancing = false;

	public void WriteToHistory(string message)
	{
		History.Add(message);
	}

	public void ClearTextBox()
	{
		GuiData.getFilteredKeys();
		terminalString = "";
		TextBox.cursorPosition = 0;
	}

	public void ResetForNewAccount()
	{
		promptIndex = 0;
		IsReady = false;
		PromptSequence.Clear();
		PromptSequence.Add(LocaleTerms.Loc("USERNAME") + " :");
		PromptSequence.Add(LocaleTerms.Loc("PASSWORD") + " :");
		PromptSequence.Add(LocaleTerms.Loc("CONFIRM PASS") + " :");
		History.Clear();
		History.Add("-- " + LocaleTerms.Loc("New " + ProjectName + " User Registration") + " --");
		currentPrompt = PromptSequence[promptIndex];
		IsNewAccountMode = true;
		terminalString = "";
		TextBox.cursorPosition = 0;
	}

	public void ResetForLogin()
	{
		promptIndex = 0;
		IsReady = false;
		PromptSequence.Clear();
		PromptSequence.Add(LocaleTerms.Loc("USERNAME") + " :");
		PromptSequence.Add(LocaleTerms.Loc("PASSWORD") + " :");
		History.Clear();
		History.Add("-- Login --");
		currentPrompt = PromptSequence[promptIndex];
		IsNewAccountMode = false;
		terminalString = "";
		TextBox.cursorPosition = 0;
	}

	private void Advance(string answer)
	{
		promptIndex++;
		Answers.Add(answer);
		if (IsNewAccountMode)
		{
			if (promptIndex == 1)
			{
				if (string.IsNullOrWhiteSpace(Answers[0]))
				{
					History.Add(" -- " + LocaleTerms.Loc("Username cannot be blank. Try Again") + " -- ");
					promptIndex = 0;
					Answers.Clear();
				}
				else if (Utils.StringContainsInvalidFilenameChars(Answers[0]))
				{
					History.Add(" -- " + LocaleTerms.Loc("Username contains invalid characters. Try Again") + " -- ");
					promptIndex = 0;
					Answers.Clear();
				}
				else if (!SaveFileManager.CanCreateAccountForName(Answers[0]))
				{
					History.Add(" -- " + LocaleTerms.Loc("Username already in use. Try Again") + " -- ");
					promptIndex = 0;
					Answers.Clear();
				}
			}
			if (promptIndex == 3)
			{
				if (string.IsNullOrWhiteSpace(answer))
				{
					History.Add(" -- " + LocaleTerms.Loc("Password Cannot be Blank! Try Again") + " -- ");
					promptIndex = 1;
					string item = Answers[0];
					Answers.Clear();
					Answers.Add(item);
				}
				else if (Answers[1] != answer)
				{
					History.Add(" -- " + LocaleTerms.Loc("Password Mismatch! Try Again") + " -- ");
					promptIndex = 1;
					string item = Answers[0];
					Answers.Clear();
					Answers.Add(item);
				}
			}
			InPasswordMode = promptIndex == 1 || promptIndex == 2;
		}
		else
		{
			InPasswordMode = promptIndex == 1;
		}
		if (promptIndex >= PromptSequence.Count)
		{
			if (IsNewAccountMode)
			{
				History.Add(" -- " + LocaleTerms.Loc("Details Confirmed") + " -- ");
				History.Add(LocaleTerms.Loc("WARNING") + " : " + LocaleTerms.Loc("Once created, a session's language cannot be changed"));
				currentPrompt = LocaleTerms.Loc("READY - PRESS ENTER TO CONFIRM");
				IsReady = true;
				return;
			}
			string item = Answers[0];
			string pass = Answers[1];
			if ((userPathCache = SaveFileManager.GetFilePathForLogin(item, pass)) == null)
			{
				promptIndex = 0;
				currentPrompt = PromptSequence[promptIndex];
				History.Add(" -- " + LocaleTerms.Loc("Invalid Login Details") + " -- ");
			}
			else
			{
				IsReady = true;
				currentPrompt = LocaleTerms.Loc("READY - PRESS ENTER TO CONFIRM");
			}
		}
		else
		{
			currentPrompt = PromptSequence[promptIndex];
		}
	}

	public void Draw(SpriteBatch sb, Rectangle dest)
	{
		int num = 300;
		Rectangle rectangle = dest;
		Rectangle rectangle2 = new Rectangle(dest.X, dest.Y, dest.Width - num, dest.Height);
		if (!IsNewAccountMode)
		{
			dest = rectangle2;
		}
		SpriteFont smallfont = GuiData.smallfont;
		int num2 = (int)(smallfont.MeasureString(currentPrompt).X + 4f);
		int num3 = (DrawFromTop ? dest.Y : (dest.Y + dest.Height - 18));
		GuiData.spriteBatch.DrawString(smallfont, currentPrompt, new Vector2(dest.X, num3), Color.White);
		if (!IsReady)
		{
			TextBox.MaskingText = InPasswordMode;
			terminalString = TextBox.doTerminalTextField(16392802, dest.X + num2, num3 - 2, dest.Width, 20, 1, terminalString, GuiData.UISmallfont);
		}
		if (!IsNewAccountMode)
		{
			Vector2 pos = new Vector2(rectangle.X + rectangle2.Width, rectangle.Y);
			if (SaveFileManager.Accounts.Count > 0)
			{
				TextItem.doFontLabel(pos, LocaleTerms.Loc("LOCAL ACCOUNTS") + " ::", GuiData.font, Color.Gray, num, 22f);
				pos.Y += 22f;
			}
			if (!HasOverlayScreen)
			{
				for (int i = 0; i < SaveFileManager.Accounts.Count; i++)
				{
					if (Button.doButton(2870300 + i + i * 12, (int)pos.X, (int)pos.Y, num, 18, SaveFileManager.Accounts[i].Username, Color.Black))
					{
						Answers = new List<string>(new string[2]
						{
							SaveFileManager.Accounts[i].Username,
							SaveFileManager.Accounts[i].Password
						});
						promptIndex = 2;
						TextBox.BoxWasActivated = true;
						IsReady = true;
						break;
					}
					int index = i;
					if (Button.doButton(7070300 + i + i * 12, (int)pos.X + num + 4, (int)pos.Y, 21, 18, "X", Color.DarkRed))
					{
						string format = LocaleTerms.Loc("Are you sure you wish to delete account {0}?");
						format = string.Format(format, "\"" + SaveFileManager.Accounts[i].Username + "\"");
						format = Utils.SuperSmartTwimForWidth(format, 400, GuiData.font);
						MessageBoxScreen messageBoxScreen = new MessageBoxScreen(format);
						messageBoxScreen.OverrideAcceptedText = LocaleTerms.Loc("Delete Account");
						messageBoxScreen.OverrideCancelText = LocaleTerms.Loc("Cancel");
						messageBoxScreen.AcceptedClicked = (Action)Delegate.Combine(messageBoxScreen.AcceptedClicked, (Action)delegate
						{
							HasOverlayScreen = false;
							SaveFileManager.DeleteUser(SaveFileManager.Accounts[index].Username);
						});
						messageBoxScreen.CancelClicked = (Action)Delegate.Combine(messageBoxScreen.CancelClicked, (Action)delegate
						{
							HasOverlayScreen = false;
						});
						Game1.getSingleton().sman.AddScreen(messageBoxScreen);
						HasOverlayScreen = true;
					}
					pos.Y += 22f;
				}
			}
		}
		if (!HasOverlayScreen && TextBox.BoxWasActivated)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(currentPrompt);
			stringBuilder.Append(" ");
			string text = terminalString;
			if (InPasswordMode)
			{
				string text2 = "";
				for (int i = 0; i < text.Length; i++)
				{
					text2 += "*";
				}
				text = text2;
			}
			stringBuilder.Append(text);
			History.Add(stringBuilder.ToString());
			Advance(terminalString);
			terminalString = "";
			TextBox.cursorPosition = 0;
			TextBox.BoxWasActivated = false;
		}
		int num4 = (DrawFromTop ? (dest.Y + 24) : (dest.Y + dest.Height + 12));
		GuiData.spriteBatch.Draw(Utils.white, new Rectangle(dest.X, num4, dest.Width / 2, 1), Utils.SlightlyDarkGray);
		num4 += 10;
		int num5 = num4 - 60;
		if (IsReady)
		{
			if (!GuiData.getKeyboadState().IsKeyDown(Keys.Enter))
			{
				CanReturnEnter = true;
			}
			if (((!HasOverlayScreen && (!IsNewAccountMode || Button.doButton(16392804, dest.X, num4, dest.Width / 3, 28, LocaleTerms.Loc("CONFIRM"), Color.White))) || (CanReturnEnter && Utils.keyPressed(GuiData.lastInput, Keys.Enter, null))) && !PreventAdvancing)
			{
				if (IsNewAccountMode)
				{
					if (Answers.Count < 3)
					{
						ResetForNewAccount();
					}
					else
					{
						string arg = Answers[0];
						string arg2 = Answers[1];
						TextBox.MaskingText = false;
						if (StartNewGameForUsernameAndPass != null)
						{
							StartNewGameForUsernameAndPass(arg, arg2);
						}
					}
				}
				else
				{
					TextBox.MaskingText = false;
					if (LoadGameForUserFileAndUsername != null)
					{
						LoadGameForUserFileAndUsername(userPathCache, Answers[0]);
					}
					History.Clear();
					currentPrompt = "";
				}
				PreventAdvancing = true;
			}
			num4 += 36;
		}
		if (!HasOverlayScreen && Button.doButton(16392806, dest.X, DrawFromTop ? num5 : num4, dest.Width / 3, 22, LocaleTerms.Loc("CANCEL"), CancelColor) && RequestGoBack != null)
		{
			InPasswordMode = false;
			TextBox.MaskingText = false;
			RequestGoBack();
		}
		float num6 = GuiData.ActiveFontConfig.tinyFontCharHeight + 8f;
		Vector2 position = new Vector2(dest.X, DrawFromTop ? ((float)num4) : ((float)(dest.Y + dest.Height - 20) - num6));
		for (int i = History.Count - 1; i >= 0; i--)
		{
			sb.DrawString(GuiData.UISmallfont, History[i], position, Color.White);
			position.Y -= num6 * (DrawFromTop ? (-1f) : 1f);
		}
	}
}
