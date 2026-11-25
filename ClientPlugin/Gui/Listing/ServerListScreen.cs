using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hacknet;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClientPlugin.Listing;

public class ServerListScreen : GameScreen
{
    private const float BUTTON_HEIGHT = 40f;
    private const float TOP_MARGIN = 60f;
    private const float MINI_BUTTON_WIDTH = 55f;
    private const float MINI_BUTTON_GAP = 5f;
    private const float LIST_WIDTH_RATIO = 0.6f;

    private Dictionary<string, bool> deleteConfirmStates = new();
    private ScrollableSectionedPanel panel;
    private int lastNumberOfServers = 0;

    public ServerListScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(0.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
        IsPopup = false;
    }

    public override void LoadContent()
    {
        base.LoadContent();
        panel = new ScrollableSectionedPanel((int)(BUTTON_HEIGHT + 5), ScreenManager.GraphicsDevice);
        panel.ScrollbarUIIndexOffset = 8801;
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

        // 标题
        SpriteFont font = GuiData.font;
        string title = "SELECT SERVERS";
        Vector2 titleSize = font.MeasureString(title);
        GuiData.spriteBatch.DrawString(font, title,
            new Vector2((viewport.Width - titleSize.X) / 2, 10f), Color.White);

        // 列表区域
        float listWidth = viewport.Width * LIST_WIDTH_RATIO;
        float listX = (viewport.Width - listWidth) / 2;
        float listTop = TOP_MARGIN + 30f;
        float listBottom = viewport.Height - 70f - 10f;
        float listHeight = listBottom - listTop;
        Rectangle scrollRect = new Rectangle((int)listX, (int)listTop, (int)listWidth, (int)listHeight);

        var servers = ClientProfileStorage.Servers;
        panel.NumberOfPanels = servers.Count;

        if (servers.Count != lastNumberOfServers)
        {
            panel.ScrollDown = 0;
            lastNumberOfServers = servers.Count;
        }

        // 服务器列表
        panel.Draw((index, dest, sb) =>
        {
            if (index >= servers.Count) return;
            var server = servers[index];
            float groupWidth = scrollRect.Width;
            float mainButtonWidth = groupWidth - MINI_BUTTON_WIDTH * 2 - MINI_BUTTON_GAP * 2;
            float currentY = dest.Y;
            float groupStartX = scrollRect.X;

            // 主按钮（选择服务器）
            if (Button.doButton(8800100 + index,
                (int)groupStartX, (int)currentY,
                (int)mainButtonWidth, (int)BUTTON_HEIGHT,
                $"{server.HostName} ({server.IP}:{server.Port})", Color.White))
            {
                OnServerSelected(server);
            }

            // 编辑按钮
            float editBtnX = groupStartX + mainButtonWidth + MINI_BUTTON_GAP;
            if (Button.doButton(8800300 + index * 10,
                (int)editBtnX, (int)currentY,
                (int)MINI_BUTTON_WIDTH, (int)BUTTON_HEIGHT,
                "Edit", Color.Orange))
            {
                ScreenManager.AddScreen(new AddServerScreen(server));
            }

            // 删除按钮
            float deleteBtnX = editBtnX + MINI_BUTTON_WIDTH + MINI_BUTTON_GAP;
            bool isConfirmed = deleteConfirmStates.ContainsKey(server.HostName) && deleteConfirmStates[server.HostName];
            string deleteBtnText = isConfirmed ? "Sure?" : "Del";
            Color deleteBtnColor = isConfirmed ? new Color(255, 100, 100) : new Color(200, 80, 80);

            if (Button.doButton(8800500 + index * 10,
                (int)deleteBtnX, (int)currentY,
                (int)MINI_BUTTON_WIDTH, (int)BUTTON_HEIGHT,
                deleteBtnText, deleteBtnColor))
            {
                if (isConfirmed)
                {
                    DeleteServer(server);
                }
                else
                {
                    deleteConfirmStates[server.HostName] = true;
                }
            }
        }, GuiData.spriteBatch, scrollRect);

        // 空列表提示
        if (servers.Count == 0)
        {
            GuiData.spriteBatch.DrawString(GuiData.smallfont,
                "No servers found.\nClick 'Add Server' to create one.",
                new Vector2(scrollRect.X + 10, scrollRect.Y + 20), Color.Gray);
        }

        // 底部按钮
        float btnY = viewport.Height - 70f;
        float btnWidth = 150f;

        if (Button.doButton(8800800,
            (int)scrollRect.X, (int)btnY,
            (int)btnWidth, 35, "Add Server", Color.LightGreen))
        {
            ScreenManager.AddScreen(new AddServerScreen());
        }

        if (Button.doButton(8800900,
            (int)(scrollRect.Right - btnWidth), (int)btnY,
            (int)btnWidth, 35, "Back", Color.Gray))
        {
            ExitScreen();
        }

        GuiData.endDraw();
        PostProcessor.end();
    }

    private void OnServerSelected(AvailableServer server)
    {
        Console.WriteLine($"[ServerListScreen] Selected: {server.HostName} ({server.IP}:{server.Port})");
        ScreenManager.AddScreen(new AccountListScreen(server));
    }

    private void DeleteServer(AvailableServer server)
    {
        deleteConfirmStates.Remove(server.HostName);
        ClientProfileStorage.RemoveServer(server.HostName);

        // 刷新
        ScreenManager.AddScreen(new ServerListScreen());
        ExitScreen();
    }
}
