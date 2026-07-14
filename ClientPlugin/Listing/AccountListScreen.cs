using System;
using System.Collections.Generic;
using System.Linq;
using Hacknet;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClientPlugin.Listing;

public class AccountListScreen : GameScreen
{
    private AvailableServer currentServer;
    private const float TOP_MARGIN = 70f;
    private const float LINE_HEIGHT = 25f;
    private ScrollableSectionedPanel panel;

    private Dictionary<string, bool> deleteConfirmStates = new();

    /// <summary>是否已触发过连接（防止 LoadContent 重复连接）</summary>
    private bool _connectionTriggered;

    public AccountListScreen(AvailableServer server)
    {
        currentServer = server;
        TransitionOnTime = TimeSpan.FromSeconds(0.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
        IsPopup = false;
    }

    public override void LoadContent()
    {
        base.LoadContent();
        panel = new ScrollableSectionedPanel((int)LINE_HEIGHT, ScreenManager.GraphicsDevice);
        panel.ScrollbarUIIndexOffset = 8821;

        // ════════════════════════════════════════════════════════
        // 打开界面时立即连接服务器并请求世界种子
        // 这样种子在用户浏览账户列表期间就在后台到达了，
        // 而不是等到点击 Login 时才连接。
        // ════════════════════════════════════════════════════════
        TriggerSeedRequest();
    }

    /// <summary>
    /// 连接当前服务器并请求世界种子。
    /// 种子通过 LiteNetLib 异步到达，EventReceiver 会保存到本地配置。
    /// </summary>
    private void TriggerSeedRequest()
    {
        if (_connectionTriggered) return;
        _connectionTriggered = true;

        if (string.IsNullOrEmpty(currentServer.IP) || currentServer.Port <= 0)
        {
            Console.WriteLine("[AccountList] Server IP/Port not configured, cannot request seed.");
            return;
        }

        // 使用第一个账户名作为临时标识，没有账户则用服务器名
        string tempName = currentServer.Accounts.FirstOrDefault()?.Name ?? currentServer.HostName;
        Console.WriteLine($"[AccountList] Auto-connecting to {currentServer.IP}:{currentServer.Port} as {tempName} for seed request...");

        ClientPluginMain.SelectedServer = currentServer;
        ClientNetworkManager.Connect(currentServer.IP, currentServer.Port, tempName);
        // REQUEST_SEED 在 PeerConnectedEvent 中自动发送
    }

    public override void HandleInput(InputState input)
    {
        base.HandleInput(input);
        GuiData.doInput(input);
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        PostProcessor.begin();
        ScreenManager.GraphicsDevice.Clear(Color.Black);
        GuiData.startDraw();

        Rectangle viewport = ScreenManager.GraphicsDevice.Viewport.Bounds;
        SpriteFont font = GuiData.font;
        SpriteFont smallFont = GuiData.smallfont;

        // 标题
        string title = $"ACCOUNTS FOR {currentServer.HostName}";
        Vector2 titleSize = font.MeasureString(title);
        GuiData.spriteBatch.DrawString(font, title,
            new Vector2((viewport.Width - titleSize.X) / 2, 15f), Color.White);

        // 服务器信息 & 种子状态
        string info = $"{currentServer.IP}:{currentServer.Port}";
        Vector2 infoSize = smallFont.MeasureString(info);
        GuiData.spriteBatch.DrawString(smallFont, info,
            new Vector2((viewport.Width - infoSize.X) / 2, 50f), Color.Gray);

        // ── 种子状态显示 ──
        float seedInfoY = 50f + infoSize.Y + 5f;
        string seedText;
        Color seedColor;
        if (currentServer.WorldSeed > 0)
        {
            seedText = $"Seed: {currentServer.WorldSeed} (received)";
            seedColor = Color.LightGreen;
        }
        else if (ClientPluginMain.WorldSeed.HasValue)
        {
            seedText = $"Seed: {ClientPluginMain.WorldSeed.Value} (received)";
            seedColor = Color.LightGreen;
            // 同步到服务器配置以便持久化
            currentServer.WorldSeed = ClientPluginMain.WorldSeed.Value;
            ClientProfileStorage.Save();
        }
        else if (ClientNetworkManager.IsConnected || ClientNetworkManager.IsConnecting)
        {
            seedText = "Seed: requesting...";
            seedColor = Color.Yellow;
        }
        else
        {
            seedText = "Seed: not requested";
            seedColor = Color.Gray;
        }
        Vector2 seedTextSize = smallFont.MeasureString(seedText);
        GuiData.spriteBatch.DrawString(smallFont, seedText,
            new Vector2((viewport.Width - seedTextSize.X) / 2, seedInfoY), seedColor);

        // 列表区域
        float startY = TOP_MARGIN + 30f;
        float listWidth = viewport.Width * 0.6f;
        float listX = (viewport.Width - listWidth) / 2;

        // 表头
        GuiData.spriteBatch.DrawString(smallFont, "Name", new Vector2(listX, startY), Color.LightGreen);
        GuiData.spriteBatch.DrawString(smallFont, "Save File", new Vector2(listX + 200, startY), Color.LightGreen);

        float listTop = startY + 30f;
        float listBottom = viewport.Height - 70f - 10f;
        float listHeight = listBottom - listTop;
        Rectangle scrollRect = new Rectangle((int)listX, (int)listTop, (int)listWidth, (int)listHeight);

        var accountsList = currentServer.Accounts.ToList();
        panel.NumberOfPanels = accountsList.Count;

        // 账户列表
        panel.Draw((index, dest, sb) =>
        {
            if (index >= accountsList.Count) return;
            var account = accountsList[index];
            float currentY = dest.Y;

            string nameStr = account.Name ?? "-";
            string saveStr = string.IsNullOrEmpty(account.SaveFilePath) ? "(no save)" : System.IO.Path.GetFileName(account.SaveFilePath);

            sb.DrawString(smallFont, nameStr, new Vector2(listX, currentY), Color.White * 0.8f);
            sb.DrawString(smallFont, saveStr, new Vector2(listX + 200, currentY), Color.White * 0.5f);

            // Login 按钮
            float loginBtnWidth = 60f;
            float deleteBtnWidth = 50f;
            float btnGap = 5f;
            float groupWidth = loginBtnWidth + btnGap + deleteBtnWidth;
            float groupStartX = listX + listWidth - groupWidth;

            int loginBtnId = 882010 + index;
            if (Button.doButton(loginBtnId, (int)groupStartX, (int)currentY,
                (int)loginBtnWidth, 20, "Login", Color.LightBlue))
            {
                Console.WriteLine($"[AccountList] Login: {account.Name} on {currentServer.HostName}");
                ClientPluginMain.SelectedAccount = account;
                ClientPluginMain.SelectedServer = currentServer;

                // 连接已在 LoadContent 中触发（TriggerSeedRequest）
                // 如果之前用的临时名称不同，更新 PlayerName
                if (!ClientNetworkManager.IsConnected && !ClientNetworkManager.IsConnecting)
                {
                    // 尚未连接或连到不同服务器，重新连接
                    ClientNetworkManager.Connect(
                        currentServer.IP, currentServer.Port, account.Name);
                }

                // 进入游戏
                // 种子注入逻辑在 GameLauncher.StartExtensionSession 中：
                // 优先级：1) 已异步到达的种子  2) 上次缓存的本地种子
                GameLauncher.LoginAndStart(currentServer, account);
            }

            // 删除按钮
            int deleteBtnId = 882110 + index;
            float deleteBtnX = groupStartX + loginBtnWidth + btnGap;
            bool isConfirmed = deleteConfirmStates.ContainsKey(account.Name) && deleteConfirmStates[account.Name];
            string deleteBtnText = isConfirmed ? "Sure?" : "Delete";
            Color deleteBtnColor = isConfirmed ? new Color(255, 100, 100) : new Color(200, 80, 80);

            if (Button.doButton(deleteBtnId, (int)deleteBtnX, (int)currentY,
                (int)deleteBtnWidth, 20, deleteBtnText, deleteBtnColor))
            {
                if (isConfirmed)
                {
                    // 执行删除
                    ClientProfileStorage.RemoveAccount(currentServer.HostName, account.Name);
                    deleteConfirmStates.Remove(account.Name);

                    // 刷新
                    ScreenManager.AddScreen(new AccountListScreen(currentServer));
                    ExitScreen();
                    return;
                }
                else
                {
                    deleteConfirmStates[account.Name] = true;
                }
            }
        }, GuiData.spriteBatch, scrollRect);

        // 空列表提示
        if (accountsList.Count == 0)
        {
            GuiData.spriteBatch.DrawString(smallFont, "No accounts yet.",
                new Vector2(listX + 10, scrollRect.Y + 20), Color.Gray);
        }

        // 底部按钮
        float btnY = viewport.Height - 70f;

        if (Button.doButton(882001, (int)listX, (int)btnY, 160, 35, "Add Account", Color.LightGreen))
        {
            ScreenManager.AddScreen(new AddAccountScreen(currentServer));
        }

        if (Button.doButton(882002, (int)(listX + listWidth - 70), (int)btnY, 150, 35, "Back", Color.Gray))
        {
            // 返回时不主动断开连接（后续可能回来继续使用）
            ExitScreen();
        }

        GuiData.endDraw();
        PostProcessor.end();
    }
}
