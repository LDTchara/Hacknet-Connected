using System;
using Microsoft.Xna.Framework.Input;
using SDL2;

namespace Hacknet.Input;

public class TextInputHook : IDisposable
{
	private string buffer = "";

	private bool backSpace = false;

	public string Buffer => buffer;

	public bool BackSpace
	{
		get
		{
			bool result = backSpace;
			backSpace = false;
			return result;
		}
	}

	public void clearBuffer()
	{
		buffer = "";
	}

	public TextInputHook(IntPtr whnd)
	{
		TextInputEXT.TextInput += OnTextInput;
		SDL.SDL_StartTextInput();
	}

	public void Dispose()
	{
		SDL.SDL_StopTextInput();
		TextInputEXT.TextInput -= OnTextInput;
	}

	private void OnTextInput(char c)
	{
		switch (c)
		{
		case '\b':
			backSpace = true;
			break;
		case '\u0016':
			buffer += SDL.SDL_GetClipboardText();
			break;
		default:
			buffer += c;
			break;
		}
	}
}
