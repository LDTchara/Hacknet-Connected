using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public static class TextureBank
{
	public static List<LoadedTexture> textures = new List<LoadedTexture>();

	public static Texture2D load(string filename, ContentManager content)
	{
		for (int i = 0; i < textures.Count; i++)
		{
			if (textures[i].path.Equals(filename))
			{
				if (!textures[i].tex.IsDisposed)
				{
					LoadedTexture value = textures[i];
					value.retainCount++;
					textures[i] = value;
					return value.tex;
				}
				textures.Remove(textures[i]);
			}
		}
		try
		{
			Texture2D texture2D = content.Load<Texture2D>(filename);
			LoadedTexture item = default(LoadedTexture);
			item.tex = texture2D;
			item.path = filename;
			item.retainCount = 1;
			textures.Add(item);
			return texture2D;
		}
		catch (Exception ex)
		{
			Console.WriteLine("File \"" + filename + "\" Experienced an Error in Loading\n" + ex);
			return null;
		}
	}

	public static Texture2D getIfLoaded(string filename)
	{
		foreach (LoadedTexture texture in textures)
		{
			if (texture.path.Equals(filename))
			{
				if (!texture.tex.IsDisposed)
				{
					return texture.tex;
				}
				textures.Remove(texture);
			}
		}
		return null;
	}

	public static void unload(Texture2D tex)
	{
		unloadWithoutRemoval(tex);
	}

	public static void unloadWithoutRemoval(Texture2D tex)
	{
		for (int i = 0; i < textures.Count; i++)
		{
			if (textures[i].tex.Equals(tex))
			{
				if (!textures[i].tex.IsDisposed)
				{
					LoadedTexture value = textures[i];
					value.retainCount--;
					textures[i] = value;
					break;
				}
				textures.Remove(textures[i]);
			}
		}
	}
}
