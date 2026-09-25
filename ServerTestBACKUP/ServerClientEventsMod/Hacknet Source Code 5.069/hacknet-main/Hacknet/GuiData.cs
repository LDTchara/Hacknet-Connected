using System.Collections.Generic;
using System.Text;
using Hacknet.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hacknet;

public static class GuiData
{
	public struct FontCongifOption
	{
		public SpriteFont smallFont;

		public SpriteFont tinyFont;

		public SpriteFont bigFont;

		public SpriteFont detailFont;

		public string name;

		public float tinyFontCharHeight;
	}

	public static Vector2 temp = default(Vector2);

	private static Point tmpPoint = default(Point);

	public static Color tmpColor = default(Color);

	public static Rectangle tmpRect = default(Rectangle);

	public static MouseState lastMouse;

	public static MouseState mouse;

	public static InputState lastInput;

	public static Color Default_Selected_Color = new Color(0, 166, 235);

	public static Color Default_Unselected_Color = new Color(255, 128, 0);

	public static Color Default_Backing_Color = new Color(30, 30, 50, 100);

	public static Color Default_Light_Backing_Color = new Color(80, 80, 100, 255);

	public static Color Default_Lit_Backing_Color = new Color(255, 199, 41, 100);

	public static Color Default_Dark_Neutral_Color = new Color(10, 10, 15, 200);

	public static Color Default_Dark_Background_Color = new Color(40, 40, 45, 180);

	public static Color Default_Trans_Grey = new Color(30, 30, 30, 100);

	public static Color Default_Trans_Grey_Bright = new Color(60, 60, 60, 100);

	public static Color Default_Trans_Grey_Dark = new Color(20, 20, 20, 200);

	public static Color Default_Trans_Grey_Strong = new Color(80, 80, 80, 100);

	public static Color Default_Trans_Grey_Solid = new Color(100, 100, 100, 255);

	public static int lastMouseWheelPos = -1;

	private static int lastMouseScroll = 0;

	public static int hot = -1;

	public static int active = -1;

	public static int enganged = -1;

	public static SpriteBatch spriteBatch;

	public static SpriteFont font;

	public static SpriteFont titlefont;

	public static SpriteFont smallfont;

	public static SpriteFont tinyfont;

	public static SpriteFont UITinyfont;

	public static SpriteFont UISmallfont;

	public static SpriteFont detailfont;

	public static bool blockingInput = false;

	public static bool blockingTextInput = false;

	public static bool willBlockTextInput = false;

	public static Vector2 scrollOffset = Vector2.Zero;

	public static float lastTimeStep = 0.016f;

	private static bool initialized = false;

	private static TextInputHook TextInputHook;

	public static List<FontCongifOption> FontConfigs = new List<FontCongifOption>();

	public static Dictionary<string, List<FontCongifOption>> LocaleFontConfigs = new Dictionary<string, List<FontCongifOption>>();

	public static FontCongifOption ActiveFontConfig = default(FontCongifOption);

	public static void InitFontOptions(ContentManager content)
	{
		string name = ActiveFontConfig.name;
		FontConfigs.Clear();
		FontConfigs.Add(new FontCongifOption
		{
			name = "default",
			smallFont = content.Load<SpriteFont>("Font12"),
			tinyFont = content.Load<SpriteFont>("Font10"),
			bigFont = content.Load<SpriteFont>("Font23"),
			tinyFontCharHeight = 10f
		});
		if (string.IsNullOrEmpty(name))
		{
			ActiveFontConfig = FontConfigs[0];
		}
		FontConfigs.Add(new FontCongifOption
		{
			name = "medium",
			smallFont = content.Load<SpriteFont>("Font14"),
			tinyFont = content.Load<SpriteFont>("Font12"),
			bigFont = content.Load<SpriteFont>("Font23"),
			tinyFontCharHeight = 14f
		});
		FontConfigs.Add(new FontCongifOption
		{
			name = "large",
			smallFont = content.Load<SpriteFont>("Font16"),
			tinyFont = content.Load<SpriteFont>("Font14"),
			bigFont = content.Load<SpriteFont>("Font23"),
			tinyFontCharHeight = 16f
		});
		bool flag = false;
		for (int i = 0; i < FontConfigs.Count; i++)
		{
			if (FontConfigs[i].name == name)
			{
				ActivateFontConfig(FontConfigs[i]);
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			ActivateFontConfig(FontConfigs[0]);
		}
		if (!LocaleFontConfigs.ContainsKey("en-us"))
		{
			LocaleFontConfigs.Add("en-us", FontConfigs);
		}
	}

	public static void ActivateFontConfig(string configName)
	{
		List<FontCongifOption> list = FontConfigs;
		if (LocaleFontConfigs.ContainsKey(Settings.ActiveLocale))
		{
			list = LocaleFontConfigs[Settings.ActiveLocale];
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].name == configName)
			{
				ActivateFontConfig(list[i]);
				break;
			}
		}
	}

	public static void ActivateFontConfig(FontCongifOption config)
	{
		if (config.detailFont != null)
		{
			detailfont = config.detailFont;
		}
		smallfont = config.smallFont;
		tinyfont = config.tinyFont;
		font = config.bigFont;
		ActiveFontConfig = config;
	}

	public static void init(GameWindow window)
	{
		if (!initialized)
		{
			TextInputHook = new TextInputHook(window.Handle);
			initialized = true;
		}
	}

	public static void doInput()
	{
		lastMouse = mouse;
		mouse = Mouse.GetState();
		if (lastMouseWheelPos == -1)
		{
			lastMouseWheelPos = mouse.ScrollWheelValue;
		}
		lastMouseScroll = lastMouseWheelPos - mouse.ScrollWheelValue;
		lastMouseWheelPos = mouse.ScrollWheelValue;
		blockingInput = false;
		blockingTextInput = willBlockTextInput;
		willBlockTextInput = false;
	}

	public static void doInput(InputState input)
	{
		doInput();
		lastInput = input;
	}

	public static void setTimeStep(float t)
	{
		lastTimeStep = t;
	}

	public static KeyboardState getKeyboadState()
	{
		return lastInput.CurrentKeyboardStates[0];
	}

	public static KeyboardState getLastKeyboadState()
	{
		return lastInput.LastKeyboardStates[0];
	}

	public static Vector2 getMousePos()
	{
		temp.X = (float)mouse.X - scrollOffset.X;
		temp.Y = (float)mouse.Y - scrollOffset.Y;
		return temp;
	}

	public static Point getMousePoint()
	{
		tmpPoint.X = mouse.X - (int)scrollOffset.X;
		tmpPoint.Y = mouse.Y - (int)scrollOffset.Y;
		return tmpPoint;
	}

	public static float getMouseWheelScroll()
	{
		return lastMouseScroll / 120;
	}

	public static bool isMouseLeftDown()
	{
		return mouse.LeftButton == ButtonState.Pressed;
	}

	public static bool mouseLeftUp()
	{
		if (lastMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released)
		{
			return true;
		}
		return false;
	}

	public static bool mouseWasPressed()
	{
		if (lastMouse.LeftButton == ButtonState.Released && mouse.LeftButton == ButtonState.Pressed)
		{
			return true;
		}
		return false;
	}

	public static void startDraw()
	{
		spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
	}

	public static void endDraw()
	{
		spriteBatch.End();
	}

	public static char[] getFilteredKeys()
	{
		string buffer = TextInputHook.Buffer;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < buffer.Length; i++)
		{
			if (buffer[i] >= ' ' && buffer[i] <= '~')
			{
				stringBuilder.Append(buffer[i]);
			}
		}
		TextInputHook.clearBuffer();
		buffer = stringBuilder.ToString();
		char[] array = new char[buffer.Length];
		for (int i = 0; i < buffer.Length; i++)
		{
			array[i] = buffer[i];
		}
		return array;
	}
}
