using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hacknet;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ClientPlugin.Listing;

public class AddAccountScreen : GameScreen
{
    private AvailableServer targetServer;
    private string terminalString = "";
    private List<string> history = new();
    private List<string> prompts = new()
    {
        "DISPLAY NAME :",
        "PASSWORD :",
        "GAME USERNAME (press Enter to use Display Name) :"
    };
    private List<string> answers = new();
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
        Rectangle dest = new(
            viewport.Width / 4, viewport.Height / 4,
            viewport.Width / 2, viewport.Height / 2
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
            TextBox.MaskingText = (promptIndex == 1); // 密码遮罩
            terminalString = TextBox.doTerminalTextField(
                8830301,
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
                    displayValue = "(autofilled with Display Name)";
                history.Add($"  {prompts[promptIndex - 1]} {displayValue}");
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

        // Back 按钮
        if (!isReady && promptIndex > 0)
        {
            int backBtnX = dest.X + 10;
            int backBtnY = dest.Y + dest.Height - 40;
            if (Button.doButton(8830302, backBtnX, backBtnY, 80, 25, "Back", Color.Gray))
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

        // 底部按钮
        int btnY2 = dest.Y + dest.Height - 60;
        if (isReady)
        {
            if (Button.doButton(8830303, x, btnY2 - 40, 100, 25, "Back", Color.Gray))
                GoBack();

            if (Button.doButton(8830304, x, btnY2, 150, 30, "SAVE", Color.White) ||
                Utils.keyPressed(GuiData.lastInput, Keys.Enter, null))
                SaveAccount();
        }

        if (Button.doButton(8830305, x + dest.Width - 150 - 80, btnY2, 150, 25, "CANCEL", new Color(125, 82, 82)))
            ExitScreen();

        GuiData.endDraw();
        PostProcessor.end();
    }

    private bool AttemptAdvance(string answer)
    {
        // Display Name 不能为空
        if (promptIndex == 0 && string.IsNullOrWhiteSpace(answer))
        {
            history.Add("ERROR: Display Name cannot be empty.");
            return false;
        }

        // Password 不能为空
        if (promptIndex == 1 && string.IsNullOrWhiteSpace(answer))
        {
            history.Add("ERROR: Password cannot be empty.");
            return false;
        }

        // GameUsername 可以为空（自动填充）
        if (promptIndex == 2)
        {
            if (answer == null) answer = "";
            // 检查重名
            if (!string.IsNullOrWhiteSpace(answer) &&
                targetServer.Accounts.Any(a => a.Name.Equals(answer, StringComparison.OrdinalIgnoreCase)))
            {
                history.Add($"ERROR: Account '{answer}' already exists on '{targetServer.HostName}'.");
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

        string displayName = answers[0];
        string password = answers[1];
        string gameUsername = answers[2];

        // GameUsername 空则用 Display Name
        if (string.IsNullOrWhiteSpace(gameUsername))
            gameUsername = displayName;

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(gameUsername))
        {
            history.Add("ERROR: Display Name and GameUsername cannot be empty.");
            return;
        }

        // 最终检查重名
        if (targetServer.Accounts.Any(a =>
            a.Name.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
        {
            history.Add("ERROR: Account with same name already exists.");
            return;
        }

        // 生成存档路径
        string saveDir = targetServer.FolderPath;
        if (!string.IsNullOrEmpty(saveDir) && !Directory.Exists(saveDir))
            Directory.CreateDirectory(saveDir);

        string saveFile = Path.Combine(saveDir, $"save_OL_{gameUsername}.xml");

        var account = new ClientAccountStructure
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = displayName,
            GameUsername = gameUsername,
            Password = password,
            SaveFilePath = saveFile
        };

        ClientProfileStorage.AddAccount(targetServer.HostName, account);

        // 创建空存档文件（最小可用 XML 根节点，后续进入游戏时会覆盖）
        if (!string.IsNullOrEmpty(saveFile) && !File.Exists(saveFile))
        {
            try
            {
                var emptyDoc = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XElement("Save",
                        new System.Xml.Linq.XElement("Player",
                            new System.Xml.Linq.XAttribute("id", account.Id)),
                        new System.Xml.Linq.XElement("Name", displayName),
                        new System.Xml.Linq.XElement("GameUserName", gameUsername),
                        new System.Xml.Linq.XElement("Password", password)
                    ));
                emptyDoc.Save(saveFile);
                Console.WriteLine($"[AddAccount] Created save file: {saveFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AddAccount] Failed to create save file: {ex.Message}");
            }
        }

        history.Add($"ACCOUNT ADDED: {displayName} (GameUsername: {gameUsername})");
        history.Add("Press CANCEL or ESC to return.");

        ExitScreen();
    }
}
