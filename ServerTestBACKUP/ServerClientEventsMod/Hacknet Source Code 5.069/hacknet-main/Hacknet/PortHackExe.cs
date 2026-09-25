using System;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class PortHackExe : ExeModule
{
	public static float CRACK_TIME = 6f;

	public static float TIME_BETWEEN_TEXT_SWITCH = 0.06f;

	public static float TIME_ALIVE_AFTER_SUCSESS = 5f;

	public static float COMPLETE_LIGHT_FLASH_TIME = 2f;

	private float progress = 0f;

	private int[] textIndex;

	private float textSwitchTimer = TIME_BETWEEN_TEXT_SWITCH;

	private int textOffsetIndex = 0;

	private float sucsessTimer = TIME_ALIVE_AFTER_SUCSESS;

	private bool hasCompleted = false;

	private Computer target;

	private PortHackCubeSequence cubeSeq = new PortHackCubeSequence();

	private RenderTarget2D renderTarget;

	private bool IsTargetingPorthackHeart = false;

	private bool hasCheckedForheart = false;

	private bool StopProgress = false;

	public PortHackExe(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
		IdentifierName = "PortHack";
	}

	public override void LoadContent()
	{
		base.LoadContent();
		int num = PortExploits.passwords.Count / 3;
		textIndex = new int[3];
		textIndex[0] = 0;
		textIndex[1] = num;
		textIndex[2] = 2 * num;
		target = Programs.getComputer(os, targetIP);
		os.write("Porthack Initialized -- Running...");
	}

	public override void Update(float t)
	{
		base.Update(t);
		if (IsTargetingPorthackHeart)
		{
			progress = Utils.rand(0.98f);
		}
		else if (!StopProgress)
		{
			progress += t / CRACK_TIME;
		}
		if (progress >= 1f)
		{
			progress = 1f;
			if (!hasCompleted)
			{
				Completed();
				hasCompleted = true;
			}
			sucsessTimer -= t;
			if (sucsessTimer <= 0f)
			{
				isExiting = true;
			}
		}
		else
		{
			target.hostileActionTaken();
		}
		if (progress < 1f)
		{
			textSwitchTimer -= t;
			if (textSwitchTimer <= 0f)
			{
				textSwitchTimer = TIME_BETWEEN_TEXT_SWITCH;
				textOffsetIndex++;
			}
		}
		if (!hasCheckedForheart && progress > 0.5f)
		{
			hasCheckedForheart = true;
			if (target.getDaemon(typeof(PorthackHeartDaemon)) is PorthackHeartDaemon porthackHeartDaemon)
			{
				IsTargetingPorthackHeart = true;
				if (os.connectedComp != null && os.connectedComp.ip == target.ip)
				{
					porthackHeartDaemon.BreakHeart();
				}
				cubeSeq.ShouldCentralSpinInfinitley = true;
			}
		}
		Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
		if (computer.ip != target.ip)
		{
			StopProgress = true;
			isExiting = true;
		}
	}

	public override void Completed()
	{
		base.Completed();
		os.takeAdmin(targetIP);
		os.write("--Porthack Complete--");
		os.PorthackCompleteFlashTime = COMPLETE_LIGHT_FLASH_TIME;
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		Rectangle destinationRectangle = new Rectangle(bounds.X + 1, bounds.Y + Module.PANEL_HEIGHT + 1, bounds.Width - 2, bounds.Height - (Module.PANEL_HEIGHT + 2));
		if (renderTarget == null)
		{
			renderTarget = new RenderTarget2D(spriteBatch.GraphicsDevice, destinationRectangle.Width, destinationRectangle.Height);
		}
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		spriteBatch.GraphicsDevice.SetRenderTarget(renderTarget);
		spriteBatch.GraphicsDevice.Clear(Color.Transparent);
		try
		{
			Rectangle dest = new Rectangle(0, 0, destinationRectangle.Width, destinationRectangle.Height);
			cubeSeq.DrawSequence(dest, t, CRACK_TIME);
		}
		catch (Exception ex)
		{
			Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex) + "\r\n\r\n");
		}
		spriteBatch.GraphicsDevice.SetRenderTarget(currentRenderTarget);
		Rectangle rectangle = bounds;
		rectangle.X++;
		rectangle.Y++;
		rectangle.Width -= 2;
		rectangle.Height -= 2;
		drawOutline();
		spriteBatch.Draw(renderTarget, destinationRectangle, Utils.AddativeWhite * ((progress >= 1f) ? 0.2f : 0.6f));
		drawTarget();
		if (progress < 1f)
		{
			Rectangle destinationRectangle2 = new Rectangle(bounds.X, bounds.Y + Module.PANEL_HEIGHT + 1, (int)((float)bounds.Width / 2f), bounds.Height - (Module.PANEL_HEIGHT + 2));
			spriteBatch.Draw(Utils.gradientLeftRight, destinationRectangle2, null, Color.Black * 0.9f, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0.2f);
			destinationRectangle2.X += bounds.Width - destinationRectangle2.Width - 1;
			spriteBatch.Draw(Utils.gradientLeftRight, destinationRectangle2, null, Color.Black * 0.9f, 0f, Vector2.Zero, SpriteEffects.None, 0.3f);
		}
		int num = bounds.Height - 16 - 16 - 4;
		num /= 12;
		Vector2 position = new Vector2(bounds.X + 3, bounds.Y + 20);
		Color color = Color.White * fade;
		if (IsTargetingPorthackHeart)
		{
			color *= 0.5f;
		}
		if (progress >= 1f)
		{
			color = Color.Gray * fade;
		}
		for (int i = 0; i < num; i++)
		{
			int index = (textIndex[0] + textOffsetIndex + i) % (PortExploits.passwords.Count - 1);
			spriteBatch.DrawString(GuiData.UITinyfont, PortExploits.passwords[index], position, color);
			index = (textIndex[1] + textOffsetIndex + i) % (PortExploits.passwords.Count - 1);
			Vector2 vector = GuiData.UITinyfont.MeasureString(PortExploits.passwords[index]);
			position.X = (float)(bounds.X + bounds.Width) - vector.X - 3f;
			Rectangle destinationRectangle3 = new Rectangle((int)position.X - 1, (int)position.Y, (int)vector.X, 12);
			if (Settings.ActiveLocale != "en-us")
			{
				destinationRectangle3.Y += 6;
			}
			spriteBatch.Draw(Utils.white, destinationRectangle3, Color.Black * 0.8f);
			spriteBatch.DrawString(GuiData.UITinyfont, PortExploits.passwords[index], position, color);
			position.X = bounds.X + 3;
			position.Y += 12f;
		}
		if (progress >= 1f)
		{
			string text = "PASSWORD";
			Vector2 vector2 = GuiData.font.MeasureString(text);
			position.X = (float)(bounds.X + bounds.Width / 2) - vector2.X / 2f;
			position.Y = (float)(bounds.Y + bounds.Height / 2) - vector2.Y;
			float num2 = Utils.QuadraticOutCurve(Math.Min(2f, TIME_ALIVE_AFTER_SUCSESS - sucsessTimer) / 2f);
			Rectangle destinationRectangle4 = new Rectangle(0, (int)position.Y - 3, (int)(num2 * (float)bounds.Width - 30f), 2);
			destinationRectangle4.X = bounds.X + (bounds.Width / 2 - destinationRectangle4.Width / 2);
			if (destinationRectangle4.Y > bounds.Y)
			{
				spriteBatch.Draw(Utils.white, destinationRectangle4, Utils.AddativeWhite * fade);
			}
			spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(text, 0.012), position, Color.White * fade);
			spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(text, 0.11), position, Utils.AddativeWhite * 0.2f * fade * fade);
			text = "FOUND";
			vector2 = GuiData.font.MeasureString(text);
			position.X = (float)(bounds.X + bounds.Width / 2) - vector2.X / 2f;
			position.Y += 35f;
			spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(text, 0.012), position, Color.White * fade);
			spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(text, 0.11), position, Utils.AddativeWhite * 0.2f * fade * fade);
			destinationRectangle4.Y = (int)(position.Y + 35f + 3f);
			if (destinationRectangle4.Y > bounds.Y)
			{
				spriteBatch.Draw(Utils.white, destinationRectangle4, Utils.AddativeWhite * fade);
			}
		}
		rectangle.X += 2;
		rectangle.Width -= 4;
		rectangle.Y = bounds.Y + bounds.Height - 2 - 16;
		rectangle.Height = 16;
		spriteBatch.Draw(Utils.white, rectangle, os.outlineColor * fade);
		rectangle.X++;
		rectangle.Y++;
		rectangle.Width -= 2;
		rectangle.Height -= 2;
		if (IsTargetingPorthackHeart)
		{
			spriteBatch.Draw(Utils.white, rectangle, Color.DarkRed * (Utils.rand(0.3f) + 0.7f) * fade);
			TextItem.doFontLabelToSize(rectangle, LocaleTerms.Loc("UNKNOWN ERROR"), GuiData.font, Utils.AddativeWhite);
		}
		else
		{
			spriteBatch.Draw(Utils.white, rectangle, os.darkBackgroundColor * fade);
			rectangle.Width = (int)((float)rectangle.Width * progress);
			spriteBatch.Draw(Utils.white, rectangle, os.highlightColor * fade);
		}
	}
}
