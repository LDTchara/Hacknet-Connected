using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public interface MainDisplayOverrideEXE
{
	bool DisplayOverrideIsActive { get; set; }

	void RenderMainDisplay(Rectangle dest, SpriteBatch sb);
}
