using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MedicalDatabaseDaemon : Daemon
{
	private class FileMedicalRecord
	{
		private const string DELIMITER = "\n-----------------\n";

		private static string[] SPLIT_DELIM = new string[1] { "\n-----------------\n" };

		public string Firstname;

		public string Lastname;

		public string record;

		public bool IsMale = true;

		public DateTime DOB;

		public FileMedicalRecord()
		{
		}

		public FileMedicalRecord(Person p)
		{
			Firstname = p.firstName;
			Lastname = p.lastName;
			IsMale = p.isMale;
			record = MedicalRecordToReport(p.medicalRecord);
			DOB = p.medicalRecord.DateofBirth;
		}

		public string MedicalRecordToReport(MedicalRecord rec)
		{
			return rec.ToString();
		}

		public override string ToString()
		{
			return Firstname + "\n-----------------\n" + Lastname + "\n-----------------\n" + (IsMale ? "male" : "female") + "\n-----------------\n" + Utils.SafeWriteDateTime(DOB) + "\n-----------------\n" + record;
		}

		public string ToEmailString()
		{
			string text = ToString();
			text = text.Replace("tions ::", "tions::\n");
			text = text.Replace("Visits ::", "Visits::\n");
			return text.Replace("Notes ::", "\nNotes ::\n");
		}

		public string GetFileName()
		{
			return Lastname.ToLower() + "_" + Firstname.ToLower() + ".rec";
		}

		public static bool RecordFromString(string rec, out FileMedicalRecord record)
		{
			CultureInfo cultureInfo = new CultureInfo("en-au");
			string[] array = rec.Split(SPLIT_DELIM, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length >= 5)
			{
				record = new FileMedicalRecord();
				record.Firstname = array[0];
				record.Lastname = array[1];
				record.IsMale = array[2] == "male";
				record.DOB = Utils.SafeParseDateTime(array[3]);
				record.record = array[4];
				return true;
			}
			record = null;
			return false;
		}
	}

	private enum MedicalDatabaseState
	{
		MainMenu,
		Search,
		Searching,
		Entry,
		Error,
		AboutScreen,
		SendReport,
		SendReportSearch,
		SendReportSending,
		SendReportComplete
	}

	private struct GridSpot
	{
		public float from;

		public float to;

		public float time;

		public float totalTime;
	}

	private const string FOLDER_NAME = "Medical";

	private const float SEARCH_TIME = 1.6f;

	private Folder recordsFolder;

	private MedicalDatabaseState state = MedicalDatabaseState.MainMenu;

	private float totalTimeThisState = 1f;

	private float elapsedTimeThisState = 0f;

	private string searchName = "";

	private string emailRecipientAddress = "";

	private FileEntry currentFile = null;

	private FileMedicalRecord currentRecord = null;

	private ScrollableSectionedPanel displayPanel;

	private string errorMessage = LocaleTerms.Loc("UNKNOWN ERROR");

	private Color theme_deep = new Color(8, 78, 90);

	private Color theme_strong = new Color(64, 157, 174);

	private Color theme_light = new Color(165, 237, 249);

	private Color theme_back = new Color(20, 20, 20);

	private Texture2D logo;

	private GridSpot[,] themeGrid = new GridSpot[20, 100];

	public MedicalDatabaseDaemon(Computer c, OS os)
		: base(c, LocaleTerms.Loc("Universal Medical Database"), os)
	{
		logo = os.content.Load<Texture2D>("Sprites/MedicalLogo");
	}

	public override void initFiles()
	{
		base.initFiles();
		recordsFolder = new Folder("Medical");
		comp.files.root.folders.Add(recordsFolder);
		for (int i = 0; i < People.all.Count; i++)
		{
			FileMedicalRecord fileMedicalRecord = new FileMedicalRecord(People.all[i]);
			FileEntry item = new FileEntry(fileMedicalRecord.ToString(), fileMedicalRecord.GetFileName());
			recordsFolder.files.Add(item);
		}
		string dataEntry = Utils.readEntireFile("Content/Post/MedicalDatabaseInfo.txt");
		comp.files.root.searchForFolder("home")?.files.Add(new FileEntry(dataEntry, "MedicalDatabaseInfo.txt"));
	}

	public override void loadInit()
	{
		base.loadInit();
		recordsFolder = comp.files.root.searchForFolder("Medical");
	}

	public override string getSaveString()
	{
		return "<MedicalDatabase />";
	}

	public void ResetThemeGrid()
	{
		for (int i = 0; i < themeGrid.GetLength(0); i++)
		{
			for (int j = 0; j < themeGrid.GetLength(1); j++)
			{
				ResetGridPoint(j, i);
			}
		}
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		elapsedTimeThisState = 0f;
		state = MedicalDatabaseState.MainMenu;
	}

	public void ResetGridPoint(int x, int y)
	{
		ref GridSpot reference = ref themeGrid[y, x];
		reference = new GridSpot
		{
			from = themeGrid[y, x].to,
			to = Utils.randm(2f),
			time = 0f,
			totalTime = Utils.randm(3f) + 1.2f
		};
	}

	private void LookupEntry()
	{
		List<string> list = new List<string>();
		string item = searchName.Trim().ToLower().Replace(" ", "_");
		list.Add(item);
		if (searchName.Contains(" "))
		{
			string text = searchName.Substring(searchName.IndexOf(" ")) + searchName.Substring(0, searchName.IndexOf(" "));
			text = text.Trim().ToLower().Replace(" ", "_");
			list.Add(text);
			text = searchName.Substring(searchName.IndexOf(" ")) + "_" + searchName.Substring(0, searchName.IndexOf(" "));
			text = text.Trim().ToLower().Replace(" ", "_");
			list.Add(text);
		}
		FileEntry fileEntry = null;
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = 0; j < recordsFolder.files.Count; j++)
			{
				if (recordsFolder.files[j].name.ToLower().StartsWith(list[i]))
				{
					fileEntry = recordsFolder.files[j];
					break;
				}
			}
			if (fileEntry != null)
			{
				break;
			}
		}
		if (fileEntry == null)
		{
			state = MedicalDatabaseState.Error;
			errorMessage = LocaleTerms.Loc("No entry found for name") + " " + searchName + "\n" + LocaleTerms.Loc("Permutations tested") + ":\n";
			for (int j = 0; j < list.Count; j++)
			{
				errorMessage = errorMessage + list[j] + "\n";
			}
			elapsedTimeThisState = 0f;
			return;
		}
		currentFile = fileEntry;
		FileMedicalRecord record = new FileMedicalRecord();
		if (FileMedicalRecord.RecordFromString(currentFile.data, out record))
		{
			currentRecord = record;
			state = MedicalDatabaseState.Entry;
			elapsedTimeThisState = 0f;
			return;
		}
		elapsedTimeThisState = 0f;
		state = MedicalDatabaseState.Error;
		errorMessage = LocaleTerms.Loc("Corrupt record") + " --\n" + LocaleTerms.Loc("Unable to parse record") + " " + currentFile.name;
	}

	private void UpdateStates(float t)
	{
		elapsedTimeThisState += t;
		if (elapsedTimeThisState >= totalTimeThisState && state == MedicalDatabaseState.Searching)
		{
			LookupEntry();
		}
	}

	private void SendReportEmail(FileMedicalRecord record, string emailAddress)
	{
		try
		{
			string mail = MailServer.generateEmail(LocaleTerms.Loc("MedicalRecord") + " - " + record.Lastname + "_" + record.Firstname, record.ToEmailString(), "records@meddb.org");
			string userTo = emailAddress;
			if (emailAddress.Contains('@'))
			{
				userTo = emailAddress.Substring(0, emailAddress.IndexOf('@'));
			}
			Computer computer = Programs.getComputer(os, "jmail");
			if (computer != null)
			{
				((MailServer)computer.getDaemon(typeof(MailServer)))?.addMail(mail, userTo);
			}
		}
		catch (Exception)
		{
		}
	}

	private void updateGrid()
	{
		float num = (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		for (int i = 0; i < themeGrid.GetLength(0); i++)
		{
			for (int j = 0; j < themeGrid.GetLength(1); j++)
			{
				GridSpot gridSpot = themeGrid[i, j];
				gridSpot.time += num;
				if (gridSpot.time >= gridSpot.totalTime)
				{
					ResetGridPoint(j, i);
				}
				else
				{
					themeGrid[i, j] = gridSpot;
				}
			}
		}
	}

	private void drawGrid(Rectangle bounds, SpriteBatch sb, int width)
	{
		int num = 12;
		int x = bounds.X + bounds.Width - num - 1;
		Rectangle destinationRectangle = new Rectangle(x, bounds.Y + 1, num, num);
		int num2;
		int num3 = (num2 = 0);
		while (destinationRectangle.Y + 1 < bounds.Y + bounds.Height)
		{
			if (destinationRectangle.Y + num + 1 >= bounds.Y + bounds.Height)
			{
				destinationRectangle.Height = bounds.Y + bounds.Height - (destinationRectangle.Y + 2);
			}
			while (destinationRectangle.X - num > bounds.X + bounds.Width - width - 1)
			{
				GridSpot gridSpot = themeGrid[num3 % themeGrid.GetLength(0), num2 % themeGrid.GetLength(1)];
				float num4 = gridSpot.time / gridSpot.totalTime;
				float num5 = gridSpot.from + num4 * (gridSpot.to - gridSpot.from);
				Color value = theme_deep;
				Color value2 = theme_strong;
				if (num5 >= 1f)
				{
					num5 -= 1f;
					value = theme_strong;
					value2 = theme_light;
				}
				Color color = Color.Lerp(value, value2, num5);
				sb.Draw(Utils.white, destinationRectangle, color);
				num3++;
				destinationRectangle.X -= num + 1;
			}
			num2++;
			destinationRectangle.X = x;
			destinationRectangle.Y += num + 1;
		}
	}

	public void DrawError(Rectangle bounds, SpriteBatch sb)
	{
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + 12, (int)((double)bounds.Width / 1.6) - 4, bounds.Height / 2);
		rectangle.Height = (int)((float)logo.Height / (float)logo.Width * (float)rectangle.Height);
		sb.Draw(logo, rectangle, theme_deep * 0.4f);
		int width = rectangle.Width;
		rectangle.Y += 100;
		rectangle.Height = 35;
		rectangle.Width = (int)((float)width * Utils.QuadraticOutCurve(Math.Min(elapsedTimeThisState * 2f, 1f)));
		DrawMessage(LocaleTerms.Loc("Error"), big: true, sb, rectangle, os.lockedColor, Color.White);
		rectangle.Y += rectangle.Height + 2;
		rectangle.Height = 100;
		rectangle.Width = (int)((float)width * Utils.QuadraticOutCurve(Math.Min(elapsedTimeThisState * 1.5f, 1f)));
		DrawMessageBot(errorMessage, big: false, sb, rectangle, theme_back, theme_light);
		rectangle.Width = width;
		rectangle.Y += rectangle.Height + 2;
		if (Button.doButton(444402002, rectangle.X, rectangle.Y, rectangle.Width, 24, LocaleTerms.Loc("Back to menu"), theme_strong))
		{
			elapsedTimeThisState = 0f;
			state = MedicalDatabaseState.MainMenu;
		}
	}

	public void DrawSendReport(Rectangle bounds, SpriteBatch sb)
	{
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + 12, (int)((double)bounds.Width / 1.6) - 4, bounds.Height / 2);
		rectangle.Height = (int)((float)logo.Height / (float)logo.Width * (float)rectangle.Height);
		sb.Draw(logo, rectangle, theme_deep * 0.4f);
		int width = rectangle.Width;
		rectangle.Y += 100;
		rectangle.Height = 35;
		rectangle.Width = (int)((float)width * Utils.QuadraticOutCurve(Math.Min(elapsedTimeThisState * 2f, 1f)));
		DrawMessage(LocaleTerms.Loc("Send Record Copy"), big: true, sb, rectangle, theme_deep, Color.White);
		rectangle.Y += rectangle.Height + 2;
		rectangle.Height = 22;
		rectangle.Width = (int)((float)width * Utils.QuadraticOutCurve(Math.Min(elapsedTimeThisState * 1.5f, 1f)));
		DrawMessageBot(LocaleTerms.Loc("Record for") + " " + currentRecord.Firstname + " " + currentRecord.Lastname, big: false, sb, rectangle, theme_back, theme_light);
		rectangle.Width = width;
		rectangle.Y += rectangle.Height + 12;
		rectangle.Height = 35;
		rectangle.Width = (int)(float)width;
		DrawMessage(LocaleTerms.Loc("Recipient Address"), big: true, sb, rectangle, theme_deep, Color.White);
		rectangle.Y += rectangle.Height + 2;
		string upperPrompt = " ---------";
		rectangle.Height = 130;
		Rectangle bounds2 = rectangle;
		if (bounds2.Width < 400)
		{
			bounds2.Width = bounds.Width - bounds.Width / 4 - 2;
		}
		if (state == MedicalDatabaseState.SendReportSearch)
		{
			emailRecipientAddress = GetStringUIControl.DrawGetStringControl(LocaleTerms.Loc("Recipient Address (Case Sensitive):") + " ", bounds2, delegate
			{
				elapsedTimeThisState = 0f;
				state = MedicalDatabaseState.Error;
				errorMessage = LocaleTerms.Loc("Error getting recipient email");
			}, delegate
			{
				state = MedicalDatabaseState.SendReport;
			}, sb, os, theme_strong, os.lockedColor, upperPrompt);
			rectangle.Y += 26;
			if (emailRecipientAddress != null)
			{
				state = MedicalDatabaseState.SendReportSending;
				elapsedTimeThisState = 1f;
			}
		}
		else
		{
			string valueText = ((emailRecipientAddress == null) ? LocaleTerms.Loc("Undefined") : emailRecipientAddress);
			GetStringUIControl.DrawGetStringControlInactive(LocaleTerms.Loc("Recipient Address") + ": ", valueText, bounds2, sb, os, upperPrompt);
		}
		rectangle.Y += rectangle.Height + 2;
		rectangle.Height = 24;
		if (state == MedicalDatabaseState.SendReport)
		{
			if (Button.doButton(444402023, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Specify Address"), theme_strong))
			{
				GetStringUIControl.StartGetString("Recipient_Address", os);
				state = MedicalDatabaseState.SendReportSearch;
			}
		}
		else if (state == MedicalDatabaseState.SendReportSending || state == MedicalDatabaseState.SendReportComplete)
		{
			float num = (elapsedTimeThisState - 1f) / 3f;
			if (state == MedicalDatabaseState.SendReportComplete)
			{
				num = 1f;
			}
			if (num >= 1f && state != MedicalDatabaseState.SendReportComplete)
			{
				state = MedicalDatabaseState.SendReportComplete;
				SendReportEmail(currentRecord, emailRecipientAddress);
			}
			sb.Draw(Utils.white, rectangle, theme_back);
			Rectangle destinationRectangle = rectangle;
			destinationRectangle.Width = (int)((float)destinationRectangle.Width * Utils.QuadraticOutCurve(num));
			sb.Draw(Utils.white, destinationRectangle, theme_light);
			sb.DrawString(GuiData.smallfont, (state == MedicalDatabaseState.SendReportComplete) ? LocaleTerms.Loc("COMPLETE") : (LocaleTerms.Loc("SENDING") + " ..."), new Vector2(rectangle.X, rectangle.Y + 2), Color.Black);
		}
		rectangle.Y += rectangle.Height + 2;
		if (state == MedicalDatabaseState.SendReportComplete && Button.doButton(444402001, rectangle.X, rectangle.Y, rectangle.Width, 24, LocaleTerms.Loc("Send to different address"), theme_light))
		{
			state = MedicalDatabaseState.SendReport;
		}
		rectangle.Y += rectangle.Height + 2;
		if (Button.doButton(444402002, rectangle.X, rectangle.Y, rectangle.Width, 24, LocaleTerms.Loc("Back to menu"), theme_strong))
		{
			elapsedTimeThisState = 0f;
			state = MedicalDatabaseState.MainMenu;
		}
	}

	public void DrawAbout(Rectangle bounds, SpriteBatch sb)
	{
		string text = null;
		Folder folder = comp.files.root.searchForFolder("home");
		if (folder != null)
		{
			FileEntry fileEntry = folder.searchForFile("MedicalDatabaseInfo.txt");
			if (fileEntry != null)
			{
				text = fileEntry.data;
			}
		}
		if (text == null)
		{
			state = MedicalDatabaseState.Error;
			errorMessage = "DatabaseInfo file not found\n~/home/MedicalDatabaseInfo.txt\nCould not be found or opened";
			elapsedTimeThisState = 0f;
			return;
		}
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + 12, (int)((double)bounds.Width / 1.6) - 4, bounds.Height / 2);
		rectangle.Height = (int)((float)logo.Height / (float)logo.Width * (float)rectangle.Height);
		sb.Draw(logo, rectangle, theme_deep * 0.4f);
		int width = rectangle.Width;
		rectangle.Y += 100;
		rectangle.Height = 35;
		rectangle.Width = (int)((float)width * Utils.QuadraticOutCurve(Math.Min(elapsedTimeThisState * 2f, 1f)));
		DrawMessage(LocaleTerms.Loc("Info"), big: true, sb, rectangle, theme_deep, Color.White);
		text = Utils.SuperSmartTwimForWidth(text, rectangle.Width - 12, GuiData.tinyfont);
		rectangle.Y += rectangle.Height + 2;
		rectangle.Height = Math.Min(bounds.Height - 200, 420);
		rectangle.Width = (int)((float)width * Utils.QuadraticOutCurve(Math.Min(elapsedTimeThisState * 1.5f, 1f)));
		DrawMessageBot(text, big: false, sb, rectangle, theme_back, theme_light);
		rectangle.Width = width;
		rectangle.Y += rectangle.Height + 2;
		if (Button.doButton(444402002, rectangle.X, rectangle.Y, rectangle.Width, 24, LocaleTerms.Loc("Back to menu"), theme_strong))
		{
			elapsedTimeThisState = 0f;
			state = MedicalDatabaseState.MainMenu;
		}
	}

	public void DrawEntry(Rectangle bounds, SpriteBatch sb)
	{
		int num = 34;
		if (displayPanel == null)
		{
			displayPanel = new ScrollableSectionedPanel(26, sb.GraphicsDevice);
		}
		List<Action<int, Rectangle, SpriteBatch>> drawCalls = new List<Action<int, Rectangle, SpriteBatch>>();
		Rectangle allTextBounds;
		Rectangle rectangle = (allTextBounds = new Rectangle(bounds.X + 2, bounds.Y + 12, (int)((double)bounds.Width / 1.6) - 4, bounds.Height / 2));
		allTextBounds.Width += 2;
		allTextBounds.Y += num;
		allTextBounds.Height = bounds.Height - num - 2 - 40 - 28;
		rectangle.Height = logo.Height / logo.Width * rectangle.Width;
		sb.Draw(logo, rectangle, theme_deep * 0.4f);
		rectangle.Height = num;
		DrawMessage(currentRecord.Lastname + ", " + currentRecord.Firstname, big: true, sb, rectangle, theme_light, theme_back);
		rectangle.Y += num;
		num = (rectangle.Height = 22);
		string[] lines = currentRecord.record.Split(Utils.newlineDelim);
		string[] separator = new string[5] { " :: ", ":: ", " ::", "::", "\n" };
		bool flag = false;
		for (int i = 0; i < lines.Length; i++)
		{
			string[] sections = Utils.SuperSmartTwimForWidth(lines[i], rectangle.Width - 12, GuiData.tinyfont).Split(separator, StringSplitOptions.RemoveEmptyEntries);
			if (sections.Length > 1)
			{
				for (int j = 0; j < sections.Length; j++)
				{
					if (j == 0 && !flag)
					{
						if (sections[j] == "Notes")
						{
							flag = true;
						}
						int secID = j;
						drawCalls.Add(delegate(int index, Rectangle drawPos, SpriteBatch sprBatch)
						{
							Rectangle dest4 = drawPos;
							dest4.Y++;
							dest4.Height -= 2;
							DrawMessage(sections[secID] + " :", big: false, sprBatch, dest4, theme_deep, theme_light);
						});
						rectangle.Y += num + 2;
					}
					else if (sections[j].Trim().Length > 0)
					{
						int subSecID = j;
						drawCalls.Add(delegate(int index, Rectangle drawPos, SpriteBatch sprBatch)
						{
							Rectangle dest3 = drawPos;
							dest3.Y++;
							dest3.Height -= 2;
							DrawMessage(sections[subSecID], big: false, sprBatch, dest3);
						});
						rectangle.Y += num + 2;
					}
				}
			}
			else if (lines[i].Trim().Length > 0)
			{
				int idx = i;
				drawCalls.Add(delegate(int index, Rectangle drawPos, SpriteBatch sprBatch)
				{
					Rectangle rectangle2 = drawPos;
					rectangle2.Y++;
					rectangle2.Height -= 2;
					DrawMessage(lines[idx], big: false, sprBatch, drawPos);
				});
				rectangle.Y += num + 2;
			}
		}
		drawCalls.Add(delegate(int index, Rectangle drawPos, SpriteBatch sprBatch)
		{
			Rectangle dest2 = drawPos;
			dest2.Y += 2;
			dest2.Height -= 4;
			DrawMessage(" ", big: false, sprBatch, dest2);
		});
		rectangle.Y += num + 2;
		displayPanel.NumberOfPanels = drawCalls.Count;
		displayPanel.Draw(delegate(int idx, Rectangle rect, SpriteBatch sprBatch)
		{
			if ((drawCalls.Count + 1) * displayPanel.PanelHeight >= allTextBounds.Height)
			{
				rect.Width -= 10;
			}
			drawCalls[idx](idx, rect, sprBatch);
		}, sb, allTextBounds);
		rectangle.Y += 2;
		if (Button.doButton(444402033, rectangle.X, bounds.Y + bounds.Height - 26, rectangle.Width, 24, LocaleTerms.Loc("Back to menu"), theme_strong))
		{
			elapsedTimeThisState = 0f;
			state = MedicalDatabaseState.MainMenu;
		}
		if (Button.doButton(444402035, rectangle.X, bounds.Y + bounds.Height - 26 - 2 - 26, rectangle.Width, 24, LocaleTerms.Loc("e-mail this record"), theme_light))
		{
			elapsedTimeThisState = 0f;
			state = MedicalDatabaseState.SendReport;
		}
		Rectangle dest = new Rectangle(rectangle.X + rectangle.Width + 2, bounds.Y + 34 + 12, (int)((double)bounds.Width / 6.5) - 2, bounds.Height - 4);
		int height = 33;
		int height2 = 22;
		dest.Height = height;
		DrawMessage(LocaleTerms.Loc("Age"), big: true, sb, dest);
		dest.Y += dest.Height + 2;
		TimeSpan timeSpan = DateTime.Now - currentRecord.DOB;
		int num2 = (int)((double)timeSpan.Days / 365.0);
		DrawMessage(string.Concat(timeSpan.Days / 365), big: true, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height;
		dest.Height = height2;
		DrawMessage(LocaleTerms.Loc("Years"), big: false, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height + 2;
		dest.Height = height;
		DrawMessage(string.Concat(timeSpan.Days - num2 * 365), big: true, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height;
		dest.Height = height2;
		DrawMessage(LocaleTerms.Loc("Days"), big: false, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height + 2;
		dest.Height = height;
		DrawMessage(string.Concat(timeSpan.Hours), big: true, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height;
		dest.Height = height2;
		DrawMessage(LocaleTerms.Loc("Hours"), big: false, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height + 2;
		dest.Height = height;
		DrawMessage(string.Concat(timeSpan.Minutes), big: true, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height;
		dest.Height = height2;
		DrawMessage(LocaleTerms.Loc("Minutes"), big: false, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height + 2;
		dest.Height = height;
		DrawMessage(string.Concat(timeSpan.Seconds), big: true, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height;
		dest.Height = height2;
		DrawMessage(LocaleTerms.Loc("Seconds"), big: false, sb, dest, Color.Transparent, theme_light);
		dest.Y += dest.Height + 2;
		dest.Height = height;
	}

	public void DrawMenu(Rectangle bounds, SpriteBatch sb)
	{
		int num = 34;
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + 12, bounds.Width / 2 - 4, num);
		DrawMessage(LocaleTerms.Loc("Universal Medical"), big: true, sb, rectangle, theme_light, Color.Black);
		rectangle.Y += num + 2;
		rectangle.Height = 20;
		DrawMessage(LocaleTerms.Loc("Records & Monitoring Services"), big: false, sb, rectangle);
		sb.Draw(destinationRectangle: new Rectangle(bounds.X + bounds.Width / 2 + 10, bounds.Y + 12, bounds.Width / 4 - 12, (int)((float)logo.Height / (float)logo.Width * ((float)bounds.Width / 4f))), texture: logo, color: theme_light);
		Rectangle destinationRectangle2 = new Rectangle(rectangle.X + 10, rectangle.Y + 40, rectangle.Width - 20, 1);
		sb.Draw(Utils.white, destinationRectangle2, Utils.SlightlyDarkGray * 0.5f);
		destinationRectangle2.Y += 4;
		sb.Draw(Utils.white, destinationRectangle2, Utils.SlightlyDarkGray * 0.5f);
		rectangle.Y += 90;
		if (!(comp.adminIP == os.thisComputer.ip))
		{
			rectangle.Height = bounds.Y + bounds.Height - rectangle.Y;
			DrawNoAdminMenuSection(rectangle, sb);
			return;
		}
		rectangle.Height = 80;
		DrawMessageBot(LocaleTerms.Loc("Information"), big: true, sb, rectangle, theme_light, Color.Black);
		rectangle.Y += rectangle.Height + 2;
		rectangle.Height = 20;
		DrawMessage(LocaleTerms.Loc("Details and Administration"), big: false, sb, rectangle);
		rectangle.Y += rectangle.Height + 2;
		if (Button.doButton(444402000, rectangle.X + 1, rectangle.Y, rectangle.Width, 24, LocaleTerms.Loc("Info"), theme_strong))
		{
			state = MedicalDatabaseState.AboutScreen;
			elapsedTimeThisState = 0f;
		}
		rectangle.Y += 60;
		rectangle.Height = 80;
		DrawMessageBot(LocaleTerms.Loc("Database"), big: true, sb, rectangle, theme_light, Color.Black);
		rectangle.Y += rectangle.Height + 2;
		rectangle.Height = 20;
		DrawMessage(LocaleTerms.Loc("Records Lookup"), big: false, sb, rectangle);
		rectangle.Y += rectangle.Height + 2;
		if (state == MedicalDatabaseState.MainMenu)
		{
			if (Button.doButton(444402005, rectangle.X + 1, rectangle.Y, rectangle.Width, 24, LocaleTerms.Loc("Search"), theme_strong))
			{
				state = MedicalDatabaseState.Search;
				elapsedTimeThisState = 0f;
				GetStringUIControl.StartGetString("Patient_Name", os);
			}
			rectangle.Y += 26;
			if (Button.doButton(444402007, rectangle.X + 1, rectangle.Y, rectangle.Width, 24, LocaleTerms.Loc("Random Entry"), theme_strong))
			{
				searchName = recordsFolder.files[Utils.random.Next(recordsFolder.files.Count)].name;
				elapsedTimeThisState = 0f;
				state = MedicalDatabaseState.Searching;
				totalTimeThisState = 1.6f;
			}
		}
		else if (state == MedicalDatabaseState.Search)
		{
			float num2 = Utils.QuadraticOutCurve(Math.Min(1f, elapsedTimeThisState * 2f));
			Rectangle bounds2 = new Rectangle(rectangle.X, rectangle.Y - 10, rectangle.Width, (int)(num2 * 72f));
			sb.Draw(destinationRectangle: new Rectangle(bounds2.X, rectangle.Y + 2, rectangle.Width, (int)(num2 * 32f)), texture: Utils.white, color: os.darkBackgroundColor);
			string text = GetStringUIControl.DrawGetStringControl(LocaleTerms.Loc("Enter patient name") + " :", bounds2, delegate
			{
				elapsedTimeThisState = 0f;
				state = MedicalDatabaseState.Error;
				errorMessage = LocaleTerms.Loc("Error in name input");
			}, delegate
			{
				elapsedTimeThisState = 0f;
				state = MedicalDatabaseState.MainMenu;
				os.terminal.executeLine();
			}, sb, os, theme_strong, theme_back, "", Color.Transparent);
			if (text != null)
			{
				searchName = text;
				elapsedTimeThisState = 0f;
				state = MedicalDatabaseState.Searching;
				totalTimeThisState = 1.6f;
			}
		}
		else if (state == MedicalDatabaseState.Searching)
		{
			Rectangle destinationRectangle4 = new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 24);
			sb.Draw(Utils.white, destinationRectangle4, theme_deep);
			destinationRectangle4.Width = (int)((float)destinationRectangle4.Width * Utils.QuadraticOutCurve(elapsedTimeThisState / totalTimeThisState));
			sb.Draw(Utils.white, destinationRectangle4, theme_light);
			destinationRectangle4.Y += destinationRectangle4.Height / 2 - 2;
			destinationRectangle4.Height = 4;
			sb.Draw(Utils.white, destinationRectangle4, theme_deep);
		}
		if (Button.doButton(444402800, rectangle.X + 1, bounds.Y + bounds.Height - 28, rectangle.Width, 24, LocaleTerms.Loc("Exit Database View"), os.lockedColor))
		{
			os.display.command = "connect";
		}
	}

	private void DrawNoAdminMenuSection(Rectangle bounds, SpriteBatch sb)
	{
		bounds.Height -= 2;
		bounds.X++;
		bounds.Width -= 2;
		bounds.Height -= 30;
		PatternDrawer.draw(bounds, 0.2f, Color.Transparent, os.brightLockedColor, sb, PatternDrawer.errorTile);
		bounds.Height += 30;
		Rectangle dest = bounds;
		dest.Height = 36;
		dest.Y = bounds.Y + bounds.Height / 2 - dest.Height / 2;
		DrawMessage(LocaleTerms.Loc("Admin Access"), big: true, sb, dest, os.brightLockedColor * 0.8f, Color.Black);
		dest.Y += dest.Height + 2;
		dest.Height = 22;
		DrawMessage(LocaleTerms.Loc("Required for use"), big: false, sb, dest, theme_back, os.brightLockedColor);
		if (Button.doButton(444402800, bounds.X + 1, bounds.Y + bounds.Height - 28, bounds.Width, 24, LocaleTerms.Loc("Exit Database View"), os.lockedColor))
		{
			os.display.command = "connect";
		}
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		UpdateStates((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		updateGrid();
		drawGrid(bounds, sb, bounds.Width / 4);
		switch (state)
		{
		case MedicalDatabaseState.MainMenu:
		case MedicalDatabaseState.Search:
		case MedicalDatabaseState.Searching:
			DrawMenu(bounds, sb);
			break;
		case MedicalDatabaseState.Entry:
			DrawEntry(bounds, sb);
			break;
		case MedicalDatabaseState.Error:
			DrawError(bounds, sb);
			break;
		case MedicalDatabaseState.AboutScreen:
			DrawAbout(bounds, sb);
			break;
		case MedicalDatabaseState.SendReport:
		case MedicalDatabaseState.SendReportSearch:
		case MedicalDatabaseState.SendReportSending:
		case MedicalDatabaseState.SendReportComplete:
			DrawSendReport(bounds, sb);
			break;
		}
	}

	private void DrawMessage(string msg, bool big, SpriteBatch sb, Rectangle dest)
	{
		DrawMessage(msg, big, sb, dest, theme_back, theme_light);
	}

	private void DrawMessage(string msg, bool big, SpriteBatch sb, Rectangle dest, Color back, Color front)
	{
		sb.Draw(Utils.white, dest, back);
		SpriteFont spriteFont = (big ? GuiData.font : GuiData.tinyfont);
		Vector2 vector = spriteFont.MeasureString(msg);
		Vector2 position = new Vector2((float)(dest.X + dest.Width - 4) - vector.X, (float)dest.Y + (float)dest.Height / 2f - vector.Y / 2f);
		if (vector.X >= (float)(dest.Width - 4) || vector.Y >= (float)dest.Height)
		{
			TextItem.doFontLabelToSize(dest, msg, spriteFont, front);
		}
		else
		{
			sb.DrawString(spriteFont, msg, position, front);
		}
	}

	private void DrawMessageBot(string msg, bool big, SpriteBatch sb, Rectangle dest, Color back, Color front)
	{
		sb.Draw(Utils.white, dest, back);
		SpriteFont spriteFont = (big ? GuiData.font : GuiData.tinyfont);
		Vector2 vector = spriteFont.MeasureString(msg);
		Vector2 position = new Vector2((float)(dest.X + dest.Width - 4) - vector.X, (float)(dest.Y + dest.Height) - vector.Y - 4f);
		if (vector.X >= (float)(dest.Width - 4) || vector.Y >= (float)(dest.Height - 4))
		{
			TextItem.doFontLabelToSize(dest, msg, spriteFont, front);
		}
		else
		{
			sb.DrawString(spriteFont, msg, position, front);
		}
	}
}
