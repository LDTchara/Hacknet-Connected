using System;
using System.Collections.Generic;

namespace Hacknet.UIUtils;

public class LCG
{
	private int _state;

	public bool Microsoft { get; set; }

	public bool BSD
	{
		get
		{
			return !Microsoft;
		}
		set
		{
			Microsoft = !value;
		}
	}

	public LCG(bool microsoft = true)
	{
		_state = (int)DateTime.Now.Ticks;
		Microsoft = microsoft;
	}

	public LCG(int n, bool microsoft = true)
	{
		_state = n;
		Microsoft = microsoft;
	}

	public void reSeed(int seed)
	{
		_state = seed;
	}

	public int Next()
	{
		if (BSD)
		{
			return _state = (1103515245 * _state + 12345) & 0x7FFFFFFF;
		}
		return ((_state = 214013 * _state + 2531011) & 0x7FFFFFFF) >> 16;
	}

	public float NextFloat()
	{
		return (float)((double)Next() / 2147483647.0);
	}

	public float NextFloatScaled()
	{
		return (float)((double)Next() / 32767.0);
	}

	public bool Flip()
	{
		if (BSD)
		{
			return (_state = (1103515245 * _state + 12345) & 0x7FFFFFFF) > 1073741823;
		}
		return ((_state = 214013 * _state + 2531011) & 0x7FFFFFFF) >> 16 > 1073741823;
	}

	public IEnumerable<int> Seq()
	{
		while (true)
		{
			yield return Next();
		}
	}
}
