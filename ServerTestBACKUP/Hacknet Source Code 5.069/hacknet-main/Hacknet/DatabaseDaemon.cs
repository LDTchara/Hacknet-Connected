using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class DatabaseDaemon : Daemon
{
	public enum DatabasePermissions
	{
		AdminOnly,
		Public
	}

	private enum DatabaseState
	{
		Welcome,
		Search,
		Browse,
		Loading,
		EntryDisplay,
		Error
	}

	private MovingBarsEffect sideEffect;

	private DatabasePermissions Permissions;

	private DatabaseState State;

	private Folder DatasetFolder;

	private Type DataType;

	private string DataTypeIdentifier;

	private bool FilenameIsPersonName = false;

	private FileEntry ActiveFile = null;

	private string errorMessage = "None";

	private object DeserializedFile = null;

	private string Foldername;

	public List<object> Dataset = null;

	private Color ThemeColor;

	private bool HadThemeColorApplied = true;

	private Vector2 BlockStartTopLeft;

	private float blockSize;

	private int blocksWide;

	private int blocksHigh;

	private bool HasSpecialCaseDraw = false;

	private Texture2D PlaceholderSprite;

	private Texture2D Triangle;

	private Dictionary<object, Texture2D> WildcardAssets = new Dictionary<object, Texture2D>();

	private ScrollableSectionedPanel ScrollPanel;

	private ScrollableTextRegion TextRegion;

	private string passwordResetHelperString = null;

	public string adminResetEmailHostID = null;

	public string adminResetPassEmailAccount = null;

	private bool AdminEmailResetStarted = false;

	private bool AdminEmailResetHasHappened = false;

	private string passwordResetMessage = "";

	public static DatabasePermissions GetDatabasePermissionsFromString(string data)
	{
		DatabasePermissions result = DatabasePermissions.Public;
		if (data.ToLower() == "private" || data.ToLower().StartsWith("admin"))
		{
			result = DatabasePermissions.AdminOnly;
		}
		return result;
	}

	public DatabaseDaemon(Computer c, OS os, string name, DatabasePermissions permissions, string DataTypeIdentifier, string Foldername = null, Color? ThemeColor = null)
		: base(c, name, os)
	{
		Init(c, os, name, permissions, DataTypeIdentifier, (Foldername == null) ? "Database" : Foldername, (!ThemeColor.HasValue) ? os.highlightColor : ThemeColor.Value);
		HadThemeColorApplied = ThemeColor.HasValue;
	}

	public DatabaseDaemon(Computer c, OS os, string name, string permissions, string DataTypeIdentifier, string Foldername = null, Color? ThemeColor = null)
		: base(c, name, os)
	{
		Init(c, os, name, GetDatabasePermissionsFromString(permissions), DataTypeIdentifier, (Foldername == null) ? "Database" : Foldername, (!ThemeColor.HasValue) ? os.highlightColor : ThemeColor.Value);
		HadThemeColorApplied = ThemeColor.HasValue;
	}

	private void Init(Computer c, OS os, string name, DatabasePermissions permissions, string DataTypeIdentifier, string Foldername, Color themeColor)
	{
		sideEffect = new MovingBarsEffect();
		Permissions = permissions;
		State = DatabaseState.Welcome;
		this.DataTypeIdentifier = DataTypeIdentifier;
		DataType = ObjectSerializer.GetTypeForName(DataTypeIdentifier);
		ScrollPanel = new ScrollableSectionedPanel(26, GuiData.spriteBatch.GraphicsDevice);
		TextRegion = new ScrollableTextRegion(GuiData.spriteBatch.GraphicsDevice);
		this.Foldername = ((Foldername == null) ? "Database" : Foldername);
		ThemeColor = themeColor;
		if (DataTypeIdentifier.EndsWith("NeopalsAccount"))
		{
			WildcardAssets.Add(Neopal.PetType.Blundo, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Blundo"));
			WildcardAssets.Add(Neopal.PetType.Chisha, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Chisha"));
			WildcardAssets.Add(Neopal.PetType.Jubdub, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Jubdub"));
			WildcardAssets.Add(Neopal.PetType.Kachici, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Kachici"));
			WildcardAssets.Add(Neopal.PetType.Kyrill, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Kyrill"));
			WildcardAssets.Add(Neopal.PetType.Myncl, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Myncl"));
			WildcardAssets.Add(Neopal.PetType.Pageri, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Pageri"));
			WildcardAssets.Add(Neopal.PetType.Psybunny, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Psybunny"));
			WildcardAssets.Add(Neopal.PetType.Scorchum, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Scorchum"));
			WildcardAssets.Add(Neopal.PetType.Unisam, os.content.Load<Texture2D>("DLC/Sprites/Neopals/Unisam"));
		}
		PlaceholderSprite = os.content.Load<Texture2D>("Sprites/Chip");
		Triangle = os.content.Load<Texture2D>("DLC/Sprites/Triangle");
	}

	public override void initFiles()
	{
		base.initFiles();
		DatasetFolder = comp.files.root.searchForFolder(Foldername);
		if (DatasetFolder == null)
		{
			DatasetFolder = new Folder(Foldername);
			comp.files.root.folders.Add(DatasetFolder);
		}
		InitDataset();
	}

	public override void loadInit()
	{
		base.loadInit();
		DatasetFolder = comp.files.root.searchForFolder(Foldername);
		DataType = ObjectSerializer.GetTypeForName(DataTypeIdentifier);
		string dataTypeIdentifier = DataTypeIdentifier;
		if (dataTypeIdentifier != null && dataTypeIdentifier == "NeopalsAccount")
		{
			HasSpecialCaseDraw = true;
		}
	}

	private void InitDataset()
	{
		if (Dataset != null)
		{
			if (DataType == typeof(Person))
			{
				FilenameIsPersonName = true;
			}
			else
			{
				FilenameIsPersonName = false;
			}
			for (int i = 0; i < Dataset.Count; i++)
			{
				string dataEntry = CleanXMLForFile(ObjectSerializer.SerializeObject(Dataset[i]));
				string filenameForObject = GetFilenameForObject(Dataset[i]);
				try
				{
				}
				catch (Exception ex)
				{
					Console.WriteLine("Deserialization error for Generic Object: " + ex);
				}
				DatasetFolder.files.Add(new FileEntry(dataEntry, filenameForObject));
			}
		}
		else if (DataTypeIdentifier.EndsWith("VehicleInfo"))
		{
			FilenameIsPersonName = true;
			DataType = People.all[0].vehicles.GetType();
			for (int i = 0; i < People.all.Count; i++)
			{
				if (People.all[i].vehicles.Count != 0)
				{
					string dataEntry = CleanXMLForFile(ObjectSerializer.SerializeObject(People.all[i].vehicles));
					List<VehicleRegistration> vehicles = People.all[i].vehicles;
					try
					{
						string filenameForObject = GetFilenameForPersonName(People.all[i].firstName, People.all[i].lastName);
						DatasetFolder.files.Add(new FileEntry(dataEntry, filenameForObject));
					}
					catch (Exception ex)
					{
						Console.WriteLine("Deserialization error for Vehicle: " + ex);
					}
				}
			}
		}
		else if (DataTypeIdentifier.EndsWith("NeopalsAccount"))
		{
			FilenameIsPersonName = false;
			HasSpecialCaseDraw = true;
			DataType = typeof(NeopalsAccount);
			if (People.all.Count == 0)
			{
				return;
			}
			for (int i = 0; i < People.all.Count; i++)
			{
				if (People.all[i].NeopalsAccount != null)
				{
					string dataEntry = CleanXMLForFile(ObjectSerializer.SerializeObject(People.all[i].NeopalsAccount));
					NeopalsAccount neopalsAccount = People.all[i].NeopalsAccount;
					try
					{
						string filenameForObject = GetFilenameForObject(neopalsAccount);
						DatasetFolder.files.Add(new FileEntry(dataEntry, filenameForObject));
					}
					catch (Exception ex)
					{
						Console.WriteLine("Deserialization error for NeopalsAccount: " + ex);
					}
				}
			}
		}
		else
		{
			if (!DataTypeIdentifier.EndsWith("Person"))
			{
				return;
			}
			FilenameIsPersonName = true;
			DataType = People.all[0].GetType();
			for (int i = 0; i < People.all.Count; i++)
			{
				if (People.all[i].vehicles.Count != 0)
				{
					string dataEntry = CleanXMLForFile(ObjectSerializer.SerializeObject(People.all[i]));
					Person person = People.all[i];
					try
					{
						string filenameForObject = GetFilenameForPersonName(People.all[i].firstName, People.all[i].lastName);
						DatasetFolder.files.Add(new FileEntry(dataEntry, filenameForObject));
					}
					catch (Exception ex)
					{
						Console.WriteLine("Deserialization error for People: " + ex);
					}
				}
			}
		}
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		State = DatabaseState.Welcome;
		AdminEmailResetStarted = false;
	}

	public object GetObjectForRecordName(string recordName)
	{
		for (int i = 0; i < DatasetFolder.files.Count; i++)
		{
			if (DatasetFolder.files[i].name.ToLower().Contains(recordName.ToLower()))
			{
				return ObjectSerializer.DeserializeObject(Utils.GenerateStreamFromString(DeCleanXMLForFile(DatasetFolder.files[i].data)), DataType);
			}
		}
		return null;
	}

	private void ResetAccessPassword()
	{
		AdminEmailResetHasHappened = true;
		try
		{
			Computer computer = Programs.getComputer(os, adminResetEmailHostID);
			MailServer mailServer = (MailServer)computer.getDaemon(typeof(MailServer));
			string userTo = adminResetPassEmailAccount;
			if (adminResetPassEmailAccount.Contains("@"))
			{
				userTo = adminResetPassEmailAccount.Substring(0, adminResetPassEmailAccount.IndexOf("@"));
			}
			string text = string.Concat(Utils.getRandomLetter(), Utils.getRandomLetter(), Utils.getRandomNumberChar(), Utils.getRandomLetter(), Utils.getRandomNumberChar());
			comp.adminPass = text;
			comp.adminIP = comp.ip;
			for (int i = 0; i < comp.users.Count; i++)
			{
				if (comp.users[i].name.ToLower() == "admin")
				{
					UserDetail value = comp.users[i];
					value.pass = text;
					comp.users[i] = value;
				}
			}
			string format = Utils.readEntireFile("Content/DLC/Docs/DatabasePasswordResetEmail.txt");
			format = string.Format(format, name, text);
			mailServer.addMail(MailServer.generateEmail($"{name} Password Reset", format, "AdminBot"), userTo);
			passwordResetMessage = "Password Reset Complete";
		}
		catch (Exception ex)
		{
			passwordResetMessage = "CRITICAL RESET ERROR\n" + ex.ToString();
		}
	}

	private string CleanXMLForFile(string data)
	{
		return data.Replace("<", "[").Replace(">", "]").Replace("`", "_")
			.Replace("\t", "    ");
	}

	private string DeCleanXMLForFile(string data)
	{
		return data.Replace("[", "<").Replace("]", ">");
	}

	private string GetFilenameForPersonName(string firstname, string lastname)
	{
		return Utils.GetNonRepeatingFilename((lastname + "_" + firstname).ToLower(), ".rec", DatasetFolder);
	}

	private string GetFilenameForObject(object obj)
	{
		return Utils.GetNonRepeatingFilename(obj.ToString().Replace(" ", "_").ToLower(), ".rec", DatasetFolder);
	}

	public override string getSaveString()
	{
		string text = (HadThemeColorApplied ? (" Color=\"" + Utils.convertColorToParseableString(ThemeColor) + "\"") : "");
		string text2 = "";
		if (!string.IsNullOrWhiteSpace(adminResetPassEmailAccount))
		{
			text2 = $" AdminEmailAccount=\"{adminResetPassEmailAccount}\" AdminEmailHostID=\"{adminResetEmailHostID}\" ";
		}
		return "<DatabaseDaemon Name=\"" + name + "\" Permissions=\"" + Permissions.ToString() + "\" DataType=\"" + DataTypeIdentifier + "\" Foldername=\"" + Foldername + "\"" + text + text2 + " />";
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		Rectangle dest = Utils.InsetRectangle(bounds, 2);
		DrawBackground(dest, sb, 6);
		switch (State)
		{
		case DatabaseState.Error:
			DrawError(dest, sb);
			break;
		case DatabaseState.Welcome:
			DrawWelcome(dest, sb);
			break;
		case DatabaseState.Browse:
			DrawBrowse(dest, sb);
			break;
		case DatabaseState.Search:
			break;
		case DatabaseState.Loading:
			break;
		case DatabaseState.EntryDisplay:
			DrawEntry(dest, sb);
			break;
		}
	}

	private void DrawWelcome(Rectangle dest, SpriteBatch sb)
	{
		TextRegion.UpdateScroll(0f);
		Rectangle drawRectForRow = GetDrawRectForRow(0, 12);
		bool flag = Permissions == DatabasePermissions.Public || comp.adminIP == os.thisComputer.ip;
		TextItem.doCenteredFontLabel(drawRectForRow, name, GuiData.font, Color.White, LockToLeft: true);
		Rectangle dest2 = new Rectangle(dest.X + 1, (int)BlockStartTopLeft.Y, dest.Width - 2, (int)(blockSize * 2f));
		PatternDrawer.draw(dest2, 0.5f, Color.Transparent, (flag ? ThemeColor : Utils.AddativeRed) * 0.2f, sb, flag ? PatternDrawer.thinStripe : PatternDrawer.errorTile);
		Rectangle drawRectForRow2 = GetDrawRectForRow(1, 12);
		TextItem.doCenteredFontLabel(drawRectForRow2, flag ? LocaleTerms.Loc("Access Granted") : LocaleTerms.Loc("Administrator Login Required"), GuiData.font, flag ? (ThemeColor * 0.8f) : (Utils.AddativeRed * 0.8f), LockToLeft: true);
		Rectangle drawRectForRow3 = GetDrawRectForRow(3, 14);
		if (flag && Button.doButton(73616101, (int)BlockStartTopLeft.X, drawRectForRow3.Y, dest.Width / 2, drawRectForRow3.Height, LocaleTerms.Loc("Browse Records"), ThemeColor))
		{
			State = DatabaseState.Browse;
		}
		drawRectForRow3.Y += drawRectForRow3.Height + 10;
		drawRectForRow3.Height /= 2;
		if (Button.doButton(73616102, (int)BlockStartTopLeft.X, drawRectForRow3.Y, dest.Width / 2, drawRectForRow3.Height, LocaleTerms.Loc("Login"), ThemeColor))
		{
			os.execute("login");
		}
		drawRectForRow3.Y += drawRectForRow3.Height + 6;
		Color color = Color.Lerp(Color.Black, ThemeColor, 0.5f);
		if (!string.IsNullOrWhiteSpace(adminResetPassEmailAccount) && Button.doButton(73616106, (int)BlockStartTopLeft.X, drawRectForRow3.Y, dest.Width / 2, drawRectForRow3.Height, AdminEmailResetHasHappened ? LocaleTerms.Loc("Password Reset In Cooldown") : (AdminEmailResetStarted ? LocaleTerms.Loc("Cancel") : LocaleTerms.Loc("Reset Access Password")), (!AdminEmailResetStarted) ? color : (AdminEmailResetHasHappened ? Color.Gray : Color.DarkRed)))
		{
			AdminEmailResetStarted = !AdminEmailResetStarted;
		}
		if (AdminEmailResetStarted)
		{
			Rectangle drawRectForRow4 = GetDrawRectForRow(3, 10);
			drawRectForRow4.X += dest.Width / 2 + 10;
			drawRectForRow4.Width -= dest.Width / 2;
			drawRectForRow4.Height = Math.Max(300, (int)(blockSize * 3f - 30f));
			drawRectForRow4.Width += 30;
			if (drawRectForRow4.Height == 300)
			{
				drawRectForRow4.Y -= (int)blockSize;
			}
			Color color2 = Color.Gray * 0.3f;
			sb.Draw(Utils.white, drawRectForRow4, Color.Black * 0.8f);
			drawRectForRow4 = Utils.InsetRectangle(drawRectForRow4, 2);
			PatternDrawer.draw(drawRectForRow4, 0.5f, color2, color2 * 0.4f, sb, PatternDrawer.thinStripe);
			int num = 20;
			sb.Draw(destinationRectangle: new Rectangle(drawRectForRow4.X - num - 2, drawRectForRow3.Y + (drawRectForRow3.Height - num) / 2, num, num), texture: Triangle, color: Color.Black * 0.8f);
			Rectangle dest3 = new Rectangle(drawRectForRow4.X + 6, drawRectForRow4.Y, drawRectForRow4.Width - 12, drawRectForRow4.Height / 6);
			TextItem.doCenteredFontLabel(dest3, LocaleTerms.Loc("Reset Admin Password"), GuiData.font, Color.LightGray, LockToLeft: true);
			Rectangle dest4 = new Rectangle(dest3.X, dest3.Y + dest3.Height + 4, dest3.Width + 9, drawRectForRow4.Height / 6 * 4);
			if (passwordResetHelperString == null)
			{
				passwordResetHelperString = Utils.readEntireFile("Content/DLC/Docs/PasswordResetText.txt");
			}
			string text = passwordResetHelperString;
			text = text.Replace("[PASSWORD]", adminResetPassEmailAccount);
			if (AdminEmailResetHasHappened)
			{
				text = passwordResetMessage;
			}
			text = Utils.SuperSmartTwimForWidth(text, dest4.Width, GuiData.UITinyfont);
			TextItem.doFontLabelToSize(dest4, text, GuiData.tinyfont, Color.White, doNotOversize: true, offsetToTopLeft: true);
			if (!AdminEmailResetHasHappened && Button.doButton(73606111, drawRectForRow4.X + 4, drawRectForRow4.Y + drawRectForRow4.Height - 28, drawRectForRow4.Width - 8, 26, LocaleTerms.Loc("Reset Password"), Color.Red))
			{
				ResetAccessPassword();
			}
		}
		drawRectForRow3.Y += drawRectForRow3.Height + 6;
		if (Button.doButton(73616129, (int)BlockStartTopLeft.X, drawRectForRow3.Y, dest.Width / 2, drawRectForRow3.Height, LocaleTerms.Loc("Exit"), Color.Black))
		{
			os.display.command = "connect";
		}
	}

	private void DrawError(Rectangle dest, SpriteBatch sb)
	{
		Rectangle drawRectForRow = GetDrawRectForRow(0, 12);
		bool flag = Permissions == DatabasePermissions.Public || comp.adminIP == os.thisComputer.ip;
		TextItem.doFontLabelToSize(drawRectForRow, name, GuiData.font, Color.White, doNotOversize: false, offsetToTopLeft: true);
		Rectangle dest2 = new Rectangle(dest.X + 1, (int)BlockStartTopLeft.Y, dest.Width - 2, (int)(blockSize * 2f));
		PatternDrawer.draw(dest2, 0.5f, Color.Transparent, Utils.AddativeRed * 0.2f, sb, flag ? PatternDrawer.thinStripe : PatternDrawer.errorTile);
		Rectangle drawRectForRow2 = GetDrawRectForRow(1, 12);
		TextItem.doFontLabelToSize(drawRectForRow2, LocaleTerms.Loc("ERROR"), GuiData.font, Utils.AddativeRed * 0.8f, doNotOversize: true, offsetToTopLeft: true);
		Rectangle drawRectForRow3 = GetDrawRectForRow(2, 14);
		if (Button.doButton(73616101, (int)BlockStartTopLeft.X, drawRectForRow3.Y, dest.Width / 2, drawRectForRow3.Height, LocaleTerms.Loc("Back"), ThemeColor))
		{
			State = DatabaseState.Welcome;
		}
		Rectangle drawRectForRow4 = GetDrawRectForRow(3, 14);
		drawRectForRow4.Height = (int)(((float)blocksHigh - 4f) * blockSize);
		drawRectForRow4 = Utils.InsetRectangle(drawRectForRow4, 2);
		string text = Utils.SuperSmartTwimForWidth(errorMessage, drawRectForRow4.Width, GuiData.smallfont);
		TextRegion.Draw(drawRectForRow4, text, sb, Utils.AddativeRed);
	}

	private void DrawBrowse(Rectangle dest, SpriteBatch spriteBatch)
	{
		Rectangle drawRectForRow = GetDrawRectForRow(0, 12);
		bool flag = Permissions == DatabasePermissions.Public || comp.adminIP == os.thisComputer.ip;
		TextItem.doFontLabelToSize(drawRectForRow, string.Format(LocaleTerms.Loc("{0} : Records"), name), GuiData.font, Color.White, doNotOversize: true, offsetToTopLeft: true);
		drawRectForRow = GetDrawRectForRow(0, 0);
		PatternDrawer.draw(drawRectForRow, 0.5f, Color.Transparent, ThemeColor * 0.2f, spriteBatch, PatternDrawer.thinStripe);
		Rectangle rectangle = new Rectangle(drawRectForRow.X + 4, drawRectForRow.Y + drawRectForRow.Height / 2 + 12, drawRectForRow.Width / 3, drawRectForRow.Height / 2 - 15);
		if (Button.doButton(71118000, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Back"), Color.Gray))
		{
			State = DatabaseState.Welcome;
		}
		if (DataType == null || string.IsNullOrWhiteSpace(DataTypeIdentifier) || DataTypeIdentifier.ToLower() == "none")
		{
			Rectangle drawRectForRow2 = GetDrawRectForRow(1, 2);
			drawRectForRow2.Height += (int)(blockSize * (float)(blocksHigh - 3));
			PatternDrawer.draw(drawRectForRow2, 0.4f, Color.Black * 0.3f, Utils.makeColorAddative(ThemeColor) * 0.25f, spriteBatch, PatternDrawer.binaryTile);
			Rectangle drawRectForRow3 = GetDrawRectForRow(3, 8);
			TextItem.doCenteredFontLabel(drawRectForRow3, " - " + LocaleTerms.Loc("API Access Enabled") + " - ", GuiData.smallfont, Color.White);
			return;
		}
		Rectangle ListDest = GetDrawRectForRow(1, 4);
		ListDest.Height += (int)(blockSize * ((float)blocksHigh - 2.5f));
		ScrollPanel.NumberOfPanels = DatasetFolder.files.Count;
		ScrollPanel.Draw(delegate(int i, Rectangle bounds, SpriteBatch sb)
		{
			FileEntry fileEntry = DatasetFolder.files[i];
			string text = "REC#" + i.ToString("000") + " : " + GetAnnounceNameForFileName(fileEntry.name);
			int num = Math.Max(160, bounds.Width / 3);
			if (Button.doButton(71118100 + i, bounds.X, bounds.Y + 2, num, bounds.Height - 4, string.Format(LocaleTerms.Loc("View Record #{0}"), i.ToString("000")), ThemeColor))
			{
				ActiveFile = DatasetFolder.files[i];
				State = DatabaseState.EntryDisplay;
				DeserializedFile = null;
			}
			Vector2 vector = GuiData.smallfont.MeasureString(text);
			Rectangle destinationRectangle = new Rectangle(bounds.X + bounds.Width / 3 + 6, bounds.Y + bounds.Height / 2 - 1, ListDest.Width - (num + (int)vector.X + 26), 1);
			sb.Draw(Utils.white, destinationRectangle, Color.White * 0.1f);
			TextItem.doFontLabel(new Vector2((float)(destinationRectangle.X + destinationRectangle.Width) + 6f, (float)bounds.Y + 2f), text, GuiData.smallfont, Color.White, bounds.Width, bounds.Height);
		}, spriteBatch, ListDest);
	}

	private void DrawEntry(Rectangle dest, SpriteBatch spriteBatch)
	{
		if (DeserializedFile == null)
		{
			try
			{
				DeserializedFile = ObjectSerializer.DeserializeObject(Utils.GenerateStreamFromString(DeCleanXMLForFile(ActiveFile.data)), DataType);
			}
			catch (Exception ex)
			{
				State = DatabaseState.Error;
				errorMessage = LocaleTerms.Loc("Error De-serializing Entry") + " : " + ActiveFile.name + "\n" + Utils.GenerateReportFromException(ex).Replace("Hacknet", "Database");
				return;
			}
		}
		Rectangle drawRectForRow = GetDrawRectForRow(0, 0);
		drawRectForRow.Height /= 2;
		TextItem.doFontLabelToSize(drawRectForRow, string.Format(LocaleTerms.Loc("Entry for {0}"), GetAnnounceNameForFileName(ActiveFile.name)), GuiData.font, Color.White, doNotOversize: true, offsetToTopLeft: true);
		drawRectForRow.Y += drawRectForRow.Height;
		if (Button.doButton(7301991, drawRectForRow.X, drawRectForRow.Y, drawRectForRow.Width / 2, drawRectForRow.Height, LocaleTerms.Loc("Back"), Color.Gray))
		{
			State = DatabaseState.Browse;
			ActiveFile = null;
			DeserializedFile = null;
			return;
		}
		Rectangle bounds = default(Rectangle);
		ref Rectangle reference = ref bounds;
		reference = new Rectangle((int)BlockStartTopLeft.X, (int)BlockStartTopLeft.Y + (int)blockSize, (int)(blockSize * (float)(blocksWide - 1)), (int)(blockSize * (float)(blocksHigh - 2)));
		if (HasSpecialCaseDraw)
		{
			ReflectiveRenderer.PreRenderForObject = (Action<Vector2, Type, string>)Delegate.Combine(ReflectiveRenderer.PreRenderForObject, (Action<Vector2, Type, string>)delegate(Vector2 pos, Type targetType, string targetValue)
			{
				DrawSpecialCase(pos, bounds, targetType, targetValue, spriteBatch);
			});
		}
		ReflectiveRenderer.RenderObject(DeserializedFile, bounds, spriteBatch, ScrollPanel, ThemeColor);
		if (HasSpecialCaseDraw)
		{
			ReflectiveRenderer.PreRenderForObject = null;
		}
	}

	private string GetAnnounceNameForFileName(string filename)
	{
		filename = filename.Replace(".rec", "");
		if (FilenameIsPersonName)
		{
			string[] array = filename.Split(new char[1] { '_' }, StringSplitOptions.None);
			return array[1] + " " + array[0];
		}
		return filename;
	}

	private Rectangle GetDrawRectForRow(int row, int inset)
	{
		Rectangle rect = new Rectangle((int)BlockStartTopLeft.X, (int)(BlockStartTopLeft.Y + blockSize * (float)row), (int)(blockSize * (float)(blocksWide - 1)), (int)blockSize);
		return Utils.InsetRectangle(rect, inset);
	}

	private void DrawBackground(Rectangle dest, SpriteBatch sb, int desiredNumOfBlocks)
	{
		Color color = Color.Gray * 0.4f;
		int num = dest.Width / (desiredNumOfBlocks + 2);
		int num2 = 9;
		float num3 = 1f;
		int num4 = dest.X + num / 2;
		int num5 = dest.Y + num / 2;
		BlockStartTopLeft = new Vector2(num4, num5);
		blocksWide = (blocksHigh = 0);
		blockSize = num;
		do
		{
			blocksWide = 0;
			do
			{
				sb.Draw(destinationRectangle: new Rectangle(Math.Max(dest.X, num4 - num2 / 2), num5 - (int)(num3 / 2f + 0.5f), num2, (int)num3), texture: Utils.white, color: color);
				sb.Draw(destinationRectangle: new Rectangle(num4 - (int)(num3 / 2f), Math.Max(dest.Y, num5 - (int)((float)num2 / 2f + 0.5f)), (int)num3, num2 / 2 - (int)(num3 / 2f)), texture: Utils.white, color: color);
				sb.Draw(destinationRectangle: new Rectangle(num4 - (int)(num3 / 2f), num5 + (int)(num3 / 2f), (int)num3, num2 / 2 - (int)(num3 / 2f)), texture: Utils.white, color: color);
				num4 += num;
				blocksWide++;
			}
			while (num4 <= dest.X + dest.Width - num / 2);
			num4 = dest.X + num / 2;
			num5 += num;
			blocksHigh++;
		}
		while (num5 <= dest.Y + dest.Height - num / 2);
	}

	public void DrawSpecialCase(Vector2 currentPos, Rectangle totalArea, Type objType, string drawnValue, SpriteBatch sb)
	{
		if (sb.Name != "AltRTBatch" && objType == typeof(Neopal.PetType))
		{
			int num = Math.Min(ReflectiveRenderer.GetEntryLineHeight() * 6, totalArea.Width / 4);
			Texture2D sprite = PlaceholderSprite;
			Neopal.PetType petType = (Neopal.PetType)Enum.Parse(objType, drawnValue);
			if (WildcardAssets.ContainsKey(petType))
			{
				sprite = WildcardAssets[petType];
			}
			Rectangle petDrawPos = default(Rectangle);
			ref Rectangle reference = ref petDrawPos;
			reference = new Rectangle(totalArea.X + totalArea.Width - num - 10, (int)currentPos.Y + 4, num, num);
			double num2 = (double)sprite.Height / (double)petDrawPos.Height;
			int num3 = totalArea.Y + totalArea.Height - petDrawPos.Y;
			int height = Math.Min(sprite.Height, (int)(num2 * (double)num3));
			Rectangle? clip = default(Rectangle?);
			ref Rectangle? reference2 = ref clip;
			reference2 = new Rectangle(0, 0, sprite.Width, height);
			petDrawPos.Height = Math.Min(petDrawPos.Height, num3);
			OS oS = os;
			oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
			{
				sb.Draw(sprite, petDrawPos, clip, Color.White);
			});
		}
	}
}
