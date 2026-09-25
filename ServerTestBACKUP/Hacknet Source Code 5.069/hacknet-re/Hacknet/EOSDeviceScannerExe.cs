using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class EOSDeviceScannerExe : ExeModule
{
	private const float TOTAL_TIME = 8f;

	private const float SHORTCUT_TIME = 3.5f;

	private const float timeBetweenBounces = 0.07f;

	private List<Vector2> locations = new List<Vector2>();

	private float timeToNextBounce = 0f;

	private List<string> ResultTitles = new List<string>();

	private List<string> ResultBodies = new List<string>();

	private float timer = 0f;

	private Computer targetComp;

	private bool IsComplete = false;

	private int devicesFound = 0;

	private bool isError = false;

	private string errorMessage = null;

	public EOSDeviceScannerExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "eOS_DeviceScanner";
		ramCost = 300;
		IdentifierName = "eOS Device Scanner";
		targetComp = os.connectedComp;
		if (targetComp == null)
		{
			targetComp = os.thisComputer;
		}
		locations.Add(new Vector2(Utils.rand(), Utils.rand()));
		locations.Add(new Vector2(Utils.rand(), Utils.rand()));
		if (!os.hasConnectionPermission(admin: true))
		{
			isError = true;
			errorMessage = LocaleTerms.Loc("ADMIN ACCESS\nREQUIRED FOR SCAN");
			IsComplete = true;
			for (int i = 0; i < 30; i++)
			{
				locations.Add(new Vector2(Utils.rand(), Utils.rand()));
			}
		}
	}

	public override void Update(float t)
	{
		base.Update(t);
		timer += t;
		bool flag = targetComp.attatchedDeviceIDs == null;
		if ((timer > 8f || (flag && timer > 3.5f)) && !IsComplete)
		{
			Completed();
		}
		if (timer < 8f && errorMessage == null)
		{
			timeToNextBounce -= t;
			if (timeToNextBounce <= 0f)
			{
				locations.Add(new Vector2(Utils.rand(), Utils.rand()));
				timeToNextBounce = 0.07f;
			}
		}
	}

	public override void Completed()
	{
		base.Completed();
		IsComplete = true;
		if (targetComp.attatchedDeviceIDs != null)
		{
			string[] array = targetComp.attatchedDeviceIDs.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			float num = 0f;
			for (int i = 0; i < array.Length; i++)
			{
				Computer device = Programs.getComputer(os, array[i]);
				if (device == null)
				{
					continue;
				}
				Action action = delegate
				{
					os.netMap.discoverNode(device);
					Vector2 loc = os.netMap.GetNodeDrawPos(device) + new Vector2(os.netMap.bounds.X, os.netMap.bounds.Y) + new Vector2(NetworkMap.NODE_SIZE / 2);
					SFX.addCircle(loc, os.highlightColor, 120f);
					os.delayer.Post(ActionDelayer.Wait(0.2), delegate
					{
						SFX.addCircle(loc, os.highlightColor, 80f);
					});
					os.delayer.Post(ActionDelayer.Wait(0.4), delegate
					{
						SFX.addCircle(loc, os.highlightColor, 65f);
					});
					string text = string.Format(LocaleTerms.Loc("eOS Device \"{0}\" opened for connection at {1}"), device.name, device.ip);
					os.write(text);
					ResultTitles.Add(device.name);
					ResultBodies.Add(device.ip + " " + device.location.ToString() + "\n" + Guid.NewGuid().ToString());
				};
				os.delayer.Post(ActionDelayer.Wait(num), action);
				num += 1f;
				devicesFound++;
			}
		}
		if (devicesFound == 0)
		{
			isError = true;
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		bool drawShadow = TextItem.DrawShadow;
		TextItem.DrawShadow = false;
		Vector2 vector = locations[0];
		Vector2 vector2 = new Vector2((float)bounds.X + 2f, (float)bounds.Y + 26f);
		Vector2 vector3 = new Vector2((float)bounds.Width - 4f, (float)bounds.Height - 30f);
		if (vector3.X > 0f && vector3.Y > 0f)
		{
			for (int i = 1; i < locations.Count; i++)
			{
				Vector2 vector4 = locations[i];
				if (i == locations.Count - 1)
				{
					vector4 = Vector2.Lerp(vector, vector4, 1f - timeToNextBounce / 0.07f);
				}
				Utils.drawLine(spriteBatch, vector2 + vector * vector3, vector2 + vector4 * vector3, Vector2.Zero, (isError ? Utils.AddativeRed : Utils.AddativeWhite) * 0.5f * ((float)i / (float)locations.Count), 0.4f);
				vector = locations[i];
			}
			for (int i = 1; i < locations.Count; i++)
			{
				spriteBatch.Draw(Utils.white, locations[i] * vector3 + vector2, Utils.AddativeWhite);
			}
		}
		SpriteFont font = ((Settings.ActiveLocale == "en-us") ? GuiData.titlefont : GuiData.font);
		if (IsComplete)
		{
			bool flag = errorMessage != null;
			Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + 26, bounds.Width - 2, bounds.Height - 28);
			spriteBatch.Draw(Utils.white, rectangle, Color.Black * 0.7f);
			Rectangle rectangle2 = rectangle;
			rectangle2.Height = Math.Min(35, bounds.Height / 5);
			string input = (flag ? "ERROR" : "SCAN COMPLETE");
			input = LocaleTerms.Loc(input);
			TextItem.doFontLabel(new Vector2(rectangle2.X, rectangle2.Y), input, font, os.highlightColor, rectangle2.Width, rectangle2.Height);
			TextItem.doFontLabel(new Vector2(rectangle2.X, rectangle2.Y), Utils.FlipRandomChars(input, 0.1), font, Utils.AddativeWhite * (0.1f * Utils.rand()), rectangle2.Width, rectangle2.Height);
			Rectangle destinationRectangle = new Rectangle(rectangle2.X, rectangle2.Y + rectangle2.Height, rectangle2.Width, 1);
			spriteBatch.Draw(Utils.white, destinationRectangle, Utils.AddativeWhite * 0.5f);
			if (!isExiting)
			{
				string text = string.Format(LocaleTerms.Loc("DEVICES FOUND : {0}"), devicesFound);
				if (flag)
				{
					text = errorMessage;
				}
				Vector2 pos = new Vector2(rectangle2.X + 2, rectangle2.Y + rectangle2.Height + 2);
				TextItem.doFontLabel(pos, text, font, ((devicesFound > 0) ? Utils.AddativeWhite : Color.Red) * 0.8f, bounds.Width - 10, flag ? ((float)bounds.Height * 0.8f) : 23f);
				pos.Y += 25f;
				for (int i = 0; i < ResultTitles.Count; i++)
				{
					if (pos.Y - (float)bounds.Y + 60f > (float)bounds.Height)
					{
						break;
					}
					TextItem.doFontLabel(pos, Utils.FlipRandomChars(ResultTitles[i], 0.01), GuiData.font, Color.Lerp(os.highlightColor, Utils.AddativeWhite, 0.2f + 0.1f * Utils.rand()), bounds.Width - 10, 24f);
					pos.Y += 22f;
					TextItem.doFontLabel(pos, ResultBodies[i], GuiData.detailfont, Utils.AddativeWhite * 0.85f, bounds.Width - 10, 30f);
					pos.Y += 30f;
					pos.Y += 4f;
				}
			}
			if (!isExiting && Button.doButton(646464029 + PID, bounds.X + 2, bounds.Y + bounds.Height - 2 - 20, bounds.Width - 50, 20, LocaleTerms.Loc("Exit"), os.lockedColor))
			{
				isExiting = true;
			}
		}
		else
		{
			int num = Math.Min(38, bounds.Height / 3);
			Rectangle rectangle3 = new Rectangle(bounds.X + 1, bounds.Y + bounds.Height / 2 - num / 2, bounds.Width - 2, num);
			spriteBatch.Draw(Utils.white, rectangle3, Color.Black * 0.7f);
			TextItem.doFontLabelToSize(rectangle3, Utils.FlipRandomChars(LocaleTerms.Loc("SCANNING"), 0.009), font, IsComplete ? os.highlightColor : (Utils.AddativeWhite * 0.8f));
			TextItem.doFontLabelToSize(rectangle3, Utils.FlipRandomChars(LocaleTerms.Loc("SCANNING"), 0.15), font, IsComplete ? os.highlightColor : (Utils.AddativeWhite * (0.18f * Utils.rand())));
		}
		TextItem.DrawShadow = drawShadow;
	}
}
