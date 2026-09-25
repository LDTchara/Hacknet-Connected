using System;
using System.Collections.Generic;
using System.IO;                               // 添加此命名空间
using System.Linq;
using Hacknet;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TAXCoreOnline.LoadAndSave.Client;

namespace TAXCoreOnline
{
    public class AddServerScreen : GameScreen
    {
        private string terminalString = "";
        private List<string> history = new List<string>();
        private List<string> prompts = new List<string>()
        {
            "HOST NAME :",
            "IP ADDRESS :",
            "PORT :"
        };
        private List<string> answers = new List<string>();
        private int promptIndex = 0;
        private bool isReady = false;
        private bool nameError = false;

        private AvailableServer editingServer = null;
        private bool isEditMode = false;

        public AddServerScreen() { }
        public AddServerScreen(AvailableServer serverToEdit)
        {
            editingServer = serverToEdit;
            isEditMode = true;
        }

        public override void LoadContent()
        {
            base.LoadContent();
            history.Clear();
            if (isEditMode && editingServer != null)
            {
                history.Add("-- EDIT SERVER --");
                history.Add("Press ENTER to keep current value or modify it.");
                answers = new List<string>
                {
                    editingServer.HostName,
                    editingServer.IP,
                    editingServer.Port.ToString()
                };
                terminalString = answers[0];
                promptIndex = 0;
                isReady = false;
            }
            else
            {
                history.Add("-- ADD NEW SERVER --");
                history.Add("Enter server details:");
                terminalString = "";
                promptIndex = 0;
                isReady = false;
            }
            TextBox.cursorPosition = terminalString.Length;
            nameError = false;
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
                ? (isEditMode ? "READY - PRESS ENTER OR SAVE TO UPDATE" : "READY - PRESS ENTER TO SAVE")
                : prompts[promptIndex];

            SpriteFont font = GuiData.smallfont;
            int promptWidth = (int)(font.MeasureString(currentPrompt).X + 12f);
            int y = dest.Y + 60;
            int x = dest.X + 40;

            GuiData.spriteBatch.DrawString(font, currentPrompt, new Vector2(x, y), Color.White);

            if (!isReady)
            {
                TextBox.MaskingText = false;
                terminalString = TextBox.doTerminalTextField(
                    18392802,
                    x + promptWidth, y - 2,
                    dest.Width - promptWidth - 80, 20, 1,
                    terminalString, GuiData.UISmallfont
                );
            }

            if (TextBox.BoxWasActivated)
            {
                string displayStr = terminalString;
                bool advanced = AttemptAdvance(displayStr);
                if (advanced)
                {
                    if (!nameError)
                        history.Add(currentPrompt + " " + displayStr);
                    terminalString = "";
                    TextBox.cursorPosition = 0;
                    nameError = false;
                    if (isEditMode && !isReady && promptIndex < answers.Count)
                    {
                        terminalString = answers[promptIndex];
                        TextBox.cursorPosition = terminalString.Length;
                    }
                }
                else
                {
                    nameError = true;
                    terminalString = "";
                    TextBox.cursorPosition = 0;
                }
                TextBox.BoxWasActivated = false;
            }

            if (!isReady && promptIndex > 0)
            {
                int backBtnX = dest.X + 10;
                int backBtnY = dest.Y + dest.Height - 40;
                if (Button.doButton(18392807, backBtnX, backBtnY, 80, 25, "Back", Color.Gray))
                    GoBack();
            }

            y += 50;
            float lineHT = GuiData.ActiveFontConfig.tinyFontCharHeight + 10f;
            Vector2 pos = new Vector2(x, y);
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (pos.Y + lineHT > dest.Y + dest.Height - 120) break;
                GuiData.spriteBatch.DrawString(GuiData.tinyfont, history[i], pos, Color.White * 0.6f);
                pos.Y += lineHT;
            }

            int btnY2 = dest.Y + dest.Height - 60;

            if (isReady)
            {
                if (Button.doButton(18392809, x, btnY2 - 40, 100, 25, "Back", Color.Gray))
                    GoBack();

                if (Button.doButton(18392804, x, btnY2, 150, 30, "SAVE", Color.White) ||
                    Utils.keyPressed(GuiData.lastInput, Keys.Enter, null))
                    SaveServer();
            }

            if (Button.doButton(18392806, x + dest.Width - 150 - 80, btnY2, 150, 25, "CANCEL", new Color(125, 82, 82)))
                ExitScreen();

            GuiData.endDraw();
            PostProcessor.end();
        }

        private bool AttemptAdvance(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer) && promptIndex == 0)
            {
                history.Add("ERROR: Host name cannot be empty.");
                return false;
            }
            if (isEditMode && promptIndex < answers.Count)
                answers[promptIndex] = answer;
            else
                answers.Add(answer);

            if (promptIndex == 0)
            {
                if (IsNameDuplicate(answer))
                {
                    history.Add($"ERROR: Server with name '{answer}' already exists. Please use a different name.");
                    if (isEditMode && promptIndex < answers.Count)
                        answers[promptIndex] = editingServer.HostName;
                    else
                        answers.RemoveAt(answers.Count - 1);
                    return false;
                }
            }

            promptIndex++;
            if (promptIndex >= prompts.Count)
                isReady = true;
            return true;
        }

        private bool IsNameDuplicate(string name)
        {
            return TAXCoreOnline.IDToServers.Values
                .Any(s => s.HostName.Equals(name, StringComparison.OrdinalIgnoreCase)
                          && (editingServer == null || s.LocalID != editingServer.LocalID));
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
                if (isEditMode && promptIndex < answers.Count)
                    answers[promptIndex] = terminalString;
                else
                {
                    if (promptIndex < answers.Count)
                        answers[promptIndex] = terminalString;
                    else
                        answers.Add(terminalString);
                }
                promptIndex--;
            }
            if (promptIndex < answers.Count)
            {
                terminalString = answers[promptIndex];
            }
            else
                terminalString = "";
            TextBox.cursorPosition = terminalString.Length;
            history.Add($"--- Back to {prompts[promptIndex]} ---");
            nameError = false;
        }

        private void SaveServer()
        {
            if (!int.TryParse(answers[2], out int port))
            {
                history.Add("INVALID PORT NUMBER. TRY AGAIN.");
                promptIndex = prompts.Count - 1;
                isReady = false;
                terminalString = answers[2];
                TextBox.cursorPosition = terminalString.Length;
                return;
            }
            string hostName = answers[0];
            string ip = answers[1];

            bool ipPortExists = TAXCoreOnline.IDToServers.Values
                .Any(s => s.IP == ip && s.Port == port
                          && (editingServer == null || s.LocalID != editingServer.LocalID));
            if (ipPortExists)
            {
                history.Add($"ERROR: Server with IP {ip}:{port} already exists. Please use a different combination.");
                isReady = false;
                promptIndex = prompts.Count - 1;
                answers[promptIndex] = "";
                terminalString = "";
                TextBox.cursorPosition = 0;
                return;
            }

            string localID;
            if (isEditMode && editingServer != null)
            {
                localID = editingServer.LocalID;
                var server = TAXCoreOnline.IDToServers[localID];
                server.HostName = hostName;
                server.IP = ip;
                server.Port = port;
            }
            else
            {
                localID = Guid.NewGuid().ToString("N").Substring(0, 8);
                var server = new AvailableServer
                {
                    LocalID = localID,
                    HostName = hostName,
                    IP = ip,
                    Port = port,
                    Accounts = new HashSet<ClientAccountStructure>()
                };
                TAXCoreOnline.IDToServers[localID] = server;
            }

            // 立即创建服务器文件夹（即使初始为空）
            string serverDir = Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves", "Client", hostName);
            try
            {
                Directory.CreateDirectory(serverDir);  // 如果已存在则不会报错
                Console.WriteLine($"Created/verified server directory: {serverDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating server directory: {ex.Message}");
            }

            SavePatches.SaveAllServersConfigToFile();
            history.Add($"SERVER {(isEditMode ? "UPDATED" : "ADDED")}: {hostName} ({ip}:{port})");
            history.Add("Press CANCEL or ESC to return.");
            ExitScreen();
        }
    }
}
