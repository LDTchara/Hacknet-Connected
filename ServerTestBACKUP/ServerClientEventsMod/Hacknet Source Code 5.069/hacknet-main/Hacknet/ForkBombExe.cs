using Microsoft.Xna.Framework;

namespace Hacknet;

internal class ForkBombExe : ExeModule
{
	public static float RAM_CHANGE_PS = 150f;

	private int targetRamUse = 999999999;

	public string runnerIP = "";

	public static string binary = "";

	public int binaryScroll = 0;

	public int charsWide = 0;

	public bool frameSwitch = false;

	public ForkBombExe(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		ramCost = 10;
		runnerIP = "UNKNOWN";
		IdentifierName = "ForkBomb";
	}

	public ForkBombExe(Rectangle location, OS operatingSystem, string ipFrom)
		: base(location, operatingSystem)
	{
		ramCost = 10;
		runnerIP = ipFrom;
		IdentifierName = "ForkBomb";
	}

	public override void LoadContent()
	{
		base.LoadContent();
		if (binary.Equals(""))
		{
			binary = Computer.generateBinaryString(5064);
		}
		float num = 7.55f;
		num = GuiData.detailfont.MeasureString("0").X - 0.15f;
		charsWide = (int)((float)bounds.Width / num + 0.5f);
	}

	public override void Killed()
	{
		TrackerCompleteSequence.NextCompleteForkbombShouldTrace = false;
		base.Killed();
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (frameSwitch)
		{
			binaryScroll++;
			if (binaryScroll >= binary.Length - (charsWide + 1))
			{
				binaryScroll = 0;
			}
		}
		frameSwitch = !frameSwitch;
		if (targetRamUse == ramCost)
		{
			return;
		}
		int num = (int)(t * RAM_CHANGE_PS);
		if (os.ramAvaliable < num)
		{
			Completed();
			return;
		}
		ramCost += num;
		if (ramCost > targetRamUse)
		{
			ramCost = targetRamUse;
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		float num = 8f;
		int num2 = binaryScroll;
		if (num2 >= binary.Length - (charsWide + 1))
		{
			num2 = 0;
		}
		Vector2 position = new Vector2(bounds.X, bounds.Y);
		while (position.Y < (float)(bounds.Y + bounds.Height) - num)
		{
			spriteBatch.DrawString(GuiData.detailfont, binary.Substring(num2, charsWide), position, Color.White);
			num2 += charsWide;
			if (num2 >= binary.Length - (charsWide + 1))
			{
				num2 = 0;
			}
			position.Y += num;
		}
	}

	public override void Completed()
	{
		base.Completed();
		if (TrackerCompleteSequence.NextCompleteForkbombShouldTrace)
		{
			TrackerCompleteSequence.NextCompleteForkbombShouldTrace = false;
			TrackerCompleteSequence.TriggerETAS(os);
			os.exes.Remove(this);
		}
		else
		{
			os.thisComputer.crash(runnerIP);
		}
	}
}
