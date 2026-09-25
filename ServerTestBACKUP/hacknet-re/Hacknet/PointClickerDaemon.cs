using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class PointClickerDaemon : Daemon
{
	private struct PointClickerStar
	{
		public Vector2 Pos;

		public float scale;

		public float life;

		public float rot;

		public float timescale;

		public Color color;
	}

	private struct UpgradeNotifier
	{
		public string text;

		public float timer;
	}

	private class PointClickerGameState
	{
		public int currentStoryElement;

		public long points;

		public List<int> upgradeCounts;

		public PointClickerGameState(int upgradesTotal)
		{
			currentStoryElement = -1;
			points = 0L;
			upgradeCounts = new List<int>();
			for (int i = 0; i < upgradesTotal; i++)
			{
				upgradeCounts.Add(0);
			}
		}

		public string ToSaveString()
		{
			string text = points + "\n" + currentStoryElement + "\n";
			for (int i = 0; i < upgradeCounts.Count; i++)
			{
				text = text + upgradeCounts[i] + ",";
			}
			return text;
		}

		public static PointClickerGameState LoadFromString(string save)
		{
			PointClickerGameState pointClickerGameState = new PointClickerGameState(0);
			string[] array = save.Split(Utils.newlineDelim);
			pointClickerGameState.points = Convert.ToInt64(array[0]);
			pointClickerGameState.currentStoryElement = Convert.ToInt32(array[1]);
			string[] array2 = array[2].Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array2.Length; i++)
			{
				pointClickerGameState.upgradeCounts.Add(Convert.ToInt32(array2[i]));
			}
			return pointClickerGameState;
		}
	}

	private enum PointClickerScreenState
	{
		Welcome,
		Error,
		Main
	}

	private float UpgradeCostMultiplier = 13f;

	private List<string> upgradeNames = new List<string>();

	private List<float> upgradeValues = new List<float>();

	private List<float> upgradeCosts = new List<float>();

	private List<string> storyBeats = new List<string>();

	private List<long> storyBeatChangers = new List<long>();

	private PointClickerScreenState state = PointClickerScreenState.Welcome;

	private PointClickerGameState activeState = null;

	private Folder savesFolder;

	private Folder rootFolder;

	private float currentRate = 0f;

	private float pointOverflow = 0f;

	private Texture2D background1;

	private Texture2D background2;

	private Texture2D logoBase;

	private Texture2D logoOverlay1;

	private Texture2D logoOverlay2;

	private Texture2D logoStar;

	private Texture2D scanlinesTextBackground;

	private RenderTarget2D logoRenderBase;

	private SpriteBatch logoBatch;

	private List<PointClickerStar> Stars = new List<PointClickerStar>();

	private Color ThemeColor = new Color(133, 239, 255, 0);

	private Color ThemeColorBacking = new Color(13, 59, 74, 250);

	private Color ThemeColorHighlight = new Color(227, 0, 121, 200);

	private ScrollableSectionedPanel scrollerPanel;

	private int hoverIndex = 0;

	private string ActiveStory = "";

	private string userFilePath = null;

	private float timeSinceLastSave = 0f;

	private List<UpgradeNotifier> UpgradeNotifiers = new List<UpgradeNotifier>();

	public PointClickerDaemon(Computer computer, string serviceName, OS opSystem)
		: base(computer, serviceName, opSystem)
	{
		InitGameSettings();
		InitLogoSettings();
		InitRest();
	}

	private void InitRest()
	{
	}

	private void InitLogoSettings()
	{
		background1 = os.content.Load<Texture2D>("EffectFiles/PointClicker/Background1");
		background2 = os.content.Load<Texture2D>("EffectFiles/PointClicker/Background2");
		logoBase = os.content.Load<Texture2D>("EffectFiles/PointClicker/BaseLogo");
		logoOverlay1 = os.content.Load<Texture2D>("EffectFiles/PointClicker/LogoOverlay1");
		logoOverlay2 = os.content.Load<Texture2D>("EffectFiles/PointClicker/LogoOverlay2");
		logoStar = os.content.Load<Texture2D>("EffectFiles/PointClicker/Star");
		scanlinesTextBackground = os.content.Load<Texture2D>("EffectFiles/ScanlinesTextBackground");
		logoRenderBase = new RenderTarget2D(GuiData.spriteBatch.GraphicsDevice, 768, 384);
		logoBatch = new SpriteBatch(GuiData.spriteBatch.GraphicsDevice);
		for (int i = 0; i < 40; i++)
		{
			AddRandomLogoStar(randomStartLife: true);
		}
	}

	private void UpdateRate()
	{
		currentRate = 0f;
		for (int i = 0; i < upgradeNames.Count; i++)
		{
			currentRate += upgradeValues[i] * (float)activeState.upgradeCounts[i];
		}
	}

	private void UpdatePoints()
	{
		if (activeState == null)
		{
			return;
		}
		if ((double)currentRate > 0.0 || (double)currentRate < -1.0)
		{
			double num = (double)currentRate * os.lastGameTime.ElapsedGameTime.TotalSeconds;
			activeState.points += (int)num;
			pointOverflow += (float)(num - (double)(int)num);
			if (pointOverflow > 1f)
			{
				int num2 = (int)pointOverflow;
				activeState.points += num2;
				pointOverflow -= num2;
			}
		}
		UpdateStory();
		if (ActiveStory == null)
		{
			ActiveStory = "";
		}
		if (activeState.points <= -1)
		{
			AchievementsManager.Unlock("pointclicker_expert", recordAndCheckFlag: true);
		}
		timeSinceLastSave += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		if (timeSinceLastSave > 10f)
		{
			SaveProgress();
		}
	}

	private void UpdateStory()
	{
		if (activeState.currentStoryElement < 0)
		{
			activeState.currentStoryElement = 0;
			ActiveStory = storyBeats[activeState.currentStoryElement] ?? "";
		}
		else if (storyBeatChangers.Count > activeState.currentStoryElement + 1 && activeState.points >= storyBeatChangers[activeState.currentStoryElement + 1])
		{
			activeState.currentStoryElement++;
			ActiveStory = storyBeats[activeState.currentStoryElement] ?? "";
		}
	}

	private void PurchaseUpgrade(int index)
	{
		if (activeState != null && (float)activeState.points >= upgradeCosts[index])
		{
			activeState.upgradeCounts[index]++;
			activeState.points -= (int)upgradeCosts[index];
			pointOverflow -= upgradeCosts[index] - (float)(int)upgradeCosts[index];
			UpdateRate();
			UpgradeNotifiers.Add(new UpgradeNotifier
			{
				text = "+" + upgradeValues[index],
				timer = 1f
			});
			SaveProgress();
			if (index >= upgradeNames.Count - 1)
			{
				AchievementsManager.Unlock("pointclicker_basic", recordAndCheckFlag: true);
			}
		}
	}

	private void SaveProgress()
	{
		for (int i = 0; i < savesFolder.files.Count; i++)
		{
			if (savesFolder.files[i].name == userFilePath)
			{
				savesFolder.files[i].data = activeState.ToSaveString();
				timeSinceLastSave = 0f;
				break;
			}
		}
	}

	private void AddRandomLogoStar(bool randomStartLife = false)
	{
		PointClickerStar pointClickerStar = default(PointClickerStar);
		pointClickerStar.Pos = new Vector2(Utils.randm(1f), Utils.randm(1f));
		pointClickerStar.life = (randomStartLife ? Utils.randm(1f) : 1f);
		pointClickerStar.rot = Utils.randm(6.48f);
		pointClickerStar.scale = 0.2f + Utils.rand(1.3f);
		pointClickerStar.timescale = 0.3f + Utils.randm(1.35f);
		PointClickerStar item = pointClickerStar;
		item.color = Utils.AddativeWhite;
		float num = Utils.randm(1f);
		int maxValue = 80;
		if (num < 0.3f)
		{
			item.color.R = (byte)(255 - Utils.random.Next(maxValue));
		}
		else if (num < 0.6f)
		{
			item.color.G = (byte)(255 - Utils.random.Next(maxValue));
		}
		else if (num < 0.9f)
		{
			item.color.B = (byte)(255 - Utils.random.Next(maxValue));
		}
		Stars.Add(item);
	}

	private void InitGameSettings()
	{
		upgradeNames.Add(LocaleTerms.Loc("Click Me!"));
		upgradeNames.Add(LocaleTerms.Loc("Autoclicker v1"));
		upgradeNames.Add(LocaleTerms.Loc("Autoclicker v2"));
		upgradeNames.Add(LocaleTerms.Loc("Pointereiellion"));
		upgradeValues.Add(0.04f);
		upgradeValues.Add(1f);
		upgradeValues.Add(10f);
		upgradeValues.Add(200f);
		storyBeats.Add("Your glorious ClickPoints empire begins");
		storyBeats.Add("The hard days of manual button clicking labour seem endless, but a better future is in sight.");
		storyBeats.Add("The investment is returned - you finally turn a profit.");
		storyBeats.Add("Your long days of labour to gather the initial 12 points are a fast-fading memory.");
		storyBeats.Add("You reach international acclaim as a prominent and incredibly wealthy point collector.");
		storyBeats.Add("Your enormous pile of points is now larger than Everest");
		storyBeats.Add("The ClickPoints continent is declared : a landmass made entirely of your insane wealth.");
		storyBeatChangers.Add(0L);
		storyBeatChangers.Add(5L);
		storyBeatChangers.Add(15L);
		storyBeatChangers.Add(200L);
		storyBeatChangers.Add(100000L);
		storyBeatChangers.Add(1000000000000L);
		storyBeatChangers.Add(11111000000000000L);
		for (int i = 3; i < 50; i++)
		{
			upgradeNames.Add(LocaleTerms.Loc("Upgrade") + " " + (i + 1));
			upgradeValues.Add((float)Math.Max(i * i * i * i * i, 0.01));
		}
		for (int j = 0; j < upgradeValues.Count; j++)
		{
			upgradeCosts.Add(upgradeValues[j] * (float)(1 + j / 50 * 5) * UpgradeCostMultiplier * (1f + UpgradeCostMultiplier * ((float)((j + 1) / upgradeValues.Count) * 5f)));
		}
		upgradeCosts[0] = 0f;
		upgradeCosts[1] = 12f;
	}

	public override void initFiles()
	{
		base.initFiles();
		rootFolder = new Folder("PointClicker");
		savesFolder = new Folder("Saves");
		int num = 50;
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			string text = null;
			do
			{
				text = People.all[i + num2].handle;
				if (text == null)
				{
					num2++;
				}
			}
			while (i + num2 < People.all.Count && text == null);
			if (i == 22)
			{
				text = "Mengsk";
			}
			if (i == 28)
			{
				text = "Bit";
			}
			if (text != null)
			{
				AddSaveForName(text, i == 22 || i == 28);
			}
		}
		rootFolder.folders.Add(savesFolder);
		comp.files.root.folders.Add(rootFolder);
		FileEntry item = new FileEntry(Computer.generateBinaryString(1000), "config.ini");
		rootFolder.files.Add(item);
		FileEntry item2 = new FileEntry(LocaleTerms.Loc("IMPORTANT : NEVER DELETE OR RE-NAME") + " \"config.ini\"\n " + LocaleTerms.Loc("IT IS SYSTEM CRITICAL!") + " " + LocaleTerms.Loc("Removing it causes instant crash. DO NOT TEST THIS"), "IMPORTANT_README_DONT_CRASH.txt");
		rootFolder.files.Add(item2);
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		state = PointClickerScreenState.Welcome;
		pointOverflow = 0f;
	}

	public override void loadInit()
	{
		base.loadInit();
		rootFolder = comp.files.root.searchForFolder("PointClicker");
		savesFolder = rootFolder.searchForFolder("Saves");
	}

	private void AddSaveForName(string name, bool isSuperHighScore = false)
	{
		PointClickerGameState pointClickerGameState = new PointClickerGameState(upgradeValues.Count);
		for (int i = 0; i < pointClickerGameState.upgradeCounts.Count; i++)
		{
			pointClickerGameState.upgradeCounts[i] = (int)(10f * (Utils.randm(1f) * ((float)i / (float)pointClickerGameState.upgradeCounts.Count)));
			if (isSuperHighScore)
			{
				pointClickerGameState.upgradeCounts[i] = 900 + (int)(Utils.randm(1f) * 99.9f);
			}
		}
		pointClickerGameState.points = Utils.random.Next();
		pointClickerGameState.currentStoryElement = Utils.random.Next(storyBeats.Count);
		FileEntry item = new FileEntry(pointClickerGameState.ToSaveString(), name + ".pcsav");
		savesFolder.files.Add(item);
	}

	public override string getSaveString()
	{
		return "<PointClicker />";
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		switch (state)
		{
		default:
			DrawWelcome(bounds, sb);
			break;
		case PointClickerScreenState.Main:
			UpdatePoints();
			DrawMainScreen(bounds, sb);
			break;
		case PointClickerScreenState.Error:
			break;
		}
		TextItem.DrawShadow = drawShadow;
	}

	private void DrawLogo(Rectangle dest, SpriteBatch sb)
	{
		float num = (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		for (int i = 0; i < Stars.Count; i++)
		{
			PointClickerStar value = Stars[i];
			value.life -= num * 2f * value.timescale;
			if (Stars[i].life <= 0f)
			{
				Stars.RemoveAt(i);
				i--;
				AddRandomLogoStar();
			}
			else
			{
				Stars[i] = value;
			}
		}
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		sb.GraphicsDevice.SetRenderTarget(logoRenderBase);
		sb.GraphicsDevice.Clear(Color.Transparent);
		logoBatch.Begin();
		float num2 = (float)(Math.Sin(os.timer / 2.2f) + 1.0) / 2f;
		logoBatch.Draw(background1, Vector2.Zero, Utils.AddativeWhite * num2);
		logoBatch.Draw(background2, Vector2.Zero, Utils.AddativeWhite * (1f - num2));
		Rectangle dest2 = new Rectangle(0, 0, logoBase.Width, logoBase.Height);
		logoBatch.Draw(logoBase, Vector2.Zero, Color.White);
		FlickeringTextEffect.DrawFlickeringSprite(logoBatch, dest2, logoBase, 4f, 0.25f, os, Color.White);
		float num3 = 0.44f + (float)(Math.Sin(os.timer * 0.823f) + 1.0) / 2f;
		logoBatch.Draw(logoOverlay1, Vector2.Zero, Utils.AddativeWhite * num3);
		logoBatch.Draw(logoOverlay2, Vector2.Zero, Utils.AddativeWhite * (1f - num3));
		logoBatch.End();
		sb.GraphicsDevice.SetRenderTarget(currentRenderTarget);
		for (int i = 0; i < Stars.Count; i++)
		{
			DrawStar(dest, sb, Stars[i]);
		}
		FlickeringTextEffect.DrawFlickeringSpriteAltWeightings(sb, dest, logoRenderBase, 4f, 0.01f, os, Utils.AddativeWhite);
	}

	private void DrawStar(Rectangle logoDest, SpriteBatch sb, PointClickerStar star)
	{
		Vector2 position = new Vector2(star.Pos.X * (float)logoDest.Width * 0.5f + (float)logoDest.X, star.Pos.Y * (float)logoDest.Height * 0.5f + (float)logoDest.Y);
		position.X += (float)logoDest.Width * 0.25f;
		position.Y += (float)logoDest.Height * 0.25f;
		float num = 0f;
		num = ((!(star.life < 0.9f)) ? (1f - (star.life - 0.9f) / 0.1f) : ((star.life - 0.1f) / 0.9f));
		float num2 = Vector2.Distance(star.Pos, new Vector2(0.5f));
		float num3 = 0.9f;
		if (num2 > num3)
		{
			num = (1f - (num2 - num3) / 1f) * num;
		}
		sb.Draw(logoStar, position, null, star.color * num, star.rot * (star.life * 0.5f), new Vector2(logoStar.Width / 2, logoStar.Height / 2), star.scale * num, SpriteEffects.None, 0.4f);
	}

	private void DrawMainScreen(Rectangle bounds, SpriteBatch sb)
	{
		string points = activeState.points.ToString();
		Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 10, bounds.Width - 2, 100);
		DrawMonospaceString(rectangle, sb, points);
		rectangle.Y -= 4;
		sb.Draw(scanlinesTextBackground, rectangle, ThemeColor * 0.2f);
		for (int i = 0; i < Stars.Count; i++)
		{
			DrawStar(rectangle, sb, Stars[i]);
		}
		Rectangle bounds2 = new Rectangle(bounds.X + 2, rectangle.Y + rectangle.Height + 12, bounds.Width / 2 - 4, bounds.Height - 12 - (rectangle.Y + rectangle.Height - bounds.Y));
		DrawUpgrades(bounds2, sb);
		float num = (float)logoRenderBase.Height / (float)logoRenderBase.Width;
		int num2 = 45;
		int num3 = bounds.Width / 2 + num2;
		int num4 = (int)((float)num3 * num);
		Rectangle bounds3 = new Rectangle(bounds.X + bounds2.Width + 4, bounds2.Y, bounds.Width - bounds2.Width - 8, bounds2.Height - num4 + 30);
		DrawHoverTooltip(bounds3, sb);
		Rectangle rectangle2 = new Rectangle(bounds.X + bounds.Width - num3 + 42, bounds.Y + bounds.Height - num4 + 10, num3 - 50, 50);
		string text = Utils.SuperSmartTwimForWidth(ActiveStory, rectangle2.Width, GuiData.smallfont);
		TextItem.doFontLabel(new Vector2(rectangle2.X, rectangle2.Y), text, GuiData.smallfont, ThemeColor, rectangle2.Width, rectangle2.Height);
		Rectangle dest = new Rectangle(bounds.X + bounds.Width - num3 + 35, bounds.Y + bounds.Height - num4 + 20, num3, num4);
		DrawLogo(dest, sb);
		if (Button.doButton(3032113, bounds.X + bounds.Width - 22, bounds.Y + bounds.Height - 22, 20, 20, "X", os.lockedColor))
		{
			state = PointClickerScreenState.Welcome;
		}
	}

	private void DrawHoverTooltip(Rectangle bounds, SpriteBatch sb)
	{
		float num = 80f;
		int num2 = bounds.Height - 80;
		Rectangle rectangle;
		if (hoverIndex > -1 && hoverIndex < upgradeNames.Count && activeState != null)
		{
			Rectangle bounds2 = bounds;
			bounds2.Height = num2;
			bool flag = upgradeCosts[hoverIndex] <= (float)activeState.points;
			float num3 = 20f;
			FancyOutlines.DrawCornerCutOutline(bounds2, sb, num3, ThemeColor);
			Rectangle dest = new Rectangle((int)((float)bounds.X + num3 + 4f), bounds.Y + 3, (int)((float)bounds.Width - (num3 + 4f) * 2f), 40);
			TextItem.doFontLabelToSize(dest, upgradeNames[hoverIndex], GuiData.font, ThemeColorHighlight);
			rectangle = new Rectangle(bounds.X + 2, bounds.Y + dest.Height + 4, bounds.Width - 4, 20);
			string text = ((!flag) ? LocaleTerms.Loc("INSUFFICIENT POINTS") : ((Settings.ActiveLocale != "en-us") ? LocaleTerms.Loc("UPGRADE AVALIABLE") : "UPGRADE AVAILABLE"));
			TextItem.doFontLabelToSize(rectangle, text, GuiData.font, flag ? (ThemeColorHighlight * 0.8f) : Color.Gray);
			rectangle.Y += rectangle.Height;
			rectangle.Height = 50;
			rectangle.X += 4;
			rectangle.Width -= 4;
			float num4 = ((activeState.points == 0) ? 1f : (upgradeCosts[hoverIndex] / (float)activeState.points));
			num4 = ((!float.IsNaN(num4)) ? (num4 * 100f) : 100f);
			DrawStatsTextBlock(LocaleTerms.Loc("COST"), string.Concat(upgradeCosts[hoverIndex]), num4.ToString("00.0") + LocaleTerms.Loc("% of current Points"), rectangle, sb, num);
			rectangle.Y += rectangle.Height;
			float num5 = ((currentRate <= 0f) ? 100f : (upgradeValues[hoverIndex] / currentRate * 100f));
			DrawStatsTextBlock("+PPS", string.Concat(upgradeValues[hoverIndex]), num5.ToString("00.0") + LocaleTerms.Loc("% of current Points Per Second"), rectangle, sb, num);
			Rectangle rectangle2 = new Rectangle((int)((float)bounds.X + num3 + 4f), rectangle.Y + rectangle.Height + 4, (int)((float)bounds.Width - (num3 + 4f) * 2f), 50);
			if (flag)
			{
				sb.Draw(scanlinesTextBackground, rectangle2, Utils.makeColorAddative(ThemeColorHighlight) * 0.6f);
				FlickeringTextEffect.DrawFlickeringText(rectangle2, "CLICK TO UPGRADE", 3f, 0.1f, GuiData.titlefont, os, Utils.AddativeWhite);
			}
			else
			{
				sb.Draw(scanlinesTextBackground, rectangle2, Color.Lerp(os.brightLockedColor, Utils.makeColorAddative(Color.Red), 0.2f + Utils.randm(0.8f)) * 0.4f);
				FlickeringTextEffect.DrawFlickeringText(rectangle2, "INSUFFICIENT POINTS", 3f, 0.1f, GuiData.titlefont, os, Utils.AddativeWhite);
			}
		}
		rectangle = new Rectangle(bounds.X + 2, bounds.Y + num2 + 4, bounds.Width - 4, 50);
		float f = ((currentRate <= 0f) ? 0f : ((float)activeState.points / currentRate));
		if (float.IsNaN(f))
		{
			f = float.PositiveInfinity;
		}
		DrawStatsTextBlock("PPS", currentRate.ToString("000.0") ?? "", f.ToString("00.0") + " " + LocaleTerms.Loc("seconds to double current points"), rectangle, sb, num);
		float num6 = (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		for (int i = 0; i < UpgradeNotifiers.Count; i++)
		{
			UpgradeNotifier value = UpgradeNotifiers[i];
			value.timer -= num6 * 4f;
			if (value.timer <= 0f)
			{
				UpgradeNotifiers.RemoveAt(i);
				i--;
			}
			else
			{
				Vector2 vector = GuiData.font.MeasureString(value.text);
				sb.DrawString(GuiData.font, value.text, new Vector2((float)rectangle.X + ((float)rectangle.Width - num) / 2f + num, rectangle.Y + 10), ThemeColorHighlight * value.timer, 0f, vector / 2f, 0.5f + (1f - value.timer) * 2.2f, SpriteEffects.None, 0.9f);
				UpgradeNotifiers[i] = value;
			}
		}
	}

	private void DrawStatsTextBlock(string anouncer, string main, string secondary, Rectangle bounds, SpriteBatch sb, float announcerWidth)
	{
		Vector2 pos = new Vector2(bounds.X, bounds.Y);
		TextItem.doFontLabel(pos, anouncer, GuiData.font, Utils.AddativeWhite, announcerWidth - 10f, (float)bounds.Height - 8f, centreVertically: true);
		pos.X += announcerWidth + 2f;
		TextItem.doFontLabel(new Vector2((float)bounds.X + announcerWidth - 12f, bounds.Y), ":", GuiData.font, Utils.AddativeWhite, 22f, bounds.Height);
		TextItem.doFontLabel(pos, main, GuiData.font, ThemeColorHighlight, (float)bounds.Width - announcerWidth, (float)bounds.Height - 8f, centreVertically: true);
		pos.Y += 29f;
		pos.X = bounds.X;
		TextItem.doFontLabel(pos, "[" + secondary + "]", GuiData.smallfont, Color.Gray, bounds.Width, bounds.Height);
	}

	private void DrawUpgrades(Rectangle bounds, SpriteBatch sb)
	{
		int panelHeight = 28;
		if (scrollerPanel == null)
		{
			scrollerPanel = new ScrollableSectionedPanel(panelHeight, sb.GraphicsDevice);
		}
		scrollerPanel.NumberOfPanels = upgradeNames.Count;
		Button.outlineOnly = true;
		Button.drawingOutline = false;
		int drawnThisCycle = 0;
		bool needsButtonChecks = bounds.Contains(GuiData.getMousePoint());
		scrollerPanel.Draw(delegate(int index, Rectangle drawAreaFull, SpriteBatch sprBatch)
		{
			Rectangle destinationRectangle = new Rectangle(drawAreaFull.X, drawAreaFull.Y, drawAreaFull.Width - 12, drawAreaFull.Height);
			int num = 115700 + index * 111;
			if (needsButtonChecks && Button.doButton(num, destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height, "", Color.Transparent))
			{
				PurchaseUpgrade(index);
			}
			else if (!needsButtonChecks && GuiData.hot == num)
			{
				GuiData.hot = -1;
			}
			bool flag = upgradeCosts[index] <= (float)activeState.points;
			bool flag2 = flag && GuiData.hot == num;
			if (GuiData.hot == num)
			{
				hoverIndex = index;
			}
			if (flag2)
			{
				int height = destinationRectangle.Height;
				int num2 = 0;
				int num3 = 0;
				if (drawAreaFull.X == 0 && drawAreaFull.Y == 0)
				{
					if (drawnThisCycle == 0)
					{
						num2 = bounds.X;
						num3 = bounds.Y;
					}
					else
					{
						num2 = bounds.X;
						num3 = bounds.Y + bounds.Height - panelHeight / 2;
					}
				}
				Rectangle rectangle = new Rectangle(num2 + destinationRectangle.X - height, num3 + destinationRectangle.Y - height, destinationRectangle.Width + 2 * height, destinationRectangle.Height + 2 * height);
				for (int i = 0; i < Stars.Count; i++)
				{
					DrawStar(rectangle, sb, Stars[i]);
				}
				sb.Draw(scanlinesTextBackground, rectangle, ThemeColor * ((GuiData.active == num) ? 0.6f : 0.3f));
			}
			sprBatch.Draw(scanlinesTextBackground, destinationRectangle, new Rectangle(scanlinesTextBackground.Width / 2, scanlinesTextBackground.Height / 9 * 4, scanlinesTextBackground.Width / 2, scanlinesTextBackground.Height / 4), flag ? (ThemeColor * 0.2f) : (Utils.AddativeWhite * 0.08f));
			if (GuiData.hot == num)
			{
				RenderedRectangle.doRectangle(destinationRectangle.X + 1, destinationRectangle.Y + 1, destinationRectangle.Width - 2, destinationRectangle.Height - 2, (!flag2) ? Color.Black : ((GuiData.active == num) ? Color.Black : ThemeColorBacking));
			}
			if (index == 0)
			{
				Utils.drawLine(sprBatch, new Vector2(destinationRectangle.X + 1, destinationRectangle.Y + 1), new Vector2(destinationRectangle.X + destinationRectangle.Width - 2, destinationRectangle.Y + 1), Vector2.Zero, ThemeColor, 0.8f);
			}
			Utils.drawLine(sprBatch, new Vector2(destinationRectangle.X + 1, destinationRectangle.Y + destinationRectangle.Height - 2), new Vector2(destinationRectangle.X + destinationRectangle.Width - 2, destinationRectangle.Y + destinationRectangle.Height - 2), Vector2.Zero, ThemeColor, 0.8f);
			if (flag)
			{
				sprBatch.Draw(Utils.white, new Rectangle(destinationRectangle.X, destinationRectangle.Y + 1, 8, destinationRectangle.Height - 2), ThemeColor);
			}
			string text = "[" + activeState.upgradeCounts[index].ToString("000") + "] " + upgradeNames[index];
			TextItem.doFontLabel(new Vector2(destinationRectangle.X + 4 + (flag ? 10 : 0), destinationRectangle.Y + 4), text, GuiData.UISmallfont, (!flag2) ? (flag ? Utils.AddativeWhite : Color.Gray) : ((GuiData.active == num) ? ThemeColor : (flag ? Color.White : Color.Gray)), destinationRectangle.Width - 6);
			drawnThisCycle++;
		}, sb, bounds);
		Button.outlineOnly = false;
		Button.drawingOutline = true;
	}

	private void DrawMonospaceString(Rectangle bounds, SpriteBatch sb, string points)
	{
		points = points.Trim();
		float num = 65f;
		float num2 = ((float)points.Length + 1f) * num;
		float num3 = 1f;
		if (num2 > (float)bounds.Width)
		{
			num3 = (float)bounds.Width / num2;
		}
		float num4 = num2 * num3;
		float num5 = ((float)bounds.Width - num4) / 2f;
		Vector2 vector = new Vector2((float)bounds.X + num5, bounds.Y);
		for (int i = 0; i < points.Length; i++)
		{
			string text = string.Concat(points[i]);
			float x = num * num3 - GuiData.titlefont.MeasureString(text).X * num3 / 2f;
			sb.DrawString(GuiData.titlefont, text, vector + new Vector2(x, 0f), Color.White, 0f, Vector2.Zero, num3, SpriteEffects.None, 0.67f);
			vector.X += num * num3;
		}
	}

	private void DrawWelcome(Rectangle bounds, SpriteBatch sb)
	{
		float num = (float)logoRenderBase.Height / (float)logoRenderBase.Width;
		int num2 = 45;
		Rectangle dest = new Rectangle(bounds.X - num2 + 20, bounds.Y, bounds.Width + num2, (int)((float)(bounds.Width + 2 * num2) * num));
		DrawLogo(dest, sb);
		Rectangle rectangle = new Rectangle(bounds.X, dest.Y + dest.Height, bounds.Width, 60);
		sb.Draw(scanlinesTextBackground, rectangle, Utils.AddativeWhite * 0.2f);
		for (int i = 0; i < Stars.Count; i++)
		{
			DrawStar(rectangle, sb, Stars[i]);
		}
		rectangle.X += 100;
		rectangle.Width = bounds.Width - 200;
		rectangle.Y += 13;
		rectangle.Height = 35;
		if (Button.doButton(98373721, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, "GO!", Utils.AddativeWhite))
		{
			activeState = null;
			string text = os.defaultUser.name;
			text = text.Replace(" ", "_");
			for (int i = 0; i < savesFolder.files.Count; i++)
			{
				if (savesFolder.files[i].name.StartsWith(text))
				{
					userFilePath = savesFolder.files[i].name;
					activeState = PointClickerGameState.LoadFromString(savesFolder.files[i].data);
					break;
				}
			}
			if (activeState == null)
			{
				activeState = new PointClickerGameState(upgradeNames.Count);
				FileEntry fileEntry = new FileEntry(activeState.ToSaveString(), text + ".pcsav");
				savesFolder.files.Add(fileEntry);
				userFilePath = fileEntry.name;
			}
			state = PointClickerScreenState.Main;
			currentRate = 0f;
			ActiveStory = "";
			UpdateRate();
			UpdateStory();
			UpdatePoints();
		}
		if (Button.doButton(98373732, bounds.X + 2, bounds.Y + bounds.Height - 19, 180, 18, LocaleTerms.Loc("Exit") + "  :<", os.lockedColor))
		{
			os.display.command = "connect";
		}
	}
}
