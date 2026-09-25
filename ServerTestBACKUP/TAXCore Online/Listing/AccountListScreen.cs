using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hacknet;
using Hacknet.Gui;
using Hacknet.Extensions;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TAXCoreOnline.LoadAndSave.Client;
using Pathfinder.Meta.Load;
using Microsoft.Xna.Framework.Audio;
using LiteNetLib;
using LiteNetLib.Utils;
using Hacknet.Localization;
using Hacknet.PlatformAPI.Storage;
using Hacknet.Screens;

namespace TAXCoreOnline
{
    public class AccountListScreen : GameScreen
    {

        private AvailableServer currentServer;
        private const float TOP_MARGIN = 70f;
        private const float LINE_HEIGHT = 25f;
        private ScrollableSectionedPanel panel;
        private SavefileLoginScreen SaveScreen = new SavefileLoginScreen();

        // 跟踪每个账户的删除确认状态
        private Dictionary<string, bool> deleteConfirmStates = new Dictionary<string, bool>();

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
            panel.ScrollbarUIIndexOffset = 6000;
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

            // 服务器信息
            string info = $"{currentServer.IP}:{currentServer.Port}  |  LocalID: {currentServer.LocalID}";
            Vector2 infoSize = smallFont.MeasureString(info);
            GuiData.spriteBatch.DrawString(smallFont, info,
                new Vector2((viewport.Width - infoSize.X) / 2, 50f), Color.Gray);

            // 列表区域
            float startY = TOP_MARGIN + 30f;
            float listWidth = viewport.Width * 0.6f;
            float listX = (viewport.Width - listWidth) / 2;

            // 表头
            GuiData.spriteBatch.DrawString(smallFont, "ID", new Vector2(listX, startY), Color.LightGreen);
            GuiData.spriteBatch.DrawString(smallFont, "Name", new Vector2(listX + 100, startY), Color.LightGreen);
            GuiData.spriteBatch.DrawString(smallFont, "Username", new Vector2(listX + 220, startY), Color.LightGreen);
            GuiData.spriteBatch.DrawString(smallFont, "Password", new Vector2(listX + 360, startY), Color.LightGreen);

            float listTop = startY + 30f;
            float listBottom = viewport.Height - 70f - 10f;
            float listHeight = listBottom - listTop;
            Rectangle scrollRect = new Rectangle((int)listX, (int)listTop, (int)listWidth, (int)listHeight);

            var accountsList = currentServer.Accounts?.ToList() ?? new List<ClientAccountStructure>();
            panel.NumberOfPanels = accountsList.Count;

            // 绘制每一行
            panel.Draw((index, dest, sb) =>
            {
            if (accountsList == null || index >= accountsList.Count) return;
            var account = accountsList[index];
            float currentY = dest.Y;

            string idStr = account.Id ?? "-";
            string nameStr = string.IsNullOrEmpty(account.Name) ? account.GameUsername : account.Name;
            string usernameStr = account.GameUsername ?? "-";
            string passStr = account.Password != null ? new string('*', account.Password.Length) : "-";

            sb.DrawString(smallFont, idStr, new Vector2(listX, currentY), Color.White * 0.8f);
            sb.DrawString(smallFont, nameStr, new Vector2(listX + 100, currentY), Color.White * 0.8f);
            sb.DrawString(smallFont, usernameStr, new Vector2(listX + 220, currentY), Color.White * 0.8f);
            sb.DrawString(smallFont, passStr, new Vector2(listX + 360, currentY), Color.White * 0.8f);

            // ===== 按钮组（Login + 删除）右对齐 =====
            // 修改：Login按钮宽度60，Delete按钮宽度40（保证"Delete"和"Sure?"完全显示）
            float loginBtnWidth = 60f;
            float deleteBtnWidth = 40f;
            float btnGap = 5f;
            float groupWidth = loginBtnWidth + btnGap + deleteBtnWidth;
            float groupStartX = listX + listWidth - groupWidth;

            OS os = OS.currentInstance;
            // Login 按钮
            int loginBtnId = 20010 + index;
            if (Button.doButton(loginBtnId, (int)groupStartX, (int)currentY,
                (int)loginBtnWidth, 20, "Login", Color.LightBlue))
                {
                    ClientAccountStructure selectedAccount = new();
                    foreach(var acc in currentServer.Accounts)
                    {
                        if (acc.Id == account.Id)
                        {
                            selectedAccount = acc;
                            break;
                        }
                    }
                    TAXCoreOnline.CurrentAccount = selectedAccount;
                    //TAXCoreOnline.CurrentAccount = TAXCoreOnline.CurrentServer.Accounts;
                    try
                    {
                       
                        Console.WriteLine(TAXCoreOnline.CurrentAccount.Id);
                        Console.WriteLine(currentServer.LocalID);
                
                        Console.WriteLine(TAXCoreOnline.CurrentAccount.GameUsername);
                        //currentServer =TAXCoreOnline.CurrentServer;
                        TAXCoreOnline.CurrentAccount.ExtensionName = "tmod";


                        ExtensionInfo eif = ExtensionInfo.ReadExtensionInfo(Path.Combine(TAXCoreOnline.EnvPath, "Extensions", TAXCoreOnline.CurrentAccount.ExtensionName));

                        LocaleActivator.ActivateLocale(eif.Language, Game1.getSingleton().Content);

                        Settings.IsInExtensionMode = true;
                        ExtensionLoader.ActiveExtensionInfo = eif;
                        SaveFileManager.Init();
                        People.init();
                        //SaveScreen.LoadGameForUserFileAndUsername();

                        //SaveScreen.ResetForNewAccount();


                        ExtensionLoader.LoadNewExtensionSession(eif, OS.currentInstance);
                        // 连接并拿到 peer
                        NetPeer peer = TAXCoreOnline.client.Connect(currentServer.IP, currentServer.Port, "");
                        EventBasedNetListener.OnNetworkReceive loginResponseHandler = null;
                        loginResponseHandler = (fromPeer, dataReader, channelNumber, deliveryMethod) =>
                        {
                            // 读取完整字符串（Put/Put 方式写入的消息用 GetString() 读取）
                            string serverMessage = dataReader.GetString();
                            Console.WriteLine($"getmessge:{serverMessage}");
                            dataReader.Recycle();

                            // 设置 CurrentAccount.Id
                            if (TAXCoreOnline.CurrentAccount != null)
                                TAXCoreOnline.CurrentAccount.Id = serverMessage;

                            // 执行一次后立即移除，避免重复接收干扰
                            TAXCoreOnline.listener.NetworkReceiveEvent -= loginResponseHandler;
                        };

                        TAXCoreOnline.listener.NetworkReceiveEvent += loginResponseHandler;

                        // 构造登录信息并发送
                        string msg = $@"<LoginInfo>
  <Name>{TAXCoreOnline.CurrentAccount.Name}</Name>
  <Gameusername>{TAXCoreOnline.CurrentAccount.GameUsername}</Gameusername>
</LoginInfo>";

                        NetDataWriter writer = new NetDataWriter();
                        writer.Put(msg);
                        peer.Send(writer, DeliveryMethod.ReliableOrdered);
                        Console.WriteLine(TAXCoreOnline.CurrentAccount.Id);
                        MemoryStream loadedStream = ClientLoadFromOLSave.LoadOnlineSaveAndUpdateAccount(
                            currentServer.LocalID, TAXCoreOnline.CurrentAccount.GameUsername);

                        if (loadedStream != null)
                        {
                            Console.WriteLine($"Logged in as {account.Name ?? account.GameUsername}");
                            OS currentOS = OS.currentInstance;


                            if (currentOS != null)
                            {
                                currentOS.ForceLoadOverrideStream = loadedStream;
                                OS.TestingPassOnly = true;
                            }

                            else
                            {
                                Console.WriteLine("Warning: No OS instance available to load save.");
                            }
                            //Start Login
                            string extpath = Path.Combine(TAXCoreOnline.EnvPath,"Extensions",TAXCoreOnline.CurrentAccount.ExtensionName);

                            //ExtensionInfo eif = ExtensionInfo.ReadExtensionInfo(extpath);
                            /*
                            <LoginInfo>
                              <Name>User</Name>
                              <Gameusername>User</Gameusername>
                            </LoginInfo>
                             
                            */
                            //ExtensionLoader.LoadNewExtensionSession(eif,os);
                            
                        }
                        else
                        {
                            Console.WriteLine($"Failed to load save for {account.GameUsername}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error logging in: {ex}");
                    }
                }

                // 删除按钮（修改：文字改为"Delete" / "Sure?"）
                int deleteBtnId = 20110 + index;
                float deleteBtnX = groupStartX + loginBtnWidth + btnGap;
                bool isConfirmed = deleteConfirmStates.ContainsKey(account.Id) && deleteConfirmStates[account.Id];
                string deleteBtnText = isConfirmed ? "Sure?" : "Delete";
                Color deleteBtnColor = isConfirmed ? new Color(255, 100, 100) : new Color(200, 80, 80);

                if (Button.doButton(deleteBtnId, (int)deleteBtnX, (int)currentY,
                    (int)deleteBtnWidth, 20, deleteBtnText, deleteBtnColor))
                {
                    if (isConfirmed)
                    {
                        // ===== 执行删除 =====
                        // 1. 删除存档文件
                        string serverDir = Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves", "Client", currentServer.HostName);
                        string saveFile = Path.Combine(serverDir, $"save_{account.GameUsername}.xml");
                        string saveOLFile = Path.Combine(serverDir, $"save_OL_{account.GameUsername}.xml");
                        if (File.Exists(saveFile)) File.Delete(saveFile);
                        if (File.Exists(saveOLFile)) File.Delete(saveOLFile);
                        Console.WriteLine($"Deleted save files for {account.GameUsername}.");

                        // 2. 从服务器账户列表中移除
                        currentServer.Accounts.Remove(account);

                        // 3. 保存配置
                        SavePatches.SaveAllServersConfigToFile();

                        // 4. 清除确认状态
                        deleteConfirmStates.Remove(account.Id);

                        // 5. 重新打开当前屏幕以刷新列表
                        ScreenManager.AddScreen(new AccountListScreen(currentServer));
                        ExitScreen();
                        return; // 避免继续绘制
                    }
                    else
                    {
                        // 进入确认状态
                        deleteConfirmStates[account.Id] = true;
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

            if (Button.doButton(20001, (int)listX, (int)btnY, 160, 35, "Add Account", Color.LightGreen))
            {
                ScreenManager.AddScreen(new AddAccountScreen(currentServer));


            }

            if (Button.doButton(20002, (int)(listX + listWidth - 70), (int)btnY, 150, 35, "Back", Color.Gray))
            {
                ExitScreen();
            }

            GuiData.endDraw();
            PostProcessor.end();
        }
    }
}
