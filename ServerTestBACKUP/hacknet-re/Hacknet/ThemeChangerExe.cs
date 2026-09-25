using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class ThemeChangerExe : ExeModule
{
	private enum ThemeChangerState
	{
		Startup,
		List,
		LoadItem,
		Activating
	}

	private const float START_LOADING_TIME = 25.5f;

	private RenderTarget2D target;

	private float loadingTimeRemaining = 25.5f;

	private ThemeChangerState state = ThemeChangerState.List;

	private SpriteBatch internalSB;

	private Texture2D circle;

	private BarcodeEffect barcodeEffect;

	private Color themeColor;

	private int remotesSelected = -1;

	private int localsSelected = -1;

	private int remoteScroll = 0;

	private int localScroll = 0;

	public ThemeChangerExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "ThemeSwitch";
		ramCost = 320;
		IdentifierName = "Theme Switch";
		targetIP = os.thisComputer.ip;
		circle = TextureBank.load("Circle", os.content);
		AchievementsManager.Unlock("themeswitch_run");
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		themeColor = os.highlightColor;
		Rectangle dest = new Rectangle(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT, bounds.Width - 4, bounds.Height - 2 - Module.PANEL_HEIGHT);
		switch (state)
		{
		case ThemeChangerState.Startup:
			loadingTimeRemaining -= t;
			DrawLoading(loadingTimeRemaining, 25.5f, dest, spriteBatch);
			break;
		default:
			DrawListing(dest, spriteBatch);
			break;
		case ThemeChangerState.LoadItem:
			break;
		case ThemeChangerState.Activating:
			break;
		}
	}

	public void DrawWithSeperateRT(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		themeColor = os.highlightColor;
		Rectangle destinationRectangle = new Rectangle(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - 3 - Module.PANEL_HEIGHT);
		bool flag = false;
		if (target == null)
		{
			target = new RenderTarget2D(base.spriteBatch.GraphicsDevice, destinationRectangle.Width, destinationRectangle.Height, mipMap: false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
			internalSB = new SpriteBatch(base.spriteBatch.GraphicsDevice);
			flag = true;
		}
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		base.spriteBatch.GraphicsDevice.SetRenderTarget(target);
		Rectangle dest = new Rectangle(0, 0, destinationRectangle.Width, destinationRectangle.Height);
		SpriteBatch spriteBatch = GuiData.spriteBatch;
		GuiData.spriteBatch = internalSB;
		internalSB.Begin();
		if (flag)
		{
			internalSB.GraphicsDevice.Clear(Color.Transparent);
		}
		switch (state)
		{
		case ThemeChangerState.Startup:
			loadingTimeRemaining -= t;
			DrawLoading(loadingTimeRemaining, 25.5f, dest, internalSB);
			break;
		default:
			base.spriteBatch.GraphicsDevice.Clear(Color.Transparent);
			DrawListing(dest, internalSB);
			break;
		case ThemeChangerState.LoadItem:
		case ThemeChangerState.Activating:
			break;
		}
		internalSB.End();
		GuiData.spriteBatch = spriteBatch;
		base.spriteBatch.GraphicsDevice.SetRenderTarget(currentRenderTarget);
		base.spriteBatch.Draw(target, destinationRectangle, Color.White);
	}

	private void DrawListing(Rectangle dest, SpriteBatch sb)
	{
		DrawHeaders(dest, sb);
		if (isExiting)
		{
			return;
		}
		Vector2 pos = new Vector2(dest.X + 2, (float)dest.Y + 60f);
		TextItem.doFontLabel(pos, LocaleTerms.Loc("Remote"), GuiData.smallfont, themeColor, bounds.Width - 20, 20f);
		pos.Y += 18f;
		sb.Draw(Utils.white, new Rectangle(bounds.X + 2, (int)pos.Y, bounds.Width - 6, 1), Utils.AddativeWhite);
		List<string> list = new List<string>();
		Folder currentFolder = Programs.getCurrentFolder(os);
		for (int i = 0; i < currentFolder.files.Count; i++)
		{
			if (ThemeManager.getThemeForDataString(currentFolder.files[i].data) != 0)
			{
				list.Add(currentFolder.files[i].name);
			}
		}
		string text = null;
		string selectedFileData = null;
		Color value = Color.Lerp(os.topBarColor, Utils.AddativeWhite, 0.2f);
		int scrollOffset = SelectableTextList.scrollOffset;
		Rectangle rectangle = new Rectangle((int)pos.X, (int)pos.Y, bounds.Width - 6, 54);
		if (list.Count > 0)
		{
			SelectableTextList.scrollOffset = remoteScroll;
			remotesSelected = SelectableTextList.doFancyList(8139191 + PID, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, list.ToArray(), remotesSelected, value, HasDraggableScrollbar: true);
			if (SelectableTextList.selectionWasChanged)
			{
				localsSelected = -1;
			}
			remoteScroll = SelectableTextList.scrollOffset;
			if (remotesSelected >= 0)
			{
				if (remotesSelected >= list.Count)
				{
					remotesSelected = -1;
				}
				else
				{
					text = list[remotesSelected];
					selectedFileData = currentFolder.searchForFile(text).data;
				}
			}
		}
		else
		{
			sb.Draw(Utils.white, rectangle, Utils.VeryDarkGray);
			TextItem.doFontLabelToSize(rectangle, "    -- " + LocaleTerms.Loc("No Valid Files") + " --    ", GuiData.smallfont, Utils.AddativeWhite);
		}
		pos.Y += rectangle.Height + 6;
		TextItem.doFontLabel(pos, LocaleTerms.Loc("Local Theme Files"), GuiData.smallfont, themeColor, bounds.Width - 20, 20f);
		pos.Y += 18f;
		sb.Draw(Utils.white, new Rectangle(bounds.X + 2, (int)pos.Y, bounds.Width - 6, 1), Utils.AddativeWhite);
		list.Clear();
		List<string> list2 = new List<string>();
		currentFolder = os.thisComputer.files.root.searchForFolder("sys");
		for (int i = 0; i < currentFolder.files.Count; i++)
		{
			if (ThemeManager.getThemeForDataString(currentFolder.files[i].data) != 0)
			{
				list.Add(currentFolder.files[i].name);
				list2.Add(currentFolder.files[i].data);
			}
		}
		currentFolder = os.thisComputer.files.root.searchForFolder("home");
		for (int i = 0; i < currentFolder.files.Count; i++)
		{
			if (ThemeManager.getThemeForDataString(currentFolder.files[i].data) != 0)
			{
				list.Add(currentFolder.files[i].name);
				list2.Add(currentFolder.files[i].data);
			}
		}
		Rectangle rectangle2 = new Rectangle((int)pos.X, (int)pos.Y, bounds.Width - 6, 72);
		if (list.Count > 0)
		{
			SelectableTextList.scrollOffset = localScroll;
			localsSelected = SelectableTextList.doFancyList(839192 + PID, rectangle2.X, rectangle2.Y, rectangle2.Width, rectangle2.Height, list.ToArray(), localsSelected, value, HasDraggableScrollbar: true);
			if (SelectableTextList.selectionWasChanged)
			{
				remotesSelected = -1;
			}
			localScroll = SelectableTextList.scrollOffset;
			if (localsSelected >= 0)
			{
				text = list[localsSelected];
				selectedFileData = list2[localsSelected];
			}
		}
		else
		{
			sb.Draw(Utils.white, rectangle2, Utils.VeryDarkGray);
			TextItem.doFontLabelToSize(rectangle2, "    -- " + LocaleTerms.Loc("No Valid Files") + " --    ", GuiData.smallfont, Utils.AddativeWhite);
		}
		SelectableTextList.scrollOffset = scrollOffset;
		pos.Y += rectangle2.Height + 2;
		Rectangle rectangle3 = new Rectangle(bounds.X + 4, (int)pos.Y + 2, bounds.Width - 8, (int)((float)dest.Height - (pos.Y - (float)dest.Y)) - 4);
		DrawApplyField(text, selectedFileData, rectangle3, sb);
	}

	private void DrawApplyField(string selectedFilename, string selectedFileData, Rectangle bounds, SpriteBatch sb)
	{
		sb.Draw(Utils.white, bounds, Utils.VeryDarkGray);
		sb.Draw(Utils.white, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), Utils.AddativeWhite);
		if (selectedFileData != null && selectedFilename != null)
		{
			OSTheme themeForDataString = ThemeManager.getThemeForDataString(selectedFileData);
			Color representativeColorForTheme = ThemeManager.GetRepresentativeColorForTheme(themeForDataString);
			Rectangle destinationRectangle = new Rectangle(bounds.X + bounds.Width / 4 * 3, bounds.Y + 2, bounds.Width / 4, bounds.Height - 2);
			sb.Draw(Utils.white, destinationRectangle, representativeColorForTheme);
			destinationRectangle.X += destinationRectangle.Width - 15;
			destinationRectangle.Width = 15;
			sb.Draw(Utils.white, destinationRectangle, Color.Black * 0.6f);
			TextItem.doFontLabel(new Vector2(bounds.X, bounds.Y + 2), selectedFilename, GuiData.smallfont, Utils.AddativeWhite, bounds.Width / 4 * 3, 25f);
			if (Button.doButton(3837791 + PID, bounds.X, bounds.Y + 25, bounds.Width / 6 * 5, 30, LocaleTerms.Loc("Activate Theme"), representativeColorForTheme))
			{
				ApplyTheme(selectedFilename, selectedFileData);
			}
		}
	}

	private void ApplyTheme(string themeFilename, string fileData)
	{
		string text = "x-serverBACKUP";
		Folder folder = os.thisComputer.files.root.searchForFolder("sys");
		FileEntry fileEntry = folder.searchForFile("x-server.sys");
		if (fileEntry != null)
		{
			bool flag = true;
			for (int i = 0; i < folder.files.Count; i++)
			{
				if (folder.files[i].name.StartsWith(text) && folder.files[i].data == fileEntry.data)
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				string nonRepeatingFilename = Utils.GetNonRepeatingFilename(text, ".sys", folder);
				folder.files.Add(new FileEntry(fileEntry.data, nonRepeatingFilename));
			}
		}
		fileEntry.data = fileData;
		ThemeManager.switchTheme(os, ThemeManager.getThemeForDataString(fileData));
	}

	private void DrawHeaders(Rectangle dest, SpriteBatch sb)
	{
		if (barcodeEffect == null)
		{
			barcodeEffect = new BarcodeEffect(bounds.Width - 4);
		}
		barcodeEffect.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		barcodeEffect.Draw(dest.X, dest.Y, dest.Width - 4, Math.Min(30, bounds.Height - Module.PANEL_HEIGHT), sb, themeColor);
		if (bounds.Height > Module.PANEL_HEIGHT + 60)
		{
			TextItem.doFontLabel(new Vector2(dest.X + 2, dest.Y + 32), "Themechanger", GuiData.font, Utils.AddativeWhite * 0.8f, bounds.Width - 80, 30f);
			int num = 60;
			if (Button.doButton(PID + 228173, bounds.X + bounds.Width - num - 2, dest.Y + 38, num, 20, LocaleTerms.Loc("Exit"), os.lockedColor))
			{
				isExiting = true;
			}
		}
	}

	private void DrawLoading(float timeRemaining, float totalTime, Rectangle dest, SpriteBatch sb)
	{
		float num = 20f;
		Vector2 loaderCentre = new Vector2((float)dest.X + (float)dest.Width / 2f, (float)dest.Y + (float)dest.Height / 2f);
		float num2 = totalTime - timeRemaining;
		float num3 = num2 / totalTime;
		sb.Draw(Utils.white, dest, Color.Black * 0.05f);
		DrawLoadingCircle(timeRemaining, totalTime, dest, loaderCentre, num, num2 * 0.2f, 1f, sb);
		DrawLoadingCircle(timeRemaining, totalTime, dest, loaderCentre, num + 20f * num3, num2 * -0.4f, 0.7f, sb);
		DrawLoadingCircle(timeRemaining, totalTime, dest, loaderCentre, num + 35f * num3, num2 * 0.5f, 0.52f, sb);
	}

	private void DrawLoadingCircle(float timeRemaining, float totalTime, Rectangle dest, Vector2 loaderCentre, float loaderRadius, float baseRotationAdd, float rotationRateRPS, SpriteBatch sb)
	{
		float num = totalTime - timeRemaining;
		int num2 = 10;
		for (int i = 0; i < num2; i++)
		{
			float num3 = (float)i / (float)num2;
			float num4 = 2f;
			float num5 = 1f;
			float num6 = (float)Math.PI * 2f;
			float num7 = num6 + num5;
			float num8 = num3 * num4;
			float num9 = 0f;
			if (num > num8)
			{
				num9 = num / num8 * rotationRateRPS % num7;
				if (num9 >= num6)
				{
					num9 = 0f;
				}
				num9 = num6 * Utils.QuadraticOutCurve(num9 / num6);
				num9 += baseRotationAdd;
				Vector2 vector = loaderCentre + Utils.PolarToCartesian(num9, loaderRadius);
				sb.Draw(circle, vector, null, Utils.AddativeWhite, 0f, Vector2.Zero, 0.1f * (loaderRadius / 120f), SpriteEffects.None, 0.3f);
				if (Utils.random.NextDouble() < 0.001)
				{
					Vector2 vector2 = loaderCentre + Utils.PolarToCartesian(num9, 20f + Utils.randm(45f));
					sb.Draw(Utils.white, vector, Utils.AddativeWhite);
					Utils.drawLine(sb, vector, vector2, Vector2.Zero, Utils.AddativeWhite * 0.4f, 0.1f);
				}
			}
		}
	}
}
