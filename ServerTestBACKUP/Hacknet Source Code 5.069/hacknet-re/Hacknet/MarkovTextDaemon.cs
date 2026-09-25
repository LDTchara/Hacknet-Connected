using System;
using System.Collections.Generic;
using Hacknet.DLC.MarkovTextGenerator;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class MarkovTextDaemon : Daemon
{
	private Corpus corpus;

	private bool CorpusBeingLoaded = false;

	private bool SentenceBeingGenerated = false;

	private bool LastSentenceWasError = false;

	public string corpusFolderPath;

	private float loadProgress = 0f;

	private int DisplayWidth = 300;

	private List<string> GeneratedSentences = new List<string>();

	private ScrollableTextRegion TextDisplay;

	public MarkovTextDaemon(Computer c, OS os, string name, string corpusLoadPath)
		: base(c, name, os)
	{
		corpusFolderPath = corpusLoadPath;
		TextDisplay = new ScrollableTextRegion(GuiData.spriteBatch.GraphicsDevice);
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		GeneratedSentences.Clear();
		TextDisplay.UpdateScroll(0f);
		if (string.IsNullOrWhiteSpace(corpusFolderPath))
		{
			throw new NullReferenceException("No corpus folder path found");
		}
		CorpusBeingLoaded = true;
		loadProgress = 0f;
		string foldername = Utils.GetFileLoadPrefix() + corpusFolderPath;
		CorpusGenerator.GenerateCorpusFromFolderThreaded(foldername, CorpusLoaded, LoadProgressUpdated);
	}

	private void CorpusLoaded(Corpus c)
	{
		CorpusBeingLoaded = false;
		if (c != null)
		{
			loadProgress = 1f;
			corpus = c;
		}
	}

	private void LoadProgressUpdated(float progress, string progressMessage)
	{
		loadProgress = progress;
	}

	private void AddNewSentence(string sentence)
	{
		if (!string.IsNullOrWhiteSpace(sentence))
		{
			string text = Utils.SuperSmartTwimForWidth(sentence, DisplayWidth, GuiData.smallfont);
			string[] collection = text.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
			GeneratedSentences.AddRange(collection);
			SentenceBeingGenerated = false;
			LastSentenceWasError = false;
		}
		else
		{
			LastSentenceWasError = true;
		}
	}

	private void PrepateToClearCorpusOnNavigateAway()
	{
		os.delayer.Post(ActionDelayer.Wait(os.lastGameTime.ElapsedGameTime.TotalSeconds * 1.999), delegate
		{
			if (os.display.command != name)
			{
				corpus = null;
			}
		});
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		PrepateToClearCorpusOnNavigateAway();
		Rectangle dest = Utils.InsetRectangle(bounds, 2);
		DisplayWidth = dest.Width;
		if (corpus == null && CorpusBeingLoaded)
		{
			DrawLoading(dest, sb);
			return;
		}
		if (Button.doButton(832192223, bounds.X + 20, bounds.Y + 20, bounds.Width / 3, 20, "Generate", null) && !SentenceBeingGenerated)
		{
			SentenceBeingGenerated = true;
			corpus.GenerateSentenceThreaded(AddNewSentence);
		}
		dest.Y += 40;
		dest.Height -= 40;
		TextDisplay.Draw(dest, GeneratedSentences, sb, Color.White);
	}

	private void DrawLoading(Rectangle dest, SpriteBatch sb)
	{
		TextItem.doCenteredFontLabel(dest, "Loading...", GuiData.font, Color.White);
		Rectangle rectangle = new Rectangle(dest.X, dest.Y + 30, dest.Width, dest.Height);
		TextItem.doCenteredFontLabel(dest, loadProgress.ToString("00.00") + "%", GuiData.font, Color.White);
	}

	public override string getSaveString()
	{
		return "<MarkovTextDaemon Name=\"" + name + "\" SourceFilesContentFolder=\"" + corpusFolderPath + "\" />";
	}
}
