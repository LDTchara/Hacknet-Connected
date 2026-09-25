using System;
using System.Runtime.InteropServices;
using Hacknet.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class WebRenderer
{
	private static WebRenderer instance;

	public static bool Enabled = true;

	public static Texture2D texture;

	private static byte[] texBuffer;

	public static XNAWebRenderer.TextureUpdatedDelegate textureUpdated = TextureUpdated;

	private static GraphicsDevice graphics;

	private static int width = 500;

	private static int height = 500;

	private static string url = "http://www.google.com";

	private static bool loadingPage;

	public static void setSize(int frameWidth, int frameHeight)
	{
		width = frameWidth;
		height = frameHeight;
		if (!Enabled)
		{
			return;
		}
		if (graphics != null)
		{
			if (texture != null)
			{
				texture.Dispose();
			}
			texture = new Texture2D(graphics, width, height, mipMap: false, SurfaceFormat.Color);
			texBuffer = new byte[width * height * 4];
			XNAWebRenderer.XNAWR_SetViewport(width, height);
		}
		loadingPage = true;
	}

	public static void init(GraphicsDevice gd)
	{
		if (!Enabled)
		{
			loadingPage = true;
			return;
		}
		instance = new WebRenderer();
		graphics = gd;
		loadingPage = true;
	}

	public static void navigateTo(string urlTo)
	{
		loadingPage = true;
		Console.WriteLine("Launching Web Thread");
		url = urlTo;
		if (Enabled)
		{
			XNAWebRenderer.XNAWR_LoadURL(new Uri(url).ToString());
		}
	}

	private static void TextureUpdated(IntPtr buffer)
	{
		try
		{
			Marshal.Copy(buffer, texBuffer, 0, texBuffer.Length);
			texture.SetData(texBuffer);
			loadingPage = false;
		}
		catch (AccessViolationException value)
		{
			Console.WriteLine(value);
			navigateTo(url);
		}
	}

	public static void drawTo(Rectangle bounds, SpriteBatch sb)
	{
		if (!loadingPage)
		{
			sb.Draw(texture, bounds, Color.White);
		}
		else
		{
			WebpageLoadingEffect.DrawLoadingEffect(bounds, sb, OS.currentInstance);
		}
	}

	public static string getURL()
	{
		return url;
	}
}
