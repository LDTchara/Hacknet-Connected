using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hacknet;

public class InputState
{
	public const int MaxInputs = 4;

	public KeyboardState[] CurrentKeyboardStates;

	public GamePadState[] CurrentGamePadStates;

	public KeyboardState[] LastKeyboardStates;

	public GamePadState[] LastGamePadStates;

	public readonly bool[] GamePadWasConnected;

	public static InputState Empty = new InputState();

	public InputState()
	{
		CurrentKeyboardStates = new KeyboardState[4];
		CurrentGamePadStates = new GamePadState[4];
		LastKeyboardStates = new KeyboardState[4];
		LastGamePadStates = new GamePadState[4];
		GamePadWasConnected = new bool[4];
	}

	public void Update()
	{
		for (int i = 0; i < 4; i++)
		{
			ref KeyboardState reference = ref LastKeyboardStates[i];
			reference = CurrentKeyboardStates[i];
			ref GamePadState reference2 = ref LastGamePadStates[i];
			reference2 = CurrentGamePadStates[i];
			ref KeyboardState reference3 = ref CurrentKeyboardStates[i];
			reference3 = Keyboard.GetState((PlayerIndex)i);
			ref GamePadState reference4 = ref CurrentGamePadStates[i];
			reference4 = GamePad.GetState((PlayerIndex)i);
			if (CurrentGamePadStates[i].IsConnected)
			{
				GamePadWasConnected[i] = true;
			}
		}
	}

	public bool IsNewKeyPress(Keys key, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
	{
		if (controllingPlayer.HasValue)
		{
			playerIndex = controllingPlayer.Value;
			int num = (int)playerIndex;
			return CurrentKeyboardStates[num].IsKeyDown(key) && LastKeyboardStates[num].IsKeyUp(key);
		}
		return IsNewKeyPress(key, PlayerIndex.One, out playerIndex) || IsNewKeyPress(key, PlayerIndex.Two, out playerIndex) || IsNewKeyPress(key, PlayerIndex.Three, out playerIndex) || IsNewKeyPress(key, PlayerIndex.Four, out playerIndex);
	}

	public bool IsNewButtonPress(Buttons button, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
	{
		if (controllingPlayer.HasValue)
		{
			playerIndex = controllingPlayer.Value;
			int num = (int)playerIndex;
			return CurrentGamePadStates[num].IsButtonDown(button) && LastGamePadStates[num].IsButtonUp(button);
		}
		return IsNewButtonPress(button, PlayerIndex.One, out playerIndex) || IsNewButtonPress(button, PlayerIndex.Two, out playerIndex) || IsNewButtonPress(button, PlayerIndex.Three, out playerIndex) || IsNewButtonPress(button, PlayerIndex.Four, out playerIndex);
	}

	public bool IsMenuSelect(PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
	{
		return IsNewKeyPress(Keys.Space, controllingPlayer, out playerIndex) || IsNewKeyPress(Keys.Enter, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.A, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.Start, controllingPlayer, out playerIndex);
	}

	public bool IsMenuCancel(PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
	{
		return IsNewKeyPress(Keys.Escape, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.B, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.Back, controllingPlayer, out playerIndex);
	}

	public bool IsMenuUp(PlayerIndex? controllingPlayer)
	{
		PlayerIndex playerIndex;
		return IsNewKeyPress(Keys.Up, controllingPlayer, out playerIndex) || IsNewKeyPress(Keys.W, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.DPadUp, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.LeftThumbstickUp, controllingPlayer, out playerIndex);
	}

	public bool IsMenuDown(PlayerIndex? controllingPlayer)
	{
		PlayerIndex playerIndex;
		return IsNewKeyPress(Keys.Down, controllingPlayer, out playerIndex) || IsNewKeyPress(Keys.D, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.DPadDown, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.LeftThumbstickDown, controllingPlayer, out playerIndex);
	}

	public bool IsPauseGame(PlayerIndex? controllingPlayer)
	{
		PlayerIndex playerIndex;
		return IsNewKeyPress(Keys.Escape, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.Back, controllingPlayer, out playerIndex) || IsNewButtonPress(Buttons.Start, controllingPlayer, out playerIndex);
	}
}
