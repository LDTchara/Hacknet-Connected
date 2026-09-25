using System;
using System.Xml;
using Hacknet.Extensions;

namespace Hacknet;

public class SASwitchToTheme : SerializableAction
{
	public string ThemePathOrName;

	public float FlickerInDuration = 2f;

	public string DelayHost;

	public float Delay;

	public override void Trigger(object os_obj)
	{
		OS oS = (OS)os_obj;
		if (Delay <= 0f)
		{
			OSTheme result = OSTheme.Custom;
			if (!Enum.TryParse<OSTheme>(ThemePathOrName, out result))
			{
				result = OSTheme.Custom;
			}
			try
			{
				oS.EffectsUpdater.StartThemeSwitch(FlickerInDuration, result, oS, (result == OSTheme.Custom) ? ThemePathOrName : null);
				return;
			}
			catch (Exception ex)
			{
				oS.write(" ");
				oS.write("---------------------- ");
				oS.write("ERROR LOADING X-SERVER");
				oS.write(ex.ToString());
				oS.write(ex.Message);
				string text = ExtensionLoader.ActiveExtensionInfo.FolderPath + "/xserver_error.txt";
				Utils.writeToFile("x-server load error for theme : " + ThemePathOrName + "\r\n." + Utils.GenerateReportFromException(ex), text);
				oS.write("---------------------- ");
				oS.write("Full report saved to " + text);
				oS.write("---------------------- ");
				oS.write(" ");
				return;
			}
		}
		Computer computer = Programs.getComputer(oS, DelayHost);
		if (computer == null)
		{
			throw new NullReferenceException(string.Concat("Computer ", computer, " could not be found as DelayHost for Function"));
		}
		float delay = Delay;
		Delay = -1f;
		DelayableActionSystem.FindDelayableActionSystemOnComputer(computer).AddAction(this, delay);
	}

	public static SerializableAction DeserializeFromReader(XmlReader rdr)
	{
		SASwitchToTheme sASwitchToTheme = new SASwitchToTheme();
		if (rdr.MoveToAttribute("Delay"))
		{
			sASwitchToTheme.Delay = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("ThemePathOrName"))
		{
			sASwitchToTheme.ThemePathOrName = rdr.ReadContentAsString();
		}
		if (rdr.MoveToAttribute("FlickerInDuration"))
		{
			sASwitchToTheme.FlickerInDuration = rdr.ReadContentAsFloat();
		}
		if (rdr.MoveToAttribute("DelayHost"))
		{
			sASwitchToTheme.DelayHost = rdr.ReadContentAsString();
		}
		return sASwitchToTheme;
	}
}
