using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class LogoDaemon : Daemon
{
	private const string FileName = "DisplayText.txt";

	private string LogoImagePath;

	private bool showsTitle;

	public string BodyText;

	public Color TextColor = Color.White;

	private Texture2D LoadedLogo;

	private bool hasTriedToLoadLogo = false;

	private TrailLoadingSpinnerEffect spinner;

	private float timeOnPage = 0f;

	public LogoDaemon(Computer c, OS os, string name, bool showsTitle, string LogoImagePath)
		: base(c, name, os)
	{
		isListed = true;
		this.showsTitle = showsTitle;
		this.LogoImagePath = LogoImagePath;
		base.name = name;
		spinner = new TrailLoadingSpinnerEffect(os);
	}

	public override void initFiles()
	{
		base.initFiles();
		if (!string.IsNullOrWhiteSpace(BodyText))
		{
			comp.files.root.searchForFolder("sys").files.Add(new FileEntry(BodyText, "DisplayText.txt"));
		}
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		timeOnPage = 0f;
		if (LoadedLogo == null && !hasTriedToLoadLogo && !string.IsNullOrWhiteSpace(LogoImagePath))
		{
			Utils.LoadImageFromContentOrExtension(LogoImagePath, os.content, delegate(Texture2D loadedTex)
			{
				hasTriedToLoadLogo = true;
				LoadedLogo = loadedTex;
			});
		}
		Folder folder = comp.files.root.searchForFolder("sys");
		FileEntry fileEntry = folder.searchForFile("DisplayText.txt");
		if (fileEntry != null)
		{
			BodyText = fileEntry.data;
		}
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		bounds = Utils.InsetRectangle(bounds, 1);
		sb.Draw(Utils.white, bounds, Color.Black * 0.4f);
		Rectangle dest = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height / 8);
		Rectangle rectangle = new Rectangle(bounds.X, bounds.Y + dest.Height, bounds.Width, bounds.Height / 8 * 7);
		if (!showsTitle)
		{
			rectangle.Y -= dest.Height;
			rectangle.Height += dest.Height;
			rectangle.Y += 6;
			rectangle.Height -= 6;
		}
		int num = 40;
		rectangle.Height -= num;
		if (!string.IsNullOrWhiteSpace(BodyText))
		{
			string[] array = BodyText.Trim().Split(Utils.robustNewlineDelim, StringSplitOptions.None);
			int num2 = (int)GuiData.smallfont.MeasureString("W").Y;
			num2 += 2;
			int num3 = 6;
			int num4 = num2 * array.Length + num3 * 2;
			rectangle.Height -= num4;
			int num5 = bounds.Y + bounds.Height - num3 - num2 + 1 - num;
			for (int num6 = array.Length - 1; num6 >= 0; num6--)
			{
				Rectangle dest2 = new Rectangle(bounds.X, num5 + 1, bounds.Width, num2 - 1);
				TextItem.doCenteredFontLabel(dest2, array[num6], GuiData.smallfont, TextColor);
				num5 -= num2;
			}
		}
		if (LoadedLogo == null)
		{
			timeOnPage += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
			spinner.Draw(rectangle, sb, 1f, 1f - timeOnPage * 0.4f, 0f, TextColor);
		}
		else
		{
			Utils.DrawSpriteAspectCorrect(rectangle, sb, LoadedLogo, Color.White);
		}
		if (showsTitle)
		{
			TextItem.doCenteredFontLabel(dest, name, GuiData.font, TextColor);
		}
		int num7 = bounds.Width / 2;
		int num8 = 26;
		if (Button.doButton(22928201, bounds.X + (bounds.Width - num7) / 2, bounds.Y + bounds.Height - (num - num8) / 2 - num8, num7, num8, LocaleTerms.Loc("Proceed"), Color.Black))
		{
			os.display.command = "connect";
		}
	}

	public override string getSaveString()
	{
		return string.Format("<LogoDaemon LogoImagePath=\"{0}\" ShowsTitle=\"{1}\" TextColor=\"{2}\" Name=\"{3}\" />", LogoImagePath, showsTitle ? "true" : "false", Utils.convertColorToParseableString(TextColor), name);
	}
}
