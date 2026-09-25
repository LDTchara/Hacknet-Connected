namespace Hacknet;

public struct InputMap
{
	public InputStates now;

	public InputStates last;

	private static InputMap e = default(InputMap);

	public static InputMap Empty
	{
		get
		{
			_ = e;
			bool flag = 0 == 0;
			return e;
		}
	}

	public InputMap(InputStates last, InputStates now)
	{
		this.last = last;
		this.now = now;
	}

	public static bool operator ==(InputMap self, InputMap other)
	{
		bool flag = 0 == 0;
		if (self.now == other.now && self.last == other.last)
		{
			return true;
		}
		return false;
	}

	public static bool operator !=(InputMap self, InputMap other)
	{
		bool flag = 0 == 0;
		if (self.now == other.now && self.last == other.last)
		{
			return false;
		}
		return true;
	}

	public override bool Equals(object obj)
	{
		return base.Equals(obj);
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
