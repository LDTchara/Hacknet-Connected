using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Daemons.Helpers;

public class IRCSystem
{
	public struct IRCLogEntry
	{
		private const string Delimiter = "//";

		private const string SerializationDelimiterReplacement = "&dsr";

		private static string[] SplitDelmiter = new string[1] { "//" };

		public string Message;

		public string Timestamp;

		public string Author;

		public string Serialize()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append((Timestamp == null) ? "" : Timestamp);
			stringBuilder.Append("//");
			stringBuilder.Append(Author);
			stringBuilder.Append("//");
			stringBuilder.Append(Message.Replace("//", "&dsr"));
			return stringBuilder.ToString();
		}

		public static IRCLogEntry Deserialize(string entry)
		{
			IRCLogEntry result = default(IRCLogEntry);
			string[] array = entry.Split(SplitDelmiter, StringSplitOptions.None);
			result.Timestamp = array[0];
			result.Author = array[1];
			result.Message = array[2].Replace("&dsr", "//");
			return result;
		}

		public static IRCLogEntry DeserializeSafe(string entry)
		{
			try
			{
				return Deserialize(entry);
			}
			catch (Exception)
			{
				return default(IRCLogEntry);
			}
		}
	}

	private const string ACTIVE_LOG_FILENAME = "active.log";

	public const string ATTACHMENT_FLAG_PREFIX = "!ATTACHMENT:";

	public const string ANNOUNCE_FLAG_PREFIX = "!ANNOUNCEMENT!";

	private const string EntryLineDelimiterMarker = "\n#";

	private static string[] EntryLineDelimiter = new string[1] { "\n#" };

	private Folder StorageFolder;

	private FileEntry ActiveLogFile;

	private int DrawnButtonIndex = 0;

	private int messagesAddedSinceLastView = 0;

	private bool isCurrentlyBeingViewed = true;

	public SoundEffect AttachmentPressedSound = null;

	public Action<string, string> LogAdded;

	public IRCSystem(Folder storageFolder)
	{
		StorageFolder = storageFolder;
		ActiveLogFile = StorageFolder.searchForFile("active.log");
		if (ActiveLogFile == null)
		{
			ActiveLogFile = new FileEntry("#", "active.log");
			StorageFolder.files.Add(ActiveLogFile);
		}
	}

	private List<IRCLogEntry> GetLogsFromFile()
	{
		string text = ActiveLogFile.data.Substring(1);
		string[] array = text.Split(EntryLineDelimiter, StringSplitOptions.RemoveEmptyEntries);
		List<IRCLogEntry> list = new List<IRCLogEntry>();
		for (int i = 0; i < array.Length; i++)
		{
			list.Add(IRCLogEntry.DeserializeSafe(array[i]));
		}
		return list;
	}

	public void AddLog(string author, string message, double timestampSecondsOffset)
	{
		DateTime dateTime = DateTime.Now - TimeSpan.FromSeconds(timestampSecondsOffset);
		string timestamp = dateTime.Hour.ToString("00") + ":" + dateTime.Minute.ToString("00");
		AddLog(author, message, timestamp);
	}

	public void AddLog(string author, string message, string timestamp = null)
	{
		if (timestamp == null)
		{
			DateTime now = DateTime.Now;
			timestamp = now.Hour.ToString("00") + ":" + now.Minute.ToString("00");
		}
		IRCLogEntry iRCLogEntry = default(IRCLogEntry);
		iRCLogEntry.Author = author;
		iRCLogEntry.Message = message;
		iRCLogEntry.Timestamp = timestamp;
		IRCLogEntry iRCLogEntry2 = iRCLogEntry;
		string text = iRCLogEntry2.Serialize();
		if (ActiveLogFile.data.Length > 1)
		{
			text = "\n#" + text;
		}
		ActiveLogFile.data += text;
		messagesAddedSinceLastView++;
		if (LogAdded != null)
		{
			LogAdded(author, message);
		}
	}

	public void LeftView()
	{
		messagesAddedSinceLastView = 0;
		isCurrentlyBeingViewed = false;
	}

	public void Draw(Rectangle dest, SpriteBatch sb, bool CanWrite, string WriteUsername, Dictionary<string, Color> HighlightKeywords)
	{
		isCurrentlyBeingViewed = true;
		if (!CanWrite)
		{
			DrawLog(dest, sb, HighlightKeywords);
		}
	}

	private void DrawLog(Rectangle dest, SpriteBatch sb, Dictionary<string, Color> HighlightKeywords)
	{
		List<IRCLogEntry> logsFromFile = GetLogsFromFile();
		int num = (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 4f);
		int num2 = 4;
		int num3 = (int)((float)dest.Height / (float)num);
		int num4 = num3;
		int num5 = logsFromFile.Count - 1;
		int num6 = 0;
		int y = dest.Y;
		DrawnButtonIndex = 1892;
		while (num4 > 0 && num5 >= 0 && dest.Height - num6 > num)
		{
			bool needsNewMessagesLineDraw = messagesAddedSinceLastView > 0 && messagesAddedSinceLastView < logsFromFile.Count && logsFromFile.Count - num5 == messagesAddedSinceLastView;
			num4 -= DrawLogEntry(logsFromFile[num5], dest, sb, HighlightKeywords, num, num4, y, needsNewMessagesLineDraw, out dest);
			dest.Y -= num2;
			num6 += num2;
			num5--;
		}
		if (num5 <= -1 && num4 > 1)
		{
			int num7 = num + 8;
			Rectangle rectangle = new Rectangle(dest.X, dest.Y + dest.Height - num7, dest.Width, num7);
			SpriteFont tinyfont = GuiData.tinyfont;
			string text = "--- " + LocaleTerms.Loc("Log Cleared by Administrator") + " ---";
			Vector2 vector = tinyfont.MeasureString(text);
			sb.DrawString(tinyfont, text, Utils.ClipVec2ForTextRendering(new Vector2((float)rectangle.X + (float)rectangle.Width / 2f - vector.X / 2f, (float)rectangle.Y + (float)rectangle.Height / 2f - vector.Y / 2f)), Color.Gray);
		}
	}

	private int DrawLogEntry(IRCLogEntry log, Rectangle startingDest, SpriteBatch sb, Dictionary<string, Color> HighlightKeywords, int lineHeight, int linesRemaining, int yNotToPass, bool needsNewMessagesLineDraw, out Rectangle dest)
	{
		dest = startingDest;
		int num = 55;
		int val = 76;
		int num2 = 4;
		if (Settings.ActiveLocale != "en-us")
		{
			val = 78;
		}
		if (GuiData.ActiveFontConfig.name.ToLower() == "medium")
		{
			val = 92;
		}
		else if (GuiData.ActiveFontConfig.name.ToLower() == "large")
		{
			val = 115;
		}
		string text = "<" + log.Author + ">";
		int num3 = (int)(GuiData.tinyfont.MeasureString(text).X + (float)num2);
		val = Math.Max(val, (int)(GuiData.tinyfont.MeasureString(text).X + (float)num2));
		int width = dest.Width - (num + num2 + val);
		string message = log.Message;
		string[] array = new string[1] { message };
		if (!log.Message.StartsWith("!ATTACHMENT:"))
		{
			message = Utils.SuperSmartTwimForWidth(message, width, GuiData.tinyfont);
			array = message.Split(Utils.newlineDelim, StringSplitOptions.None);
		}
		Rectangle rectangle = new Rectangle(dest.X + num + num2 + val, dest.Y, dest.Width - (num + num2 + val), dest.Height);
		Rectangle dest2 = new Rectangle(dest.X, dest.Y, num + val, dest.Height);
		Color color = Color.LightBlue;
		if (HighlightKeywords.ContainsKey(log.Author))
		{
			color = HighlightKeywords[log.Author];
		}
		Color defaultColor = Color.Lerp(Color.White, color, 0.22f);
		if (needsNewMessagesLineDraw)
		{
			int num4 = array.Length;
			sb.Draw(destinationRectangle: new Rectangle(dest.X, dest.Y + dest.Height - lineHeight * num4 + 1, dest.Width, 1), texture: Utils.white, color: Color.White * 0.5f);
		}
		int num5 = array.Length - 1;
		while (num5 >= 0 && linesRemaining > 0)
		{
			if (num5 == 0)
			{
				DrawLine("[" + log.Timestamp + "] ", dest2, sb, Color.White);
				int x = dest2.X;
				dest2.X = dest2.X + dest2.Width - num3;
				DrawLine(text, dest2, sb, color);
				dest2.X = x;
			}
			DrawLine(array[num5], rectangle, sb, defaultColor);
			dest.Height -= lineHeight;
			rectangle.Height = dest.Height;
			dest2.Height = dest.Height;
			linesRemaining--;
			if (dest.Y + dest.Height - 6 <= yNotToPass)
			{
				needsNewMessagesLineDraw = false;
				break;
			}
			num5--;
		}
		Rectangle destinationRectangle2 = rectangle;
		destinationRectangle2.Width = 1;
		destinationRectangle2.X -= 5;
		destinationRectangle2.Height = lineHeight * array.Length + 4;
		destinationRectangle2.Y = rectangle.Y + rectangle.Height + 2;
		sb.Draw(Utils.white, destinationRectangle2, Color.White * 0.12f);
		return array.Length;
	}

	private void DrawLine(string line, Rectangle dest, SpriteBatch sb, Color defaultColor)
	{
		Vector2 vector = Utils.ClipVec2ForTextRendering(new Vector2(dest.X, (float)(dest.Y + dest.Height) - (GuiData.ActiveFontConfig.tinyFontCharHeight + 1f)));
		if (line.StartsWith("!ATTACHMENT:"))
		{
			if (AttachmentRenderer.RenderAttachment(line.Substring("!ATTACHMENT:".Length), OS.currentInstance, vector, DrawnButtonIndex, AttachmentPressedSound))
			{
				DrawnButtonIndex++;
			}
		}
		else if (line.StartsWith("!ANNOUNCEMENT!"))
		{
			sb.Draw(destinationRectangle: new Rectangle((int)vector.X - 2, (int)vector.Y, dest.Width + 2, (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 6f)), texture: Utils.white, color: Color.Red * 0.22f);
			sb.DrawString(GuiData.tinyfont, line, vector, defaultColor);
		}
		else
		{
			sb.DrawString(GuiData.tinyfont, line, vector, defaultColor);
		}
	}
}
