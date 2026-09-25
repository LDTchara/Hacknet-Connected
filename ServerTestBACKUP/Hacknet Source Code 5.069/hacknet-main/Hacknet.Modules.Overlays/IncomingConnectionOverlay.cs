using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Modules.Overlays;

public class IncomingConnectionOverlay
{
	private const float DURATION = 6f;

	public bool IsActive = false;

	private float timeElapsed = 0f;

	private Texture2D CautionSign;

	private Texture2D CautionSignBG;

	private static Color DrawColor = new Color(290, 0, 0, 0);

	private SoundEffect sound1;

	private SoundEffect sound2;

	public IncomingConnectionOverlay(object OSobj)
	{
		CautionSign = ((OS)OSobj).content.Load<Texture2D>("Sprites/Icons/CautionIcon");
		CautionSignBG = ((OS)OSobj).content.Load<Texture2D>("Sprites/Icons/CautionIconBG");
		sound1 = ((OS)OSobj).content.Load<SoundEffect>("SFX/DoomShock");
		sound2 = ((OS)OSobj).content.Load<SoundEffect>("SFX/BrightFlash");
	}

	public void Activate()
	{
		IsActive = true;
		timeElapsed = 0f;
		sound1.Play();
		sound2.Play();
	}

	public void Update(float dt)
	{
		timeElapsed += dt;
		if (timeElapsed > 6f)
		{
			IsActive = false;
		}
	}

	public void Draw(Rectangle dest, SpriteBatch sb)
	{
		if (!IsActive)
		{
			return;
		}
		float num = timeElapsed;
		if (timeElapsed > 5.5f)
		{
			num -= 5.5f;
		}
		if (!(num <= 0.5f) || !(num % 0.1f < 0.05f))
		{
			int num2 = 120;
			float num3 = timeElapsed / 6f;
			float num4 = 1f;
			float num5 = 0.2f;
			if (timeElapsed < num5)
			{
				num4 = timeElapsed / num5;
				num2 = (int)((double)num2 * (double)(timeElapsed / num5));
			}
			else if (timeElapsed > 6f - num5)
			{
				float num6 = 1f - (timeElapsed - (6f - num5));
				num4 = num6;
				num2 = (int)((double)num2 * (double)num6);
			}
			Rectangle destinationRectangle = new Rectangle(dest.X, dest.Y + dest.Height / 2 - num2 / 2, dest.Width, num2);
			sb.Draw(Utils.white, destinationRectangle, Color.Black * 0.9f * num4);
			string text = "INCOMING CONNECTION";
			string text2 = "External unsyndicated UDP traffic on port 22\nLogging all activity to ~/log";
			int num7 = dest.Width / 3;
			int num8 = (int)(24f * num4);
			Rectangle dest2 = new Rectangle(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, num8);
			PatternDrawer.draw(dest2, 1f, Color.Transparent, DrawColor, sb, PatternDrawer.warningStripe);
			dest2.Y += num2 - num8;
			PatternDrawer.draw(dest2, 1f, Color.Transparent, DrawColor, sb, PatternDrawer.warningStripe);
			int num9 = 700;
			Rectangle rectangle = new Rectangle(destinationRectangle.X + destinationRectangle.Width / 2 - num9 / 2, destinationRectangle.Y, num9, destinationRectangle.Height);
			int width = (int)((float)CautionSign.Width / (float)CautionSign.Height * (float)num2);
			Rectangle rect = new Rectangle(rectangle.X, rectangle.Y, width, num2);
			rect = Utils.InsetRectangle(rect, -30);
			sb.Draw(CautionSignBG, rect, Color.Black * num4);
			int num10 = 4;
			rect = new Rectangle(rect.X + num10, rect.Y + num10, rect.Width - num10 * 2, rect.Height - num10 * 2);
			sb.Draw(CautionSign, rect, Color.Lerp(Color.Red, DrawColor, 0.95f + 0.05f * Utils.rand()));
			Rectangle dest3 = new Rectangle(rectangle.X + rect.Width + 2 * num10 - 18, rectangle.Y + 4, rectangle.Width - (rect.Width + num10 * 2) + 20, (int)((double)rectangle.Height * 0.8));
			TextItem.doFontLabelToSize(dest3, text, GuiData.titlefont, DrawColor);
			dest3.Y += dest3.Height - 27;
			dest3.Height = (int)((double)rectangle.Height * 0.2);
			TextItem.doFontLabelToSize(dest3, text2, GuiData.detailfont, DrawColor * num4);
		}
	}
}
