using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class CoreModule : Module
{
	private static Texture2D LockSprite;

	public bool inputLocked = false;

	private bool guiInputLockStatus = false;

	public CoreModule(Rectangle location, OS operatingSystem)
		: base(location, operatingSystem)
	{
	}

	public override void LoadContent()
	{
		base.LoadContent();
		if (LockSprite == null)
		{
			LockSprite = os.content.Load<Texture2D>("Lock");
		}
	}

	public override void PreDrawStep()
	{
		base.PreDrawStep();
		if (inputLocked)
		{
			guiInputLockStatus = GuiData.blockingInput;
			GuiData.blockingInput = true;
		}
	}

	public override void PostDrawStep()
	{
		base.PostDrawStep();
		if (inputLocked)
		{
			GuiData.blockingInput = false;
			GuiData.blockingInput = guiInputLockStatus;
			Rectangle destinationRectangle = bounds;
			if (destinationRectangle.Contains(GuiData.getMousePoint()))
			{
				GuiData.spriteBatch.Draw(Utils.white, destinationRectangle, Color.Gray * 0.5f);
				Vector2 position = new Vector2(destinationRectangle.X + destinationRectangle.Width / 2 - LockSprite.Width / 2, destinationRectangle.Y + destinationRectangle.Height / 2 - LockSprite.Height / 2);
				GuiData.spriteBatch.Draw(LockSprite, position, Color.White);
			}
		}
	}
}
