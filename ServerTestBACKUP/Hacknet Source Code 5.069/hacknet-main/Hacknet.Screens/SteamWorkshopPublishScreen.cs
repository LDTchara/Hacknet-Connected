using System;
using System.IO;
using Hacknet.Extensions;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Steamworks;

namespace Hacknet.Screens;

public class SteamWorkshopPublishScreen
{
	public Action GoBack;

	private bool isInUpload = false;

	private bool showLoadingSpinner = false;

	public bool HasInitializedSteamCallbacks = false;

	private string currentStatusMessage = "";

	private string currentTitleMessage = "";

	private string currentBodyMessage = "";

	private Texture2D spinnerTex;

	private float spinnerRot = 0f;

	private ExtensionInfo ActiveInfo;

	protected CallResult<CreateItemResult_t> m_CreateItemResult;

	protected CallResult<SubmitItemUpdateResult_t> SubmitItemUpdateResult_t;

	protected CallResult<NumberOfCurrentPlayers_t> m_NumberOfCurrentPlayers;

	private AppId_t hacknetAppID = new AppId_t(365450u);

	private UGCUpdateHandle_t updateHandle;

	private DateTime transferStarted;

	public SteamWorkshopPublishScreen(ContentManager content)
	{
		spinnerTex = content.Load<Texture2D>("Sprites/Spinner");
	}

	private void SubmitUpdateResult(SubmitItemUpdateResult_t callback, bool bIOFailure)
	{
		Console.WriteLine("Upload complete!");
		if (callback.m_eResult == EResult.k_EResultOK)
		{
			currentTitleMessage = "Update Complete!";
			currentBodyMessage = "Upload completed successfully";
		}
		else
		{
			currentTitleMessage = "Update Failed";
			currentBodyMessage = "Error code: " + callback.m_eResult;
		}
		isInUpload = false;
		showLoadingSpinner = false;
	}

	private void CreateItemResult(CreateItemResult_t callback, bool bIOFailure)
	{
		switch (callback.m_eResult)
		{
		case EResult.k_EResultInsufficientPrivilege:
			currentTitleMessage = "Error: Insufficient Privilege";
			currentBodyMessage = "The user creating the item is currently banned in the community";
			return;
		case EResult.k_EResultTimeout:
			currentTitleMessage = "Error: Timeout";
			currentBodyMessage = "Current user is not currently logged into steam";
			return;
		case EResult.k_EResultNotLoggedOn:
			currentTitleMessage = "Error: Not logged on";
			currentBodyMessage = "The user creating the item is currently banned in the community";
			return;
		}
		ulong publishedFileId = callback.m_nPublishedFileId.m_PublishedFileId;
		showLoadingSpinner = false;
		currentTitleMessage = "Extension successfully created!";
		currentBodyMessage = "Extension Publish ID: " + callback.m_nPublishedFileId;
		ActiveInfo.WorkshopPublishID = string.Concat(callback.m_nPublishedFileId.m_PublishedFileId);
		if (callback.m_bUserNeedsToAcceptWorkshopLegalAgreement)
		{
			SteamFriends.ActivateGameOverlayToWebPage("steam://url/CommunityFilePage/" + callback.m_nPublishedFileId);
		}
		string text = ActiveInfo.FolderPath + "/ExtensionInfo.xml";
		string text2 = Utils.readEntireFile(text);
		text2 = text2.Replace("<WorkshopPublishID>NONE</WorkshopPublishID>", "<WorkshopPublishID>" + publishedFileId + "</WorkshopPublishID>");
		File.WriteAllText(text, text2);
	}

	private void PerformUpdate(ExtensionInfo info)
	{
		updateHandle = SteamUGC.StartItemUpdate(hacknetAppID, new PublishedFileId_t((ulong)Convert.ToInt64(info.WorkshopPublishID)));
		isInUpload = true;
		bool flag = true;
		flag &= SteamUGC.SetItemTitle(updateHandle, info.Name);
		flag &= SteamUGC.SetItemDescription(updateHandle, info.WorkshopDescription);
		flag &= SteamUGC.SetItemTags(updateHandle, info.WorkshopTags.Split(new string[3] { " ,", ", ", "," }, StringSplitOptions.RemoveEmptyEntries));
		string text = Path.Combine(Directory.GetCurrentDirectory(), info.FolderPath) + "/" + info.WorkshopPreviewImagePath;
		text = text.Replace("\\", "/");
		if (File.Exists(text))
		{
			FileInfo fileInfo = new FileInfo(text);
			if (fileInfo.Length < 1000000)
			{
				flag &= SteamUGC.SetItemPreview(updateHandle, text);
			}
			else
			{
				currentStatusMessage = "Workshop Preview Image too large! Max size 1mb. Ignoring...";
			}
		}
		ERemoteStoragePublishedFileVisibility eVisibility = ((info.WorkshopVisibility > 0) ? ((info.WorkshopVisibility == 1) ? ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly : ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate) : ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic);
		flag &= SteamUGC.SetItemVisibility(updateHandle, eVisibility);
		string text2 = Path.Combine(Directory.GetCurrentDirectory(), info.FolderPath);
		text2 = text2.Replace("\\", "/");
		Console.WriteLine("Content Path: " + text2);
		if (!(flag & SteamUGC.SetItemContent(updateHandle, text2)))
		{
			Console.WriteLine("Error!");
		}
		string path = info.FolderPath + "/Changelog.txt";
		string pchChangeNote = "";
		if (File.Exists(path))
		{
			pchChangeNote = File.ReadAllText(path);
		}
		SteamAPICall_t hAPICall = SteamUGC.SubmitItemUpdate(updateHandle, pchChangeNote);
		SubmitItemUpdateResult_t.Set(hAPICall);
		transferStarted = DateTime.Now;
		showLoadingSpinner = true;
		isInUpload = true;
		currentBodyMessage = "Uploading to Steam Workshop...";
		currentTitleMessage = "Upload in progress";
	}

	private void OnNumberOfCurrentPlayers(NumberOfCurrentPlayers_t pCallback, bool bIOFailure)
	{
		currentBodyMessage = currentBodyMessage + "\nPlayer callback: " + pCallback.m_cPlayers;
	}

	private void InitSteamCallbacks()
	{
		m_CreateItemResult = CallResult<CreateItemResult_t>.Create(CreateItemResult);
		m_NumberOfCurrentPlayers = CallResult<NumberOfCurrentPlayers_t>.Create(OnNumberOfCurrentPlayers);
		SubmitItemUpdateResult_t = CallResult<Steamworks.SubmitItemUpdateResult_t>.Create(SubmitUpdateResult);
		HasInitializedSteamCallbacks = true;
	}

	public void Update()
	{
		SteamAPI.RunCallbacks();
	}

	public Vector2 Draw(SpriteBatch sb, Rectangle dest, ExtensionInfo info)
	{
		ActiveInfo = info;
		if (!HasInitializedSteamCallbacks)
		{
			InitSteamCallbacks();
		}
		Update();
		Vector2 result = new Vector2(dest.X, dest.Y);
		bool flag = info.WorkshopPublishID != "NONE";
		currentStatusMessage = (flag ? "Ready to push Updates" : "Ready to create in steam");
		if (!flag && string.IsNullOrWhiteSpace(currentBodyMessage))
		{
			currentBodyMessage = "By submitting this item, you agree to the workshop terms of service\nhttp://steamcommunity.com/sharedfiles/workshoplegalagreement";
		}
		Vector2 vector = new Vector2(result.X + (float)(dest.Width / 2), result.Y);
		TextItem.doFontLabel(vector, currentStatusMessage, GuiData.font, Color.Gray, (float)dest.Width / 2f, 30f);
		vector.Y += 30f;
		TextItem.doFontLabel(vector, currentTitleMessage, GuiData.font, Color.White, (float)dest.Width / 2f, 30f);
		vector.Y += 30f;
		Vector2 pos = vector;
		if (showLoadingSpinner)
		{
			vector.X += 16f;
			spinnerRot += 0.1f;
			sb.Draw(destinationRectangle: new Rectangle((int)vector.X, (int)vector.Y + 20, 40, 40), texture: spinnerTex, sourceRectangle: null, color: Color.White, rotation: spinnerRot, origin: spinnerTex.GetCentreOrigin(), effects: SpriteEffects.None, layerDepth: 0.5f);
			pos.X += 45f;
		}
		if (isInUpload)
		{
			Rectangle rectangle = new Rectangle((int)pos.X, (int)pos.Y + 6, dest.Width / 2, 20);
			ulong punBytesProcessed = 0uL;
			ulong punBytesTotal = 1uL;
			SteamUGC.GetItemUpdateProgress(updateHandle, out punBytesProcessed, out punBytesTotal);
			double num = (double)punBytesProcessed / (double)punBytesTotal;
			if (punBytesTotal == 0)
			{
				num = 0.0;
			}
			sb.Draw(Utils.white, Utils.InsetRectangle(rectangle, -1), Utils.AddativeWhite * 0.7f);
			sb.Draw(Utils.white, rectangle, Utils.VeryDarkGray);
			rectangle.Width = (int)((double)rectangle.Width * num);
			sb.Draw(Utils.white, rectangle, Color.LightBlue);
			pos.Y += 31f;
			if (punBytesTotal != 0)
			{
				string text = string.Format("{0}% - {1}mb of {2}mb Transfered", (num * 100.0).ToString("00.00"), (punBytesProcessed / 1000000).ToString("0.00"), (punBytesTotal / 1000000).ToString("0.00"));
				Utils.DrawStringMonospace(sb, text, GuiData.smallfont, pos, Color.White, 9f);
				pos.Y += 20f;
				TimeSpan time = DateTime.Now - transferStarted;
				TimeSpan time2 = TimeSpan.FromSeconds(time.TotalSeconds / Math.Max(num, 0.01));
				text = $"ETA: {getTimespanDisplayString(time2)} // Elapsed : {getTimespanDisplayString(time)}";
				Utils.DrawStringMonospace(sb, text, GuiData.smallfont, pos, Color.White, 9f);
				pos.Y += 25f;
			}
		}
		TextItem.doFontLabel(pos, currentBodyMessage, GuiData.smallfont, Color.White * 0.8f, (float)dest.Width / 2f, 30f);
		int num2 = 40;
		int width = 450;
		bool flag2 = !showLoadingSpinner;
		if (!isInUpload)
		{
			if (Button.doButton(371711001, (int)result.X, (int)result.Y, width, num2, flag ? " - Item Created -" : "Create Entry in Steam Workshop", flag ? Color.LightBlue : Color.Gray) && !flag && flag2)
			{
				CreateExtensionInSteam(info);
			}
			result.Y += num2 + 4;
			if (Button.doButton(371711003, (int)result.X, (int)result.Y, width, num2, "Upload to Steam Workshop", flag ? Color.LightBlue : Color.Gray) && flag2)
			{
				PerformUpdate(info);
			}
			result.Y += num2 + 4;
			if (Button.doButton(371711005, (int)result.X, (int)result.Y + 10, width, num2 - 10, "Back to Extension Menu", Color.Black) && flag2 && GoBack != null)
			{
				GoBack();
			}
			result.Y += num2 + 4;
		}
		return result;
	}

	private string getTimespanDisplayString(TimeSpan time)
	{
		double num = time.TotalSeconds / 60.0;
		num -= num % 1.0;
		double num2 = time.TotalSeconds % 60.0;
		string text = ((num >= 1.0) ? (num.ToString("0") + "m ") : "");
		return text + num2.ToString("00") + "s";
	}

	private void CreateExtensionInSteam(ExtensionInfo info)
	{
		SteamAPICall_t hAPICall = SteamUGC.CreateItem(hacknetAppID, EWorkshopFileType.k_EWorkshopFileTypeFirst);
		m_CreateItemResult.Set(hAPICall);
		showLoadingSpinner = true;
		currentTitleMessage = "Creating Extension with Steam...";
	}
}
