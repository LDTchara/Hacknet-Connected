using Microsoft.Xna.Framework;

namespace Hacknet;

internal class MedicalPortExe : ExeModule
{
	private const float RUNTIME = 22f;

	private const float COMPLETE_TIME = 2f;

	private float elapsedTime = 0f;

	private float sucsessTimer = 0f;

	private Color[] displayData;

	private bool[] CompletedIndexes;

	private Color DarkBaseColor = new Color(5, 0, 36);

	private Color LightBaseColor = new Color(39, 32, 83);

	private Color DarkFinColor = new Color(179, 25, 94);

	private Color LightFinColor = new Color(225, 14, 79);

	public MedicalPortExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		needsProxyAccess = true;
		name = "KBT_PortTest";
		ramCost = 400;
		IdentifierName = "KBT Port Tester";
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (sucsessTimer <= 0f)
		{
			elapsedTime += t;
			if (elapsedTime >= 22f)
			{
				Complete();
			}
		}
		else
		{
			sucsessTimer -= t;
			if (sucsessTimer <= 0f)
			{
				isExiting = true;
			}
		}
		if (displayData == null)
		{
			InitializeDisplay();
		}
		UpdateDisplay();
	}

	private void InitializeDisplay()
	{
		displayData = new Color[bounds.Height - 4 - Module.PANEL_HEIGHT];
		CompletedIndexes = new bool[displayData.Length];
		for (int i = 0; i < displayData.Length; i++)
		{
			CompletedIndexes[i] = false;
		}
	}

	private void UpdateDisplay()
	{
		float num = 22f / (float)displayData.Length;
		if (elapsedTime % num < 0.02f)
		{
			int num2 = 0;
			int num3;
			do
			{
				num3 = Utils.random.Next(displayData.Length);
				num2++;
			}
			while (CompletedIndexes[num3] && num2 < bounds.Height * 2);
			CompletedIndexes[num3] = true;
		}
		int num4 = Utils.random.Next(displayData.Length);
		if (CompletedIndexes[num4])
		{
			ref Color reference = ref displayData[num4];
			reference = Color.Lerp(displayData[num4], LightBaseColor, Utils.rand(0.5f));
		}
		for (int i = 0; i < displayData.Length; i++)
		{
			if (CompletedIndexes[i])
			{
				ref Color reference2 = ref displayData[i];
				reference2 = Color.Lerp(displayData[i], Color.Lerp(DarkFinColor, LightFinColor, Utils.rand(1f)), 0.05f);
			}
			else
			{
				ref Color reference3 = ref displayData[i];
				reference3 = Color.Lerp(displayData[i], Color.Lerp(DarkBaseColor, LightBaseColor, Utils.rand(1f)), 0.05f);
			}
		}
	}

	private void Complete()
	{
		Programs.getComputer(os, targetIP)?.openPort(104, os.thisComputer.ip);
		sucsessTimer = 2f;
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		if (displayData == null)
		{
			return;
		}
		Rectangle destinationRectangle = bounds;
		destinationRectangle.X++;
		destinationRectangle.Width -= 2;
		destinationRectangle.Y += 2 + Module.PANEL_HEIGHT;
		destinationRectangle.Height = 1;
		int width = destinationRectangle.Width;
		for (int i = 0; i < displayData.Length; i++)
		{
			float num = 0.85f * (1f - elapsedTime / 22f);
			float num2 = 0.95f - num;
			if (CompletedIndexes[i])
			{
				destinationRectangle.Width = (int)((float)width * (Utils.rand(num) + num2));
			}
			else
			{
				destinationRectangle.Width = width;
			}
			spriteBatch.Draw(Utils.white, destinationRectangle, displayData[i]);
			destinationRectangle.Y++;
			if (destinationRectangle.Y > bounds.Y + bounds.Height - 2)
			{
				break;
			}
		}
	}
}
