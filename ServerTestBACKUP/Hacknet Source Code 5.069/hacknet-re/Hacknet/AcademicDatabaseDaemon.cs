using System;
using System.Collections.Generic;
using System.Globalization;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class AcademicDatabaseDaemon : Daemon
{
	private enum ADDEditField
	{
		None,
		Uni,
		Degree,
		GPA
	}

	private enum ADDState
	{
		Welcome,
		Seach,
		MultiMatchSearch,
		Entry,
		PendingResult,
		EntryNotFound,
		MultipleEntriesFound,
		InfoPanel,
		EditPerson,
		EditEntry
	}

	public const string ROOT_FOLDERNAME = "academic_data";

	public const string ENTRIES_FOLDERNAME = "entry_cache";

	public const string CONFIG_FILENAME = "config.sys";

	public const string INFO_FILENAME = "info.txt";

	private const float SEARCH_TIME = 3.6f;

	private const float MULTI_MATCH_SEARCH_TIME = 0.7f;

	public const string DEGREE_SPLIT_DELIM = "--------------------";

	private ADDState state = ADDState.Welcome;

	private Folder root;

	private Folder entries;

	private Color themeColor;

	private Color backThemeColor;

	private Color darkThemeColor;

	private Texture2D loadingCircle;

	private string searchedName;

	private string foundFileName;

	private float searchStartTime = 0f;

	private List<Degree> searchedDegrees;

	private List<string> searchResultsNames;

	private string infoText;

	private bool needsDeletionConfirmation = false;

	private int editedIndex = 0;

	private ADDEditField editedField;

	private List<Vector2> backBars;

	private List<Vector2> topBars;

	public AcademicDatabaseDaemon(Computer c, string serviceName, OS os)
		: base(c, serviceName, os)
	{
		themeColor = new Color(53, 96, 156);
		backThemeColor = new Color(27, 58, 102);
		darkThemeColor = new Color(12, 20, 40, 100);
		init();
	}

	private void init()
	{
		backBars = new List<Vector2>();
		topBars = new List<Vector2>();
		searchedDegrees = new List<Degree>();
		searchResultsNames = new List<string>();
		for (int i = 0; i < 5; i++)
		{
			backBars.Add(new Vector2(0.5f, Utils.randm(1f)));
			topBars.Add(new Vector2(0.5f, Utils.randm(1f)));
		}
		loadingCircle = os.content.Load<Texture2D>("Sprites/Spinner");
	}

	public override void initFiles()
	{
		root = comp.files.root.searchForFolder("academic_data");
		if (root == null)
		{
			root = new Folder("academic_data");
			comp.files.root.folders.Add(root);
		}
		entries = new Folder("entry_cache");
		root.folders.Add(entries);
		string dataEntry = Utils.readEntireFile("Content/LocPost/AcademicDatabaseInfo.txt");
		FileEntry item = new FileEntry(dataEntry, "info.txt");
		root.files.Add(item);
		infoText = dataEntry;
		initFilesFromPeople(People.all);
	}

	public override void loadInit()
	{
		root = comp.files.root.searchForFolder("academic_data");
		entries = root.searchForFolder("entry_cache");
		FileEntry fileEntry = root.searchForFile("info.txt");
		infoText = ((fileEntry != null) ? fileEntry.data : LocaleTerms.Loc("DESCRIPTION FILE NOT FOUND"));
	}

	public void initFilesFromPeople(List<Person> people = null)
	{
		if (people == null)
		{
			people = People.all;
		}
		for (int i = 0; i < people.Count; i++)
		{
			if (people[i].degrees.Count > 0)
			{
				addFileForPerson(people[i]);
			}
		}
	}

	private void addFileForPerson(Person p)
	{
		entries.files.Add(getFileForPerson(p));
	}

	private FileEntry getFileForPerson(Person p)
	{
		FileEntry fileEntry = new FileEntry();
		fileEntry.name = convertNameToFileNameStart(p.FullName);
		int num = 0;
		while (entries.searchForFile(fileEntry.name) != null)
		{
			if (num == 0)
			{
				fileEntry.name = fileEntry.name.Substring(0, fileEntry.name.Length - 1) + num;
			}
			else
			{
				fileEntry.name += "1";
			}
			num++;
		}
		string text = p.FullName + "\n--------------------\n";
		for (int i = 0; i < p.degrees.Count; i++)
		{
			object obj = text;
			text = string.Concat(obj, p.degrees[i].name, "\n", p.degrees[i].uni, "\n", p.degrees[i].GPA);
			text += "\n--------------------";
		}
		fileEntry.data = text;
		return fileEntry;
	}

	public string convertNameToFileNameStart(string name)
	{
		return name.ToLower().Replace(" ", "_");
	}

	public FileEntry findFileForName(string name)
	{
		FileEntry fileEntry = null;
		searchResultsNames.Clear();
		string value = convertNameToFileNameStart(name);
		for (int i = 0; i < entries.files.Count; i++)
		{
			if (entries.files[i].name.StartsWith(value))
			{
				string data = entries.files[i].data;
				if (fileEntry == null)
				{
					fileEntry = entries.files[i];
				}
				searchResultsNames.Add(data.Substring(0, data.IndexOf('\n')));
			}
		}
		if (searchResultsNames.Count > 1)
		{
			return null;
		}
		if (fileEntry == null)
		{
			return null;
		}
		foundFileName = fileEntry.name;
		return fileEntry;
	}

	private void setDegreesFromFileEntryData(string file)
	{
		string[] separator = new string[1] { "--------------------" };
		string[] array = file.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		CultureInfo provider = new CultureInfo("en-au");
		searchedName = array[0];
		searchedDegrees.Clear();
		for (int i = 1; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(Utils.newlineDelim, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length >= 3)
			{
				string degreeName = array2[0];
				string uniName = array2[1];
				float degreeGPA = (float)Convert.ToDouble(array2[2], provider);
				Degree item = new Degree(degreeName, uniName, degreeGPA);
				searchedDegrees.Add(item);
			}
		}
	}

	public bool doesDegreeExist(string owner_name, string degree_name, string uni_name, float gpaMin)
	{
		List<Degree> list = searchedDegrees;
		FileEntry fileEntry = findFileForName(owner_name);
		if (fileEntry != null)
		{
			setDegreesFromFileEntryData(fileEntry.data);
			bool result = false;
			for (int i = 0; i < searchedDegrees.Count; i++)
			{
				if (searchedDegrees[i].GPA >= gpaMin && (degree_name == null || searchedDegrees[i].name.ToLower().Contains(degree_name.ToLower())) && (uni_name == null || searchedDegrees[i].uni.ToLower().Equals(uni_name.ToLower())))
				{
					result = true;
					break;
				}
			}
			searchedDegrees = list;
			return result;
		}
		return false;
	}

	public bool hasDegrees(string owner_name)
	{
		FileEntry fileEntry = findFileForName(owner_name);
		if (fileEntry == null)
		{
			return false;
		}
		List<Degree> list = searchedDegrees;
		setDegreesFromFileEntryData(fileEntry.data);
		bool result = true;
		if (searchedDegrees.Count <= 0)
		{
			result = false;
		}
		searchedDegrees = list;
		return result;
	}

	private void doPreEntryViewSearch()
	{
		FileEntry fileEntry = findFileForName(searchedName);
		if (fileEntry != null)
		{
			setDegreesFromFileEntryData(fileEntry.data);
		}
		else if (searchResultsNames.Count > 1)
		{
			state = ADDState.MultipleEntriesFound;
		}
		else
		{
			state = ADDState.EntryNotFound;
		}
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		drawSideBar(bounds, sb);
		int num = (int)((double)bounds.Width / 5.0);
		bounds.Width -= num;
		bounds.X += num;
		drawTitle(bounds, sb);
		bounds.Y += 30;
		bounds.Height -= 30;
		switch (state)
		{
		case ADDState.Welcome:
		{
			bool flag = comp.adminIP == os.thisComputer.ip;
			Rectangle destinationRectangle = bounds;
			destinationRectangle.Y = bounds.Y + 60 - 20;
			destinationRectangle.Height = 22;
			sb.Draw(Utils.white, destinationRectangle, flag ? themeColor : darkThemeColor);
			string text = LocaleTerms.Loc("Valid Administrator Account Detected");
			if (!flag)
			{
				text = LocaleTerms.Loc("Non-Admin Account Active");
			}
			Vector2 vector = GuiData.smallfont.MeasureString(text);
			sb.DrawString(position: new Vector2((float)(destinationRectangle.X + destinationRectangle.Width / 2) - vector.X / 2f, destinationRectangle.Y), spriteFont: GuiData.smallfont, text: text, color: Color.Black);
			if (Button.doButton(456011, bounds.X + 30, bounds.Y + bounds.Height / 2 - 15, bounds.Width / 2, 40, LocaleTerms.Loc("About This Server"), themeColor))
			{
				state = ADDState.InfoPanel;
			}
			if (Button.doButton(456001, bounds.X + 30, bounds.Y + bounds.Height / 2 - 15 + 50, bounds.Width / 2, 40, LocaleTerms.Loc("Search Entries"), themeColor))
			{
				state = ADDState.Seach;
				os.execute("getString Name");
			}
			if (Button.doButton(456005, bounds.X + 30, bounds.Y + bounds.Height / 2 - 15 + 100, bounds.Width / 2, 40, LocaleTerms.Loc("Exit"), themeColor))
			{
				os.display.command = "connect";
			}
			break;
		}
		case ADDState.Seach:
			drawSearchState(bounds, sb);
			break;
		case ADDState.MultiMatchSearch:
		case ADDState.PendingResult:
		{
			if (Button.doButton(456010, bounds.X + 2, bounds.Y + 2, 160, 30, LocaleTerms.Loc("Back"), darkThemeColor))
			{
				state = ADDState.Welcome;
			}
			sb.Draw(position: new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), origin: new Vector2(loadingCircle.Width / 2, loadingCircle.Height / 2), texture: loadingCircle, sourceRectangle: null, color: Color.White, rotation: (float)((double)os.timer % Math.PI * 3.0), scale: Vector2.One, effects: SpriteEffects.None, layerDepth: 0.5f);
			float num2 = os.timer - searchStartTime;
			if ((state == ADDState.PendingResult && num2 > 3.6f) || (state == ADDState.MultiMatchSearch && num2 > 0.7f))
			{
				state = ADDState.Entry;
				needsDeletionConfirmation = true;
				doPreEntryViewSearch();
			}
			break;
		}
		case ADDState.Entry:
		case ADDState.EditPerson:
			drawEntryState(bounds, sb);
			break;
		case ADDState.MultipleEntriesFound:
			drawMultipleEntriesState(bounds, sb);
			break;
		case ADDState.EntryNotFound:
			if (Button.doButton(456010, bounds.X + 2, bounds.Y + 20, 160, 30, LocaleTerms.Loc("Back"), darkThemeColor))
			{
				state = ADDState.Welcome;
			}
			if (Button.doButton(456015, bounds.X + 2, bounds.Y + 55, 160, 30, LocaleTerms.Loc("Search Again"), darkThemeColor))
			{
				state = ADDState.Seach;
				os.execute("getString Name");
			}
			TextItem.doFontLabel(new Vector2(bounds.X + 2, bounds.Y + 90), LocaleTerms.Loc("No Entries Found"), GuiData.font, null);
			break;
		case ADDState.InfoPanel:
		{
			if (Button.doButton(456010, bounds.X + 2, bounds.Y + 30, 160, 30, LocaleTerms.Loc("Back"), darkThemeColor))
			{
				state = ADDState.Welcome;
			}
			Vector2 pos = new Vector2(bounds.X + 20, bounds.Y + 70);
			TextItem.doFontLabel(pos, LocaleTerms.Loc("Information"), GuiData.font, null);
			pos.Y += 40f;
			FileEntry fileEntry = root.searchForFile("info.txt");
			string text = "ERROR: Unhandled System.IO.FileNotFoundException\nFile \"info.txt\" was not found";
			if (fileEntry != null)
			{
				text = DisplayModule.cleanSplitForWidth(fileEntry.data, bounds.Width - 80);
			}
			TextItem.DrawShadow = false;
			TextItem.doFontLabel(pos, text, GuiData.smallfont, null, bounds.Width - 40);
			break;
		}
		case ADDState.EditEntry:
			drawEditDegreeState(bounds, sb);
			break;
		}
	}

	private void drawSearchState(Rectangle bounds, SpriteBatch sb)
	{
		if (Button.doButton(456010, bounds.X + 2, bounds.Y + 2, 160, 30, LocaleTerms.Loc("Back"), darkThemeColor))
		{
			state = ADDState.Welcome;
		}
		string text = "";
		int num = bounds.X + 6;
		int num2 = bounds.Y + 100;
		num2 += (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), LocaleTerms.Loc("Please enter the name you with to search for:"), null).Y + 10;
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		string[] array = os.getStringCache.Split(separator, StringSplitOptions.None);
		if (array.Length > 1)
		{
			text = array[1];
			if (text.Equals(""))
			{
				text = os.terminal.currentLine;
			}
		}
		Rectangle destinationRectangle = new Rectangle(num, num2, bounds.Width - 12, 80);
		sb.Draw(Utils.white, destinationRectangle, os.darkBackgroundColor);
		num2 += 28;
		destinationRectangle.X = num + (int)TextItem.doMeasuredSmallLabel(new Vector2(num, num2), LocaleTerms.Loc("Name") + ": " + text, null).X + 2;
		destinationRectangle.Y = num2;
		destinationRectangle.Width = 7;
		destinationRectangle.Height = 20;
		if (os.timer % 1f < 0.3f)
		{
			sb.Draw(Utils.white, destinationRectangle, os.outlineColor);
		}
		num2 += 122;
		if (array.Length > 2 || Button.doButton(30, num, num2, 300, 22, LocaleTerms.Loc("Search"), os.highlightColor))
		{
			if (array.Length <= 2)
			{
				os.terminal.executeLine();
			}
			if (text.Length > 0)
			{
				state = ADDState.PendingResult;
				searchedName = text;
				searchStartTime = os.timer;
				comp.log("ACADEMIC_DATABASE::RecordSearch_:_" + text);
			}
			else
			{
				state = ADDState.EntryNotFound;
			}
		}
	}

	private void drawMultipleEntriesState(Rectangle bounds, SpriteBatch sb)
	{
		float num = 22f;
		float num2 = 2f;
		Vector2 pos = new Vector2((float)bounds.X + 20f, (float)bounds.Y + 10f);
		int num3 = (int)Math.Min(searchResultsNames.Count, ((float)bounds.Height - 40f - 40f - 80f) / (num + num2));
		TextItem.doFontLabel(pos, LocaleTerms.Loc("Multiple Matches"), GuiData.font, null);
		pos.Y += 30f;
		if (num3 > searchResultsNames.Count)
		{
			TextItem.doFontLabel(new Vector2(pos.X, pos.Y - 18f), LocaleTerms.Loc("Some Results Omitted"), GuiData.tinyfont, null);
		}
		sb.Draw(Utils.white, new Rectangle((int)pos.X, (int)pos.Y, (int)((float)bounds.Width - pos.X - 5f), 2), Color.White);
		pos.Y += 12f;
		for (int i = 0; i < num3; i++)
		{
			if (Button.doButton(1237000 + i, (int)pos.X, (int)pos.Y, (int)((double)bounds.Width * 0.666), (int)num, searchResultsNames[i], darkThemeColor))
			{
				searchedName = searchResultsNames[i];
				state = ADDState.MultiMatchSearch;
				searchStartTime = os.timer;
			}
			pos.Y += num + num2;
		}
		pos.Y += 5f;
		sb.Draw(Utils.white, new Rectangle((int)pos.X, (int)pos.Y, (int)((float)bounds.Width - pos.X - 5f), 2), Color.White);
		pos.Y += 10f;
		if (Button.doButton(12346080, (int)pos.X, (int)pos.Y, 160, 25, LocaleTerms.Loc("Refine Search"), themeColor))
		{
			state = ADDState.Seach;
			os.execute("getString Name");
		}
		if (Button.doButton(12346085, (int)pos.X + 170, (int)pos.Y, 160, 25, LocaleTerms.Loc("Go Back"), darkThemeColor))
		{
			state = ADDState.Welcome;
		}
	}

	private void drawEntryState(Rectangle bounds, SpriteBatch sb)
	{
		if (state == ADDState.Entry && os.hasConnectionPermission(admin: true))
		{
			state = ADDState.EditPerson;
		}
		if (Button.doButton(456010, bounds.X + 2, bounds.Y + 2, 160, 30, LocaleTerms.Loc("Back"), darkThemeColor))
		{
			state = ADDState.Welcome;
		}
		float num = (float)bounds.X + 20f;
		float num2 = (float)bounds.Y + 50f;
		TextItem.doFontLabel(new Vector2(num, num2), searchedName, GuiData.font, null, (float)bounds.Width - (num - (float)bounds.X), 60f);
		num2 += 30f;
		if (searchedDegrees.Count == 0)
		{
			TextItem.doFontLabel(new Vector2(num, num2), " -" + LocaleTerms.Loc("No Degrees Found"), GuiData.smallfont, null);
		}
		for (int i = 0; i < searchedDegrees.Count; i++)
		{
			string text = LocaleTerms.Loc("Degree") + " :" + searchedDegrees[i].name + "\n" + LocaleTerms.Loc("Uni") + "      :" + searchedDegrees[i].uni;
			text = text + "\nGPA      :" + searchedDegrees[i].GPA;
			TextItem.doFontLabel(new Vector2(num, num2), text, GuiData.smallfont, null, (float)bounds.Width - ((float)bounds.X - num), 50f);
			num2 += 60f;
			if (state != ADDState.EditPerson)
			{
				continue;
			}
			num2 -= 10f;
			if (Button.doButton(457900 + i, (int)num, (int)num2, 100, 20, LocaleTerms.Loc("Edit"), null))
			{
				state = ADDState.EditEntry;
				editedField = ADDEditField.None;
				editedIndex = i;
			}
			if (Button.doButton(456900 + i, (int)num + 105, (int)num2, 100, 20, needsDeletionConfirmation ? LocaleTerms.Loc("Delete") : LocaleTerms.Loc("Confirm?"), needsDeletionConfirmation ? Color.Gray : Color.Red))
			{
				if (needsDeletionConfirmation)
				{
					needsDeletionConfirmation = false;
				}
				else
				{
					comp.log("ACADEMIC_DATABASE::RecordDeletion_:_#" + i + "_: " + searchedName.Replace(" ", "_"));
					searchedDegrees.RemoveAt(i);
					saveChangesToEntry();
					i--;
					needsDeletionConfirmation = true;
				}
			}
			num2 += 35f;
		}
		num2 += 10f;
		if (state == ADDState.EditPerson && Button.doButton(458009, (int)num, (int)num2, 200, 30, LocaleTerms.Loc("Add Degree"), themeColor))
		{
			Degree item = new Degree("UNKNOWN", "UNKNOWN", 0f);
			searchedDegrees.Add(item);
			editedIndex = searchedDegrees.IndexOf(item);
			state = ADDState.EditEntry;
			comp.log("ACADEMIC_DATABASE::RecordAdd_:_#" + editedIndex + "_: " + searchedName.Replace(" ", "_"));
		}
	}

	private void drawEditDegreeState(Rectangle bounds, SpriteBatch sb)
	{
		if (Button.doButton(456010, bounds.X + 2, bounds.Y + 2, 160, 30, LocaleTerms.Loc("Back"), darkThemeColor))
		{
			state = ADDState.Entry;
		}
		float x = (float)bounds.X + 10f;
		float y = (float)bounds.Y + 60f;
		Vector2 pos = new Vector2(x, y);
		bool flag = editedField == ADDEditField.None;
		TextItem.doSmallLabel(pos, LocaleTerms.Loc("University") + ":", null);
		pos.X += 110f;
		TextItem.doSmallLabel(pos, searchedDegrees[editedIndex].uni, (editedField == ADDEditField.Uni) ? themeColor : Color.White);
		if (editedField == ADDEditField.Uni)
		{
			Vector2 vector = GuiData.smallfont.MeasureString(searchedDegrees[editedIndex].uni);
			Rectangle destinationRectangle = new Rectangle((int)(pos.X + vector.X + 4f), (int)pos.Y, 10, 16);
			if (os.timer % 0.6f < 0.3f)
			{
				sb.Draw(Utils.white, destinationRectangle, Utils.AddativeWhite * 0.8f);
			}
		}
		pos.X -= 110f;
		pos.Y += 20f;
		if (flag && Button.doButton(46700, (int)pos.X, (int)pos.Y, 110, 20, LocaleTerms.Loc("Edit"), darkThemeColor))
		{
			editedField = ADDEditField.Uni;
			os.execute("getString University");
		}
		pos.Y += 30f;
		TextItem.doSmallLabel(pos, LocaleTerms.Loc("Degree") + ":", null);
		pos.X += 110f;
		TextItem.doSmallLabel(pos, searchedDegrees[editedIndex].name, (editedField == ADDEditField.Degree) ? themeColor : Color.White);
		if (editedField == ADDEditField.Degree)
		{
			Vector2 vector = GuiData.smallfont.MeasureString(searchedDegrees[editedIndex].name);
			Rectangle destinationRectangle = new Rectangle((int)(pos.X + vector.X + 4f), (int)pos.Y, 10, 16);
			if (os.timer % 0.6f < 0.3f)
			{
				sb.Draw(Utils.white, destinationRectangle, Utils.AddativeWhite * 0.8f);
			}
		}
		pos.X -= 110f;
		pos.Y += 20f;
		if (flag && Button.doButton(46705, (int)pos.X, (int)pos.Y, 110, 20, LocaleTerms.Loc("Edit"), darkThemeColor))
		{
			editedField = ADDEditField.Degree;
			os.execute("getString Degree");
		}
		pos.Y += 30f;
		TextItem.doSmallLabel(pos, "GPA:", null);
		pos.X += 110f;
		TextItem.doSmallLabel(pos, string.Concat(searchedDegrees[editedIndex].GPA), (editedField == ADDEditField.GPA) ? themeColor : Color.White);
		if (editedField == ADDEditField.GPA)
		{
			Vector2 vector = GuiData.smallfont.MeasureString(string.Concat(searchedDegrees[editedIndex].GPA));
			Rectangle destinationRectangle = new Rectangle((int)(pos.X + vector.X + 4f), (int)pos.Y, 10, 16);
			if (os.timer % 0.6f < 0.3f)
			{
				sb.Draw(Utils.white, destinationRectangle, Utils.AddativeWhite * 0.8f);
			}
		}
		pos.X -= 110f;
		pos.Y += 20f;
		if (flag && Button.doButton(46710, (int)pos.X, (int)pos.Y, 110, 20, LocaleTerms.Loc("Edit"), darkThemeColor))
		{
			editedField = ADDEditField.GPA;
			os.execute("getString GPA");
		}
		pos.Y += 30f;
		if (editedField != 0)
		{
			if (doEditField())
			{
				editedField = ADDEditField.None;
				os.getStringCache = "";
				saveChangesToEntry();
			}
		}
		else if (Button.doButton(486012, bounds.X + 2, bounds.Y + bounds.Height - 40, 230, 30, LocaleTerms.Loc("Save And Return"), backThemeColor))
		{
			state = ADDState.Entry;
			comp.log("ACADEMIC_DATABASE::RecordEdit_:_#" + editedIndex + "_: " + searchedName.Replace(" ", "_"));
		}
	}

	private bool doEditField()
	{
		string text = "";
		string[] separator = new string[1] { "#$#$#$$#$&$#$#$#$#" };
		string[] array = os.getStringCache.Split(separator, StringSplitOptions.None);
		if (array.Length > 1)
		{
			text = array[1];
			if (text.Equals(""))
			{
				text = os.terminal.currentLine;
			}
			setEditedFieldValue(text);
		}
		if (array.Length > 2)
		{
			if (array.Length <= 2)
			{
				os.terminal.executeLine();
			}
			if (text.Length > 0)
			{
				setEditedFieldValue(text);
				return true;
			}
		}
		return false;
	}

	private void setEditedFieldValue(string value)
	{
		CultureInfo provider = new CultureInfo("en-au");
		switch (editedField)
		{
		case ADDEditField.Uni:
			searchedDegrees[editedIndex] = new Degree(searchedDegrees[editedIndex].name, value, searchedDegrees[editedIndex].GPA);
			break;
		case ADDEditField.Degree:
			searchedDegrees[editedIndex] = new Degree(value, searchedDegrees[editedIndex].uni, searchedDegrees[editedIndex].GPA);
			break;
		case ADDEditField.GPA:
		{
			float degreeGPA = 2f;
			try
			{
				if (value.Length > 0)
				{
					degreeGPA = (float)Convert.ToDouble(value, provider);
				}
			}
			catch
			{
			}
			searchedDegrees[editedIndex] = new Degree(searchedDegrees[editedIndex].name, searchedDegrees[editedIndex].uni, degreeGPA);
			break;
		}
		case ADDEditField.None:
			break;
		}
	}

	private void drawTitle(Rectangle bounds, SpriteBatch sb)
	{
		TextItem.doFontLabel(new Vector2(bounds.X, bounds.Y), LocaleTerms.Loc("International Academic Database"), GuiData.font, null, bounds.Width - 6);
	}

	private void drawSideBar(Rectangle bounds, SpriteBatch sb)
	{
		updateSideBar();
		Rectangle rectangle = new Rectangle(bounds.X + 2, bounds.Y + 1, bounds.Width / 8, bounds.Height - 2);
		int num = bounds.Width / 15;
		Rectangle destinationRectangle = rectangle;
		destinationRectangle.Width = (int)((double)(float)rectangle.Width * 1.5);
		sb.Draw(Utils.white, destinationRectangle, darkThemeColor);
		for (int i = 0; i < backBars.Count; i++)
		{
			destinationRectangle.X = (int)((float)rectangle.X + backBars[i].X * (float)rectangle.Width);
			destinationRectangle.Width = (int)((float)num * backBars[i].Y);
			sb.Draw(Utils.white, destinationRectangle, backThemeColor);
			destinationRectangle.X = (int)((float)rectangle.X + topBars[i].X * (float)rectangle.Width);
			destinationRectangle.Width = (int)((float)num * topBars[i].Y * 0.5f);
			sb.Draw(Utils.white, destinationRectangle, themeColor);
		}
		destinationRectangle.X = rectangle.X;
		destinationRectangle.Width = (int)((double)(float)rectangle.Width * 1.5);
		sb.Draw(Utils.gradient, destinationRectangle, Color.Black);
	}

	private void updateSideBar()
	{
		float num = os.timer * 0.4f;
		for (int i = 0; i < backBars.Count; i++)
		{
			backBars[i] = new Vector2((float)(0.5 + Math.Sin(num * (float)i) * 0.5), (float)Math.Abs(Math.Sin(num * (float)i)));
			topBars[i] = new Vector2((float)(0.5 + Math.Sin((0f - num) * (float)(backBars.Count - i)) * 0.5), (float)Math.Abs(Math.Sin((double)(0f - num) / 2.0 * (double)i)));
		}
	}

	private void saveChangesToEntry()
	{
		string[] array = searchedName.Split(Utils.spaceDelim);
		Person person = new Person(array[0], array[1], male: true);
		person.degrees = searchedDegrees;
		FileEntry fileForPerson = getFileForPerson(person);
		entries.searchForFile(foundFileName).data = fileForPerson.data;
	}

	public override void navigatedTo()
	{
		state = ADDState.Welcome;
	}

	public override void userAdded(string name, string pass, byte type)
	{
	}

	public override string getSaveString()
	{
		return "<AcademicDatabse name=\"" + name + "\"/>";
	}
}
