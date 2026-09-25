using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Misc;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;
using SDL2;

namespace Hacknet;

internal static class Utils
{
	public static float PARRALAX_MULTIPLIER = 1f;

	public static float MIN_DIFF_FOR_PARRALAX = 0.1f;

	public static Random random = new Random();

	public static byte[] byteBuffer = new byte[1];

	public static readonly string LevelStateFilename = "LevelState.lst";

	public static Texture2D white;

	public static Texture2D gradient;

	public static Texture2D gradientLeftRight;

	public static AudioEmitter emitter;

	public static Vector3 vec3;

	public static StorageDevice device;

	public static Color col;

	public static Color VeryDarkGray = new Color(22, 22, 22);

	public static Color SlightlyDarkGray = new Color(100, 100, 100);

	public static Color AddativeWhite = new Color(255, 255, 255, 0);

	public static Color AddativeRed = new Color(255, 15, 15, 0);

	private static HSLColor hslColor = new HSLColor(1f, 1f, 1f);

	public static char[] newlineDelim = new char[1] { '\n' };

	public static string[] robustNewlineDelim = new string[2] { "\r\n", "\n" };

	public static char[] spaceDelim = new char[1] { ' ' };

	public static string[] commaDelim = new string[3] { " ,", ", ", "," };

	public static string[] directorySplitterDelim = new string[2] { "/", "\\" };

	public static string[] WhitespaceDelim = new string[3] { "\r\n", "\n", " " };

	public static LCG LCG = new LCG();

	public static Vector2 getParallax(Vector2 objectPosition, Vector2 CameraPosition, float objectDepth, float focusDepth)
	{
		float num;
		if (objectDepth >= focusDepth)
		{
			num = ((objectDepth - focusDepth > 0.1f) ? ((objectDepth - focusDepth) * PARRALAX_MULTIPLIER) : (0f * PARRALAX_MULTIPLIER));
			return new Vector2((CameraPosition.X - objectPosition.X) * num, 0f);
		}
		num = ((objectDepth - focusDepth < -0.05f) ? ((objectDepth - focusDepth) * PARRALAX_MULTIPLIER) : 0f);
		return new Vector2((CameraPosition.X - objectPosition.X) * ((num == 0f) ? 0f : (num - 1f)), 0f);
	}

	public static void drawLine(SpriteBatch spriteBatch, Vector2 vector1, Vector2 vector2, Vector2 OffsetPosition, Color Colour, float Depth)
	{
		Texture2D texture = white;
		float x = Vector2.Distance(vector1, vector2);
		float rotation = (float)Math.Atan2(vector2.Y - vector1.Y, vector2.X - vector1.X);
		spriteBatch.Draw(texture, OffsetPosition + vector1, null, Colour, rotation, Vector2.Zero, new Vector2(x, 1f), SpriteEffects.None, Depth);
	}

	public static void drawLine(SpriteBatch spriteBatch, Vector2 vector1, Vector2 vector2, Vector2 OffsetPosition, Color Colour, float Depth, Texture2D altTex = null)
	{
		Texture2D texture = ((altTex == null) ? white : altTex);
		float x = Vector2.Distance(vector1, vector2);
		float rotation = (float)Math.Atan2(vector2.Y - vector1.Y, vector2.X - vector1.X);
		spriteBatch.Draw(texture, OffsetPosition + vector1, null, Colour, rotation, Vector2.Zero, new Vector2(x, 1f), SpriteEffects.None, Depth);
	}

	public static void drawLineAlt(SpriteBatch spriteBatch, Vector2 vector1, Vector2 vector2, Vector2 OffsetPosition, Color Colour, float Depth, float width, Texture2D altTex = null)
	{
		Texture2D texture2D = ((altTex == null) ? white : altTex);
		float num = Vector2.Distance(vector1, vector2);
		float rotation = (float)Math.Atan2(vector2.Y - vector1.Y, vector2.X - vector1.X);
		float y = 1f;
		if (texture2D.Height > 1)
		{
			y = 1f / (float)texture2D.Height;
		}
		spriteBatch.Draw(texture2D, OffsetPosition + vector1, null, Colour, rotation, Vector2.Zero, new Vector2(num / (float)texture2D.Width * width, y), SpriteEffects.FlipHorizontally, Depth);
	}

	public static bool keyPressed(InputState input, Keys key, PlayerIndex? player)
	{
		if (GuiData.blockingTextInput)
		{
			return false;
		}
		int num = 0;
		if (player.HasValue)
		{
			num = (int)player.Value;
		}
		KeyboardState keyboardState = input.CurrentKeyboardStates[num];
		KeyboardState keyboardState2 = input.LastKeyboardStates[num];
		if (keyboardState.IsKeyDown(key) && keyboardState2.IsKeyUp(key))
		{
			return true;
		}
		return false;
	}

	public static bool buttonPressed(InputState input, Buttons button, PlayerIndex? player)
	{
		GamePadState gamePadState = input.CurrentGamePadStates[(int)player.Value];
		GamePadState gamePadState2 = input.LastGamePadStates[(int)player.Value];
		if (gamePadState.IsButtonDown(button) && gamePadState2.IsButtonUp(button))
		{
			return true;
		}
		return false;
	}

	public static bool arraysAreTheSame(Keys[] a, Keys[] b)
	{
		if (a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i].CompareTo(b[i]) == 0)
			{
				return false;
			}
		}
		return true;
	}

	public static bool arraysAreTheSame(Buttons[] a, Buttons[] b)
	{
		if (a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i].CompareTo(b[i]) == 0)
			{
				return false;
			}
		}
		return true;
	}

	public static float rand(float range)
	{
		return (float)(random.NextDouble() * (double)range - random.NextDouble() * (double)range);
	}

	public static float randm(float range)
	{
		return (float)(random.NextDouble() * (double)range);
	}

	public static float rand()
	{
		return (float)random.NextDouble();
	}

	public static byte getRandomByte()
	{
		random.NextBytes(byteBuffer);
		return byteBuffer[0];
	}

	public static Rectangle GetFullscreen()
	{
		Viewport viewport = GuiData.spriteBatch.GraphicsDevice.Viewport;
		return new Rectangle(0, 0, viewport.Width, viewport.Height);
	}

	public static AudioEmitter emitterAtPosition(float x, float y)
	{
		if (emitter == null)
		{
			emitter = new AudioEmitter();
		}
		vec3.X = x;
		vec3.Y = y;
		vec3.Z = 0f;
		emitter.Position = vec3;
		return emitter;
	}

	public static AudioEmitter emitterAtPosition(float x, float y, float z)
	{
		if (emitter == null)
		{
			emitter = new AudioEmitter();
		}
		vec3.X = x;
		vec3.Y = y;
		vec3.Z = z;
		emitter.Position = vec3;
		return emitter;
	}

	public static Texture2D White(ContentManager content)
	{
		if (white == null || white.IsDisposed)
		{
			white = TextureBank.load("Other\\white", content);
		}
		return white;
	}

	public static Color makeColor(byte r, byte g, byte b, byte a)
	{
		col.R = r;
		col.G = g;
		col.B = b;
		col.A = a;
		return col;
	}

	public static Color makeColorAddative(Color c)
	{
		col.R = c.R;
		col.G = c.G;
		col.B = c.B;
		col.A = 0;
		return col;
	}

	public static bool flipCoin()
	{
		return random.NextDouble() > 0.5;
	}

	public static byte randomCompType()
	{
		if (flipCoin())
		{
			return 1;
		}
		return 2;
	}

	public static void writeToFile(string data, string filename)
	{
		using StreamWriter streamWriter = new StreamWriter(filename);
		streamWriter.Write(data);
		streamWriter.Flush();
		streamWriter.Close();
	}

	public static void SafeWriteToFile(string data, string filename)
	{
		string text = filename + ".tmp";
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(text));
		}
		using (StreamWriter streamWriter = new StreamWriter(text, append: false))
		{
			streamWriter.Write(data);
			streamWriter.Flush();
			streamWriter.Close();
		}
		if (File.Exists(filename))
		{
			File.Delete(filename);
		}
		File.Move(text, filename);
	}

	public static void SafeWriteToFile(byte[] data, string filename)
	{
		string text = filename + ".tmp";
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(text));
		}
		using (StreamWriter streamWriter = new StreamWriter(text, append: false))
		{
			streamWriter.Write(data);
			streamWriter.Flush();
			streamWriter.Close();
		}
		File.Delete(filename);
		File.Move(text, filename);
	}

	public static void appendToFile(string data, string filename)
	{
		StreamWriter streamWriter = new StreamWriter(filename, append: true);
		streamWriter.Write(data);
		streamWriter.Close();
	}

	public static string readEntireFile(string filename)
	{
		if (Settings.ActiveLocale != "en-us")
		{
			filename = LocalizedFileLoader.GetLocalizedFilepath(filename);
		}
		StreamReader streamReader = new StreamReader(File.OpenRead(filename));
		string data = streamReader.ReadToEnd();
		streamReader.Close();
		return LocalizedFileLoader.FilterStringForLocalization(data);
	}

	public static char getRandomLetter()
	{
		return Convert.ToChar(Convert.ToInt32(Math.Floor(26.0 * random.NextDouble() + 65.0)));
	}

	public static char getRandomChar()
	{
		if (random.NextDouble() > 0.7)
		{
			return string.Concat(Math.Min((int)Math.Floor((double)random.Next(0, 10)), 9))[0];
		}
		return getRandomLetter();
	}

	public static char getRandomNumberChar()
	{
		return string.Concat(Math.Min((int)Math.Floor((double)random.Next(0, 10)), 9))[0];
	}

	public static Color convertStringToColor(string input)
	{
		Color result = Color.White;
		char[] separator = new char[3] { ',', ' ', '/' };
		string[] array = input.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 3)
		{
			return result;
		}
		byte r = byte.MaxValue;
		byte g = byte.MaxValue;
		byte b = byte.MaxValue;
		byte alpha = byte.MaxValue;
		for (int i = 0; i < 4; i++)
		{
			if (array.Length <= i)
			{
				continue;
			}
			try
			{
				byte b2 = Convert.ToByte(array[i]);
				switch (i)
				{
				case 0:
					r = b2;
					break;
				case 1:
					g = b2;
					break;
				case 2:
					b = b2;
					break;
				case 3:
					alpha = b2;
					break;
				}
			}
			catch (FormatException)
			{
			}
			catch (OverflowException)
			{
			}
		}
		return new Color(r, g, b, alpha);
	}

	public static string convertColorToParseableString(Color c)
	{
		return c.R + "," + c.G + "," + c.B + ((c.A != byte.MaxValue) ? ("," + c.A) : "");
	}

	public static void ClipLineSegmentsForRect(Rectangle dest, Vector2 left, Vector2 right, out Vector2 leftOut, out Vector2 rightOut)
	{
		leftOut = left;
		rightOut = right;
		if (left.X < (float)dest.X)
		{
			leftOut.X = dest.X;
		}
		if (right.X < (float)dest.X)
		{
			rightOut.X = dest.X;
		}
		if (left.X > (float)(dest.X + dest.Width))
		{
			leftOut.X = dest.X + dest.Width;
		}
		if (right.X > (float)(dest.X + dest.Width))
		{
			rightOut.X = dest.X + dest.Width;
		}
		if (left.Y < (float)dest.Y)
		{
			leftOut.Y = dest.Y;
		}
		if (right.Y < (float)dest.Y)
		{
			rightOut.Y = dest.Y;
		}
		if (left.Y > (float)(dest.Y + dest.Height))
		{
			leftOut.Y = dest.Y + dest.Height;
		}
		if (right.Y > (float)(dest.Y + dest.Height))
		{
			rightOut.Y = dest.Y + dest.Height;
		}
	}

	public static Rectangle getClipRectForSpritePos(Rectangle bounds, Texture2D tex, Vector2 pos, float scale)
	{
		int num = (int)((float)tex.Width * scale);
		int num2 = (int)((float)tex.Height * scale);
		int num3;
		int num4 = (num3 = 0);
		int num5 = tex.Width;
		int num6 = tex.Height;
		if (pos.X < (float)bounds.X)
		{
			num4 += (int)(((float)bounds.X - pos.X) / scale);
		}
		if (pos.Y < (float)bounds.Y)
		{
			num3 += (int)(((float)bounds.Y - pos.Y) / scale);
		}
		if (pos.X + (float)num > (float)(bounds.X + bounds.Width))
		{
			num5 -= (int)((pos.X + (float)num - (float)(bounds.X + bounds.Width)) * (1f / scale));
		}
		if (pos.Y + (float)num2 > (float)(bounds.Y + bounds.Height))
		{
			num6 -= (int)((pos.Y + (float)num2 - (float)(bounds.Y + bounds.Height)) * (1f / scale));
		}
		if (num4 > tex.Width)
		{
			num4 = tex.Width;
			num5 = 0;
		}
		if (num3 > tex.Height)
		{
			num3 = tex.Height;
			num6 = 0;
		}
		return new Rectangle(num4, num3, num5, num6);
	}

	public static Rectangle getClipRectForSpritePos(Rectangle bounds, Texture2D tex, Vector2 pos, Vector2 scale)
	{
		int num = (int)((float)tex.Width * scale.X);
		int num2 = (int)((float)tex.Height * scale.Y);
		int num3;
		int num4 = (num3 = 0);
		int num5 = tex.Width;
		int num6 = tex.Height;
		if (pos.X < (float)bounds.X)
		{
			int num7 = (int)(((float)bounds.X - pos.X) / scale.X);
			num4 += num7;
			num5 -= num7;
		}
		if (pos.Y < (float)bounds.Y)
		{
			int num7 = (int)(((float)bounds.Y - pos.Y) / scale.Y);
			num3 += num7;
			num6 -= num7;
		}
		if (pos.X + (float)num > (float)(bounds.X + bounds.Width))
		{
			num5 -= (int)((pos.X + (float)num - (float)(bounds.X + bounds.Width)) * (1f / scale.X));
		}
		if (pos.Y + (float)num2 > (float)(bounds.Y + bounds.Height))
		{
			num6 -= (int)((pos.Y + (float)num2 - (float)(bounds.Y + bounds.Height)) * (1f / scale.Y));
		}
		if (num4 > tex.Width)
		{
			num4 = tex.Width;
			num5 = 0;
		}
		if (num3 > tex.Height)
		{
			num3 = tex.Height;
			num6 = 0;
		}
		return new Rectangle(num4, num3, num5, num6);
	}

	public static Rectangle getClipRectForSpritePos(Rectangle bounds, Texture2D tex, Vector2 pos, Vector2 scale, Vector2 origin)
	{
		return getClipRectForSpritePos(bounds, tex, pos - origin * scale, scale);
	}

	public static void RenderSpriteIntoClippedRectDest(Rectangle fullBounds, Rectangle targetBounds, Texture2D tex, Color c, SpriteBatch sb)
	{
		Vector2 scale = new Vector2((float)targetBounds.Width / (float)tex.Width, (float)targetBounds.Height / (float)tex.Height);
		Vector2 vector = new Vector2(targetBounds.X, targetBounds.Y);
		Rectangle clipRectForSpritePos = getClipRectForSpritePos(fullBounds, tex, vector, scale, Vector2.Zero);
		Vector2 position = vector + new Vector2((float)clipRectForSpritePos.X * scale.X, (float)clipRectForSpritePos.Y * scale.Y);
		sb.Draw(tex, position, clipRectForSpritePos, c, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.5f);
	}

	public static Vector2 GetCentreOrigin(this Texture2D tex)
	{
		return new Vector2(tex.Width / 2, tex.Height / 2);
	}

	public static string SmartTwimForWidth(string data, int width, SpriteFont font)
	{
		string[] array = data.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i];
			if (!(font.MeasureString(text).X > (float)width))
			{
				continue;
			}
			string text2 = "";
			for (int j = 0; j < text.Length; j++)
			{
				bool flag = false;
				char c = text[j];
				if (c == '\t' || c == ' ')
				{
					text2 += text[j];
				}
				else
				{
					flag = true;
				}
				if (flag)
				{
					break;
				}
			}
			string[] collection = text.Substring(text2.Length).Split(new string[1] { " " }, StringSplitOptions.None);
			List<string> list = new List<string>(collection);
			string text3 = "";
			string text4 = "";
			while (list.Count > 0)
			{
				if (font.MeasureString(text4 + " " + list[0]).X > (float)width)
				{
					text3 = text3 + text2 + text4.Trim() + "\r\n";
					text4 = "";
				}
				text4 = text4 + list[0] + " ";
				list.RemoveAt(0);
			}
			array[i] = text3 + text2 + text4.Trim() + "\r\n";
		}
		string text5 = "";
		for (int k = 0; k < array.Length; k++)
		{
			text5 = text5 + array[k] + "\r\n";
		}
		return text5.Trim();
	}

	public static Stream GenerateStreamFromString(string s)
	{
		MemoryStream memoryStream = new MemoryStream();
		StreamWriter streamWriter = new StreamWriter(memoryStream);
		streamWriter.Write(s);
		streamWriter.Flush();
		memoryStream.Position = 0L;
		return memoryStream;
	}

	public static string SuperSmartTwimForWidth(string data, int width, SpriteFont font)
	{
		if (width <= 0)
		{
			return data;
		}
		string[] array = data.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i];
			if (!(font.MeasureString(text).X > (float)width))
			{
				continue;
			}
			string text2 = "";
			for (int j = 0; j < text.Length; j++)
			{
				bool flag = false;
				char c = text[j];
				if (c == '\t' || c == ' ')
				{
					text2 += text[j];
				}
				else
				{
					flag = true;
				}
				if (flag)
				{
					break;
				}
			}
			string[] collection = text.Substring(text2.Length).Split(new string[1] { " " }, StringSplitOptions.None);
			List<string> list = new List<string>(collection);
			string text3 = "";
			StringBuilder stringBuilder = new StringBuilder();
			while (list.Count > 0)
			{
				if (font.MeasureString(stringBuilder.ToString() + " " + list[0]).X > (float)width)
				{
					text3 = text3 + text2 + stringBuilder.ToString().Trim() + "\r\n";
					stringBuilder.Clear();
				}
				int num = 1;
				if (font.MeasureString(list[0]).X > (float)width)
				{
					int num2 = 0;
					int num3 = 0;
					int num4 = 1;
					if (font.MeasureString(list[0].Substring(0, num)).X >= (float)width && font.MeasureString(list[0].Substring(0, num - 1)).X < (float)width)
					{
						num4 = num;
					}
					for (num2 = num4; num2 < list[0].Length && !(font.MeasureString(list[0].Substring(0, num2)).X >= (float)width); num2++)
					{
						int num5 = 40;
						if (num2 + num5 < list[0].Length && !(font.MeasureString(list[0].Substring(0, num2 + num5)).X >= (float)width))
						{
							num2 += num5;
							num3 += num5;
						}
					}
					num2--;
					num = num2;
					if (num2 == 0)
					{
						stringBuilder.Append(list[0]);
						stringBuilder.Append(" ");
						list.RemoveAt(0);
					}
					else
					{
						string text4 = list[0];
						stringBuilder.Append(list[0].Substring(0, num2));
						stringBuilder.Append(" ");
						list[0] = text4.Substring(num2);
					}
				}
				else
				{
					stringBuilder.Append(list[0]);
					stringBuilder.Append(" ");
					list.RemoveAt(0);
				}
			}
			bool flag2 = array.Length > i + 1 && string.IsNullOrWhiteSpace(array[i + 1]);
			array[i] = text3 + text2 + stringBuilder.ToString().Trim() + (flag2 ? "" : "\r\n");
		}
		string text5 = "";
		for (int k = 0; k < array.Length; k++)
		{
			text5 = text5 + array[k] + "\r\n";
		}
		return text5.Trim();
	}

	public static float QuadraticOutCurve(float point)
	{
		return (-100000000f * point * (point - 2f) - 1f) / 100000000f;
	}

	public static float CubicInCurve(float point)
	{
		return 100000000f * (point /= 1f) * point * point / 100000000f;
	}

	public static float CubicOutCurve(float point)
	{
		return 100000000f * ((point = point / 1f - 1f) * point * point + 1f) / 100000000f;
	}

	public static RenderTarget2D GetCurrentRenderTarget()
	{
		RenderTargetBinding[] renderTargets = GuiData.spriteBatch.GraphicsDevice.GetRenderTargets();
		if (renderTargets.Length == 0)
		{
			return null;
		}
		return (RenderTarget2D)renderTargets[0].RenderTarget;
	}

	public static float SmoothStep(float edge0, float edge1, float x)
	{
		x = (float)Math.Min(Math.Max((x - edge0) / (edge1 - edge0), 0.0), 1.0);
		return x * x * x * (x * (x * 6f - 15f) + 10f);
	}

	public static Rectangle InsetRectangle(Rectangle rect, int inset)
	{
		return new Rectangle(rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);
	}

	public static Vector2 GetNearestPointOnCircle(Vector2 point, Vector2 CircleCentre, float circleRadius)
	{
		float num = point.X - CircleCentre.X;
		float num2 = point.Y - CircleCentre.Y;
		float num3 = (float)Math.Sqrt(num * num + num2 * num2);
		float x = CircleCentre.X + num / num3 * circleRadius;
		float y = CircleCentre.Y + num2 / num3 * circleRadius;
		return new Vector2(x, y);
	}

	public static float Clamp(float val, float min, float max)
	{
		if (val < min)
		{
			val = min;
		}
		if (val > max)
		{
			val = max;
		}
		return val;
	}

	public static Vector2 Clamp(Vector2 val, float min, float max)
	{
		return new Vector2(Clamp(val.X, min, max), Clamp(val.Y, min, max));
	}

	public static string RandomFromArray(string[] array)
	{
		return array[random.Next(array.Length)];
	}

	public static string GetNonRepeatingFilename(string filename, string extension, Folder f)
	{
		string text = filename;
		int num = 0;
		bool flag = false;
		do
		{
			flag = true;
			for (int i = 0; i < f.files.Count; i++)
			{
				if (f.files[i].name == text + extension)
				{
					num++;
					text = filename + "(" + num + ")";
					flag = false;
					break;
				}
			}
		}
		while (!flag);
		return text + extension;
	}

	public static string FlipRandomChars(string original, double chancePerChar)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < original.Length; i++)
		{
			if (random.NextDouble() < chancePerChar)
			{
				stringBuilder.Append(getRandomChar());
			}
			else
			{
				stringBuilder.Append(original[i]);
			}
		}
		return stringBuilder.ToString();
	}

	public static Vector3 ColorToVec3(Color c)
	{
		float num = (float)(int)c.A / 255f;
		return new Vector3((float)(int)c.R / 255f * num, (float)(int)c.G / 255f * num, (float)(int)c.B / 255f * num);
	}

	public static Color AdditivizeColor(Color c)
	{
		col.R = c.R;
		col.G = c.G;
		col.B = c.B;
		col.A = 0;
		return col;
	}

	public static Vector2 RotatePoint(Vector2 point, float angle)
	{
		return PolarToCartesian(angle + GetPolarAngle(point), point.Length());
	}

	public static bool DebugGoFast()
	{
		return Settings.debugCommandsEnabled && GuiData.getKeyboadState().IsKeyDown(Keys.LeftAlt);
	}

	public static bool FieldContainsAttributeOfType(FieldInfo field, Type attributeType)
	{
		object[] customAttributes = field.GetCustomAttributes(inherit: true);
		foreach (object obj in customAttributes)
		{
			if (obj.GetType() == attributeType)
			{
				return true;
			}
		}
		return false;
	}

	public static void SendRealWorldEmail(string subject, string to, string body)
	{
		try
		{
			MailAddress mailAddress = new MailAddress("fractalalligatordev@gmail.com");
			MailAddress to2 = new MailAddress(to);
			SmtpClient smtpClient = new SmtpClient();
			smtpClient.Host = "smtp.gmail.com";
			smtpClient.Port = 587;
			smtpClient.EnableSsl = true;
			smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
			smtpClient.UseDefaultCredentials = false;
			smtpClient.Credentials = new NetworkCredential(mailAddress.Address, "rgaekwivookrsfqg");
			SmtpClient smtpClient2 = smtpClient;
			MailMessage mailMessage = new MailMessage(mailAddress, to2);
			mailMessage.Subject = subject;
			mailMessage.Body = body;
			using MailMessage message = mailMessage;
			smtpClient2.Send(message);
		}
		catch (Exception)
		{
		}
	}

	public static string GenerateReportFromException(Exception ex)
	{
		string text = string.Concat(ex.GetType().ToString(), "\r\n\r\n", ex.Message, "\r\n\r\nSource : ", ex.Source, "\r\n\r\n", ex, "\r\n\r\n");
		if (ex.InnerException != null)
		{
			text += "Inner : ---------------\r\n\r\n";
			string text2 = GenerateReportFromException(ex.InnerException);
			text2 = text2.Replace("\t", "\0");
			text2 = text2.Replace("\r\n", "\r\n\t");
			text2 = text2.Replace("\0", "\t");
			text = text + text2 + "\r\n\r\n";
		}
		text = text + "\r\n\r\n" + ex.StackTrace;
		text = FileSanitiser.purifyStringForDisplay(text);
		try
		{
			text = SuperSmartTwimForWidth(text, 800, GuiData.smallfont);
		}
		catch (Exception)
		{
		}
		return text;
	}

	public static string GenerateReportFromExceptionCompact(Exception ex)
	{
		string text = ex.Message + "\r\n\r\nSource : " + ex.Source + "  ::  " + ex.GetType().ToString();
		if (ex.InnerException != null)
		{
			text += "\r\nInner : ---------------\r\n";
			string text2 = GenerateReportFromExceptionCompact(ex.InnerException);
			text2 = text2.Replace("\t", "\0");
			text2 = text2.Replace("\r\n", "\r\n\t");
			text2 = text2.Replace("\0", "\t");
			text = text + text2 + "\r\n---------------\r\n\r\n";
		}
		text = FileSanitiser.purifyStringForDisplay(text);
		try
		{
			text = SuperSmartTwimForWidth(text, 800, GuiData.smallfont);
		}
		catch (Exception)
		{
		}
		return text;
	}

	public static bool FloatEquals(float a, float b)
	{
		return Math.Abs(a - b) < 0.0001f;
	}

	public static Vector2 PolarToCartesian(float angle, float magnitude)
	{
		return new Vector2(magnitude * (float)Math.Cos(angle), magnitude * (float)Math.Sin(angle));
	}

	public static float GetPolarAngle(Vector2 point)
	{
		return (float)Math.Atan2(point.Y, point.X);
	}

	public static Vector3 NormalizeRotationVector(Vector3 rot)
	{
		return new Vector3((float)((double)rot.X % (Math.PI * 2.0)), (float)((double)rot.Y % (Math.PI * 2.0)), (float)((double)rot.Z % (Math.PI * 2.0)));
	}

	public static void FillEverywhereExcept(Rectangle bounds, Rectangle fullscreen, SpriteBatch sb, Color col)
	{
		Rectangle destinationRectangle = new Rectangle(fullscreen.X, fullscreen.Y, bounds.X - fullscreen.X, fullscreen.Height);
		Rectangle destinationRectangle2 = new Rectangle(bounds.X, fullscreen.Y, bounds.Width, bounds.Y - fullscreen.Y);
		Rectangle destinationRectangle3 = new Rectangle(bounds.X, bounds.Y + bounds.Height, bounds.Width, fullscreen.Height - (bounds.Y + bounds.Height));
		Rectangle destinationRectangle4 = new Rectangle(bounds.X + bounds.Width, fullscreen.Y, fullscreen.Width - (bounds.X + bounds.Width), fullscreen.Height);
		sb.Draw(white, destinationRectangle, col);
		sb.Draw(white, destinationRectangle4, col);
		sb.Draw(white, destinationRectangle2, col);
		sb.Draw(white, destinationRectangle3, col);
	}

	public static bool CheckStringIsRenderable(string input)
	{
		for (int i = 0; i < input.Length; i++)
		{
			if (input[i] != '\n' && !GuiData.font.Characters.Contains(input[i]))
			{
				char c = input[i];
				Console.WriteLine("\r\n------------------\r\nInvalid Char : {" + input[i] + "}\r\n----------------------\r\n");
				return false;
			}
		}
		return true;
	}

	public static bool CheckStringIsTitleRenderable(string input)
	{
		string source = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890,./!@#$%^&*()<>\\\":;{}_-+= \r\n?[]`~'|";
		for (int i = 0; i < input.Length; i++)
		{
			if (!source.Contains(input[i]))
			{
				char c = input[i];
				Console.WriteLine("\r\n------------------\r\nInvalid Char : {" + input[i] + "}\r\n----------------------\r\n");
				return false;
			}
		}
		return true;
	}

	public static bool StringContainsInvalidFilenameChars(string input)
	{
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		char[] array = new char[5] { '&', '<', '>', '\'', '"' };
		for (int i = 0; i < invalidFileNameChars.Length; i++)
		{
			if (input.Contains(invalidFileNameChars[i]))
			{
				return true;
			}
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (input.Contains(array[i]))
			{
				return true;
			}
		}
		return false;
	}

	public static string CleanStringToRenderable(string input)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string source = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890,./!@#$%^&*()<>\\\":;{}_-+= \r\n?[]`~'|";
		for (int i = 0; i < input.Length; i++)
		{
			if (!source.Contains(input[i]))
			{
				stringBuilder.Append('?');
			}
			else
			{
				stringBuilder.Append(input[i]);
			}
		}
		return stringBuilder.ToString();
	}

	public static string CleanFilterStringToRenderable(string input)
	{
		return CleanStringToLanguageRenderable(input.Replace("\t", "    ").Replace("…", "..."));
	}

	public static string CleanStringToLanguageRenderable(string input)
	{
		input = input.Replace("\t", "    ");
		StringBuilder stringBuilder = new StringBuilder();
		string source = "\r\n";
		for (int i = 0; i < input.Length; i++)
		{
			if (source.Contains(input[i]))
			{
				stringBuilder.Append(input[i]);
			}
			else if (!GuiData.font.Characters.Contains(input[i]))
			{
				stringBuilder.Append('?');
			}
			else
			{
				stringBuilder.Append(input[i]);
			}
		}
		return stringBuilder.ToString();
	}

	public static void AppendToErrorFile(string text)
	{
		try
		{
			if (SDL.SDL_GetPlatform().Equals("Windows"))
			{
				string text2 = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/My Games/Hacknet/Reports/";
				if (!Directory.Exists(text2))
				{
					Directory.CreateDirectory(text2);
				}
				string text3 = "RuntimeErrors.txt";
				if (!File.Exists(text2 + text3))
				{
					File.WriteAllText(text2 + text3, "Hacknet v" + MainMenu.OSVersion + " Runtime ErrorLog\r\n\r\n");
				}
				string value = "-----\r\n" + MainMenu.OSVersion + " : " + DateTime.Now.ToString();
				using StreamWriter streamWriter = File.AppendText(text2 + text3);
				streamWriter.WriteLine(value);
				streamWriter.WriteLine(text);
				return;
			}
		}
		catch (Exception)
		{
		}
	}

	public static void AppendToWarningsFile(string text)
	{
		try
		{
			string path = GetFileLoadPrefix() + "warnings.txt";
			string text2 = "";
			if (File.Exists(path))
			{
				text2 = File.ReadAllText(path) + "--------------------------------\r\n\r\n";
			}
			text2 += text;
			File.WriteAllText(path, text2);
		}
		catch (Exception)
		{
		}
	}

	public static string SerializeListToCSV(List<string> list)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < list.Count; i++)
		{
			if (i != 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append(list[i]);
		}
		return stringBuilder.ToString();
	}

	public static string[] SplitToTokens(string input)
	{
		return (from Match m in Regex.Matches(input, "[\\\"].+?[\\\"]|[^ ]+")
			select m.Value).ToList().ToArray();
	}

	public static string[] SplitToTokens(string[] input)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < input.Length; i++)
		{
			stringBuilder.Append(input[i]);
			stringBuilder.Append(" ");
		}
		return SplitToTokens(stringBuilder.ToString());
	}

	public static string ExtractBracketedSection(string input, out string bracketedBit)
	{
		string result = input;
		bracketedBit = "";
		int num = input.IndexOf('(');
		if (num == -1)
		{
			return result;
		}
		int num2 = input.Substring(num).IndexOf(')');
		if (num2 == -1)
		{
			return result;
		}
		num2 += num;
		result = input.Substring(0, num) + input.Substring(num2 + 1, input.Length - (num2 + 1));
		bracketedBit = input.Substring(num, num2 - num + 1);
		return result;
	}

	public static Color ColorFromHexString(string hexString)
	{
		if (hexString.StartsWith("#"))
		{
			hexString = hexString.Substring(1);
		}
		uint num = uint.Parse(hexString, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		Color result = Color.White;
		if (hexString.Length == 8)
		{
			result.A = (byte)(num >> 24);
			result.R = (byte)(num >> 16);
			result.G = (byte)(num >> 8);
			result.B = (byte)num;
		}
		else
		{
			if (hexString.Length != 6)
			{
				throw new InvalidOperationException("Invald hex representation of an ARGB or RGB color value.");
			}
			result.R = (byte)(num >> 16);
			result.G = (byte)(num >> 8);
			result.B = (byte)num;
		}
		return result;
	}

	public static string ReadEntireContentsOfStream(Stream input)
	{
		TextReader textReader = new StreamReader(input);
		string result = textReader.ReadToEnd();
		input.Flush();
		input.Close();
		input.Dispose();
		return result;
	}

	public static void SendErrorEmail(Exception ex, string postfix = "", string extraData = "")
	{
		string text = GenerateReportFromException(ex);
		text = text + "\r\n White:" + white;
		text = text + "\r\n WhiteDisposed:" + white.IsDisposed;
		text = text + "\r\n SmallFont:" + GuiData.smallfont;
		text = text + "\r\n TinyFont:" + GuiData.tinyfont;
		text = text + "\r\n LineEffectTarget:" + FlickeringTextEffect.GetReportString();
		text = text + "\r\n PostProcessort stuff:" + PostProcessor.GetStatusReportString();
		object obj = text;
		text = string.Concat(obj, "\r\nRESOLUTION:\r\n ", Game1.getSingleton().GraphicsDevice.PresentationParameters.BackBufferWidth, "x");
		text = text + Game1.getSingleton().GraphicsDevice.PresentationParameters.BackBufferHeight + "\r\nFullscreen: ";
		text += (Game1.getSingleton().graphics.IsFullScreen ? "true" : "false");
		text = text + "\r\n Adapter: " + Game1.getSingleton().GraphicsDevice.Adapter.Description;
		text = text + "\r\n Device Name: " + Game1.getSingleton().GraphicsDevice.Adapter.DeviceName;
		text = text + "\r\n Status: " + Game1.getSingleton().GraphicsDevice.GraphicsDeviceStatus;
		text = text + "\r\n Extra:\r\n" + extraData + "\r\n";
		SendRealWorldEmail("Hacknet " + postfix + MainMenu.OSVersion + " Crash " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString(), "hacknetbugs+Hacknet@gmail.com", text);
	}

	public static void SendThreadedErrorReport(Exception ex, string postfix = "", string extraData = "")
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			SendErrorEmail(ex, postfix, extraData);
		});
		thread.IsBackground = true;
		thread.Name = postfix + "_Errorthread";
		thread.Start();
	}

	public static DateTime SafeParseDateTime(string input)
	{
		DateTime result = default(DateTime);
		if (DateTime.TryParseExact(input, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			return result;
		}
		if (DateTime.TryParse(input, new CultureInfo("en-au"), DateTimeStyles.None, out result))
		{
			return result;
		}
		if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			return result;
		}
		Console.WriteLine("Error Parsing DateTime : " + input);
		return DateTime.Now;
	}

	public static string SafeWriteDateTime(DateTime input)
	{
		return input.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
	}

	public static Color GetComplimentaryColor(Color c)
	{
		hslColor = HSLColor.FromRGB(c);
		hslColor.Luminosity = Math.Max(0.4f, hslColor.Luminosity);
		hslColor.Saturation = Math.Max(0.4f, hslColor.Saturation);
		hslColor.Saturation = Math.Min(0.55f, hslColor.Saturation);
		hslColor.Hue -= (float)Math.PI;
		if (hslColor.Hue < 0f)
		{
			hslColor.Hue += (float)Math.PI * 2f;
		}
		return hslColor.ToRGB();
	}

	public static void MeasureTimedSpeechSection()
	{
		string text = readEntireFile("Content/Post/BitSpeech.txt");
		string[] array = text.Split(newlineDelim);
		float num = 0f;
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i];
			float num2 = 0f;
			for (int j = 0; j < text2.Length; j++)
			{
				num2 = ((text2[j] != '#') ? ((text2[j] != '%') ? (num2 + 0.05f) : (num2 + 0.5f)) : (num2 + 1f));
			}
			Console.WriteLine("LINE " + i + ": " + num + "  --  " + num2 + "   : " + array[i]);
			num += num2;
		}
	}

	public static Color HSL2RGB(double h, double sl, double l)
	{
		double num = l;
		double num2 = l;
		double num3 = l;
		double num4 = ((l <= 0.5) ? (l * (1.0 + sl)) : (l + sl - l * sl));
		if (num4 > 0.0)
		{
			double num5 = l + l - num4;
			double num6 = (num4 - num5) / num4;
			h *= 6.0;
			int num7 = (int)h;
			double num8 = h - (double)num7;
			double num9 = num4 * num6 * num8;
			double num10 = num5 + num9;
			double num11 = num4 - num9;
			switch (num7)
			{
			case 0:
				num = num4;
				num2 = num10;
				num3 = num5;
				break;
			case 1:
				num = num11;
				num2 = num4;
				num3 = num5;
				break;
			case 2:
				num = num5;
				num2 = num4;
				num3 = num10;
				break;
			case 3:
				num = num5;
				num2 = num11;
				num3 = num4;
				break;
			case 4:
				num = num10;
				num2 = num5;
				num3 = num4;
				break;
			case 5:
				num = num4;
				num2 = num5;
				num3 = num11;
				break;
			}
		}
		Color result = default(Color);
		result.R = Convert.ToByte(num * 255.0);
		result.G = Convert.ToByte(num2 * 255.0);
		result.B = Convert.ToByte(num3 * 255.0);
		result.A = byte.MaxValue;
		return result;
	}

	public static void RGB2HSL(Color rgb, out double h, out double s, out double l)
	{
		double num = (double)(int)rgb.R / 255.0;
		double num2 = (double)(int)rgb.G / 255.0;
		double num3 = (double)(int)rgb.B / 255.0;
		h = 0.0;
		s = 0.0;
		l = 0.0;
		double val = Math.Max(num, num2);
		val = Math.Max(val, num3);
		double val2 = Math.Min(num, num2);
		val2 = Math.Min(val2, num3);
		l = (val2 + val) / 2.0;
		if (l <= 0.0)
		{
			return;
		}
		double num4 = (s = val - val2);
		if (s > 0.0)
		{
			s /= ((l <= 0.5) ? (val + val2) : (2.0 - val - val2));
			double num5 = (val - num) / num4;
			double num6 = (val - num2) / num4;
			double num7 = (val - num3) / num4;
			if (num == val)
			{
				h = ((num2 == val2) ? (5.0 + num7) : (1.0 - num6));
			}
			else if (num2 == val)
			{
				h = ((num3 == val2) ? (1.0 + num5) : (3.0 - num7));
			}
			else
			{
				h = ((num == val2) ? (3.0 + num6) : (5.0 - num5));
			}
			h /= 6.0;
		}
	}

	public static void ActOnAllFilesRevursivley(string foldername, Action<string> FileAction)
	{
		string[] files = Directory.GetFiles(foldername);
		for (int i = 0; i < files.Length; i++)
		{
			FileAction(files[i]);
		}
		string[] directories = Directory.GetDirectories(foldername);
		for (int i = 0; i < directories.Length; i++)
		{
			ActOnAllFilesRevursivley(directories[i], FileAction);
		}
	}

	public static string GetFileLoadPrefix()
	{
		if (Settings.IsInExtensionMode)
		{
			return ExtensionLoader.ActiveExtensionInfo.FolderPath + "/";
		}
		return "Content/";
	}

	public static Vector2 ClipVec2ForTextRendering(Vector2 input)
	{
		input.X = (int)((double)input.X + 0.5);
		input.Y = (int)((double)input.Y + 0.5);
		return input;
	}

	public static string SerializeObject(object o)
	{
		Type type = o.GetType();
		string text = type.Name;
		if (text.StartsWith("Hacknet."))
		{
			text = text.Substring("Hacknet.".Length);
		}
		StringBuilder stringBuilder = new StringBuilder("<" + text + ">");
		FieldInfo[] fields = type.GetFields();
		for (int i = 0; i < fields.Length; i++)
		{
			string name = fields[i].Name;
			object value = fields[i].GetValue(o);
			if (value != null)
			{
				string text2 = value.ToString();
				Type fieldType = fields[i].FieldType;
				if (fieldType == typeof(Color))
				{
					text2 = convertColorToParseableString((Color)fields[i].GetValue(o));
				}
				stringBuilder.Append(string.Format(CultureInfo.InvariantCulture, "\n\t<{0}>{1}</{0}>", new object[2] { name, text2 }));
			}
		}
		stringBuilder.Append("\n</" + text + ">");
		return stringBuilder.ToString();
	}

	public static object DeserializeObject(Stream s, Type t)
	{
		using XmlReader xmlReader = XmlReader.Create(s);
		object obj = Activator.CreateInstance(t);
		FieldInfo[] fields = t.GetFields();
		XmlNamespaceManager namespaceResolver = new XmlNamespaceManager(new NameTable());
		while (!xmlReader.EOF)
		{
			if (!string.IsNullOrWhiteSpace(xmlReader.Name))
			{
				for (int i = 0; i < fields.Length; i++)
				{
					if (fields[i].Name == xmlReader.Name)
					{
						xmlReader.MoveToContent();
						object obj2 = null;
						if (fields[i].FieldType == typeof(Color))
						{
							obj2 = convertStringToColor(xmlReader.ReadElementContentAsString());
						}
						if (obj2 == null)
						{
							obj2 = xmlReader.ReadElementContentAs(fields[i].FieldType, namespaceResolver);
						}
						fields[i].SetValue(obj, obj2);
					}
				}
			}
			xmlReader.Read();
		}
		return obj;
	}

	public static void ProcessXmlElementInParent(XmlReader rdr, string ParentTag, string ElementTag, Action ProcessElement)
	{
		int num = 0;
		if (!(rdr.Name == ParentTag))
		{
			return;
		}
		do
		{
			if (rdr.Name == ElementTag && rdr.IsStartElement())
			{
				ProcessElement?.Invoke();
			}
			do
			{
				rdr.Read();
				num++;
			}
			while (rdr.IsEmptyElement && !rdr.EOF);
			if (rdr.Name == ParentTag && num > 10)
			{
				num++;
			}
			if (rdr.EOF)
			{
				throw new FormatException("Unexpected End of File looking for " + ElementTag + " in " + ParentTag + " tag. Made " + num + " reads.");
			}
		}
		while (!(rdr.Name == ParentTag) || rdr.IsStartElement());
	}

	public static bool PublicInstancePropertiesEqual<T>(T self, T to, params string[] ignore) where T : class
	{
		if (self != null && to != null)
		{
			Type type = self.GetType();
			List<string> list = new List<string>(ignore);
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				if (list.Contains(propertyInfo.Name))
				{
					continue;
				}
				object value = type.GetProperty(propertyInfo.Name).GetValue(self, null);
				object value2 = type.GetProperty(propertyInfo.Name).GetValue(to, null);
				if (propertyInfo.PropertyType.IsClass && !propertyInfo.PropertyType.Module.ScopeName.Equals("CommonLanguageRuntimeLibrary"))
				{
					if (!PublicInstancePropertiesEqual(value, value2, ignore))
					{
						return false;
					}
				}
				else if (value != value2 && (value == null || !value.Equals(value2)))
				{
					return false;
				}
			}
			return true;
		}
		return self == to;
	}

	public static Rectangle DrawSpriteAspectCorrect(Rectangle dest, SpriteBatch sb, Texture2D sprite, Color color, bool ForceToBottom = false)
	{
		Rectangle rectangle = dest;
		float num = (float)dest.Width / (float)dest.Height;
		float num2 = (float)sprite.Width / (float)sprite.Height;
		if (!FloatEquals(num, num2))
		{
			if (num > num2)
			{
				int height = dest.Height;
				int num3 = (int)((float)dest.Height * num2);
				int num4 = (int)((float)(dest.Width - num3) / 2f);
				rectangle = new Rectangle(dest.X + num4, dest.Y, num3, height);
			}
			else
			{
				int num3 = dest.Width;
				int height = (int)((float)dest.Width / num2);
				int num5 = (int)((float)(dest.Height - height) / (ForceToBottom ? 1f : 2f));
				rectangle = new Rectangle(dest.X, dest.Y + num5, num3, height);
			}
		}
		sb.Draw(sprite, rectangle, color);
		return rectangle;
	}

	public static int GetXForAlignment(AlignmentX align, int width, int margin, int objectWidth)
	{
		int num = 0;
		return align switch
		{
			AlignmentX.Left => margin, 
			AlignmentX.Right => width - (objectWidth + margin), 
			_ => width / 2 - objectWidth / 2, 
		};
	}

	public static void LoadImageFromContentOrExtension(string imagePath, ContentManager content, Action<Texture2D> LoadComplete)
	{
		Texture2D obj = null;
		if (imagePath == null)
		{
			return;
		}
		string text = GetFileLoadPrefix() + imagePath;
		if (!text.EndsWith(".jpg") && !text.EndsWith(".png"))
		{
			obj = ((!File.Exists(text + ".xnb")) ? content.Load<Texture2D>(imagePath) : content.Load<Texture2D>("../" + text));
		}
		else if (File.Exists(text))
		{
			using FileStream stream = File.OpenRead(text);
			obj = Texture2D.FromStream(Game1.getSingleton().GraphicsDevice, stream);
		}
		LoadComplete?.Invoke(obj);
	}

	public static float RobustReadAsFloat(XmlReader rdr)
	{
		string text = rdr.ReadContentAsString();
		text = text.Replace(",", ".");
		return (float)Convert.ToDouble(text, CultureInfo.InvariantCulture);
	}

	public static string[] GetQuoteSeperatedArgs(string[] args)
	{
		List<string> list = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < args.Length; i++)
		{
			stringBuilder.Append(args[i]);
			stringBuilder.Append(" ");
		}
		string text = stringBuilder.ToString().Trim();
		bool flag = false;
		StringBuilder stringBuilder2 = null;
		int num = -1;
		while (num < text.Length - 1)
		{
			num++;
			char c = text[num];
			if (stringBuilder2 == null)
			{
				if (flag && c == '"')
				{
					flag = false;
					continue;
				}
				if (!flag)
				{
					switch (c)
					{
					case '"':
						flag = true;
						continue;
					case ' ':
						continue;
					}
				}
				stringBuilder2 = new StringBuilder();
				stringBuilder2.Append(c);
			}
			else if (flag && c == '"')
			{
				list.Add(stringBuilder2.ToString());
				flag = false;
				stringBuilder2 = null;
			}
			else if (!flag && c == ' ')
			{
				list.Add(stringBuilder2.ToString());
				flag = false;
				stringBuilder2 = null;
			}
			else
			{
				stringBuilder2.Append(c);
			}
		}
		if (stringBuilder2 != null)
		{
			list.Add(stringBuilder2.ToString());
		}
		return list.ToArray();
	}

	public static SpriteFont GetTitleFontForLocalizedString(string data)
	{
		for (int i = 0; i < data.Length; i++)
		{
			if (!GuiData.titlefont.Characters.Contains(data[i]))
			{
				return GuiData.font;
			}
		}
		return GuiData.titlefont;
	}

	public static void DrawStringMonospace(SpriteBatch spriteBatch, string text, SpriteFont font, Vector2 pos, Color c, float charWidth)
	{
		for (int i = 0; i < text.Length; i++)
		{
			spriteBatch.DrawString(font, string.Concat(text[i]), ClipVec2ForTextRendering(pos), c);
			pos.X += charWidth;
		}
	}
}
