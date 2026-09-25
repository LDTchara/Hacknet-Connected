using System;
using System.Collections.Generic;
using Hacknet.Daemons.Helpers;
using Hacknet.Gui;
using Hacknet.Localization;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MailServer : Daemon
{
	public struct EMailData
	{
		public string sender;

		public string body;

		public string subject;

		public List<string> attachments;

		public EMailData(string sendr, string bod, string subj, List<string> _attachments)
		{
			sender = sendr;
			body = bod;
			subject = subj;
			attachments = _attachments;
		}
	}

	public const int TYPE = 0;

	public const int SENDER = 1;

	public const int SUBJECT = 2;

	public const int BODY = 3;

	public const int ATTACHMENT = 4;

	public const int UNREAD = 0;

	public const int READ = 1;

	public const float MISSION_COMPLETE_FLASH_TIME = 3f;

	public static bool shouldGenerateJunk = true;

	private Folder root;

	private Folder accounts;

	private Folder userFolder;

	private List<int> rootPath;

	private List<int> accountsPath;

	private UserDetail user;

	private FileEntry selectedEmail;

	private int state;

	private int inboxPage = 0;

	private int totalPagesDetected = -1;

	private static string emailSplitDelimiter = "@*&^#%@)_!_)*#^@!&*)(#^&\n";

	private static string[] emailSplitDelims = new string[1] { emailSplitDelimiter };

	private static string[] spaceDelim = new string[1] { "#%#" };

	private Color themeColor = new Color(125, 5, 6);

	public Color textColor;

	private string[] emailData;

	public bool shouldGenerateJunkEmails = true;

	public Color evenLine;

	public Color oddLine;

	public Color senderDarkeningColor;

	public Color seperatorLineColor;

	private List<MailResponder> responders;

	public Texture2D panel;

	public Texture2D corner;

	public Texture2D unopenedIcon;

	private Rectangle panelRect;

	private bool missionIncompleteReply;

	private static SoundEffect buttonSound;

	private ScrollableSectionedPanel sectionedPanel;

	public Action setupComplete;

	private List<string> emailReplyStrings = new List<string>();

	private bool addingNewReplyString = false;

	public MailServer(Computer c, string name, OS os)
		: base(c, name, os)
	{
		state = 0;
		panel = os.content.Load<Texture2D>("Panel");
		corner = os.content.Load<Texture2D>("Corner");
		unopenedIcon = os.content.Load<Texture2D>("UnopenedMail");
		buttonSound = os.content.Load<SoundEffect>("SFX/Bip");
		panelRect = default(Rectangle);
		evenLine = new Color(80, 81, 83);
		oddLine = new Color(58, 58, 58);
		senderDarkeningColor = new Color(0, 0, 0, 100);
		seperatorLineColor = Color.Transparent;
		textColor = Color.White;
		responders = new List<MailResponder>();
	}

	public override void initFiles()
	{
		base.initFiles();
		initFilesystem();
		for (int i = 0; i < 10; i++)
		{
			comp.users.Add(new UserDetail(UsernameGenerator.getName()));
		}
		for (int i = 0; i < comp.users.Count; i++)
		{
			UserDetail userDetail = comp.users[i];
			if (userDetail.type == 1 || userDetail.type == 0 || userDetail.type == 2)
			{
				Folder folder = new Folder(userDetail.name);
				folder.files.Add(new FileEntry("Username: " + userDetail.name + "\nPassword: " + userDetail.pass, "AccountInfo"));
				Folder folder2 = new Folder("inbox");
				if (shouldGenerateJunkEmails && userDetail.name != os.defaultUser.name)
				{
					addJunkEmails(folder2);
				}
				folder.folders.Add(folder2);
				folder.folders.Add(new Folder("sent"));
				accounts.folders.Add(folder);
			}
		}
		if (setupComplete != null)
		{
			setupComplete();
		}
	}

	public override void loadInit()
	{
		base.loadInit();
		root = comp.files.root.searchForFolder("mail");
		accounts = root.searchForFolder("accounts");
	}

	public void addJunkEmails(Folder f)
	{
		if (shouldGenerateJunk)
		{
			int num = Utils.random.Next(10);
			for (int i = 0; i < num; i++)
			{
				f.files.Add(new FileEntry(generateEmail("Re: Junk", BoatMail.JunkEmail, "admin@" + comp.name), "Re:_Junk#" + OS.currentElapsedTime));
			}
		}
	}

	public void initFilesystem()
	{
		rootPath = new List<int>();
		accountsPath = new List<int>();
		root = comp.files.root.searchForFolder("mail");
		if (root == null)
		{
			root = new Folder("mail");
			comp.files.root.folders.Add(root);
		}
		rootPath.Add(comp.files.root.folders.IndexOf(root));
		accountsPath.Add(comp.files.root.folders.IndexOf(root));
		accounts = new Folder("accounts");
		accountsPath.Add(0);
		root.folders.Add(accounts);
	}

	public void setThemeColor(Color newThemeColor)
	{
		themeColor = newThemeColor;
	}

	public void addResponder(MailResponder resp)
	{
		responders.Add(resp);
	}

	public void removeResponder(MailResponder resp)
	{
		responders.Remove(resp);
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		state = 0;
		inboxPage = 0;
		totalPagesDetected = -1;
	}

	public void viewInbox(UserDetail newUser)
	{
		userFolder = null;
		state = 3;
		for (int i = 0; i < comp.users.Count; i++)
		{
			if (!comp.users[i].name.Equals(newUser.name))
			{
				continue;
			}
			user = comp.users[i];
			for (int j = 0; j < accounts.folders.Count; j++)
			{
				if (accounts.folders[j].name.Equals(user.name))
				{
					userFolder = accounts.folders[j];
					break;
				}
			}
			break;
		}
		comp.currentUser = user;
		if (userFolder != null)
		{
		}
	}

	public void addMail(string mail, string userTo)
	{
		for (int i = 0; i < responders.Count; i++)
		{
			responders[i].mailReceived(mail, userTo);
		}
		Folder folder = null;
		bool flag = false;
		for (int j = 0; j < accounts.folders.Count; j++)
		{
			if (accounts.folders[j].name.Equals(userTo))
			{
				folder = accounts.folders[j];
				folder = folder.folders[0];
				break;
			}
		}
		if (folder != null)
		{
			folder.files.Insert(0, new FileEntry(mail, getSubject(mail)));
			return;
		}
		throw new NullReferenceException("User " + userTo + " has no valid mail account on this mail server :" + comp.idName + ". Check account type and name matching!");
	}

	public bool MailWithSubjectExists(string userName, string mailSubject)
	{
		for (int i = 0; i < accounts.folders.Count; i++)
		{
			if (!accounts.folders[i].name.Equals(userName))
			{
				continue;
			}
			Folder folder = accounts.folders[i];
			for (int j = 0; j < folder.files.Count; j++)
			{
				string[] array = folder.files[j].data.Split(emailSplitDelims, StringSplitOptions.None);
				if (array.Length >= 4 && array[2].ToLower() == mailSubject.ToLower())
				{
					return true;
				}
			}
			break;
		}
		return false;
	}

	public override void userAdded(string name, string pass, byte type)
	{
		base.userAdded(name, pass, type);
		if (type == 0 || type == 1 || type == 2)
		{
			Folder folder = new Folder(name);
			folder.files.Add(new FileEntry("Username: " + name + "\nPassword: " + pass, "AccountInfo"));
			Folder folder2 = new Folder("inbox");
			addJunkEmails(folder2);
			folder.folders.Add(folder2);
			folder.folders.Add(new Folder("sent"));
			accounts.folders.Add(folder);
		}
	}

	public override string getSaveString()
	{
		return "<MailServer name=\"" + name + "\" color=\"" + Utils.convertColorToParseableString(themeColor) + "\"/>";
	}

	public virtual void drawTopBar(Rectangle bounds, SpriteBatch sb)
	{
		panelRect = new Rectangle(bounds.X + 1, bounds.Y, bounds.Width - (corner.Width + 2), panel.Height);
		sb.Draw(panel, panelRect, themeColor);
		sb.Draw(corner, new Vector2(bounds.X + bounds.Width - (corner.Width + 1), bounds.Y), themeColor);
	}

	public virtual void drawBackingGradient(Rectangle boundsTo, SpriteBatch sb)
	{
		Rectangle destinationRectangle = boundsTo;
		destinationRectangle.X++;
		destinationRectangle.Width -= 2;
		destinationRectangle.Height -= 2;
		sb.Draw(Utils.gradient, destinationRectangle, Color.Black);
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		Vector2 position = new Vector2(bounds.X + 8, bounds.Y + 120);
		drawTopBar(bounds, sb);
		switch (state)
		{
		case 0:
			sb.DrawString(GuiData.font, name + " " + LocaleTerms.Loc("Mail Server"), position, textColor);
			position.Y += 80f;
			position.Y += 35f;
			if (Button.doButton(800002, (int)position.X, (int)position.Y, 300, 40, LocaleTerms.Loc("Login"), themeColor))
			{
				state = 2;
				os.displayCache = "";
				os.execute("login");
				while (os.displayCache.Equals(""))
				{
				}
				os.display.command = name;
			}
			position.Y += 45f;
			if (Button.doButton(800003, (int)position.X, (int)position.Y, 300, 30, LocaleTerms.Loc("Exit"), themeColor))
			{
				os.display.command = "connect";
			}
			break;
		case 1:
			sb.DrawString(GuiData.font, LocaleTerms.Loc("Test"), position, textColor);
			position.Y += 80f;
			if (Button.doButton(800001, (int)position.X, (int)position.Y, 300, 60, "testing", null))
			{
				state = 0;
			}
			break;
		case 2:
			drawBackingGradient(bounds, sb);
			sb.DrawString(GuiData.font, LocaleTerms.Loc("login"), position, textColor);
			position.Y += 80f;
			doLoginDisplay(bounds, sb);
			break;
		case 3:
			doInboxDisplay(bounds, sb);
			break;
		case 4:
			doEmailViewerDisplay(bounds, sb);
			break;
		case 5:
			doRespondDisplay(bounds, sb);
			break;
		}
	}

	private int GetRenderTextHeight()
	{
		return (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 2f);
	}

	private int DrawMailMessageText(Rectangle textBounds, SpriteBatch sb, string[] text)
	{
		if (sectionedPanel == null || sectionedPanel.PanelHeight != GetRenderTextHeight())
		{
			sectionedPanel = new ScrollableSectionedPanel(GetRenderTextHeight(), sb.GraphicsDevice);
		}
		sectionedPanel.NumberOfPanels = text.Length;
		int itemsDrawn = 0;
		sectionedPanel.Draw(delegate(int index, Rectangle dest, SpriteBatch spBatch)
		{
			spBatch.DrawString(GuiData.tinyfont, LocalizedFileLoader.SafeFilterString(text[index]), new Vector2(dest.X, dest.Y), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.8f);
			itemsDrawn++;
		}, sb, textBounds);
		if (sectionedPanel.NumberOfPanels * sectionedPanel.PanelHeight < textBounds.Height)
		{
			return sectionedPanel.NumberOfPanels * sectionedPanel.PanelHeight;
		}
		return textBounds.Height;
	}

	public void doEmailViewerDisplay(Rectangle bounds, SpriteBatch sb)
	{
		Vector2 vector = new Vector2(bounds.X + 2, bounds.Y + 20);
		if (Button.doButton(800007, (int)vector.X, (int)vector.Y, bounds.Width - 20 - corner.Width, 30, LocaleTerms.Loc("Return to Inbox"), os.darkBackgroundColor))
		{
			state = 3;
		}
		vector.Y += 35f;
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = bounds.X + 1;
		tmpRect.Y = (int)vector.Y;
		tmpRect.Width = bounds.Width - 2;
		tmpRect.Height = 38;
		sb.Draw(Utils.white, tmpRect, textColor);
		vector.Y += 3f;
		sb.DrawString(GuiData.UITinyfont, "<" + emailData[1] + ">", vector, Color.Black);
		vector.Y += 18f;
		sb.DrawString(GuiData.UITinyfont, LocaleTerms.Loc("Subject") + ": " + LocalizedFileLoader.SafeFilterString(emailData[2]), vector, Color.Black);
		vector.Y += 25f;
		vector.X += 20f;
		int num = 25;
		int num2 = (emailData.Length - 4 + 1) * num;
		Rectangle textBounds = new Rectangle((int)vector.X, (int)vector.Y, bounds.Width - 22, (int)((float)bounds.Height - (vector.Y - (float)bounds.Y) - (float)num2));
		string text = "";
		if (LocaleActivator.ActiveLocaleIsCJK())
		{
			string text2 = "\t";
			string text3 = emailData[3];
			text3 = text3.Replace("。\n", text2);
			text3 = text3.Replace("\n", "");
			text3 = text3.Replace(text2, "。\n").Replace(text2, "！\n").Replace(text2, "!\n");
			text = Utils.SuperSmartTwimForWidth(LocalizedFileLoader.SafeFilterString(text3), bounds.Width - 50, GuiData.tinyfont);
			text = text.Replace("\r\n\r\n\r\n", "\r\n\r\n");
		}
		else
		{
			text = Utils.SmartTwimForWidth(LocalizedFileLoader.SafeFilterString(emailData[3]), bounds.Width - 50, GuiData.tinyfont);
		}
		vector.Y += DrawMailMessageText(textBounds, sb, text.Split(Utils.newlineDelim));
		vector.Y += num - 5;
		int num3 = 0;
		for (int i = 4; i < emailData.Length; i++)
		{
			if (AttachmentRenderer.RenderAttachment(emailData[i], os, vector, num3, buttonSound))
			{
				num3++;
				vector.Y += num;
			}
		}
		vector.Y = bounds.Y + bounds.Height - 35;
		if (Button.doButton(90200, (int)vector.X, (int)vector.Y, 300, 30, LocaleTerms.Loc("Reply"), null))
		{
			emailReplyStrings.Clear();
			addingNewReplyString = false;
			missionIncompleteReply = false;
			state = 5;
		}
	}

	private void DrawButtonGlow(Vector2 dpos, Vector2 labelSize)
	{
		Rectangle rectangle = new Rectangle((int)(dpos.X + labelSize.X + 5f), (int)dpos.Y, 20, 17);
		float num = Utils.QuadraticOutCurve(1f - os.timer % 1f);
		float num2 = 8.5f;
		Rectangle destinationRectangle = Utils.InsetRectangle(rectangle, (int)(-1f * (num2 * (1f - num))));
		GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, Utils.AddativeWhite * num * 0.32f);
		GuiData.spriteBatch.Draw(Utils.white, rectangle, Color.Black * 0.7f);
	}

	public virtual void doInboxHeader(Rectangle bounds, SpriteBatch sb)
	{
		Vector2 vector = new Vector2(bounds.X + 2, bounds.Y + 20);
		string text = string.Format(LocaleTerms.Loc("Logged in as {0}"), user.name);
		sb.DrawString(GuiData.font, text, vector, textColor);
		vector.Y += 26f;
		if (Button.doButton(800007, bounds.X + bounds.Width - 95, (int)vector.Y, 90, 30, LocaleTerms.Loc("Logout"), themeColor))
		{
			state = 0;
		}
		if (totalPagesDetected > 0)
		{
			int num = 100;
			int num2 = 20;
			vector.Y = bounds.Y + 91 - (num2 + 4);
			if (inboxPage < totalPagesDetected && Button.doButton(801008, (int)vector.X, (int)vector.Y, num, num2, "<", null))
			{
				inboxPage++;
			}
			vector.X += num + 2;
			vector.X += TextItem.doMeasuredFontLabel(vector, inboxPage + 1 + " / " + (totalPagesDetected + 1), GuiData.tinyfont, Utils.AddativeWhite).X + 4f;
			if (inboxPage > 0 && Button.doButton(801009, (int)vector.X, (int)vector.Y, num, num2, ">", null))
			{
				inboxPage--;
			}
		}
	}

	public void doInboxDisplay(Rectangle bounds, SpriteBatch sb)
	{
		int num = 24;
		doInboxHeader(bounds, sb);
		Vector2 vector = new Vector2(bounds.X + 2, bounds.Y + 91);
		Folder folder = userFolder.folders[0];
		Rectangle destinationRectangle = new Rectangle(bounds.X + 2, (int)vector.Y, bounds.Width - 4, num);
		Button.outlineOnly = true;
		int num2 = 0;
		while (destinationRectangle.Y + destinationRectangle.Height < bounds.Y + bounds.Height - 2)
		{
			sb.Draw(Utils.white, destinationRectangle, (num2 % 2 == 0) ? evenLine : oddLine);
			num2++;
			int height = destinationRectangle.Height;
			destinationRectangle.Height = 1;
			if (num2 > 1)
			{
				sb.Draw(Utils.white, destinationRectangle, seperatorLineColor);
			}
			destinationRectangle.Height = height;
			destinationRectangle.Y += num;
		}
		Rectangle tmpRect = GuiData.tmpRect;
		drawBackingGradient(bounds, sb);
		tmpRect.X = bounds.X + 1;
		tmpRect.Y = (int)vector.Y - 3;
		tmpRect.Width = bounds.Width - 4;
		tmpRect.Height = 3;
		sb.Draw(Utils.white, tmpRect, themeColor);
		tmpRect.X += tmpRect.Width;
		tmpRect.Width = 3;
		sb.Draw(Utils.white, tmpRect, themeColor);
		tmpRect.X = destinationRectangle.X;
		tmpRect.Y = (int)vector.Y;
		tmpRect.Width = 160;
		tmpRect.Height = bounds.Height - (tmpRect.Y - bounds.Y) - 1;
		sb.Draw(Utils.white, tmpRect, senderDarkeningColor);
		tmpRect.X += tmpRect.Width;
		tmpRect.Width = 3;
		sb.Draw(Utils.white, tmpRect, themeColor);
		tmpRect.X = destinationRectangle.X;
		tmpRect.Y = (int)vector.Y;
		tmpRect.Width = 160;
		tmpRect.Height = bounds.Height - (tmpRect.Y - bounds.Y) - 1;
		destinationRectangle.Y = (int)vector.Y;
		TextItem.DrawShadow = false;
		float num3 = bounds.Height - 2;
		int num4 = num2;
		int num5 = Math.Max(0, num4 * inboxPage - 1);
		totalPagesDetected = (int)((float)folder.files.Count / (float)num4);
		int num6 = Math.Min(num4, folder.files.Count - num5);
		for (int i = num5; i < num5 + num6; i++)
		{
			try
			{
				string[] array = folder.files[i].data.Split(emailSplitDelims, StringSplitOptions.None);
				byte b = Convert.ToByte(array[0]);
				int num7 = 8100 + i;
				if (GuiData.hot == num7)
				{
					sb.Draw(destinationRectangle: new Rectangle(bounds.X + 2, destinationRectangle.Y + 1, bounds.Width - 4, num - 2), texture: Utils.white, color: (i % 2 == 0) ? (Color.White * 0.07f) : (Color.Black * 0.2f));
				}
				if (Button.doButton(num7, bounds.X + 2, destinationRectangle.Y + 1, bounds.Width - 4, num - 2, "", Color.Transparent))
				{
					state = 4;
					selectedEmail = folder.files[i];
					emailData = selectedEmail.data.Split(emailSplitDelims, StringSplitOptions.None);
					selectedEmail.data = "1" + selectedEmail.data.Substring(1);
					if (sectionedPanel != null)
					{
						sectionedPanel.ScrollDown = 0f;
					}
				}
				if (b == 0)
				{
					sb.Draw(unopenedIcon, vector + Vector2.One, themeColor);
				}
				TextItem.doFontLabel(vector + new Vector2(unopenedIcon.Width + 1, 2f), array[1], GuiData.tinyfont, textColor, tmpRect.Width - unopenedIcon.Width - 3, num);
				TextItem.doFontLabel(vector + new Vector2(tmpRect.Width + 10, 2f), array[2], GuiData.tinyfont, textColor, bounds.Width - tmpRect.Width - 20, num);
				vector.Y += num;
				destinationRectangle.Y = (int)vector.Y;
			}
			catch (FormatException)
			{
			}
		}
		Button.outlineOnly = false;
	}

	public void doRespondDisplay(Rectangle bounds, SpriteBatch sb)
	{
		Vector2 vector = new Vector2(bounds.X + 2, bounds.Y + 20);
		string text = null;
		int width = bounds.Width - 20 - corner.Width;
		if (Button.doButton(800007, (int)vector.X, (int)vector.Y, width, 30, LocaleTerms.Loc("Return to Inbox"), os.darkBackgroundColor))
		{
			state = 3;
		}
		vector.Y += 50f;
		int num = 24;
		TextItem.doFontLabel(vector, LocaleTerms.Loc("Additional Details") + " :", GuiData.smallfont, null, (float)bounds.Width - (vector.X - (float)bounds.Width) * 1.2f);
		vector.Y += num;
		for (int i = 0; i < emailReplyStrings.Count; i++)
		{
			TextItem.doFontLabel(vector + new Vector2(25f, 0f), emailReplyStrings[i], GuiData.tinyfont, null, (float)bounds.Width - (vector.X - (float)bounds.X) * 2f - 20f);
			float num2 = Math.Min(GuiData.tinyfont.MeasureString(emailReplyStrings[i]).X, (float)bounds.Width - (vector.X - (float)bounds.X) * 2f - 20f);
			if (Button.doButton(80000 + i * 100, (int)(vector.X + num2 + 30f), (int)vector.Y, 20, 20, "-", null))
			{
				emailReplyStrings.RemoveAt(i);
			}
			vector.Y += num;
		}
		if (addingNewReplyString)
		{
			string data = null;
			bool flag = Programs.parseStringFromGetStringCommand(os, out data);
			if (data == null)
			{
				data = "";
			}
			vector.Y += 5f;
			GuiData.spriteBatch.Draw(Utils.white, new Rectangle(bounds.X + 1, (int)vector.Y, bounds.Width - 2 - bounds.Width / 9, 40), os.indentBackgroundColor);
			vector.Y += 10f;
			TextItem.doFontLabel(vector + new Vector2(25f, 0f), data, GuiData.tinyfont, null);
			Vector2 vector2 = GuiData.tinyfont.MeasureString(data);
			vector2.Y = 0f;
			if (os.timer % 1f <= 0.5f)
			{
				GuiData.spriteBatch.Draw(Utils.white, new Rectangle((int)(vector.X + vector2.X + 2f) + 25, (int)vector.Y, 4, 20), Color.White);
			}
			int num3 = bounds.Width - 1 - bounds.Width / 10;
			if (flag || Button.doButton(8000094, bounds.X + num3 - 4, (int)vector.Y - 10, bounds.Width / 9 - 3, 40, LocaleTerms.Loc("Add"), os.highlightColor))
			{
				if (!flag)
				{
					os.terminal.executeLine();
				}
				addingNewReplyString = false;
				emailReplyStrings.Add(data);
				text = null;
			}
			else
			{
				text = data;
			}
		}
		else if (Button.doButton(8000098, (int)(vector.X + 25f), (int)vector.Y, 20, 20, "+", null))
		{
			addingNewReplyString = true;
			os.execute("getString Detail");
			os.terminal.executionPreventionIsInteruptable = true;
		}
		vector.Y += 50f;
		if (Button.doButton(800008, (int)vector.X, (int)vector.Y, width, 30, LocaleTerms.Loc("Send"), null) && os.currentMission != null)
		{
			if (text != null)
			{
				os.terminal.executeLine();
				addingNewReplyString = false;
				if (!string.IsNullOrEmpty(text))
				{
					emailReplyStrings.Add(text);
				}
			}
			bool flag2 = false;
			ActiveMission currentMission = os.currentMission;
			flag2 = attemptCompleteMission(os.currentMission);
			if (!flag2)
			{
				for (int i = 0; i < os.branchMissions.Count; i++)
				{
					if (flag2)
					{
						break;
					}
					flag2 = attemptCompleteMission(os.branchMissions[i]);
					if (flag2)
					{
						os.branchMissions.Clear();
					}
				}
			}
			if (!flag2)
			{
				missionIncompleteReply = true;
			}
			else
			{
				AddSentEmailRecordFileForMissionCompletion(currentMission, emailReplyStrings);
				currentMission = null;
			}
		}
		vector.Y += 45f;
		if (Settings.forceCompleteEnabled && Button.doButton(800009, (int)vector.X, (int)vector.Y, width, 30, LocaleTerms.Loc("Force Complete"), null))
		{
			if (os.currentMission != null)
			{
				os.currentMission.finish();
				os.MissionCompleteFlashTime = 3f;
			}
			state = 3;
		}
		vector.Y += 70f;
		if (missionIncompleteReply && comp.idName == "jmail")
		{
			Rectangle dest = new Rectangle(bounds.X + 2, (int)vector.Y, bounds.Width - 4, 128);
			PatternDrawer.draw(dest, 1f, os.lockedColor * 0.1f, os.brightLockedColor, sb, PatternDrawer.errorTile);
			string text2 = LocaleTerms.Loc("Mission Incomplete");
			Vector2 vector3 = GuiData.font.MeasureString(text2);
			TextItem.doLabel(new Vector2((float)(bounds.X + bounds.Width / 2) - vector3.X / 2f, vector.Y + 40f), text2, null);
		}
	}

	private void AddSentEmailRecordFileForMissionCompletion(ActiveMission mission, List<string> additionalDetails)
	{
		try
		{
			Folder folder = userFolder.searchForFolder("sent");
			string text = mission.email.subject + " completed.";
			if (additionalDetails != null && additionalDetails.Count > 0)
			{
				text += "\nRequested Details:\n";
				for (int i = 0; i < additionalDetails.Count; i++)
				{
					text = text + additionalDetails[i] + "\n";
				}
			}
			string text2 = generateEmail(mission.email.subject + " : Complete", text, user.name + "@" + comp.name);
			string subject = getSubject(text2);
			FileEntry item = new FileEntry(text2, subject);
			folder.files.Add(item);
		}
		catch (Exception)
		{
		}
	}

	private bool attemptCompleteMission(ActiveMission mission)
	{
		if (mission.isComplete(emailReplyStrings) && (mission.ShouldIgnoreSenderVerification || mission.email.sender == emailData[1]))
		{
			mission.finish();
			state = 3;
			os.MissionCompleteFlashTime = 3f;
			return true;
		}
		return false;
	}

	public void doLoginDisplay(Rectangle bounds, SpriteBatch sb)
	{
		int num = bounds.X + 20;
		int num2 = bounds.Y + 100;
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		string[] array = os.displayCache.Split(separator, StringSplitOptions.None);
		string text = "";
		string text2 = "";
		int num3 = -1;
		int num4 = 0;
		if (array[0].Equals("loginData"))
		{
			text = ((!(array[1] != "")) ? os.terminal.currentLine : array[1]);
			if (array.Length > 2)
			{
				num4 = 1;
				text2 = array[2];
				if (text2.Equals(""))
				{
					for (int i = 0; i < os.terminal.currentLine.Length; i++)
					{
						text2 += "*";
					}
				}
				else
				{
					string text3 = "";
					for (int i = 0; i < text2.Length; i++)
					{
						text3 += "*";
					}
					text2 = text3;
				}
			}
			if (array.Length > 3)
			{
				num4 = 2;
				num3 = Convert.ToInt32(array[3]);
			}
		}
		Rectangle tmpRect = GuiData.tmpRect;
		tmpRect.X = bounds.X + 2;
		tmpRect.Y = num2;
		tmpRect.Height = 200;
		tmpRect.Width = bounds.Width - 4;
		sb.Draw(Utils.white, tmpRect, (num3 == 0) ? os.lockedColor : os.indentBackgroundColor);
		if (num3 != 0 && num3 != -1)
		{
			for (int i = 0; i < comp.users.Count; i++)
			{
				if (!comp.users[i].name.Equals(text))
				{
					continue;
				}
				user = comp.users[i];
				for (int j = 0; j < accounts.folders.Count; j++)
				{
					if (accounts.folders[j].name.Equals(user.name))
					{
						userFolder = accounts.folders[j];
						break;
					}
				}
				break;
			}
			state = 3;
		}
		tmpRect.Height = 22;
		num2 += 30;
		Vector2 vector = TextItem.doMeasuredLabel(new Vector2(num, num2), LocaleTerms.Loc("Login") + " ", textColor);
		if (num3 == 0)
		{
			num += (int)vector.X;
			TextItem.doLabel(new Vector2(num, num2), LocaleTerms.Loc("Failed"), os.brightLockedColor);
			num -= (int)vector.X;
		}
		num2 += 60;
		if (num4 == 0)
		{
			tmpRect.Y = num2;
			sb.Draw(Utils.white, tmpRect, os.subtleTextColor);
		}
		sb.DrawString(GuiData.smallfont, LocaleTerms.Loc("username") + " :", new Vector2(num, num2), textColor);
		num += 100;
		sb.DrawString(GuiData.smallfont, text, new Vector2(num, num2), textColor);
		num -= 100;
		num2 += 30;
		if (num4 == 1)
		{
			tmpRect.Y = num2;
			sb.Draw(Utils.white, tmpRect, os.subtleTextColor);
		}
		sb.DrawString(GuiData.smallfont, LocaleTerms.Loc("password") + " :", new Vector2(num, num2), textColor);
		num += 100;
		sb.DrawString(GuiData.smallfont, text2, new Vector2(num, num2), textColor);
		num2 += 30;
		num -= 100;
		if (num3 != -1)
		{
			if (Button.doButton(12345, num, num2, 70, 30, LocaleTerms.Loc("Back"), os.indentBackgroundColor))
			{
				state = 0;
			}
			if (Button.doButton(123456, num + 75, num2, 70, 30, LocaleTerms.Loc("Retry"), os.indentBackgroundColor))
			{
				os.displayCache = "";
				os.execute("login");
				while (os.displayCache.Equals(""))
				{
				}
				os.display.command = name;
			}
			return;
		}
		num2 += 65;
		for (int i = 0; i < comp.users.Count; i++)
		{
			if (comp.users[i].known && validUser(comp.users[i].type))
			{
				if (Button.doButton(123457 + i, num, num2, 300, 25, "User: " + comp.users[i].name + " Pass: " + comp.users[i].pass, os.darkBackgroundColor))
				{
					forceLogin(comp.users[i].name, comp.users[i].pass);
				}
				num2 += 27;
			}
		}
	}

	public void forceLogin(string username, string pass)
	{
		string prompt = os.terminal.prompt;
		os.terminal.currentLine = username;
		os.terminal.executeLine();
		while (os.terminal.prompt.Equals(prompt))
		{
		}
		os.terminal.currentLine = pass;
		os.terminal.executeLine();
	}

	public new static bool validUser(byte type)
	{
		return Daemon.validUser(type) || type == 2;
	}

	public static string generateEmail(string subject, string body, string sender)
	{
		string text = "";
		text = "0" + emailSplitDelimiter;
		text = text + cleanString(sender) + emailSplitDelimiter;
		text = text + cleanString(subject) + emailSplitDelimiter;
		return text + minimalCleanString(body);
	}

	public static string generateEmail(string subject, string body, string sender, List<string> attachments)
	{
		string text = generateEmail(subject, body, sender);
		text += emailSplitDelimiter;
		for (int i = 0; i < attachments.Count; i++)
		{
			text = text + attachments[i] + emailSplitDelimiter;
		}
		return text;
	}

	public static string getSubject(string mail)
	{
		string[] array = mail.Split(emailSplitDelims, StringSplitOptions.None);
		return array[2].Replace(' ', '_');
	}

	public static string cleanString(string s)
	{
		return s.Replace('\n', '_').Replace('\r', '_').Replace(emailSplitDelimiter, "_");
	}

	public static string minimalCleanString(string s)
	{
		return s.Replace('\t', ' ');
	}
}
