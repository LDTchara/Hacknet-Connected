using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Hacknet.Effects;

public class AudioVisualizer
{
	private VisualizationData visData = new VisualizationData();

	private List<ReadOnlyCollection<float>> samplesHistory;

	private ReadOnlyCollection<float> previousSamples = null;

	private double SecondsSinceLastDataUpdate = 0.0;

	public void Draw(Rectangle bounds, SpriteBatch sb)
	{
		if (OS.currentInstance == null || !SettingsLoader.ShouldDrawMusicVis)
		{
			return;
		}
		if (samplesHistory == null)
		{
			samplesHistory = new List<ReadOnlyCollection<float>>();
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
			samplesHistory.Add(new ReadOnlyCollection<float>(new float[0]));
		}
		if (MediaPlayer.State == MediaState.Playing && OS.currentInstance != null && OS.currentInstance.lastGameTime != null)
		{
			SecondsSinceLastDataUpdate += OS.currentInstance.lastGameTime.ElapsedGameTime.TotalSeconds;
			if (SecondsSinceLastDataUpdate >= 1.0 / 24.0)
			{
				MediaPlayer.IsVisualizationEnabled = true;
				try
				{
					MediaPlayer.GetVisualizationData(visData);
					SecondsSinceLastDataUpdate = 0.0;
				}
				catch (OverflowException)
				{
				}
				catch (NullReferenceException)
				{
				}
				catch (IndexOutOfRangeException)
				{
				}
				catch (Exception)
				{
				}
			}
		}
		if (visData == null)
		{
			return;
		}
		List<float> list = new List<float>(visData.Samples.Count);
		for (int i = 0; i < visData.Samples.Count; i++)
		{
			float num = Math.Max(0f, Math.Abs(visData.Samples[i]));
			if (num > 1f)
			{
				num = 0f;
			}
			list.Add(num);
		}
		ReadOnlyCollection<float> readOnlyCollection = new ReadOnlyCollection<float>(list);
		if (previousSamples == null)
		{
			previousSamples = visData.Samples;
		}
		float num2 = (float)bounds.Height / (float)readOnlyCollection.Count;
		Vector2 vector = new Vector2(bounds.X, bounds.Y);
		float num3 = 0.2f;
		for (int i = 0; i < readOnlyCollection.Count; i++)
		{
			int index = i;
			float num4 = Math.Abs(readOnlyCollection[index]);
			sb.Draw(Utils.white, new Rectangle((int)vector.X, (int)vector.Y, (int)((float)bounds.Width * num4), Math.Max(1, (int)num2)), OS.currentInstance.highlightColor * (0.2f + num4 / 2f) * num3);
			vector.Y += num2;
		}
		samplesHistory.Add(new ReadOnlyCollection<float>(visData.Samples.ToArray()));
		samplesHistory.RemoveAt(0);
		for (int j = 0; j < samplesHistory.Count; j++)
		{
			float num5 = (float)j / (float)(samplesHistory.Count - 1);
			vector.Y = bounds.Y;
			for (int i = 0; i < samplesHistory[j].Count; i++)
			{
				Color color = ((j >= samplesHistory.Count - 1) ? (Utils.AddativeWhite * 0.7f) : OS.currentInstance.highlightColor);
				sb.Draw(Utils.white, new Vector2((float)bounds.X + (samplesHistory[j][i] * ((float)bounds.Width / 4f) + (float)bounds.Width * 0.75f) + (float)(j * 2) - (float)samplesHistory.Count, vector.Y), null, color * 0.6f * (0.01f + num5) * 0.4f);
				vector.Y += num2;
			}
		}
	}
}
