using System;

namespace Hacknet.Effects;

public class ActiveEffectsUpdater
{
	private bool ScreenBleedActive = false;

	private float ScreenBleedTimeLeft = 0f;

	private float ScreenBleedStartTime = 0f;

	private string screenBleedTitle = "UNKNOWN";

	private string screenBleedL1;

	private string screenBleedL2;

	private string screenBleedL3;

	private string screenBleedCompleteAction = null;

	private OSTheme oldTheme;

	private string oldThemePath;

	private CustomTheme oldCustomTheme;

	private OSTheme newTheme;

	private string newThemePath;

	private CustomTheme newCustomTheme;

	private float themeSwapTimeRemaining;

	private float originalThemeSwapTime;

	public void Update(float dt, object osobj)
	{
		OS os = (OS)osobj;
		if (themeSwapTimeRemaining > 0f)
		{
			themeSwapTimeRemaining -= dt;
			if (themeSwapTimeRemaining <= 0f)
			{
				CompleteThemeSwap(os);
				return;
			}
			bool flag = Utils.randm(1f) > themeSwapTimeRemaining / originalThemeSwapTime;
			OSTheme theme = (flag ? newTheme : oldTheme);
			string text = (flag ? newThemePath : oldThemePath);
			ThemeManager.LastLoadedCustomTheme = (flag ? newCustomTheme : oldCustomTheme);
			if (text != null)
			{
				ThemeManager.switchTheme(os, text);
			}
			else
			{
				ThemeManager.switchTheme(os, theme);
			}
		}
		if (!ScreenBleedActive)
		{
			return;
		}
		ScreenBleedTimeLeft -= dt;
		float val = 1f - ScreenBleedTimeLeft / ScreenBleedStartTime;
		PostProcessor.dangerModePercentComplete = Math.Max(0f, Math.Min(val, 1f));
		PostProcessor.dangerModeEnabled = true;
		if (ScreenBleedTimeLeft <= 0f)
		{
			ScreenBleedActive = false;
			PostProcessor.dangerModePercentComplete = 0f;
			PostProcessor.dangerModeEnabled = false;
			if (!string.IsNullOrWhiteSpace(screenBleedCompleteAction))
			{
				RunnableConditionalActions.LoadIntoOS(screenBleedCompleteAction, os);
			}
		}
		else
		{
			OS oS = os;
			oS.postFXDrawActions = (Action)Delegate.Combine(oS.postFXDrawActions, (Action)delegate
			{
				TraceDangerSequence.DrawCountdownOverlay(Utils.CheckStringIsTitleRenderable(screenBleedTitle) ? GuiData.titlefont : GuiData.font, GuiData.smallfont, os, screenBleedTitle, screenBleedL1, screenBleedL2, screenBleedL3);
			});
		}
	}

	public void StartThemeSwitch(float time, OSTheme newTheme, object osobj, string customThemePath = null)
	{
		oldTheme = ThemeManager.currentTheme;
		oldThemePath = ((oldTheme == OSTheme.Custom) ? ThemeManager.LastLoadedCustomThemePath : null);
		oldCustomTheme = ((oldTheme == OSTheme.Custom) ? ThemeManager.LastLoadedCustomTheme : null);
		if (themeSwapTimeRemaining > 0f)
		{
			CompleteThemeSwap(osobj);
		}
		originalThemeSwapTime = (themeSwapTimeRemaining = time);
		this.newTheme = newTheme;
		newThemePath = customThemePath;
		try
		{
			if (time <= 0f)
			{
				CompleteThemeSwap(osobj);
			}
			else if (newThemePath != null)
			{
				ThemeManager.switchTheme(osobj, newThemePath);
				newCustomTheme = ThemeManager.LastLoadedCustomTheme;
			}
			else
			{
				newCustomTheme = null;
			}
		}
		catch (Exception ex)
		{
			time = (themeSwapTimeRemaining = 0f);
			throw ex;
		}
	}

	public void CompleteThemeSwap(object osobj)
	{
		OS oS = (OS)osobj;
		if (newThemePath != null)
		{
			ThemeManager.setThemeOnComputer(oS.thisComputer, newThemePath);
			ThemeManager.switchTheme(oS, newThemePath);
		}
		else
		{
			ThemeManager.setThemeOnComputer(oS.thisComputer, newTheme);
			ThemeManager.switchTheme(oS, newTheme);
		}
	}

	public void StartScreenBleed(float time, string title, string line1, string line2, string line3, string completeAction)
	{
		ScreenBleedStartTime = (ScreenBleedTimeLeft = time);
		screenBleedTitle = title;
		screenBleedL1 = line1;
		screenBleedL2 = line2;
		screenBleedL3 = line3;
		screenBleedCompleteAction = completeAction;
		ScreenBleedActive = true;
	}

	public void CancelScreenBleedEffect()
	{
		ScreenBleedActive = false;
		PostProcessor.dangerModePercentComplete = 0f;
		PostProcessor.dangerModeEnabled = false;
	}
}
