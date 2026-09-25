using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Hacknet;

public static class MusicManager
{
	private const int PLAYING = 0;

	private const int FADING_OUT = 1;

	private const int FADING_IN = 2;

	private const int STOPPED = 3;

	private static float DEFAULT_FADE_TIME = (Settings.isConventionDemo ? 0.5f : 2f);

	public static float FADE_TIME = DEFAULT_FADE_TIME;

	public static Song curentSong;

	private static Song nextSong;

	private static string nextSongName;

	public static bool isPlaying;

	public static bool isMuted;

	public static string currentSongName;

	private static float destinationVolume;

	private static float fadeVolume;

	private static float fadeTimer = 0f;

	private static int state;

	private static ContentManager contentManager;

	private static bool initialized = false;

	public static bool dataLoadedFromOutsideFile = false;

	private static Dictionary<string, Song> loadedSongs = new Dictionary<string, Song>();

	private static bool IsMediaPlayerCrashDisabled = false;

	public static void init(ContentManager content)
	{
		try
		{
			contentManager = content;
			currentSongName = "Music\\Revolve";
			curentSong = content.Load<Song>(currentSongName);
			isPlaying = false;
			if (!dataLoadedFromOutsideFile)
			{
				isMuted = false;
				MediaPlayer.Volume = 0.3f;
			}
			destinationVolume = MediaPlayer.Volume;
			state = 3;
			initialized = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine("Music Error: Could not initialize first song load\r\n" + Utils.GenerateReportFromException(ex));
		}
	}

	public static void playSong()
	{
		try
		{
			if (!isPlaying && initialized)
			{
				if (!Settings.soundDisabled)
				{
					MediaPlayer.Play(curentSong);
					MediaPlayer.IsRepeating = true;
				}
				isPlaying = true;
				state = 0;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error playing " + Utils.GenerateReportFromException(ex));
		}
	}

	public static void toggleMute()
	{
		isMuted = !isMuted;
		MediaPlayer.IsMuted = isMuted;
	}

	public static void setIsMuted(bool muted)
	{
		isMuted = muted;
		MediaPlayer.IsMuted = isMuted;
	}

	public static void stop()
	{
		MediaPlayer.Stop();
		isPlaying = false;
		state = 3;
	}

	public static float getVolume()
	{
		return destinationVolume;
	}

	public static void setVolume(float volume)
	{
		MediaPlayer.Volume = volume;
		destinationVolume = volume;
	}

	public static void playSongImmediatley(string songname)
	{
		if (currentSongName != songname)
		{
			try
			{
				curentSong = contentManager.Load<Song>(songname);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error switching to song: " + songname + "\r\n" + Utils.GenerateReportFromException(ex));
			}
		}
		if (curentSong != null)
		{
			isPlaying = false;
			currentSongName = songname;
			playSong();
			setVolume(destinationVolume);
		}
	}

	public static void loadAsCurrentSong(string songname)
	{
		try
		{
			curentSong = contentManager.Load<Song>(songname);
			nextSongName = songname;
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error switching to song as current: " + songname + "\r\n" + Utils.GenerateReportFromException(ex));
		}
		if (curentSong != null)
		{
			isPlaying = false;
			currentSongName = songname;
		}
	}

	public static void loadAsCurrentSongUnsafe(string songname)
	{
		curentSong = contentManager.Load<Song>(songname);
		if (curentSong != null)
		{
			isPlaying = false;
			currentSongName = songname;
		}
	}

	public static void transitionToSong(string songName)
	{
		try
		{
			if (currentSongName != songName)
			{
				Thread thread = new Thread(loadSong);
				thread.IsBackground = true;
				nextSongName = songName;
				thread.Start();
				Console.WriteLine("Started song loader thread");
				state = 1;
				fadeTimer = FADE_TIME;
				currentSongName = songName;
			}
		}
		catch
		{
			Console.WriteLine("Error transitioning to Song");
		}
	}

	private static void loadSong()
	{
		try
		{
			nextSong = contentManager.Load<Song>(nextSongName);
			if (loadedSongs.ContainsKey(nextSongName))
			{
				nextSong = loadedSongs[nextSongName];
			}
			else
			{
				loadedSongs.Add(nextSongName, nextSong);
			}
		}
		catch (ArgumentException)
		{
		}
		catch (ContentLoadException ex2)
		{
			if (OS.TestingPassOnly)
			{
				throw ex2;
			}
			if (OS.currentInstance != null)
			{
				OS.currentInstance.write(ex2.ToString());
				OS.currentInstance.write(ex2.Message);
			}
		}
	}

	public static void Update(float t)
	{
		switch (state)
		{
		case 0:
			fadeVolume = destinationVolume;
			FADE_TIME = DEFAULT_FADE_TIME;
			break;
		case 1:
			fadeTimer -= t;
			fadeVolume = destinationVolume * (fadeTimer / FADE_TIME);
			if (fadeVolume <= 0f)
			{
				if (nextSong != null)
				{
					state = 2;
					MediaPlayer.Volume = 0f;
					curentSong = nextSong;
					nextSong = null;
					fadeTimer = FADE_TIME;
					if (Settings.soundDisabled || IsMediaPlayerCrashDisabled)
					{
						break;
					}
					try
					{
						MediaPlayer.Play(curentSong);
						break;
					}
					catch (InvalidOperationException)
					{
						IsMediaPlayerCrashDisabled = true;
						if (OS.currentInstance != null && OS.currentInstance.terminal != null)
						{
							OS.currentInstance.write("-------------------------------");
							OS.currentInstance.write("-------------WARNING-----------");
							OS.currentInstance.write("-------------------------------");
							OS.currentInstance.write("HacknetOS VM Audio hook could not be established.");
							OS.currentInstance.write("Music Playback Disabled - Media Player (VM Hook:WindowsMediaPlayer)");
							OS.currentInstance.write("Has been uninstalled or disabled.");
							OS.currentInstance.write("-------------------------------");
							OS.currentInstance.write("-------------WARNING-----------");
							OS.currentInstance.write("-------------------------------");
						}
						break;
					}
				}
				fadeVolume = 0f;
			}
			else
			{
				MediaPlayer.Volume = fadeVolume;
			}
			break;
		case 2:
			fadeTimer -= t;
			fadeVolume = destinationVolume * (1f - fadeTimer / FADE_TIME);
			if (fadeVolume >= destinationVolume)
			{
				MediaPlayer.Volume = destinationVolume;
				state = 0;
			}
			else
			{
				MediaPlayer.Volume = fadeVolume;
			}
			break;
		}
	}
}
