using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class DeathRowDatabaseDaemon : Daemon
{
	public struct DeathRowEntry
	{
		public string FName;

		public string LName;

		public string RecordNumber;

		public string Age;

		public string Date;

		public string Country;

		public string PriorRecord;

		public string IncidentReport;

		public string Statement;
	}

	public const string ROOT_FOLDERNAME = "dr_database";

	public const string RECORDS_FOLDERNAME = "records";

	public const string SERVER_INFO_FILENAME = "ServerDetails.txt";

	private static Texture2D Logo;

	private static Texture2D Circle;

	private Folder root;

	private Folder records;

	private int SelectedIndex = -1;

	private Color themeColor = new Color(207, 44, 19);

	private Vector2 recordScrollPosition = Vector2.Zero;

	public DeathRowDatabaseDaemon(Computer c, string serviceName, OS os)
		: base(c, serviceName, os)
	{
		if (Logo == null)
		{
			Logo = os.content.Load<Texture2D>("Sprites/DeathRowLogo");
		}
		if (Circle == null)
		{
			Circle = os.content.Load<Texture2D>("Sprites/ThinCircleOutline");
		}
	}

	public override void initFiles()
	{
		base.initFiles();
		root = new Folder("dr_database");
		records = new Folder("records");
		root.folders.Add(records);
		comp.files.root.folders.Add(root);
		FileEntry item = new FileEntry(Utils.readEntireFile("Content/Post/DeathRowServerInfo.txt").Replace('\t', ' '), "ServerDetails.txt");
		root.files.Add(item);
		LoadRecords();
	}

	private void LoadRecords(string data = null)
	{
		string text = Utils.readEntireFile("Content/Post/DeathRow.txt");
		if (data != null)
		{
			text = data;
		}
		string text2 = Utils.readEntireFile("Content/Post/DeathRowSpecials.txt");
		text += text2;
		string[] array = text.Split(new string[1] { "\r\n###%%##%%##%%##\r\n" }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			DeathRowEntry deathRowEntry = ConvertStringToRecord(array[i]);
			if (deathRowEntry.FName != null)
			{
				string nameEntry = deathRowEntry.LName + "_" + deathRowEntry.FName + "[" + deathRowEntry.RecordNumber + "]";
				string dataEntry = array[i].Replace("#", "#\n");
				FileEntry item = new FileEntry(dataEntry, nameEntry);
				records.files.Add(item);
			}
		}
		records.files.Sort((FileEntry f1, FileEntry f2) => string.Compare(f1.name, f2.name));
	}

	private DeathRowEntry ConvertStringToRecord(string data)
	{
		string[] array = data.Split(new string[2] { "#\n", "#" }, StringSplitOptions.None);
		DeathRowEntry result = default(DeathRowEntry);
		if (array.Length >= 9)
		{
			result.FName = array[0];
			result.LName = array[1];
			result.RecordNumber = array[2];
			result.Age = array[3];
			result.Date = array[4];
			result.Country = array[5];
			result.PriorRecord = array[6];
			result.IncidentReport = array[7];
			result.Statement = array[8];
		}
		return result;
	}

	public override void loadInit()
	{
		base.loadInit();
		root = comp.files.root.searchForFolder("dr_database");
		records = root.searchForFolder("records");
	}

	public override string getSaveString()
	{
		return "<DeathRowDatabase />";
	}

	private string ConvertFilesToOutput()
	{
		string text = "\r\n###%%##%%##%%##\r\n";
		string text2 = "#";
		string text3 = "";
		records.files.Sort((FileEntry f1, FileEntry f2) => string.Compare(f1.name, f2.name));
		for (int i = 0; i < records.files.Count; i++)
		{
			DeathRowEntry deathRowEntry = ConvertStringToRecord(records.files[i].data);
			if (deathRowEntry.RecordNumber != null)
			{
				string text4 = deathRowEntry.FName + text2 + deathRowEntry.LName + text2 + deathRowEntry.RecordNumber + text2 + deathRowEntry.Age + text2;
				string text5 = text4;
				text4 = text5 + deathRowEntry.Date + text2 + deathRowEntry.Country + text2 + deathRowEntry.PriorRecord + text2 + deathRowEntry.IncidentReport + text2 + deathRowEntry.Statement;
				text4 += text;
				text3 += text4;
			}
		}
		return text3;
	}

	private void TestStringConversion()
	{
		string data = ConvertFilesToOutput();
		records.files.Clear();
		LoadRecords(data);
	}

	public bool ContainsRecordForName(string fName, string lName)
	{
		string value = lName + "_" + fName;
		for (int i = 0; i < records.files.Count; i++)
		{
			if (records.files[i].name.StartsWith(value))
			{
				return true;
			}
		}
		return false;
	}

	public DeathRowEntry GetRecordForName(string fName, string lName)
	{
		string value = lName + "_" + fName;
		for (int i = 0; i < records.files.Count; i++)
		{
			if (records.files[i].name.StartsWith(value))
			{
				return ConvertStringToRecord(records.files[i].data);
			}
			try
			{
				DeathRowEntry result = ConvertStringToRecord(records.files[i].data);
				if (result.FName.ToLower() == fName.ToLower() && result.LName.ToLower() == lName.ToLower())
				{
					return result;
				}
			}
			catch (Exception)
			{
			}
		}
		return default(DeathRowEntry);
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		string[] array = new string[records.files.Count];
		for (int i = 0; i < records.files.Count; i++)
		{
			try
			{
				array[i] = records.files[i].name.Substring(0, records.files[i].name.IndexOf('[')).Replace("_", ", ");
			}
			catch (Exception)
			{
				array[i] = "UNKNOWN" + i;
			}
		}
		int num = bounds.Width / 3;
		int selectedIndex = SelectedIndex;
		SelectedIndex = SelectableTextList.doFancyList(832190831, bounds.X + 1, bounds.Y + 4, num, bounds.Height - 8, array, SelectedIndex, themeColor, HasDraggableScrollbar: true);
		if (SelectedIndex != selectedIndex)
		{
			recordScrollPosition = Vector2.Zero;
		}
		sb.Draw(Utils.white, new Rectangle(bounds.X + num - 1, bounds.Y + 1, 2, bounds.Height - 2), themeColor);
		DeathRowEntry entry = default(DeathRowEntry);
		Rectangle bounds2 = bounds;
		bounds2.X += num;
		bounds2.Width -= num + 1;
		if (SelectedIndex >= 0 && SelectedIndex < records.files.Count)
		{
			entry = ConvertStringToRecord(records.files[SelectedIndex].data);
		}
		if (entry.RecordNumber != null)
		{
			DrawRecord(bounds2, sb, entry);
		}
		else
		{
			DrawTitleScreen(bounds2, sb);
		}
	}

	private void DrawTitleScreen(Rectangle bounds, SpriteBatch sb)
	{
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		Rectangle destinationRectangle = new Rectangle(bounds.X + 12, bounds.Y + 12, Logo.Width, Logo.Height);
		sb.Draw(Logo, destinationRectangle, Color.White);
		float num = 1f;
		float num2 = 1.4f;
		float num3 = os.timer % 3f;
		if (num3 > 1f)
		{
			num3 = 0f;
		}
		float num4 = 1f - num3;
		num4 *= 0.4f;
		if (num3 == 0f)
		{
			num4 = 0f;
		}
		num3 = Utils.QuadraticOutCurve(num3) * (num2 - num);
		if (num3 > 0f)
		{
			num3 += num;
		}
		destinationRectangle = new Rectangle((int)((float)destinationRectangle.X - (float)(destinationRectangle.Width / 2) * num3), (int)((float)destinationRectangle.Y - (float)(destinationRectangle.Height / 2) * num3), (int)((float)destinationRectangle.Width * num3), (int)((float)destinationRectangle.Height * num3));
		destinationRectangle.X += Logo.Width / 2;
		destinationRectangle.Y += Logo.Height / 2;
		sb.Draw(Circle, destinationRectangle, Color.White * num4);
		Vector2 pos = new Vector2(bounds.X + Logo.Width + 22, bounds.Y + 14);
		TextItem.doFontLabel(pos, "DEATH ROW", GuiData.titlefont, themeColor, (float)(bounds.Width - Logo.Width) - 26f, 50f);
		pos.Y += 45f;
		TextItem.doFontLabel(pos, "EXECUTED OFFENDERS LISTING", GuiData.titlefont, Color.White, (float)(bounds.Width - Logo.Width) - 26f, 40f);
		pos.Y += Logo.Height - 40;
		pos.X = bounds.X + 12;
		FileEntry fileEntry = root.searchForFile("ServerDetails.txt");
		if (fileEntry != null)
		{
			string text = Utils.SmartTwimForWidth(fileEntry.data, bounds.Width - 30, GuiData.tinyfont);
			TextItem.doFontLabel(pos, text, GuiData.tinyfont, Color.White, bounds.Width - 20, (float)bounds.Height - 200f);
		}
		if (Button.doButton(166261601, bounds.X + 6, bounds.Y + bounds.Height - 26, bounds.Width - 12, 22, LocaleTerms.Loc("Exit"), Color.Black))
		{
			os.display.command = "connect";
		}
		TextItem.DrawShadow = drawShadow;
	}

	private void DrawRecord(Rectangle bounds, SpriteBatch sb, DeathRowEntry entry)
	{
		bounds.X += 2;
		bounds.Width -= 2;
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		int num = 850;
		ScrollablePanel.beginPanel(98302836, new Rectangle(bounds.X, bounds.Y, bounds.Width, num), recordScrollPosition);
		int num2 = bounds.Width - 16;
		Vector2 vector = new Vector2(5f, 5f);
		GuiData.spriteBatch.Draw(Logo, new Rectangle((int)vector.X, (int)vector.Y, 60, 60), Color.White);
		vector.X += 70f;
		TextItem.doFontLabel(vector, "DEATH ROW : EXECUTED OFFENDERS LISTING", GuiData.titlefont, themeColor, num2 - 80, 45f);
		vector.Y += 22f;
		if (Button.doButton(98102855, (int)vector.X, (int)vector.Y, bounds.Width / 2, 25, "Return", Color.Black))
		{
			SelectedIndex = -1;
		}
		vector.X = 5f;
		vector.Y += 55f;
		TextItem.doFontLabel(vector, "RECORD " + entry.RecordNumber, GuiData.titlefont, Color.White, num2 - 4, 60f);
		vector.Y += 70f;
		int seperatorHeight = 18;
		int margin = 12;
		vector = DrawCompactLabel(LocaleTerms.Loc("Name") + ":", entry.LName + ", " + entry.FName, vector, margin, seperatorHeight, num2);
		vector = DrawCompactLabel(LocaleTerms.Loc("Age") + ":", entry.Age, vector, margin, seperatorHeight, num2);
		seperatorHeight = 20;
		margin = 20;
		Vector2 zero = Vector2.Zero;
		TextItem.doFontLabel(vector, LocaleTerms.Loc("Incident Report") + ":", GuiData.smallfont, themeColor, num2);
		vector.Y += seperatorHeight;
		TextItem.DrawShadow = false;
		vector.Y += Math.Max(TextItem.doMeasuredSmallLabel(vector, Utils.SmartTwimForWidth(entry.IncidentReport, num2, GuiData.smallfont), Color.White).Y, seperatorHeight);
		vector.Y += margin;
		TextItem.doFontLabel(vector, LocaleTerms.Loc("Final Statement") + ":", GuiData.smallfont, themeColor, num2);
		vector.Y += seperatorHeight;
		zero = TextItem.doMeasuredSmallLabel(vector, Utils.SmartTwimForWidth(entry.Statement, num2, GuiData.smallfont), Color.White);
		vector.Y += margin;
		recordScrollPosition = ScrollablePanel.endPanel(98302836, recordScrollPosition, bounds, num - bounds.Height, onlyScrollWithMouseOver: true);
		TextItem.DrawShadow = drawShadow;
	}

	private Vector2 DrawCompactLabel(string label, string value, Vector2 drawPos, int margin, int seperatorHeight, int textWidth)
	{
		TextItem.doFontLabel(drawPos, label, GuiData.smallfont, themeColor, textWidth);
		drawPos.Y += seperatorHeight;
		TextItem.doFontLabel(drawPos, value, GuiData.smallfont, Color.White, textWidth);
		drawPos.Y += seperatorHeight;
		drawPos.Y += margin;
		return drawPos;
	}
}
