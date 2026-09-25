using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class TuneswapExe : ExeModule
{
	private RaindropsEffect backdrop;

	private Color themeColor = Color.Pink;

	private string oldPlayingSong = null;

	private List<string> SongOptions = new List<string>(new string[8] { "DLC\\Music\\snidelyWhiplash", "DLC\\Music\\Userspacelike", "DLC\\Music\\Slow_Motion", "DLC\\Music\\World_Chase", "DLC\\Music\\HOME_Resonance", "DLC\\Music\\Remi2", "DLC\\Music\\Remi_Finale", "DLC\\Music\\DreamHead" });

	private List<string> SongNames = new List<string>(new string[8] { "Snidely Whiplash", "Payload (AKA Userspacelike)", "Slow Motion", "World Chase", "Resonance", "ClearText", "Sabotage (AKA Altitude_Loss)", "Dream Head" });

	private List<string> ArtistNames = new List<string>(new string[8] { "OGRE", "The Algorithm", "Tonspender", "Cinematrik", "HOME", "The Algorithm", "The Algorithm", "HOME" });

	private string mousedOverArtistName = null;

	public TuneswapExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		needsProxyAccess = false;
		name = "Tuneswap";
		ramCost = 300;
		IdentifierName = "Tuneswap";
		backdrop = new RaindropsEffect();
		backdrop.Init(os.content);
		backdrop.MaxVerticalLandingVariane = 0.06f;
		backdrop.FallRate = 0.2f;
		themeColor = new Color(38, 171, 146, 0) * 0.4f;
	}

	public override void Update(float t)
	{
		base.Update(t);
		backdrop.Update(t, 50f);
	}

	public override void Completed()
	{
		base.Completed();
	}

	private bool CanActivateSong(int i)
	{
		return MusicManager.currentSongName != SongOptions[i];
	}

	private void ActivateSong(string song)
	{
		if (!SongOptions.Contains(MusicManager.currentSongName))
		{
			oldPlayingSong = MusicManager.currentSongName;
		}
		try
		{
			MusicManager.playSongImmediatley(song);
		}
		catch (Exception)
		{
			os.write("Tuneswap.exe :: ERROR PLAYING SONG " + song + "\n -- EXITING\n");
			isExiting = true;
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		Rectangle contentAreaDest = GetContentAreaDest();
		Rectangle dest = contentAreaDest;
		dest.Height = (int)((float)dest.Height + 20f);
		backdrop.Render(dest, spriteBatch, themeColor, 5f, 30f);
		int num = contentAreaDest.X + 10;
		int width = contentAreaDest.Width - 20;
		int num2 = contentAreaDest.Y + 10;
		if (isExiting)
		{
			return;
		}
		mousedOverArtistName = null;
		for (int i = 0; i < SongOptions.Count; i++)
		{
			Rectangle destinationRectangle = new Rectangle(num, num2, width, 20);
			spriteBatch.Draw(Utils.white, destinationRectangle, Color.Black * 0.85f);
			int num3 = 10777001 + PID * 1000 + i;
			if (Button.doButton(num3, num, num2, width, 20, SongNames[i], CanActivateSong(i) ? themeColor : Color.Gray))
			{
				ActivateSong(SongOptions[i]);
			}
			if (GuiData.hot == num3)
			{
				mousedOverArtistName = ArtistNames[i];
			}
			num2 += 23;
		}
		if (oldPlayingSong != null)
		{
			if (Button.doButton(10797009 + PID * 1000, num, num2, width, 20, LocaleTerms.Loc("Default") + " Track", themeColor))
			{
				ActivateSong(oldPlayingSong);
			}
			num2 += 23;
		}
		if (mousedOverArtistName != null)
		{
			spriteBatch.DrawString(GuiData.smallfont, mousedOverArtistName, new Vector2(num, num2), Color.White);
		}
		Rectangle destinationRectangle2 = new Rectangle(num, contentAreaDest.Y + contentAreaDest.Height - 24, width, 20);
		spriteBatch.Draw(Utils.white, destinationRectangle2, Color.Black * 0.6f);
		if (Button.doButton(109271000 + PID, num, contentAreaDest.Y + contentAreaDest.Height - 24, width, 20, LocaleTerms.Loc("Exit"), os.lockedColor))
		{
			isExiting = true;
		}
	}

	public void RenderMainDisplay(Rectangle dest, SpriteBatch sb)
	{
	}
}
