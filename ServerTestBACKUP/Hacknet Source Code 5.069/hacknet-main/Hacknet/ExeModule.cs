using System;
using Hacknet.Gui;
using Microsoft.Xna.Framework;

namespace Hacknet;

internal class ExeModule : Module
{
	public static float FADEOUT_RATE = 0.5f;

	public static float MOVE_UP_RATE = 350f;

	public static int DEFAULT_RAM_COST = 246;

	public int PID = 0;

	public float fade = 1f;

	public bool isExiting = false;

	public bool needsRemoval = false;

	public float moveUpBy = 0f;

	public int ramCost = DEFAULT_RAM_COST;

	private int baseRamCost = 0;

	public string targetIP = "";

	public bool needsProxyAccess = false;

	public string IdentifierName = "UNKNOWN";

	public ExeModule(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		targetIP = ((operatingSystem.connectedComp == null) ? operatingSystem.thisComputer.ip : operatingSystem.connectedComp.ip);
		byte b = 0;
		bool flag = false;
		do
		{
			flag = false;
			b = Utils.getRandomByte();
			for (int i = 0; i < os.exes.Count; i++)
			{
				if (os.exes[i].PID == b)
				{
					flag = true;
					break;
				}
			}
		}
		while (flag);
		os.currentPID = b;
		PID = b;
		bounds = location;
	}

	public override void LoadContent()
	{
		bounds.Height = ramCost;
	}

	public override void Update(float t)
	{
		bounds.Height = ramCost;
		if (isExiting)
		{
			if (fade >= 1f)
			{
				baseRamCost = ramCost;
			}
			ramCost = (int)((float)baseRamCost * fade);
			bounds.Height = ramCost;
			fade -= t * FADEOUT_RATE;
			if (fade <= 0f)
			{
				needsRemoval = true;
			}
		}
		if (moveUpBy >= 0f)
		{
			int num = (int)(MOVE_UP_RATE * t);
			bounds.Y -= num;
			moveUpBy -= num;
			bounds.Y = Math.Max(bounds.Y, os.ram.bounds.Y + RamModule.contentStartOffset);
		}
	}

	public override void Draw(float t)
	{
	}

	public virtual void Completed()
	{
	}

	public virtual void Killed()
	{
	}

	public virtual void drawOutline()
	{
		Rectangle destinationRectangle = bounds;
		RenderedRectangle.doRectangleOutline(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height, 1, os.moduleColorSolid);
		destinationRectangle.X++;
		destinationRectangle.Y++;
		destinationRectangle.Width -= 2;
		destinationRectangle.Height -= 2;
		spriteBatch.Draw(Utils.white, destinationRectangle, os.moduleColorBacking * fade);
	}

	public virtual void drawTarget(string typeName = "app:")
	{
		if (bounds.Height > 14)
		{
			string text = "IP: " + targetIP;
			Rectangle destinationRectangle = bounds;
			destinationRectangle.Height = Math.Min(bounds.Height, 14);
			destinationRectangle.X++;
			destinationRectangle.Width -= 2;
			spriteBatch.Draw(Utils.white, destinationRectangle, os.exeModuleTopBar);
			RenderedRectangle.doRectangleOutline(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height, 1, os.topBarColor);
			if (destinationRectangle.Height >= 14)
			{
				Vector2 vector = GuiData.detailfont.MeasureString(text);
				spriteBatch.DrawString(GuiData.detailfont, text, new Vector2((float)(bounds.X + bounds.Width) - vector.X, bounds.Y), os.exeModuleTitleText);
				spriteBatch.DrawString(GuiData.detailfont, typeName + IdentifierName, new Vector2(bounds.X + 2, bounds.Y), os.exeModuleTitleText);
			}
		}
	}

	public Rectangle GetContentAreaDest()
	{
		return new Rectangle(bounds.X + 1, bounds.Y + Module.PANEL_HEIGHT, bounds.Width - 2, bounds.Height - Module.PANEL_HEIGHT - 1);
	}
}
