using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class SongChangerDaemon : Daemon
{
	private MovingBarsEffect topEffect;

	private MovingBarsEffect botEffect;

	public SongChangerDaemon(Computer c, OS os)
		: base(c, LocaleTerms.Loc("Music Changer"), os)
	{
		topEffect = new MovingBarsEffect();
		botEffect = new MovingBarsEffect
		{
			IsInverted = true
		};
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		topEffect.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		botEffect.Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
		int num = 30;
		Rectangle rectangle = new Rectangle(bounds.X + 30, bounds.Y + bounds.Height / 2 - num / 2, bounds.Width - 60, num);
		Rectangle bounds2 = rectangle;
		bounds2.Height = 60;
		bounds2.Y -= bounds2.Height;
		topEffect.Draw(sb, bounds2, 1f, 3f, 1f, os.highlightColor);
		if (Button.doButton(73518921, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, LocaleTerms.Loc("Shuffle Music"), os.highlightColor))
		{
			int maxValue = 12;
			int value = Utils.random.Next(maxValue) + 1;
			MissionFunctions.runCommand(value, "changeSong");
		}
		bounds2.Y += bounds2.Height + num;
		botEffect.Draw(sb, bounds2, 1f, 3f, 1f, os.highlightColor);
		Rectangle rectangle2 = new Rectangle(bounds.X + 4, bounds.Y + bounds.Height - 4 - 20, (int)((float)bounds.Width * 0.5f), 20);
		if (Button.doButton(73518924, rectangle2.X, rectangle2.Y, rectangle2.Width, rectangle2.Height, LocaleTerms.Loc("Exit"), os.lockedColor))
		{
			os.display.command = "connect";
		}
	}

	public override string getSaveString()
	{
		return "<SongChangerDaemon />";
	}
}
