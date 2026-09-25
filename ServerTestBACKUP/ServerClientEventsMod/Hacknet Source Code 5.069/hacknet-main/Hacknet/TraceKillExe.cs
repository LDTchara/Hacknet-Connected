using System;
using System.Collections.Generic;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class TraceKillExe : ExeModule
{
	public struct PointImpactEffect
	{
		public const float TransInTime = 1f;

		public const float TransOutTime = 2f;

		public ConnectedNodeEffect cne;

		public float timeEnabled;

		public Vector2 location;

		public float scaleModifier;

		public bool HasHighlightCircle;

		public PointImpactEffect(Vector2 location, OS os)
		{
			cne = new ConnectedNodeEffect(os, intense: true);
			timeEnabled = 0f;
			this.location = location;
			scaleModifier = 1f;
			HasHighlightCircle = true;
		}
	}

	private const float TIME_BETWEEN_FOCUS_POINTS = 4f;

	private const float FOCUS_POINT_TRANSITION_TIME = 1.2f;

	private const float MAX_FOCUS_POINT_IDLE_TIME = 0.1f;

	private static Color BoatBarColor = new Color(0, 0, 0, 200);

	private RenderTarget2D effectTarget;

	private SpriteBatch effectSB;

	private SoundEffect traceKillSound;

	private Vector2 EffectFocus = new Vector2(0.5f);

	private float timer = 0f;

	private Vector2 focusPointLocation = Vector2.Zero;

	private float timeTillNextFocusPoint = 1f;

	private bool isOnFocusPoint = false;

	private float focusPointTransitionTime = 0f;

	private float timeOnFocusPoint = 0f;

	private float focusPointIdleTime = 0f;

	private bool hasDoneBurstForThisFocusPoint = false;

	private List<PointImpactEffect> ImpactEffects = new List<PointImpactEffect>();

	private Texture2D circle;

	private BarcodeEffect BotBarcode;

	private float traceActivityTimer = 0f;

	public TraceKillExe(Rectangle location, OS operatingSystem, string[] p)
		: base(location, operatingSystem)
	{
		name = "TraceKill";
		ramCost = 600;
		IdentifierName = "TraceKill";
		targetIP = os.thisComputer.ip;
		effectSB = new SpriteBatch(GuiData.spriteBatch.GraphicsDevice);
		circle = os.content.Load<Texture2D>("Circle");
		traceKillSound = os.content.Load<SoundEffect>("SFX/TraceKill");
	}

	public override void Update(float t)
	{
		base.Update(t);
		UpdateEffect(t);
		bool flag = os.connectedComp != null && os.connectedComp.idName == "dGibson";
		if (!isExiting && !flag)
		{
			if (os.traceTracker.timeSinceFreezeRequest > 0.25f)
			{
				traceKillSound.Play();
			}
			os.traceTracker.timeSinceFreezeRequest = 0f;
		}
		if (BotBarcode != null)
		{
			BotBarcode.Update(t * (os.traceTracker.active ? 3f : 2f));
		}
		if (os.traceTracker.active)
		{
			traceActivityTimer += t;
		}
		else
		{
			traceActivityTimer = 0f;
		}
	}

	private void UpdateEffect(float t)
	{
		float num = 1f;
		if (os.traceTracker.active)
		{
			num = 1.6f;
		}
		timer += t * num;
		Vector2 vector = new Vector2((float)Math.Sin((double)timer * 2.0), (float)Math.Sin(timer)) * 0.4f + new Vector2(0.5f);
		if (isOnFocusPoint && focusPointTransitionTime >= 1.2f)
		{
			EffectFocus = focusPointLocation;
			timeOnFocusPoint += t;
			if (!hasDoneBurstForThisFocusPoint)
			{
				Vector2 location = focusPointLocation;
				PointImpactEffect item = new PointImpactEffect(location, os);
				ImpactEffects.Add(item);
				hasDoneBurstForThisFocusPoint = true;
			}
			if (timeOnFocusPoint >= focusPointIdleTime)
			{
				isOnFocusPoint = false;
				focusPointTransitionTime = 1.2f;
				timeTillNextFocusPoint = Utils.randm(4f);
			}
		}
		else if ((isOnFocusPoint && focusPointTransitionTime < 1.2f) || (!isOnFocusPoint && focusPointTransitionTime > 0f))
		{
			focusPointTransitionTime += t * (isOnFocusPoint ? 1f : (-1f));
			float point = focusPointTransitionTime / 1.2f;
			EffectFocus = Vector2.Lerp(vector, focusPointLocation, Utils.QuadraticOutCurve(point));
		}
		else
		{
			EffectFocus = vector;
			timeTillNextFocusPoint -= t;
			if (timeTillNextFocusPoint <= 0f)
			{
				isOnFocusPoint = true;
				focusPointTransitionTime = 0f;
				timeOnFocusPoint = 0f;
				hasDoneBurstForThisFocusPoint = false;
				focusPointIdleTime = Utils.randm(0.1f);
				focusPointLocation = new Vector2(Utils.randm(1f), Utils.randm(1f));
			}
		}
		for (int i = 0; i < ImpactEffects.Count; i++)
		{
			PointImpactEffect value = ImpactEffects[i];
			value.timeEnabled += t;
			if (value.timeEnabled > 3f)
			{
				ImpactEffects.RemoveAt(i);
				i--;
			}
			else
			{
				ImpactEffects[i] = value;
			}
		}
	}

	public override void Draw(float t)
	{
		base.Draw(t);
		drawOutline();
		drawTarget();
		DrawEffect(t);
		if (bounds.Height > 100)
		{
			Rectangle dest = new Rectangle(bounds.X + 1, bounds.Y + 60, (int)((double)bounds.Width * 0.7), 40);
			TextItem.doRightAlignedBackingLabelFill(dest, os.traceTracker.active ? "SUPPRESSION ACTIVE" : "        SCANNING...", GuiData.titlefont, BoatBarColor, os.traceTracker.active ? Color.Lerp(Color.Red, os.brightLockedColor, Utils.rand(1f)) : (Color.White * 0.8f));
			if (!os.traceTracker.active)
			{
				dest.Y += dest.Height + 2;
				dest.Height = 16;
				TextItem.doRightAlignedBackingLabel(dest, "TraceKill v0.8011", GuiData.detailfont, BoatBarColor, Color.DarkGray);
			}
		}
		if (bounds.Height > 40)
		{
			if (BotBarcode == null)
			{
				BotBarcode = new BarcodeEffect(bounds.Width - 2, inverted: true);
			}
			int num = 12;
			int num2 = 22;
			BotBarcode.Draw(bounds.X + 1, bounds.Y + bounds.Height - (num + num2), bounds.Width - 2, num, spriteBatch, BoatBarColor);
			Rectangle destinationRectangle = new Rectangle(bounds.X + 1, bounds.Y + bounds.Height - num2 - 1, bounds.Width - 2, num2);
			spriteBatch.Draw(Utils.white, destinationRectangle, BoatBarColor);
			int num3 = 130;
			if (Button.doButton(34004301, destinationRectangle.X + destinationRectangle.Width - num3 - 2, destinationRectangle.Y + 2, num3, destinationRectangle.Height - 4, "EXIT", os.brightLockedColor))
			{
				isExiting = true;
			}
			Rectangle dest2 = new Rectangle(destinationRectangle.X + 2, destinationRectangle.Y + 5, destinationRectangle.Width - num3 - 4, destinationRectangle.Height / 2);
			TextItem.doRightAlignedBackingLabel(dest2, traceActivityTimer.ToString("0.000000"), GuiData.detailfont, Color.Transparent, Color.White * 0.8f);
			dest2.Y += dest2.Height - 2;
			TextItem.doRightAlignedBackingLabel(dest2, (os.connectedComp != null) ? os.connectedComp.ip : "UNKNOWN", GuiData.detailfont, Color.Transparent, Color.White * 0.8f);
		}
	}

	private void DrawEffect(float t)
	{
		int num = 16;
		Rectangle rectangle = new Rectangle(bounds.X + 1, bounds.Y + num, bounds.Width - 2, bounds.Height - (num + 2));
		if (effectTarget == null || effectTarget.Width != rectangle.Width || effectTarget.Height < rectangle.Height)
		{
			if (effectTarget != null)
			{
				effectTarget.Dispose();
			}
			effectTarget = new RenderTarget2D(spriteBatch.GraphicsDevice, rectangle.Width, rectangle.Height, mipMap: false, SurfaceFormat.Rgba64, DepthFormat.None, 4, RenderTargetUsage.PlatformContents);
		}
		RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
		spriteBatch.GraphicsDevice.SetRenderTarget(effectTarget);
		spriteBatch.GraphicsDevice.Clear(Color.Transparent);
		effectSB.Begin();
		Rectangle dest = rectangle;
		dest.X = 0;
		dest.Y = 0;
		DrawEffectFill(effectSB, dest);
		DrawImpactEffects(effectSB, dest);
		effectSB.End();
		spriteBatch.GraphicsDevice.SetRenderTarget(currentRenderTarget);
		Rectangle value = rectangle;
		value.X = (value.Y = 0);
		spriteBatch.Draw(effectTarget, rectangle, value, Color.White);
	}

	private void DrawImpactEffects(SpriteBatch sb, Rectangle dest)
	{
		Color color = (os.traceTracker.active ? Color.Red : Utils.AddativeWhite);
		for (int i = 0; i < ImpactEffects.Count; i++)
		{
			PointImpactEffect pointImpactEffect = ImpactEffects[i];
			Vector2 vector = new Vector2((float)dest.X + pointImpactEffect.location.X * (float)dest.Width, (float)dest.Y + pointImpactEffect.location.Y * (float)dest.Height);
			if (pointImpactEffect.timeEnabled <= 1f)
			{
				float num = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(pointImpactEffect.timeEnabled / 1f));
				pointImpactEffect.cne.color = color * num;
				pointImpactEffect.cne.ScaleFactor = num;
				sb.Draw(circle, vector, null, color * (1f - num), 0f, new Vector2(circle.Width / 2, circle.Height / 2), num / (float)circle.Width * 30f, SpriteEffects.None, 0.7f);
			}
			else
			{
				float num2 = Utils.QuadraticOutCurve((pointImpactEffect.timeEnabled - 1f) / 2f);
				pointImpactEffect.cne.color = color * (1f - num2);
				pointImpactEffect.cne.ScaleFactor = 1f;
			}
			pointImpactEffect.cne.draw(sb, vector);
		}
	}

	private void DrawEffectFill(SpriteBatch sb, Rectangle dest)
	{
		sb.Draw(Utils.white, dest, os.indentBackgroundColor * 0.8f);
		float num = 10f;
		float num2 = 2.5f;
		float lineThickness = 2f;
		float num3 = 0.08f;
		Color color = (os.traceTracker.active ? os.lockedColor : (Utils.VeryDarkGray * 0.4f));
		Color value = (os.traceTracker.active ? os.brightLockedColor : Color.Gray);
		Vector2 targetPos = EffectFocus * new Vector2(dest.Width, dest.Height);
		List<Action> list = new List<Action>();
		int num4 = 0;
		Vector2 vector = new Vector2((float)dest.X - num / 2f, (float)dest.Y - num);
		while (vector.Y + lineThickness < (float)(dest.Y + dest.Height) + num)
		{
			vector.X = (float)dest.X - num / 2f;
			while (vector.X + lineThickness < (float)(dest.X + dest.Width) + num)
			{
				Vector2 pos = vector;
				float amount = 1f - Utils.QuadraticOutCurve(Vector2.Distance(pos, targetPos) / (float)dest.Height);
				Color highlightColor = Color.Lerp(color, value, amount);
				float length = Math.Min(num * 1.1f, Vector2.Distance(vector, targetPos));
				DrawTracerLine(sb, pos, lineThickness, targetPos, length, color, highlightColor);
				list.Add(delegate
				{
					DrawTracerLineShadow(sb, pos, lineThickness, targetPos, length, Color.Black * 0.6f);
				});
				vector.X += num + lineThickness / 2f;
			}
			num4++;
			num += num2;
			lineThickness += num3;
			vector.Y += num;
			if (num <= 0f)
			{
				break;
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			list[i]();
		}
	}

	private void DrawTracerLine(SpriteBatch sb, Vector2 pos, float thickness, Vector2 target, float length, Color baseColor, Color highlightColor)
	{
		float rotation = (float)Math.Atan2(target.Y - pos.Y, target.X - pos.X);
		Rectangle destinationRectangle = new Rectangle((int)pos.X, (int)pos.Y, (int)length, (int)thickness);
		sb.Draw(Utils.white, destinationRectangle, null, baseColor, rotation, Vector2.Zero, SpriteEffects.None, 0.7f);
		sb.Draw(Utils.gradientLeftRight, destinationRectangle, null, highlightColor, rotation, Vector2.Zero, SpriteEffects.None, 0.7f);
	}

	private void DrawTracerLineShadow(SpriteBatch sb, Vector2 pos, float thickness, Vector2 target, float length, Color color)
	{
		float rotation = (float)Math.Atan2(target.Y - pos.Y, target.X - pos.X);
		sb.Draw(Utils.gradient, pos, null, color, rotation, Vector2.Zero, new Vector2(length, (int)thickness), SpriteEffects.None, 0.6f);
		Rectangle rectangle = new Rectangle((int)pos.X, (int)pos.Y, (int)length, (int)thickness);
	}
}
