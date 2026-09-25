using System;
using System.Collections.Generic;

namespace Hacknet;

internal class ActionDelayer
{
	public delegate bool Condition(ActionDelayer messagePump);

	private struct Pair
	{
		public Condition Condition;

		public Action Action;
	}

	private List<Pair> pairs = new List<Pair>();

	private List<Pair> nextPairs = new List<Pair>();

	public DateTime Time { get; private set; }

	public void Pump()
	{
		Time = DateTime.Now;
		pairs.AddRange(nextPairs);
		nextPairs.Clear();
		for (int i = 0; i < pairs.Count; i++)
		{
			Pair pair = pairs[i];
			if (pair.Condition(this))
			{
				pair.Action();
				pairs.RemoveAt(i--);
			}
		}
	}

	public void RunAllDelayedActions()
	{
		pairs.AddRange(nextPairs);
		nextPairs.Clear();
		Time = DateTime.MaxValue;
		for (int i = 0; i < pairs.Count; i++)
		{
			Pair pair = pairs[i];
			if (pair.Condition(this))
			{
				pair.Action();
				pairs.RemoveAt(i--);
			}
		}
	}

	public void Post(Condition condition, Action action)
	{
		nextPairs.Add(new Pair
		{
			Condition = condition,
			Action = action
		});
	}

	public void PostAnimation(IEnumerator<Condition> animation)
	{
		Action tick = null;
		tick = delegate
		{
			if (animation.MoveNext())
			{
				Post(animation.Current, tick);
			}
		};
		tick();
	}

	public static Condition WaitUntil(DateTime time)
	{
		return (ActionDelayer x) => x.Time >= time;
	}

	public static Condition Wait(double time)
	{
		return WaitUntil(DateTime.Now + TimeSpan.FromSeconds(time));
	}

	public static Condition NextTick()
	{
		return (ActionDelayer x) => true;
	}

	public static Condition FileDeleted(Folder f, string filename)
	{
		return (ActionDelayer x) => !f.containsFile(filename);
	}
}
