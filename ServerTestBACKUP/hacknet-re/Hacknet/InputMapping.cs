using Microsoft.Xna.Framework.Input;

namespace Hacknet;

public class InputMapping
{
	private static InputStates ret;

	private static InputMap map;

	private static InputStates lastCalculatedState;

	public static InputStates getStatesFromKeys(KeyboardState keys, GamePadState pad, GamePadThumbSticks sticks)
	{
		_ = ret;
		bool flag = 0 == 0;
		if (keys.IsKeyDown(Keys.Right) || pad.DPad.Right == ButtonState.Pressed)
		{
			ret.movement = 1f;
		}
		else if (keys.IsKeyDown(Keys.Left) || pad.DPad.Left == ButtonState.Pressed)
		{
			ret.movement = -1f;
		}
		else if (sticks.Left.X != 0f)
		{
			ret.movement = sticks.Left.X;
		}
		else
		{
			ret.movement = 0f;
		}
		if (pad.Buttons.A == ButtonState.Pressed || keys.IsKeyDown(Keys.Up))
		{
			ret.jumping = true;
		}
		else
		{
			ret.jumping = false;
		}
		if (pad.Buttons.B == ButtonState.Pressed || keys.IsKeyDown(Keys.RightShift))
		{
			ret.useItem = true;
		}
		else
		{
			ret.useItem = false;
		}
		if (keys.IsKeyDown(Keys.D) || pad.DPad.Right == ButtonState.Pressed)
		{
			ret.wmovement = 1f;
		}
		else if (keys.IsKeyDown(Keys.A) || pad.DPad.Left == ButtonState.Pressed)
		{
			ret.wmovement = -1f;
		}
		else if (sticks.Left.X != 0f)
		{
			ret.wmovement = sticks.Left.X;
		}
		else
		{
			ret.wmovement = 0f;
		}
		if (pad.Buttons.A == ButtonState.Pressed || keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Space) || pad.Triggers.Left > 0f)
		{
			ret.wjumping = true;
		}
		else
		{
			ret.wjumping = false;
		}
		if (pad.Buttons.B == ButtonState.Pressed || keys.IsKeyDown(Keys.LeftShift))
		{
			ret.wuseItem = true;
		}
		else
		{
			ret.wuseItem = false;
		}
		lastCalculatedState = ret;
		return ret;
	}

	public static InputMap getMapFromKeys(KeyboardState keys, GamePadState pad)
	{
		map.last = lastCalculatedState;
		map.now = getStatesFromKeys(keys, pad, pad.ThumbSticks);
		return map;
	}
}
