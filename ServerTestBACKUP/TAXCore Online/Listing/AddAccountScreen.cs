using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TAXCoreOnline.LoadAndSave.Client;

namespace TAXCoreOnline
{
    public class AddAccountScreen : GameScreen
    {
        private AvailableServer targetServer;

        private string terminalString = "";
        private List<string> history = new List<string>();
        private List<string> prompts = new List<string>()
        {
            "USERNAME (display name) :",
            "PASSWORD :",
            "GAME USERNAME (press Enter to use Username) :"
        };
        private List<string> answers = new List<string>();
        private int promptIndex = 0;
        private bool isReady = false;

        public AddAccountScreen(AvailableServer server)
        {
            targetServer = server;
        }

        public override void LoadContent()
        {
            base.LoadContent();
            history.Clear();
            history.Add($"-- ADD ACCOUNT TO {targetServer.HostName} --");
            history.Add("Enter account details:");
            terminalString = "";
            promptIndex = 0;
            isReady = false;
            TextBox.cursorPosition = 0;
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
            Rectangle dest = new Rectangle(
                viewport.Width / 4,
                viewport.Height / 4,
                viewport.Width / 2,
                viewport.Height / 2
            );
            GuiData.spriteBatch.Draw(Utils.white, dest, new Color(15, 15, 15));

            string currentPrompt = isReady
                ? "READY - PRESS ENTER TO SAVE"
                : prompts[promptIndex];

            SpriteFont font = GuiData.smallfont;
            int promptWidth = (int)(font.MeasureString(currentPrompt).X + 12f);
            int y = dest.Y + 60;
            int x = dest.X + 40;

            GuiData.spriteBatch.DrawString(font, currentPrompt, new Vector2(x, y), Color.White);

            if (!isReady)
            {
                TextBox.MaskingText = (promptIndex == 1);
                terminalString = TextBox.doTerminalTextField(
                    18393001,
                    x + promptWidth, y - 2,
                    dest.Width - promptWidth - 80, 20, 1,
                    terminalString, GuiData.UISmallfont
                );
            }

            if (TextBox.BoxWasActivated)
            {
                string value = terminalString;
                if (AttemptAdvance(value))
                {
                    string displayValue = value;
                    if (promptIndex == 2 && string.IsNullOrEmpty(value))
                        displayValue = "(autofilled with Username)";
                    history.Add($"  {currentPrompt} {displayValue}");
                    terminalString = "";
                    TextBox.cursorPosition = 0;
                }
                else
                {
                    terminalString = "";
                    TextBox.cursorPosition = 0;
                }
                TextBox.BoxWasActivated = false;
            }

            // 非就绪状态：左下角 Back 按钮
            if (!isReady && promptIndex > 0)
            {
                int backBtnX = dest.X + 10;
                int backBtnY = dest.Y + dest.Height - 40;
                if (Button.doButton(18393002, backBtnX, backBtnY, 80, 25, "Back", Color.Gray))
                    GoBack();
            }

            // 历史记录
            y += 50;
            float lineHT = GuiData.ActiveFontConfig.tinyFontCharHeight + 10f;
            Vector2 pos = new Vector2(x, y);
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (pos.Y + lineHT > dest.Y + dest.Height - 120) break;
                GuiData.spriteBatch.DrawString(GuiData.tinyfont, history[i], pos, Color.White * 0.6f);
                pos.Y += lineHT;
            }

            // 就绪状态：显示 Save 和 Back 按钮
            int btnY2 = dest.Y + dest.Height - 60;
            if (isReady)
            {
                if (Button.doButton(18393003, x, btnY2 - 40, 100, 25, "Back", Color.Gray))
                    GoBack();

                if (Button.doButton(18393004, x, btnY2, 150, 30, "SAVE", Color.White) ||
                    Utils.keyPressed(GuiData.lastInput, Keys.Enter, null))
                    SaveAccount();
            }

            if (Button.doButton(18393005, x + dest.Width - 150 - 80, btnY2, 150, 25, "CANCEL", new Color(125, 82, 82)))
                ExitScreen();

            GuiData.endDraw();
            PostProcessor.end();
        }

        private bool AttemptAdvance(string answer)
        {
            // 第一个字段（Name）不能为空，且立即检查重名（与已有账户的 Name 或 GameUsername）
            if (promptIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(answer))
                {
                    history.Add("ERROR: Username cannot be empty.");
                    return false;
                }
                if (IsNameOrGameUsernameDuplicate(answer))
                {
                    history.Add($"ERROR: Username '{answer}' already exists on server '{targetServer.HostName}'.");
                    return false;
                }
            }

            // 第二个字段（Password）不能为空
            if (promptIndex == 1 && string.IsNullOrWhiteSpace(answer))
            {
                history.Add("ERROR: Password cannot be empty.");
                return false;
            }

            // 第三个字段（GameUsername）可以为空，如果非空则立即检查重名
            if (promptIndex == 2)
            {
                if (answer == null) answer = "";
                if (!string.IsNullOrWhiteSpace(answer) && IsNameOrGameUsernameDuplicate(answer))
                {
                    history.Add($"ERROR: GameUsername '{answer}' already exists on server '{targetServer.HostName}'.");
                    return false;
                }
            }

            if (promptIndex < answers.Count)
                answers[promptIndex] = answer;
            else
                answers.Add(answer);

            promptIndex++;
            if (promptIndex >= prompts.Count)
                isReady = true;
            return true;
        }

        /// <summary>
        /// 检查 name 是否与已有账户的 Name 或 GameUsername 重复（忽略大小写）
        /// </summary>
        private bool IsNameOrGameUsernameDuplicate(string name)
        {
            if (targetServer.Accounts == null) return false;
            return targetServer.Accounts.Any(a =>
                (a.Name != null && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                (a.GameUsername != null && a.GameUsername.Equals(name, StringComparison.OrdinalIgnoreCase))
            );
        }

        private void GoBack()
        {
            if (isReady)
            {
                isReady = false;
                promptIndex = prompts.Count - 1;
                if (promptIndex < answers.Count)
                {
                    answers[promptIndex] = "";
                    terminalString = "";
                    TextBox.cursorPosition = 0;
                }
            }
            else
            {
                if (promptIndex <= 0) return;
                if (promptIndex < answers.Count)
                    answers[promptIndex] = terminalString;
                else
                    answers.Add(terminalString);
                promptIndex--;
            }

            if (promptIndex < answers.Count)
                terminalString = answers[promptIndex];
            else
                terminalString = "";
            TextBox.cursorPosition = terminalString.Length;
            history.Add($"--- Back to {prompts[promptIndex]} ---");
        }

        private void SaveAccount()
        {
            if (answers.Count < 3)
            {
                history.Add("ERROR: Missing input fields.");
                return;
            }

            string username = answers[0];
            string password = answers[1];
            string gameUsername = answers[2];

            if (string.IsNullOrWhiteSpace(gameUsername))
                gameUsername = username;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(gameUsername))
            {
                history.Add("ERROR: Username and GameUsername cannot be empty.");
                return;
            }

            // 最终检查：防止自动填充后重复
            if (targetServer.Accounts != null &&
                targetServer.Accounts.Any(a =>
                    (a.Name != null && a.Name.Equals(username, StringComparison.OrdinalIgnoreCase)) ||
                    (a.GameUsername != null && a.GameUsername.Equals(gameUsername, StringComparison.OrdinalIgnoreCase))
                ))
            {
                history.Add("ERROR: Account with same Name or GameUsername already exists.");
                return;
            }

            var newAccount = new ClientAccountStructure
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = username,
                GameUsername = gameUsername,
                Password = password,
                SaveFilePath = "",
                Flags = "needs_initial_save"
            };

            if (targetServer.Accounts == null)
                targetServer.Accounts = new HashSet<ClientAccountStructure>();
            targetServer.Accounts.Add(newAccount);

            SavePatches.SaveAllServersConfigToFile();
            // 确保账户对应的存档文件存在（空文件即可）
            string accountSavePath = Path.Combine(
                TAXCoreOnline.EnvPath, "OnlineSaves", "Client",
                targetServer.HostName, $"save_OL_{gameUsername}.xml"
            );
            string accountDir = Path.GetDirectoryName(accountSavePath);
            if (!Directory.Exists(accountDir))
                Directory.CreateDirectory(accountDir);
            if (!File.Exists(accountSavePath))
            {

                // 写入一个最小可用的 XML 根节点，后续保存游戏时会覆盖
                var emptyDoc = new XDocument(new XElement("Save"));
                emptyDoc.Save(accountSavePath);
                Console.WriteLine($"Created initial save file: {accountSavePath}");
            }

            history.Add($"ACCOUNT ADDED: {username} (GameUsername: {gameUsername})");
            history.Add("Press CANCEL or ESC to return.");

            ExitScreen();
        }
    }
}
