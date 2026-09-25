using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hacknet;
using LiteNetLib;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

using Microsoft.Xna.Framework.Input;

using TAXCoreOnline.LoadAndSave.Client;
using Pathfinder.Options;

namespace TAXCoreOnline
{
    public class ServerListScreen : GameScreen
    {
        private const float BUTTON_HEIGHT = 40f;
        private const float TOP_MARGIN = 60f;
        private const float MINI_BUTTON_WIDTH = 55f;
        private const float MINI_BUTTON_GAP = 5f;
        private const float LIST_WIDTH_RATIO = 0.6f; // 与AccountList一致

        private Dictionary<string, bool> deleteConfirmStates = new Dictionary<string, bool>();
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
            panel.ScrollbarUIIndexOffset = 5000;
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

            // 计算居中列表区域（与AccountList一致）
            float listWidth = viewport.Width * LIST_WIDTH_RATIO;
            float listX = (viewport.Width - listWidth) / 2;
            float listTop = TOP_MARGIN + 30f; // 留出标题间距
            float listBottom = viewport.Height - 70f - 10f; // 底部按钮预留空间
            float listHeight = listBottom - listTop;
            Rectangle scrollRect = new Rectangle((int)listX, (int)listTop, (int)listWidth, (int)listHeight);

            var servers = new List<AvailableServer>(TAXCoreOnline.IDToServers.Values);
            panel.NumberOfPanels = servers.Count;

            // 检测服务器数量变化，重置滚动位置
            if (servers.Count != lastNumberOfServers)
            {
                panel.ScrollDown = 0;
                lastNumberOfServers = servers.Count;
            }

            // 使用滚动面板绘制服务器列表
            panel.Draw((index, dest, sb) =>
            {
                if (index >= servers.Count) return;
                var server = servers[index];

                // 整个按钮组占满列表宽度（考虑边距）
                float groupWidth = scrollRect.Width; // 占满列表区域
                float mainButtonWidth = groupWidth - MINI_BUTTON_WIDTH * 2 - MINI_BUTTON_GAP * 2;
                float currentY = dest.Y;

                float groupStartX = scrollRect.X; // 从左边开始

                // 主按钮（选择服务器）
                if (Button.doButton(100 + index,
                    (int)groupStartX, (int)currentY,
                    (int)mainButtonWidth, (int)BUTTON_HEIGHT,
                    $"{server.HostName} ({server.IP}:{server.Port})", Color.White))
                {
                    OnServerSelected(server);
                }

                // 编辑按钮
                float editBtnX = groupStartX + mainButtonWidth + MINI_BUTTON_GAP;
                if (Button.doButton(300 + index * 10,
                    (int)editBtnX, (int)currentY,
                    (int)MINI_BUTTON_WIDTH, (int)BUTTON_HEIGHT,
                    "Edit", Color.Orange))
                {
                    ScreenManager.AddScreen(new AddServerScreen(server));
                }

                // 删除按钮
                float deleteBtnX = editBtnX + MINI_BUTTON_WIDTH + MINI_BUTTON_GAP;
                bool isConfirmed = deleteConfirmStates.ContainsKey(server.LocalID) && deleteConfirmStates[server.LocalID];
                string deleteBtnText = isConfirmed ? "Sure?" : "Del";
                Color deleteBtnColor = isConfirmed ? new Color(255, 100, 100) : new Color(200, 80, 80);

                if (Button.doButton(301 + index * 10,
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
                        deleteConfirmStates[server.LocalID] = true;
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

            // 底部按钮：左右端分别与列表区域对齐
            float btnY = viewport.Height - 70f;
            float btnWidth = 150f;

            // 添加服务器按钮（左端与列表左端对齐）
            if (Button.doButton(9998,
                (int)scrollRect.X, (int)btnY,
                (int)btnWidth, 35, "Add Server", Color.LightGreen))
            {
                ScreenManager.AddScreen(new AddServerScreen());
            }

            // 返回按钮（右端与列表右端对齐）
            if (Button.doButton(9999,
                (int)(scrollRect.Right - btnWidth), (int)btnY,
                (int)btnWidth, 35, "Back", Color.Gray))
            {
                ExitScreen();
                Settings.IsInExtensionMode = false;
            }

            GuiData.endDraw();
            PostProcessor.end();
        }

        private void OnServerSelected(AvailableServer server)
        {
            Console.WriteLine("[ServerListScreen] Server selected, opening account list...");
            Console.WriteLine($"  HostName: {server.HostName}, IP: {server.IP}:{server.Port}");
            bool isConnectionSuccess = false;
            Ping pingsender = new Ping();
            PingOptions opt = new PingOptions();
            opt.DontFragment = true;
            string data = "thereisnothing";
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int timeout = 120;
            string address = server.IP;
            PingReply reply = pingsender.Send(address, timeout, buffer, opt);
            if (reply.Status == IPStatus.Success)
            {
                isConnectionSuccess = true;
            }
            else isConnectionSuccess = false;




            if (isConnectionSuccess) ScreenManager.AddScreen(new AccountListScreen(server));
            else Console.WriteLine($"[ServerListScreen] Failed to connect to server {server.HostName} at {server.IP}:{server.Port}");
        }

        private void DeleteServer(AvailableServer server)
        {
            deleteConfirmStates.Remove(server.LocalID);

            // ===== 删除实体文件夹（包括所有账户存档） =====
            string serverDir = Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves", "Client", server.HostName);
            try
            {
                if (Directory.Exists(serverDir))
                {
                    Directory.Delete(serverDir, true); // 递归删除所有子文件
                    Console.WriteLine($"Deleted server directory: {serverDir}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting server directory: {ex.Message}");
            }

            // 移除字典中的服务器
            TAXCoreOnline.IDToServers.Remove(server.LocalID);

            // 更新 XML 配置文件
            SavePatches.SaveAllServersConfigToFile();

            // 刷新屏幕：重新打开本屏幕，退出当前屏幕
            ScreenManager.AddScreen(new ServerListScreen());
            ExitScreen();
        }
    }
}
