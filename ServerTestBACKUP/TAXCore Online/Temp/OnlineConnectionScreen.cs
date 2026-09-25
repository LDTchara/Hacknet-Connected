using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Gui;
using Hacknet.Effects;
using Hacknet.Screens;
using Hacknet.PlatformAPI.Storage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TAXCoreOnline
{
    public class OnlineConnectionScreen : GameScreen
    {
        private string terminalString = "";
        private List<string> history = new List<string>();
        private List<string> prompts = new List<string>() { "USERNAME :", "SERVER IP :", "PORT :", "PASSWORD :" };
        private List<string> answers = new List<string>();
        private int promptIndex = 0;
        private bool isReady = false;
        private bool inPasswordMode = false;
        private bool isSyncing = false;
        
        private List<ExtensionInfo> extensions = new List<ExtensionInfo>();
        private int selectedExtensionIndex = -1;
        private bool showingExtensions = false;
        private string serverExtensionName = null;

        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private HexGridBackground hexBackground;
        private static OnlineConnectionScreen Instance;

        public OnlineConnectionScreen()
        {
            this.TransitionOnTime = TimeSpan.FromSeconds(0.5);
            this.TransitionOffTime = TimeSpan.FromSeconds(0.5);
            this.hexBackground = new HexGridBackground(Game1.getSingleton().Content);
            Instance = this;
        }

        public override void LoadContent()
        {
            base.LoadContent();
            LoadExtensions();
            history.Add("-- TAXCore Online Connection --");
            terminalString = "";
            TextBox.cursorPosition = 0;
        }

        private void LoadExtensions()
        {
            if (!Directory.Exists("Extensions/")) Directory.CreateDirectory("Extensions/");
            string[] dirs = Directory.GetDirectories("Extensions/");
            foreach (var dir in dirs)
            {
                if (ExtensionInfo.ExtensionExists(dir))
                {
                    try { extensions.Add(ExtensionInfo.ReadExtensionInfo(dir)); } catch { }
                }
            }
            
            // If only one extension exists, auto-select it
            if (extensions.Count == 1)
            {
                selectedExtensionIndex = 0;
                history.Add("AUTO-SELECTED EXTENSION: " + extensions[0].Name);
            }
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
            ScreenManager.FadeBackBufferToBlack(255);
            GuiData.startDraw();
            
            Rectangle viewport = ScreenManager.GraphicsDevice.Viewport.Bounds;
            Rectangle dest = new Rectangle(viewport.Width / 4, viewport.Height / 4, viewport.Width / 2, viewport.Height / 2);
            
            // Draw Background - Same as New Session / Main Menu
            Rectangle fullScreen = new Rectangle(0, 0, viewport.Width, viewport.Height);
            GuiData.spriteBatch.Draw(Utils.white, fullScreen, Color.Black);
            if (Settings.DrawHexBackground)
            {
                this.hexBackground.Draw(fullScreen, GuiData.spriteBatch, Color.Transparent, Settings.lighterColorHexBackground ? new Color(20, 20, 20) : new Color(15, 15, 15, 0), HexGridBackground.ColoringAlgorithm.NegaitiveSinWash, 0f);
            }
            
            if (showingExtensions)
            {
                DrawExtensionSelection(dest);
            }
            else
            {
                DrawLoginStyle(dest);
            }
            
            GuiData.endDraw();
            PostProcessor.end();
        }

        private void DrawLoginStyle(Rectangle dest)
        {
            string currentPrompt = isSyncing ? "SYNCING STATE FROM SERVER..." : (isReady ? "READY - PRESS ENTER TO CONNECT" : prompts[promptIndex]);
            SpriteFont font = GuiData.smallfont;
            int promptWidth = (int)(font.MeasureString(currentPrompt).X + 12f);
            
            int y = dest.Y + 60;
            int x = dest.X + 40;

            // Draw Prompt
            GuiData.spriteBatch.DrawString(font, currentPrompt, new Vector2(x, y), Color.White);
            
            if (!isReady)
            {
                TextBox.MaskingText = inPasswordMode;
                terminalString = TextBox.doTerminalTextField(16392802, x + promptWidth, y - 2, dest.Width - promptWidth - 80, 20, 1, terminalString, GuiData.UISmallfont);
            }

            // Handle Enter
            if (TextBox.BoxWasActivated)
            {
                string displayStr = inPasswordMode ? new string('*', terminalString.Length) : terminalString;
                history.Add(currentPrompt + " " + displayStr);
                Advance(terminalString);
                terminalString = "";
                TextBox.cursorPosition = 0;
                TextBox.BoxWasActivated = false;
            }

            y += 50;
            
            // Draw History (Original Style)
            float lineHT = GuiData.ActiveFontConfig.tinyFontCharHeight + 10f;
            Vector2 pos = new Vector2(x, y);
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (pos.Y + lineHT > dest.Y + dest.Height - 120) break;
                GuiData.spriteBatch.DrawString(GuiData.tinyfont, history[i], pos, Color.White * 0.6f);
                pos.Y += lineHT;
            }

            // Buttons at the bottom
            int btnY = dest.Y + dest.Height - 60;
            
            // Confirm Button
            if (isReady)
            {
                if (Button.doButton(16392804, x, btnY, 150, 30, "CONNECT", Color.White) || Utils.keyPressed(GuiData.lastInput, Keys.Enter, null))
                {
                    Connect();
                }
            }

            // Cancel Button
            if (Button.doButton(16392806, x + dest.Width - 150 - 80, btnY, 150, 25, "CANCEL", new Color(125, 82, 82)))
            {
                ExitScreen();
            }
            
            // Extension Selection Button
            if (!isReady && promptIndex == 0)
            {
                string extName = selectedExtensionIndex >= 0 ? extensions[selectedExtensionIndex].Name : "Default (Server)";
                if (Button.doButton(16392808, x, btnY - 50, 300, 25, "Load Extension: " + extName, Color.DarkSlateGray))
                {
                    showingExtensions = true;
                }
            }
        }

        private void DrawExtensionSelection(Rectangle dest)
        {
            int x = dest.X + 40;
            int y = dest.Y + 40;
            TextItem.doFontLabel(new Vector2(x, y), "SELECT EXTENSION", GuiData.font, Color.White, dest.Width - 80, 40);
            y += 60;
            
            // Server Default Option
            if (Button.doButton(3998, x, y, dest.Width - 80, 25, "SERVER DEFAULT (SYNC FROM SERVER)", selectedExtensionIndex == -1 ? Color.Cyan : Color.White))
            {
                selectedExtensionIndex = -1;
                showingExtensions = false;
            }
            y += 35;

            for (int i = 0; i < extensions.Count; i++)
            {
                if (Button.doButton(3000 + i, x, y, dest.Width - 80, 25, extensions[i].Name, selectedExtensionIndex == i ? Color.Cyan : Color.White))
                {
                    selectedExtensionIndex = i;
                    showingExtensions = false;
                }
                y += 35;
                if (y > dest.Y + dest.Height - 100) break;
            }
            
            if (Button.doButton(3999, x, dest.Y + dest.Height - 60, 150, 25, "BACK", Color.Gray))
            {
                showingExtensions = false;
            }
        }

        private void Advance(string answer)
        {
            answers.Add(answer);
            promptIndex++;
            if (promptIndex >= prompts.Count)
            {
                isReady = true;
            }
            else
            {
                inPasswordMode = (prompts[promptIndex].Contains("PASSWORD"));
            }
        }

        private void Connect()
        {
            try
            {
                string user = answers[0];
                string ip = answers[1];
                int port = int.Parse(answers[2]);
                string pass = answers[3];

                client = new TcpClient(ip, port);
                stream = client.GetStream();
                
                TAXCoreOnline.Client = client;
                TAXCoreOnline.Stream = stream;
                TAXCoreOnline.Username = user;
                
                isSyncing = true; // Set syncing state
                
                byte[] loginData = Encoding.UTF8.GetBytes($"LOGIN:{user}:{pass}\n");
                stream.Write(loginData, 0, loginData.Length);
                
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();
                
                // We don't call StartGame here anymore. 
                // It will be called once SYNC_EXT is received or timed out.
            }
            catch (Exception ex)
            {
                history.Add("CONNECTION FAILED: " + ex.Message);
                promptIndex = 0;
                answers.Clear();
                isReady = false;
                isSyncing = false;
            }
        }

        private void StartGame()
        {
            // Reset OS state before starting a new session
            MainMenu.resetOS();

            if (selectedExtensionIndex >= 0)
            {
                ExtensionInfo info = extensions[selectedExtensionIndex];
                
                // CRITICAL: We MUST set IsInExtensionMode before initializing OS or anything else
                Settings.IsInExtensionMode = true;
                ExtensionLoader.ActiveExtensionInfo = info;
                
                // This is the correct way to initialize an extension's session
                // It handles themes, music, and internal settings.
                // We use the same method MainMenu uses via ExtensionMenuScreen
                SaveFileManager.Init(true);
                
                // Set the locale for the extension
                Hacknet.Localization.LocaleActivator.ActivateLocale(info.Language, Game1.getSingleton().Content);
            }
            else
            {
                // If no extension selected, ensure we are NOT in extension mode
                Settings.IsInExtensionMode = false;
                ExtensionLoader.ActiveExtensionInfo = null;
                SaveFileManager.Init(true); // Still init save manager for vanilla online
            }
            
            OS os = new OS();
            os.SaveUserAccountName = TAXCoreOnline.Username;
            os.SaveGameUserName = "online_save.xml";
            
            // If we are in extension mode, we need to load the extension data into the OS
            if (Settings.IsInExtensionMode && ExtensionLoader.ActiveExtensionInfo != null)
            {
                ExtensionLoader.LoadNewExtensionSession(ExtensionLoader.ActiveExtensionInfo, os);
            }
            
            ScreenManager.AddScreen(os, new PlayerIndex?(ScreenManager.controllingPlayer));
            ExitScreen();
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[8192];
            while (client != null && client.Connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    
                    foreach (var line in message.Split('\n'))
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        HandleServerMessage(line);
                    }
                }
                catch { break; }
            }
        }

        private void HandleServerMessage(string line)
        {
            if (line.StartsWith("NODE_LOAD:"))
            {
                string xml = line.Substring(10);
                // We need to be in OS context to load nodes
                // For now we queue it or handle it if OS exists
                Action loadAction = () => {
                    OS os = GetActiveOS();
                    if (os != null) {
                        try {
                            XmlReader reader = XmlReader.Create(new StringReader(xml));
                            Computer c = (Computer)ComputerLoader.loadComputer(xml, false, false);
                            if (c != null) {
                                os.netMap.nodes.Add(c);
                                os.write($"[SERVER] Hot-loaded node: {c.name}");
                            }
                        } catch (Exception ex) { Console.WriteLine("Failed to load node: " + ex.Message); }
                    }
                };
                QueueAction(loadAction);
            }
            else if (line.StartsWith("SYNC_EXT:"))
            {
                string extName = line.Substring(9);
                QueueAction(() => {
                    Instance.serverExtensionName = extName;
                    Instance.history.Add("SERVER EXTENSION: " + extName);
                    
                    // Try to auto-select the matching local extension
                    if (Instance.selectedExtensionIndex == -1 && extName != "None")
                    {
                        bool found = false;
                        for (int i = 0; i < Instance.extensions.Count; i++)
                        {
                            if (Instance.extensions[i].Name == extName)
                            {
                                Instance.selectedExtensionIndex = i;
                                Instance.history.Add("MATCHING LOCAL EXTENSION AUTO-SELECTED.");
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            Instance.history.Add("WARNING: SERVER USES EXTENSION [" + extName + "] WHICH WAS NOT FOUND LOCALLY.");
                        }
                    }
                    
                    // Now that we have synced the extension, we can start the game
                    Instance.isSyncing = false;
                    Instance.StartGame();
                });
            }
            else if (line.StartsWith("CHAT:"))
            {
                string[] parts = line.Split(':');
                if (parts.Length >= 3)
                {
                    string sender = parts[1];
                    string msg = string.Join(":", parts.Skip(2));
                    QueueAction(() => {
                        OS os = GetActiveOS();
                        if (os != null) os.write($"[CHAT] {sender}: {msg}");
                    });
                }
            }
            else if (line.StartsWith("SYSTEM:"))
            {
                string msg = line.Substring(7);
                QueueAction(() => {
                    OS os = GetActiveOS();
                    if (os != null) os.write($"[SYSTEM] {msg}");
                });
            }
            else if (line.StartsWith("PLAYER_ACTION:"))
            {
                string[] parts = line.Split(':');
                if (parts.Length >= 3)
                {
                    string sender = parts[1];
                    string action = string.Join(":", parts.Skip(2));
                    QueueAction(() => {
                        OS os = GetActiveOS();
                        if (os != null) os.write($"[ACTION] {sender}: {action}");
                    });
                }
            }
        }

        private static List<Action> actionQueue = new List<Action>();
        private static void QueueAction(Action a) { lock(actionQueue) actionQueue.Add(a); }
        
        public static void UpdateActions()
        {
            lock(actionQueue) {
                for (int i = 0; i < actionQueue.Count; i++) actionQueue[i].Invoke();
                actionQueue.Clear();
            }
        }

        private OS GetActiveOS()
        {
            if (ScreenManager == null) return null;
            foreach (var s in ScreenManager.GetScreens()) if (s is OS os) return os;
            return null;
        }
    }
}

