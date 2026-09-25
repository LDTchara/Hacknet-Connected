using System;
using System.Collections.Generic;
using System.Text;
using AlienFXManagedWrapper;
using Microsoft.Xna.Framework;

namespace Hacknet.PlatformAPI;

public static class AlienwareFXManager
{
	private const double MIN_SECONDS_BETWEEN_UPDATES = 0.1;

	public static bool IsRunning = false;

	private static ILightFXController LightFX;

	private static bool HasUpdatedPostFlash = false;

	private static uint numDevices;

	private static List<uint> deviceLightCounts = new List<uint>();

	private static List<List<string>> deviceLightDescriptions = new List<List<string>>();

	private static DateTime TimeStarted;

	private static DateTime LastUpdateTime;

	private static Color LastLogoColor;

	private static Color LastMidKeyColor;

	private static Color LastOutKeyColor;

	private static Color LastOtherColor;

	public static void Init()
	{
		LightFX = new LightFXController();
		if (LightFX.LFX_Initialize() != 0)
		{
			return;
		}
		LightFX.LFX_GetNumDevices(out numDevices);
		for (uint num = 0u; num < numDevices; num++)
		{
			LightFX.LFX_GetNumLights(num, out var numLights);
			deviceLightCounts.Add(numLights);
			List<string> list = new List<string>();
			StringBuilder stringBuilder = new StringBuilder(255);
			for (uint num2 = 0u; num2 < numLights; num2++)
			{
				LightFX.LFX_GetLightDescription(num, num2, (StringBuilder)(object)stringBuilder, 255);
				list.Add(stringBuilder.ToString());
			}
			deviceLightDescriptions.Add(list);
		}
		TimeStarted = DateTime.Now;
		IsRunning = true;
	}

	public static void UpdateForOS(object OS_Obj)
	{
		if (IsRunning)
		{
			if (OS_Obj != null)
			{
				OS oS = (OS)OS_Obj;
				_ = oS.warningFlashTimer;
				bool flag = 1 == 0;
				UpdateLightsForThemeAndFlash(oS);
				HasUpdatedPostFlash = false;
			}
			else
			{
				double totalSeconds = (DateTime.Now - TimeStarted).TotalSeconds;
				CycleAllLights(Color.Red, Color.White, (float)totalSeconds);
			}
		}
	}

	private static void UpdateLightsForThemeAndFlash(OS os)
	{
		if (os.thisComputer.disabled)
		{
			UpdateLightColors(Color.Black, Color.Black, Color.Black, Color.Black);
			return;
		}
		float amount = Math.Max(0f, Math.Min(1f, os.warningFlashTimer));
		Color value = Color.Lerp(os.AFX_KeyboardOuter, Color.Red, amount);
		value = Color.Lerp(value, Color.White, os.PorthackCompleteFlashTime / PortHackExe.COMPLETE_LIGHT_FLASH_TIME);
		Color value2 = Color.Lerp(os.AFX_WordLogo, Color.Red, amount);
		value2 = Color.Lerp(value2, Color.White, os.MissionCompleteFlashTime / 3f);
		UpdateLightColors(value2, Color.Lerp(os.AFX_KeyboardMiddle, Color.Red, amount), value, Color.Lerp(os.AFX_Other, Color.Red, amount));
	}

	private static void CycleAllLights(Color from, Color to, float time)
	{
		double totalSeconds = (DateTime.Now - LastUpdateTime).TotalSeconds;
		if (!(totalSeconds >= 0.1))
		{
			return;
		}
		for (uint num = 0u; num < numDevices; num++)
		{
			for (uint num2 = 0u; num2 < deviceLightCounts[(int)num]; num2++)
			{
				float num3 = (float)Math.Sin((double)time / ((double)num2 / 2.0));
				num3 = (num3 + 1f) / 2f;
				Color c = Color.Lerp(from, to, num3);
				LFX_ColorStruct lightCol = Col2LFXC(c);
				LightFX.LFX_SetLightColor(num, num2, ref lightCol);
			}
		}
		LightFX.LFX_Update();
		LastUpdateTime = DateTime.Now;
	}

	private static void UpdateLightColors(Color LogoColor, Color KeyboardMiddleColor, Color KeyboardOuterColor, Color StatusColor)
	{
		if (LogoColor == LastLogoColor && KeyboardMiddleColor == LastMidKeyColor && KeyboardOuterColor == LastOutKeyColor && StatusColor == LastOtherColor)
		{
			return;
		}
		double totalSeconds = (DateTime.Now - LastUpdateTime).TotalSeconds;
		if (!(totalSeconds >= 0.1))
		{
			return;
		}
		for (uint num = 0u; num < numDevices; num++)
		{
			for (uint num2 = 0u; num2 < deviceLightCounts[(int)num]; num2++)
			{
				LFX_ColorStruct lightCol = Col2LFXC(LogoColor);
				string text = deviceLightDescriptions[(int)num][(int)num2].ToLower();
				if (text.Contains("keyboard"))
				{
					lightCol = ((!text.Contains("middle")) ? Col2LFXC(KeyboardOuterColor) : Col2LFXC(KeyboardMiddleColor));
				}
				else if (text.Contains("status"))
				{
					lightCol = Col2LFXC(StatusColor);
				}
				LightFX.LFX_SetLightColor(num, num2, ref lightCol);
			}
		}
		LightFX.LFX_Update();
		LastLogoColor = LogoColor;
		LastMidKeyColor = KeyboardMiddleColor;
		LastOutKeyColor = KeyboardOuterColor;
		LastOtherColor = StatusColor;
		LastUpdateTime = DateTime.Now;
	}

	private static LFX_ColorStruct Col2LFXC(Color c)
	{
		float num = (float)(c.R + c.G + c.B) / 765f;
		LFX_ColorStruct result = default(LFX_ColorStruct);
		result.red = c.R;
		result.green = c.G;
		result.blue = c.B;
		result.brightness = byte.MaxValue;
		return result;
	}

	public static void ReleaseHandle()
	{
		if (IsRunning)
		{
			LightFX.LFX_Release();
		}
	}
}
