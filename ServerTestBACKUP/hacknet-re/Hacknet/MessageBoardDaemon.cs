using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MessageBoardDaemon : AuthenticatingDaemon, IMonitorableDaemon
{
	private enum MessageBoardState
	{
		Thread,
		Board
	}

	private const int THREAD_PREVIEW_HEIGHT = 415;

	private static Dictionary<MessageBoardPostImage, Texture2D> Images;

	private static Color UsernameColor = new Color(17, 119, 67);

	private static Color ImplicationColor = new Color(56, 184, 131);

	private string boardsListingString = "[a/b/c/d/e/f/g/gif/h/hr/k/m/o/p/r/s/t/u/v/vg/vr/w/wg][i/ic][r9k][s4s][cm/hm/lgbt/y][3/adv/an/asp/cgl/ck/co/diy/fa/fit/gd/hc/int/jp/lit/mlp/mu/n/out/po/pol/sci/soc/sp/tg/toy/trv/tv/vp/wsg/x][rs]";

	public string BoardName = "/el/ - " + LocaleTerms.Loc("Digital Security");

	public List<string> ThreadsToAdd = new List<string>();

	private MessageBoardState state = MessageBoardState.Board;

	private ScrollableSectionedPanel threadsPanel;

	private MessageBoardThread viewingThread;

	private Vector2 ThreadScrollPosition = Vector2.Zero;

	private int CurrentThreadHeight = 100;

	private Folder rootFolder;

	private Folder threadsFolder;

	public Action<string, string> MessageAdded;

	public MessageBoardDaemon(Computer c, OS os)
		: base(c, LocaleTerms.Loc("Message Board"), os)
	{
		if (Images == null)
		{
			Images = new Dictionary<MessageBoardPostImage, Texture2D>();
			Images.Add(MessageBoardPostImage.Academic, os.content.Load<Texture2D>("Sprites/Academic_Logo"));
			Images.Add(MessageBoardPostImage.Sun, os.content.Load<Texture2D>("Sprites/Sun"));
			Images.Add(MessageBoardPostImage.Snake, os.content.Load<Texture2D>("Sprites/Snake"));
			Images.Add(MessageBoardPostImage.Circle, os.content.Load<Texture2D>("CircleOutline"));
			Images.Add(MessageBoardPostImage.Duck, os.content.Load<Texture2D>("Sprites/Duck"));
			Images.Add(MessageBoardPostImage.Page, os.content.Load<Texture2D>("Sprites/Page"));
			Images.Add(MessageBoardPostImage.Speech, os.content.Load<Texture2D>("Sprites/SpeechBubble"));
			Images.Add(MessageBoardPostImage.Mod, os.content.Load<Texture2D>("Sprites/Hammer"));
			Images.Add(MessageBoardPostImage.Chip, os.content.Load<Texture2D>("Sprites/Chip"));
		}
		threadsPanel = new ScrollableSectionedPanel(415, GuiData.spriteBatch.GraphicsDevice);
	}

	public override void initFiles()
	{
		base.initFiles();
		rootFolder = new Folder("ImageBoard");
		threadsFolder = new Folder("Threads");
		rootFolder.folders.Add(threadsFolder);
		comp.files.root.folders.Add(rootFolder);
		for (int i = 0; i < ThreadsToAdd.Count; i++)
		{
			AddThread(ThreadsToAdd[i]);
		}
		ThreadsToAdd.Clear();
	}

	public override void loadInit()
	{
		base.loadInit();
		rootFolder = comp.files.root.searchForFolder("ImageBoard");
		threadsFolder = rootFolder.searchForFolder("Threads");
	}

	public void SubscribeToAlertActionFroNewMessage(Action<string, string> act)
	{
		MessageAdded = (Action<string, string>)Delegate.Combine(MessageAdded, act);
	}

	public void UnSubscribeToAlertActionFroNewMessage(Action<string, string> act)
	{
		MessageAdded = (Action<string, string>)Delegate.Remove(MessageAdded, act);
	}

	public bool ShouldDisplayNotifications()
	{
		return true;
	}

	public string GetName()
	{
		return name;
	}

	public override string getSaveString()
	{
		return "<MessageBoard name=\"" + name + "\" boardName=\"" + BoardName + "\"/>";
	}

	public void AddThread(string threadData)
	{
		if (threadsFolder == null)
		{
			ThreadsToAdd.Add(threadData);
			return;
		}
		threadData = ComputerLoader.filter(threadData);
		string text = "test.txt";
		do
		{
			text = Utils.getRandomByte().ToString("000") + Utils.getRandomByte().ToString("000") + Utils.getRandomByte().ToString("000") + ".tm";
		}
		while (threadsFolder.searchForFile(text) != null);
		if (MessageAdded != null)
		{
			MessageAdded("anon", threadData);
		}
		threadsFolder.files.Add(new FileEntry(threadData, text));
	}

	public MessageBoardThread ParseThread(string threadData)
	{
		try
		{
			string[] array = threadData.Split(new string[3] { "------------------------------------------\r\n", "------------------------------------------\n", "------------------------------------------" }, StringSplitOptions.None);
			string text = array[0];
			text = text.Replace("\n", "");
			text = text.Replace("\r", "");
			MessageBoardThread messageBoardThread = default(MessageBoardThread);
			messageBoardThread.id = text;
			messageBoardThread.posts = new List<MessageBoardPost>();
			MessageBoardThread result = messageBoardThread;
			for (int i = 1; i < array.Length; i++)
			{
				if (array[i].Length <= 1)
				{
					continue;
				}
				MessageBoardPost item = default(MessageBoardPost);
				string text2 = array[i];
				if (array[i].StartsWith("#"))
				{
					string value = array[i].Substring(1, array[i].IndexOf('\n'));
					try
					{
						MessageBoardPostImage img = (MessageBoardPostImage)Enum.Parse(typeof(MessageBoardPostImage), value);
						item.img = img;
						text2 = array[i].Substring(array[i].IndexOf('\n') + 1);
					}
					catch (ArgumentException)
					{
						item.img = MessageBoardPostImage.None;
					}
				}
				else
				{
					item.img = MessageBoardPostImage.None;
				}
				item.text = text2;
				result.posts.Add(item);
			}
			return result;
		}
		catch (Exception)
		{
			MessageBoardThread result2 = default(MessageBoardThread);
			result2.id = "error";
			result2.posts = new List<MessageBoardPost>(new MessageBoardPost[1]
			{
				new MessageBoardPost
				{
					img = MessageBoardPostImage.None,
					text = "-- Error Parsing Thread --"
				}
			});
			return result2;
		}
	}

	public void ViewThread(MessageBoardThread thread, int width, int margin, int ImageSize, int headerOffset)
	{
		CurrentThreadHeight = 20;
		for (int i = 0; i < thread.posts.Count; i++)
		{
			CurrentThreadHeight += MeasurePost(thread.posts[i], width, margin, ImageSize, headerOffset) + margin * 2;
		}
		state = MessageBoardState.Thread;
		viewingThread = thread;
		ThreadScrollPosition = Vector2.Zero;
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		state = MessageBoardState.Board;
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		int num = 60;
		Rectangle dest = new Rectangle(bounds.X, bounds.Y, bounds.Width, num);
		DrawHeader(sb, dest);
		Rectangle rectangle = bounds;
		rectangle.Y += num;
		rectangle.Height -= num + 1;
		switch (state)
		{
		case MessageBoardState.Thread:
			DrawFullThreadView(sb, viewingThread, rectangle);
			return;
		}
		threadsPanel.NumberOfPanels = threadsFolder.files.Count;
		Action<int, Rectangle, SpriteBatch> drawSection = delegate(int index, Rectangle drawArea, SpriteBatch sBatch)
		{
			MessageBoardThread thread = ParseThread(threadsFolder.files[index].data);
			DrawThread(thread, sBatch, drawArea, isPreview: true);
		};
		threadsPanel.Draw(drawSection, sb, rectangle);
	}

	private void DrawHeader(SpriteBatch sb, Rectangle dest)
	{
		int num = 4;
		int num2 = 200;
		sb.Draw(Utils.white, new Rectangle(dest.X + num, dest.Y + 4, dest.Width - num * 2, 1), Color.White * 0.5f);
		Vector2 position = new Vector2(dest.X + num, dest.Y + 5);
		int num3 = 0;
		int num4 = 7;
		while (position.X + (float)(num4 * 2) < (float)(dest.X + dest.Width) && num3 < boardsListingString.Length)
		{
			sb.DrawString(GuiData.detailfont, string.Concat(boardsListingString[num3]), position, Color.White * 0.6f);
			num3++;
			position.X += num4;
		}
		sb.DrawString(GuiData.detailfont, "]", position, Color.White * 0.8f);
		try
		{
			if (BoardName == null)
			{
				BoardName = name;
			}
			TextItem.doFontLabel(new Vector2(dest.X + num, dest.Y + 22), BoardName, GuiData.font, os.highlightColor, dest.Width - (num2 + 6 + 22), dest.Height);
		}
		catch (Exception)
		{
		}
		if (state != MessageBoardState.Board && Button.doButton(1931655802, dest.X + dest.Width - num2 - 6, dest.Y + dest.Height / 2 - 4, num2, 24, LocaleTerms.Loc("Back to Board"), os.highlightColor))
		{
			state = MessageBoardState.Board;
		}
		sb.Draw(Utils.white, new Rectangle(dest.X + num, dest.Y + dest.Height - 2, dest.Width - num * 2, 1), Color.White * 0.5f);
	}

	private void DrawFullThreadView(SpriteBatch sb, MessageBoardThread thread, Rectangle dest)
	{
		Rectangle rectangle = dest;
		rectangle.Height = CurrentThreadHeight + 50;
		bool flag = CurrentThreadHeight > dest.Height;
		if (flag)
		{
			ScrollablePanel.beginPanel(1931655001, rectangle, ThreadScrollPosition);
			rectangle.X = (rectangle.Y = 0);
		}
		DrawThread(thread, GuiData.spriteBatch, rectangle);
		if (flag)
		{
			float maxScroll = Math.Max(dest.Height, CurrentThreadHeight - dest.Height);
			ThreadScrollPosition = ScrollablePanel.endPanel(1931655001, ThreadScrollPosition, dest, maxScroll);
		}
	}

	private void DrawThread(MessageBoardThread thread, SpriteBatch sb, Rectangle bounds, bool isPreview = false)
	{
		int num = 4;
		int num2 = 8;
		int num3 = 450;
		int height = 36;
		int num4 = 20;
		int num5 = 80;
		int num6 = 30;
		SpriteFont tinyfont = GuiData.tinyfont;
		Rectangle dest = new Rectangle(bounds.X + num2, bounds.Y + num2, num3, height);
		int num7 = bounds.Y;
		List<MessageBoardPost> list = thread.posts;
		if (isPreview)
		{
			list = GetLastPostsToFitHeight(thread, 415 - num6, bounds.Width, num2, num5, num4, num6);
		}
		for (int i = 0; i < list.Count; i++)
		{
			MessageBoardPost messageBoardPost = list[i];
			int num8 = bounds.Width - 4 * num2;
			if (messageBoardPost.img != 0)
			{
				num8 -= num5;
			}
			string text = Utils.SmartTwimForWidth(messageBoardPost.text, num8, tinyfont);
			Vector2 vector = tinyfont.MeasureString(text);
			vector.Y *= 1.3f;
			dest.Y = num7;
			int num9 = (int)vector.X + num2 * 4;
			if (messageBoardPost.img != 0)
			{
				num9 += num5 + num2;
			}
			dest.Width = Math.Max(num3, num9);
			dest.Height = (int)vector.Y + 2 * num2;
			dest.Width = Math.Max(dest.Width, num5 + 2 * num2);
			if (messageBoardPost.img != 0)
			{
				dest.Height = Math.Max(dest.Height, num5 + 2 * num2);
			}
			dest.Height += num4 + num2 * 2;
			DrawPost(text, messageBoardPost.img, dest, num2, num5, num4, sb, tinyfont);
			num7 += dest.Height + num;
			if (i == 0 && isPreview)
			{
				MessageBoardThread thread2 = thread;
				int num10 = thread.posts.Count - list.Count;
				string text2 = "[+] " + string.Format(LocaleTerms.Loc("{0} posts and image replies omitted"), num10);
				string text3 = LocaleTerms.Loc("Click here to view.");
				TextItem.doFontLabel(new Vector2(dest.X, num7), text2, GuiData.tinyfont, os.lightGray, 288f, 18f, centreVertically: true);
				if (Button.doButton(17839000 + thread.id.GetHashCode(), dest.X + 290, num7 - 2, bounds.Width - 316, 17, text3, null))
				{
					Console.WriteLine("clicked " + i);
					ViewThread(thread2, bounds.Width, num2, num5, num4);
				}
				num7 += 16 + num;
			}
		}
		sb.Draw(Utils.white, new Rectangle(bounds.X + num2, bounds.Y + bounds.Height - 6, bounds.Width - num2 * 2, 1), Color.White * 0.5f);
	}

	private int MeasurePost(MessageBoardPost post, int width, int margin, int ImageSize, int postHeaderOffset)
	{
		SpriteFont tinyfont = GuiData.tinyfont;
		int num = postHeaderOffset;
		int num2 = width - 4 * margin;
		if (post.img != 0)
		{
			num2 -= ImageSize;
		}
		string text = Utils.SmartTwimForWidth(post.text, num2, tinyfont);
		Vector2 vector = tinyfont.MeasureString(text);
		vector.Y *= 1.3f;
		num += (int)vector.Y;
		num += 2 * margin;
		return Math.Max(num, ImageSize + 2 * margin);
	}

	private void DrawPost(string text, MessageBoardPostImage img, Rectangle dest, int margin, int ImageSize, int postheaderOffset, SpriteBatch sb, SpriteFont font)
	{
		sb.Draw(Utils.white, dest, os.highlightColor * 0.2f);
		Vector2 vector = new Vector2(dest.X + margin, dest.Y + margin);
		sb.Draw(Utils.white, new Rectangle((int)vector.X, (int)vector.Y, postheaderOffset - 5, postheaderOffset - 5), os.indentBackgroundColor);
		sb.DrawString(GuiData.smallfont, LocaleTerms.Loc("Anonymous"), vector + new Vector2(postheaderOffset, -2f), UsernameColor);
		Vector2 vector2 = GuiData.smallfont.MeasureString(LocaleTerms.Loc("Anonymous"));
		sb.DrawString(GuiData.detailfont, "01/01/1970(Thu)00:00 UTC+0:0", vector + new Vector2(Math.Max(112f - (float)postheaderOffset, vector2.X + 4f) + (float)postheaderOffset, 3f), Utils.SlightlyDarkGray);
		vector.Y += postheaderOffset;
		if (img != 0 && Images.ContainsKey(img))
		{
			sb.Draw(destinationRectangle: new Rectangle(dest.X + margin, dest.Y + margin + postheaderOffset, ImageSize, ImageSize), texture: Images[img], color: Color.White);
			vector.X += ImageSize + margin + margin;
		}
		string[] array = text.Split(Utils.newlineDelim);
		float y = font.MeasureString(array[0]).Y;
		for (int i = 0; i < array.Length; i++)
		{
			sb.DrawString(font, array[i], vector + new Vector2(0f, (float)i * (y + 2f)), array[i].StartsWith(">") ? ImplicationColor : Color.White);
		}
	}

	private List<MessageBoardPost> GetLastPostsToFitHeight(MessageBoardThread thread, int height, int width, int margin, int ImageSize, int PostHeaderOffset, int ThreadFooterSize, int maxOPSize = int.MaxValue)
	{
		List<MessageBoardPost> list = new List<MessageBoardPost>();
		int num = ThreadFooterSize;
		num += margin * 4;
		int val = MeasurePost(thread.posts[0], width, margin, ImageSize, PostHeaderOffset);
		val = Math.Min(maxOPSize, val);
		list.Add(thread.posts[0]);
		num += val;
		int num2 = thread.posts.Count - 1;
		while (num2 >= 0)
		{
			val = MeasurePost(thread.posts[num2], width, margin, ImageSize, PostHeaderOffset);
			if (num + val < height)
			{
				list.Insert(1, thread.posts[num2]);
				num += val;
				num2--;
				continue;
			}
			break;
		}
		return list;
	}
}
