namespace Hacknet;

public struct InputStates
{
	public float movement;

	public bool jumping;

	public bool useItem;

	public float wmovement;

	public bool wjumping;

	public bool wuseItem;

	public static bool operator ==(InputStates self, InputStates other)
	{
		if (self.movement == other.movement && self.jumping == other.jumping && self.useItem == other.useItem && self.wmovement == other.wmovement && self.wjumping == other.wjumping && self.wuseItem == other.wuseItem)
		{
			return true;
		}
		return false;
	}

	public static bool operator !=(InputStates self, InputStates other)
	{
		if (self.movement == other.movement && self.jumping == other.jumping && self.useItem == other.useItem && self.wmovement == other.wmovement && self.wjumping == other.wjumping && self.wuseItem == other.wuseItem)
		{
			return false;
		}
		return true;
	}

	public override bool Equals(object obj)
	{
		if (obj is InputStates)
		{
			if ((InputStates)obj == this)
			{
				return true;
			}
			return false;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
