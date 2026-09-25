using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MemoryForensicsExe : ExeModule, MainDisplayOverrideEXE
{
	public enum MemForensicsState
	{
		Error,
		SelectingFile,
		ReadingFile,
		Main,
		Processing,
		DisplayingSolution
	}

	private const float FileReadTime = 2f;

	private const float BaseProcessingTime = 2f;

	public MemForensicsState State = MemForensicsState.ReadingFile;

	private string ErrorMessage = "Unknown Error";

	private float timeInCurrentState = 0f;

	private float processingTimeThisBatch = 4f;

	private string filenameLoaded = "UNKNOWN";

	private MemoryContents ActiveMem = null;

	private ShiftingGridEffect GridEffect = new ShiftingGridEffect();

	private Color ThemeColorMain = new Color(30, 59, 44, 0);

	private Color ThemeColorLight = new Color(89, 181, 183, 0);

	private Color ThemeColorDark = new Color(19, 51, 35, 0);

	private List<string> OutputData = new List<string>();

	private bool IsDisplayingImages = false;

	private List<Texture2D> OutputTextures = new List<Texture2D>();

	private string AnnouncementData = "Unknown";

	private Vector2 PanelScroll = Vector2.Zero;

	private FlyoutEffect flyoutEffect;

	public bool DisplayOverrideIsActive { get; set; }

	public MemoryForensicsExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		needsProxyAccess = false;
		name = "MemForensics";
		ramCost = 300;
		IdentifierName = "MemForensics";
		if (p.Length <= 1)
		{
			ErrorMessage = "No file specified.";
			State = MemForensicsState.Error;
		}
		else
		{
			LoadFile(p[1], Programs.getCurrentFolder(os));
		}
		DisplayOverrideIsActive = true;
	}

	public static MemoryForensicsExe GenerateInstanceOrNullFromArguments(Rectangle location, OS os, string[] p)
	{
		for (int i = 0; i < os.exes.Count; i++)
		{
			if (os.exes[i] is MemoryForensicsExe memoryForensicsExe)
			{
				if (p.Length > 1)
				{
					memoryForensicsExe.LoadFile(p[1], Programs.getCurrentFolder(os));
				}
				else
				{
					memoryForensicsExe.State = MemForensicsState.SelectingFile;
					memoryForensicsExe.timeInCurrentState = 0f;
				}
				return null;
			}
		}
		return new MemoryForensicsExe(location, os, p);
	}

	internal void LoadFile(string filename, Folder f)
	{
		FileEntry fileEntry = f.searchForFile(filename);
		if (fileEntry == null)
		{
			fileEntry = f.searchForFile(filename + ".md");
			if (fileEntry == null)
			{
				State = MemForensicsState.Error;
				ErrorMessage = string.Format(LocaleTerms.Loc("File {0} not found in current folder!"), filename);
				return;
			}
		}
		try
		{
			ActiveMem = MemoryContents.GetMemoryFromEncodedFileString(fileEntry.data);
			filenameLoaded = filename;
			State = MemForensicsState.ReadingFile;
			timeInCurrentState = 0f;
		}
		catch (Exception ex)
		{
			State = MemForensicsState.Error;
			ErrorMessage = LocaleTerms.Loc("Error deserializing memory dump.") + "\r\n\r\n" + Utils.GenerateReportFromException(ex);
		}
	}

	public override void Update(float t)
	{
		base.Update(t);
		timeInCurrentState += t;
		if (State == MemForensicsState.ReadingFile && timeInCurrentState >= 2f)
		{
			timeInCurrentState = 0f;
			State = MemForensicsState.Main;
		}
		if (State == MemForensicsState.Processing && timeInCurrentState >= processingTimeThisBatch)
		{
			timeInCurrentState = 0f;
			State = MemForensicsState.DisplayingSolution;
		}
		if (State == MemForensicsState.Main)
		{
			OutputData.Clear();
		}
		GridEffect.Update(t);
	}

	private void StartLoadingInTexturesForMemory()
	{
		OutputTextures.Clear();
		for (int i = 0; i < OutputData.Count; i++)
		{
			string text = OutputData[i];
			Texture2D item = null;
			string text2 = Utils.GetFileLoadPrefix() + text;
			if (text2.EndsWith(".jpg") || text2.EndsWith(".png"))
			{
				using FileStream stream = File.OpenRead(text2);
				item = Texture2D.FromStream(Game1.getSingleton().GraphicsDevice, stream);
			}
			else
			{
				item = os.content.Load<Texture2D>(text);
			}
			OutputTextures.Add(item);
		}
	}

	public override void Completed()
	{
		base.Completed();
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle contentAreaDest = GetContentAreaDest();
		if (flyoutEffect == null)
		{
			flyoutEffect = new FlyoutEffect(GuiData.spriteBatch.GraphicsDevice, os.content, contentAreaDest.Width, contentAreaDest.Height);
		}
		flyoutEffect.Draw((float)os.lastGameTime.ElapsedGameTime.TotalSeconds, contentAreaDest, spriteBatch, delegate(SpriteBatch sb, Rectangle innerDest)
		{
			ZoomingDotGridEffect.Render(innerDest, sb, os.timer, ThemeColorDark);
		});
		spriteBatch.Draw(Utils.white, contentAreaDest, Color.Black * 0.6f);
		if (!isExiting)
		{
			Rectangle rectangle = new Rectangle(contentAreaDest.X + 8, contentAreaDest.Y + contentAreaDest.Height - 30 - 8, contentAreaDest.Width - 16, 30);
			if (Button.doButton(383023811 + PID, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Exit"), os.lockedColor))
			{
				isExiting = true;
			}
			rectangle.Y -= rectangle.Height + 8;
			if (Button.doButton(383023822 + PID, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, DisplayOverrideIsActive ? LocaleTerms.Loc("Close Main Display") : LocaleTerms.Loc("Re-Open Main Display"), ThemeColorMain))
			{
				DisplayOverrideIsActive = !DisplayOverrideIsActive;
			}
		}
	}

	private void MoveToProcessing()
	{
		State = MemForensicsState.Processing;
		timeInCurrentState = 0f;
		processingTimeThisBatch = 2f + (Utils.randm(2f) - 1f);
		PanelScroll = Vector2.Zero;
		IsDisplayingImages = false;
	}

	public void RenderMainDisplay(Rectangle dest, SpriteBatch sb)
	{
		dest = RenderMainDisplayHeaders(dest, sb);
		switch (State)
		{
		case MemForensicsState.Error:
		{
			PatternDrawer.draw(dest, 1f, Color.Transparent, Utils.AddativeRed, spriteBatch, PatternDrawer.errorTile);
			Rectangle rectangle2 = new Rectangle(dest.X, dest.Y + dest.Height / 3, dest.Width, 60);
			sb.Draw(Utils.white, rectangle2, Color.Black * 0.4f);
			TextItem.doFontLabelToSize(rectangle2, LocaleTerms.Loc("ERROR"), GuiData.font, Color.White, doNotOversize: true, offsetToTopLeft: true);
			Rectangle rectangle3 = new Rectangle(dest.X, rectangle2.Y + rectangle2.Height + 2, dest.Width, dest.Height / 2);
			sb.Draw(Utils.white, rectangle3, Color.Black * 0.8f);
			string data = ((ErrorMessage.Length > 500) ? ErrorMessage.Substring(0, 500) : ErrorMessage);
			data = Utils.SuperSmartTwimForWidth(data, rectangle3.Width, GuiData.smallfont);
			TextItem.doFontLabelToSize(rectangle3, data, GuiData.smallfont, Utils.AddativeRed, doNotOversize: true, offsetToTopLeft: true);
			if (Button.doButton(381023801, dest.X + 2, rectangle3.Y + rectangle3.Height + 2, bounds.Width / 3, 30, LocaleTerms.Loc("Close"), os.lockedColor))
			{
				isExiting = true;
			}
			break;
		}
		case MemForensicsState.SelectingFile:
			break;
		case MemForensicsState.ReadingFile:
		{
			Rectangle rectangle = new Rectangle(dest.X, dest.Y, (int)((float)dest.Width * (timeInCurrentState / 2f)), dest.Height);
			rectangle = new Rectangle(dest.X + (int)((float)dest.Width * (1f - timeInCurrentState / 2f)), dest.Y, (int)((float)dest.Width * (timeInCurrentState / 2f)), dest.Height);
			GridEffect.RenderGrid(rectangle, sb, ThemeColorDark * 0.5f, ThemeColorMain, ThemeColorDark);
			break;
		}
		case MemForensicsState.Main:
			DrawMainStateBackground(dest, sb);
			RenderMenuMainState(dest, sb);
			break;
		case MemForensicsState.Processing:
			DrawMainStateBackground(dest, sb);
			RenderResultsDisplayMainState(dest, sb, isProcessing: true);
			break;
		case MemForensicsState.DisplayingSolution:
			DrawMainStateBackground(dest, sb);
			RenderResultsDisplayMainState(dest, sb, isProcessing: false);
			break;
		}
	}

	private Rectangle RenderMainDisplayHeaders(Rectangle dest, SpriteBatch sb)
	{
		if (Button.doButton(381023001, dest.X + 3, dest.Y + 2, dest.Width / 2, 25, LocaleTerms.Loc("Close Display"), Color.Gray))
		{
			DisplayOverrideIsActive = false;
		}
		dest.Y += 30;
		dest.Height -= 30;
		return dest;
	}

	private void DrawMainStateBackground(Rectangle dest, SpriteBatch sb)
	{
		GridEffect.RenderGrid(dest, sb, ThemeColorLight, ThemeColorMain, ThemeColorDark, centreEffect: true);
	}

	private void RenderMenuMainState(Rectangle dest, SpriteBatch sb)
	{
		int num = 5;
		int num2 = Math.Min(dest.Height / (num + 1), 32);
		int num3 = (int)((float)(dest.Height - num2 * (num + 1)) / 2f);
		bool flag = State == MemForensicsState.Processing;
		Color value = (flag ? Color.Gray : Color.White);
		Rectangle dest2 = new Rectangle(dest.X + 20, dest.Y + num3, (int)((float)dest.Width * 0.9f), num2);
		sb.Draw(destinationRectangle: new Rectangle(dest.X + 2, dest2.Y - 4, dest.Width - 4, dest2.Height * (num + 1)), texture: Utils.white, color: Color.Black * 0.8f);
		if (num2 <= 6)
		{
			return;
		}
		TextItem.doFontLabelToSize(dest2, LocaleTerms.Loc("Memory Dump") + " : " + filenameLoaded, GuiData.smallfont, Color.LightGray, doNotOversize: true, offsetToTopLeft: true);
		dest2.Y += dest2.Height + 2;
		if (Button.doButton(381023801, dest2.X, dest2.Y, dest2.Width, dest2.Height, LocaleTerms.Loc("Process Recent Commands Run..."), value) && !flag)
		{
			MoveToProcessing();
			OutputData.Clear();
			OutputData.AddRange(ActiveMem.CommandsRun);
			AnnouncementData = LocaleTerms.Loc("Results for recently run commands remaining in cached memory") + "::";
		}
		dest2.Y += dest2.Height + 2;
		if (Button.doButton(381023803, dest2.X, dest2.Y, dest2.Width, dest2.Height, LocaleTerms.Loc("Process Files in Memory..."), value) && !flag)
		{
			MoveToProcessing();
			OutputData.Clear();
			OutputData.AddRange(ActiveMem.DataBlocks);
			AnnouncementData = LocaleTerms.Loc("Results for accessed non-binary file fragments in cached memory") + "::";
		}
		dest2.Y += dest2.Height + 2;
		if (Button.doButton(381023807, dest2.X, dest2.Y, dest2.Width, dest2.Height, LocaleTerms.Loc("Process Images in Memory..."), value) && !flag)
		{
			MoveToProcessing();
			IsDisplayingImages = true;
			OutputData.Clear();
			OutputData.AddRange(ActiveMem.Images);
			Task.Factory.StartNew(delegate
			{
				StartLoadingInTexturesForMemory();
			});
			AnnouncementData = LocaleTerms.Loc("Results for accessed image-type tagged binary fragments in cached memory") + "::";
		}
		dest2.Y += dest2.Height + 2;
		if (Button.doButton(381023809, dest2.X, dest2.Y, dest2.Width, dest2.Height, LocaleTerms.Loc("Exit"), os.lockedColor))
		{
			isExiting = true;
		}
		dest2.Y += dest2.Height + 2;
	}

	private void RenderResultsDisplayMainState(Rectangle dest, SpriteBatch sb, bool isProcessing)
	{
		Color color = new Color(5, 5, 5, 230);
		Rectangle rectangle = new Rectangle(dest.X + 22, dest.Y + 30, dest.Width - 44, 40);
		sb.Draw(Utils.white, rectangle, color);
		rectangle.X += 10;
		TextItem.doFontLabelToSize(rectangle, isProcessing ? LocaleTerms.Loc("PROCESSING") : LocaleTerms.Loc("OUTPUT"), GuiData.font, Color.White, doNotOversize: true, offsetToTopLeft: true);
		rectangle.X -= 10;
		int num = 200;
		int num2 = 30;
		if (Button.doButton(381023909, rectangle.X + rectangle.Width - (num + 6), rectangle.Y + (rectangle.Height / 2 - num2 / 2), num, num2, isProcessing ? LocaleTerms.Loc("Cancel") : LocaleTerms.Loc("Return to Menu"), Color.White))
		{
			State = MemForensicsState.Main;
			timeInCurrentState = 0f;
		}
		rectangle.Y += rectangle.Height;
		rectangle.Height = 1;
		sb.Draw(Utils.white, rectangle, Color.White);
		if (isProcessing)
		{
			Rectangle destinationRectangle = new Rectangle(dest.X + 2, rectangle.Y + rectangle.Height + 2, dest.Width - 4, 20);
			sb.Draw(Utils.white, destinationRectangle, Color.Black);
			float num3 = timeInCurrentState / processingTimeThisBatch;
			destinationRectangle.Width = (int)((float)destinationRectangle.Width * num3);
			sb.Draw(Utils.white, destinationRectangle, Utils.makeColorAddative(Color.LightBlue));
			return;
		}
		Rectangle rectangle2 = new Rectangle(rectangle.X, rectangle.Y + rectangle.Height + 2, rectangle.Width, 1);
		rectangle2.Height = dest.Y + dest.Height - (rectangle.Y + rectangle.Height + 4);
		sb.Draw(Utils.white, rectangle2, color);
		if (OutputData.Count <= 0)
		{
			TextItem.doFontLabelToSize(rectangle2, " - " + LocaleTerms.Loc("No Valid Matches Found") + " - ", GuiData.smallfont, Color.White, doNotOversize: true);
			return;
		}
		Rectangle drawbounds = new Rectangle(0, 0, rectangle2.Width, rectangle2.Height);
		float num4 = 10f;
		for (int i = 0; i < OutputData.Count; i++)
		{
			if (IsDisplayingImages)
			{
				if (OutputTextures.Count < i)
				{
					break;
				}
				num4 += (float)(Math.Min(drawbounds.Width - 16, OutputTextures[i].Height) + 30);
			}
			else
			{
				num4 += 26f + GuiData.smallfont.MeasureString(Utils.SuperSmartTwimForWidth(OutputData[i], drawbounds.Width - 16, GuiData.smallfont)).Y;
			}
		}
		drawbounds.Height = (int)num4;
		ScrollablePanel.beginPanel(3371001, drawbounds, PanelScroll);
		Vector2 pos = new Vector2(drawbounds.X + 14, drawbounds.Y);
		TextItem.doMeasuredFontLabel(pos, AnnouncementData, GuiData.tinyfont, Color.White);
		pos.Y += 20f;
		Rectangle destinationRectangle2 = new Rectangle(drawbounds.X, (int)pos.Y, drawbounds.Width, 1);
		GuiData.spriteBatch.Draw(Utils.white, destinationRectangle2, Color.White);
		pos.Y += 20f;
		for (int i = 0; i < OutputData.Count; i++)
		{
			if (IsDisplayingImages)
			{
				if (OutputTextures.Count < i)
				{
					break;
				}
				Rectangle dest2 = new Rectangle((int)pos.X + 8, (int)pos.Y, drawbounds.Width - 16, drawbounds.Width - 16);
				dest2.Height = Math.Min(dest2.Height, OutputTextures[i].Height);
				Rectangle rectangle3 = Utils.DrawSpriteAspectCorrect(dest2, GuiData.spriteBatch, OutputTextures[i], Color.White);
				float num5 = rectangle3.Y - dest2.Y + rectangle3.Height;
				pos.Y += num5 + 12f;
			}
			else
			{
				string text = Utils.SuperSmartTwimForWidth(OutputData[i], drawbounds.Width - 16, GuiData.smallfont);
				Vector2 vector = TextItem.doMeasuredFontLabel(pos, text, GuiData.smallfont, Color.White * 0.9f);
				Rectangle destinationRectangle3 = new Rectangle(drawbounds.X + 6, (int)pos.Y + (int)((vector.Y - 3f) / 2f), 3, 3);
				GuiData.spriteBatch.Draw(Utils.white, destinationRectangle3, Color.Gray);
				pos.Y += vector.Y + 6f;
			}
			destinationRectangle2 = new Rectangle(drawbounds.X, (int)pos.Y, rectangle2.Width, 1);
			GuiData.spriteBatch.Draw(Utils.white, destinationRectangle2, Color.Gray);
			pos.Y += 10f;
		}
		PanelScroll = ScrollablePanel.endPanel(3371001, PanelScroll, rectangle2, 2000f);
		float num6 = num4 - (float)rectangle2.Height;
		if (PanelScroll.Y > num6)
		{
			PanelScroll.Y = num6;
		}
	}
}
