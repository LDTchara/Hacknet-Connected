using System.Collections.Generic;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

internal class UploadServerDaemon : AuthenticatingDaemon
{
	private enum UploadServerState
	{
		Menu,
		Login,
		Active
	}

	private const string DEFAULT_FOLDERNAME = "Drop";

	private const string STORAGE_FOLDER_FOLDERNAME = "Uploads";

	private const string MESSAGE_FILENAME = "Server_Message.txt";

	private const int NUMBER_OF_ARROWS = 90;

	private const float MAX_FADE_TIME = 2f;

	private static string MESSAGE_FILE_DATA = null;

	private static Vector2 ARROW_VELOCITY = new Vector2(0f, 400f);

	private Texture2D arrow;

	private Color themeColor;

	private Color darkThemeColor;

	private Color lightThemeColor;

	private Folder root;

	private Folder storageFolder;

	private UploadServerState state = UploadServerState.Menu;

	public string Foldername;

	public bool needsAuthentication;

	public bool hasReturnViewButton = false;

	private int uploadFileCountLastFrame;

	private float uploadDetectedEffectTimer = 0f;

	private List<Vector2> arrowPositions;

	private List<float> arrowFades;

	private List<float> arrowDepths;

	public UploadServerDaemon(Computer computer, string serviceName, Color themeColor, OS opSystem, string foldername = null, bool needsAuthentication = false)
		: base(computer, serviceName, opSystem)
	{
		arrow = os.content.Load<Texture2D>("Arrow");
		this.themeColor = themeColor;
		lightThemeColor = Color.Lerp(themeColor, Color.White, 0.4f);
		darkThemeColor = Color.Lerp(themeColor, Color.Black, 0.8f);
		Foldername = foldername;
		if (Foldername == null)
		{
			Foldername = "Drop";
		}
		this.needsAuthentication = needsAuthentication;
		MESSAGE_FILE_DATA = Utils.readEntireFile("Content/LocPost/UploadServerText.txt");
	}

	public override string getSaveString()
	{
		return "<UploadServerDaemon name=\"" + name + "\" foldername=\"" + Foldername + "\" color=\"" + Utils.convertColorToParseableString(themeColor) + "\" needsAuth=\"" + needsAuthentication + "\" hasReturnViewButton=\"" + hasReturnViewButton + "\" />";
	}

	public override void initFiles()
	{
		base.initFiles();
		root = comp.files.root.searchForFolder(Foldername);
		if (root == null)
		{
			root = new Folder(Foldername);
			comp.files.root.folders.Add(root);
		}
		storageFolder = root.searchForFolder("Uploads");
		if (storageFolder == null)
		{
			storageFolder = new Folder("Uploads");
			root.folders.Add(storageFolder);
		}
		root.files.Add(new FileEntry(MESSAGE_FILE_DATA, "Server_Message.txt"));
		uploadFileCountLastFrame = storageFolder.files.Count;
	}

	public override void loadInit()
	{
		base.loadInit();
		root = comp.files.root.searchForFolder(Foldername);
		storageFolder = root.searchForFolder("Uploads");
	}

	public override void navigatedTo()
	{
		base.navigatedTo();
		if (!needsAuthentication)
		{
			moveToActiveState();
		}
		else
		{
			state = UploadServerState.Menu;
		}
	}

	private void moveToActiveState()
	{
		state = UploadServerState.Active;
		Programs.sudo(os, delegate
		{
			string[] args = new string[2]
			{
				"cd",
				Foldername + "/Uploads"
			};
			Programs.cd(args, os);
			os.display.command = name;
		});
		if (!comp.userLoggedIn)
		{
			comp.userLoggedIn = true;
		}
	}

	public override void draw(Rectangle bounds, SpriteBatch sb)
	{
		base.draw(bounds, sb);
		drawBackground(bounds, sb);
		int num = bounds.X + 10;
		int num2 = bounds.Y + 10;
		TextItem.doFontLabel(new Vector2(num, num2), name, GuiData.font, null, bounds.Width - 20, bounds.Height);
		num2 += (int)GuiData.font.MeasureString(name).Y + 2;
		string text = "Error: System.IO.FileNotFoundException went unhandled\n";
		text += "File Server_Message.txt not found\n";
		text += "at UploadDaemonCore.RenderModule.HelptextDisplay.cs\nline 107 position 95";
		FileEntry fileEntry = root.searchForFile("Server_Message.txt");
		if (fileEntry != null)
		{
			text = fileEntry.data;
		}
		Vector2 vector = TextItem.doMeasuredFontLabel(new Vector2(num, num2), text, GuiData.tinyfont, null);
		if (hasReturnViewButton && Button.doButton(50821549, num - 2, (int)((float)num2 + vector.Y + 8f), bounds.Width / 3, 26, "Exit Upload View", themeColor))
		{
			os.display.command = "connect";
		}
	}

	public void drawBackground(Rectangle bounds, SpriteBatch sb)
	{
		bounds = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
		sb.Draw(Utils.gradient, bounds, themeColor);
		sb.Draw(arrow, new Rectangle(bounds.X, bounds.Y + bounds.Height / 3, bounds.Width / 2, (int)((float)bounds.Height * 0.66666f) + 1), new Rectangle(arrow.Width / 2, 0, arrow.Width / 2, arrow.Height), darkThemeColor);
		if (arrowPositions == null)
		{
			arrowPositions = new List<Vector2>();
			arrowFades = new List<float>();
			arrowDepths = new List<float>();
			for (int i = 0; i < 90; i++)
			{
				float num = 70f;
				Vector2 item = new Vector2((float)((double)(0f - num) + Utils.random.NextDouble() * (double)(num + (float)bounds.Width)), (float)((double)bounds.Height / (Utils.random.NextDouble() * 2.0)));
				arrowPositions.Add(item);
				arrowFades.Add((float)(0.10000000149011612 + 0.8999999985098839 * Utils.random.NextDouble()));
				arrowDepths.Add((float)(0.20000000298023224 + (Utils.random.NextDouble() - 0.2)));
			}
		}
		float num2 = (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		for (int i = 0; i < arrowPositions.Count; i++)
		{
			arrowPositions[i] -= num2 * ARROW_VELOCITY * arrowDepths[i];
			arrowFades[i] -= num2 / 2f;
			if (arrowFades[i] < 0f)
			{
				float num = 70f;
				Vector2 item = new Vector2((float)((double)(0f - num) + Utils.random.NextDouble() * (double)(num + (float)bounds.Width)), (float)((double)bounds.Height / (Utils.random.NextDouble() * 2.0)));
				arrowPositions[i] = item;
				arrowFades[i] = (float)(0.5 + 1.5 * Utils.random.NextDouble());
				arrowDepths[i] = (float)(0.20000000298023224 + Utils.random.NextDouble() * 0.4);
			}
			float num3 = 0.3f * arrowDepths[i];
			Vector2 pos = new Vector2((float)bounds.X + arrowPositions[i].X, (float)(bounds.Y + bounds.Height) + arrowPositions[i].Y);
			Rectangle clipRectForSpritePos = Utils.getClipRectForSpritePos(bounds, arrow, pos, num3);
			Vector2 vector = new Vector2(clipRectForSpritePos.X, clipRectForSpritePos.Y);
			pos += vector;
			sb.Draw(destinationRectangle: new Rectangle((int)pos.X, (int)pos.Y, (int)(num3 * (float)(clipRectForSpritePos.Width - clipRectForSpritePos.X)), (int)(num3 * (float)(clipRectForSpritePos.Height - clipRectForSpritePos.Y))), texture: arrow, sourceRectangle: clipRectForSpritePos, color: lightThemeColor * (arrowDepths[i] * 1.5f) * arrowFades[i], rotation: 0f, origin: Vector2.Zero, effects: SpriteEffects.None, layerDepth: arrowDepths[i]);
		}
		drawUploadDetectedEffect(bounds, sb);
	}

	public void drawUploadDetectedEffect(Rectangle bounds, SpriteBatch sb)
	{
		uploadDetectedEffectTimer -= (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
		if (uploadFileCountLastFrame < storageFolder.files.Count)
		{
			uploadDetectedEffectTimer = 4f;
			uploadFileCountLastFrame = storageFolder.files.Count;
		}
		if (uploadDetectedEffectTimer > 0f)
		{
			Color white = Color.White;
			white.A = 0;
			white *= uploadDetectedEffectTimer / 4f;
			bool drawShadow = TextItem.DrawShadow;
			TextItem.DrawShadow = false;
			TextItem.doFontLabel(new Vector2(bounds.X + bounds.Width / 3, bounds.Y + bounds.Height - 60), "UPLOAD COMPLETE", GuiData.titlefont, white, bounds.Width / 3 * 2, 58f);
			TextItem.DrawShadow = drawShadow;
		}
	}
}
