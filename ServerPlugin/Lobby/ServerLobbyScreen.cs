using System;
using Hacknet;
using Hacknet.Gui;
using HNC.Lobby;
using Microsoft.Xna.Framework;

namespace ServerPlugin;

/// <summary>
/// 服务端游戏里的准备大厅界面：终端 start 后弹出，开局（LAUNCH_SERVER）时关闭。
/// 显示客户端人数、名单和准备状态；主机可以准备 / 取消准备、移出客户端；
/// 全员准备但人数不足时弹出"是否强制开始"确认框。
/// </summary>
internal class ServerLobbyScreen : GameScreen
{
    // 按钮 id 段：8930000 起（避开原版和其它插件界面）
    private const int IdReady = 8930001;
    private const int IdForce = 8930002;
    private const int IdDecline = 8930003;
    private const int IdClose = 8930004;
    private const int IdKickBase = 8930100;

    private readonly LobbyChatBox _chat = new();

    public ServerLobbyScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(0.3);
        TransitionOffTime = TimeSpan.FromSeconds(0.3);
        IsPopup = false;
    }

    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
        if (otherScreenHasFocus || IsExiting) return;

        string? text = _chat.Update();
        if (text != null && ServerManager.IsConnected && ServerLobby.State != null)
            ServerLobby.SendChat(text);
    }

    public override void UnloadContent()
    {
        base.UnloadContent();
        ServerLobby.OnScreenUnloaded(this);
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
        try
        {
            DrawContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lobby] 界面绘制错误: {ex.Message}");
        }
        GuiData.endDraw();
        PostProcessor.end();
    }

    private void DrawContent()
    {
        Rectangle viewport = ScreenManager.GraphicsDevice.Viewport.Bounds;
        int cx = viewport.Width / 2;
        var state = ServerLobby.State;

        LobbyUi.Centered(GuiData.font, "PREPARATION LOBBY", cx, 20f, Color.White);
        LobbyUi.Centered(GuiData.smallfont, "Server: " + ServerLobby.ServerName, cx, 62f, Color.Gray);

        // 与终端断开：大厅无法继续，只能关闭
        if (!ServerManager.IsConnected)
        {
            LobbyUi.Centered(GuiData.smallfont, "Lost connection to the server terminal.", cx, 140f, Color.OrangeRed);
            if (Button.doButton(IdClose, cx - 80, 190, 160, 32, "Close", Color.Gray))
                ServerLobby.Reset();
            return;
        }

        if (state == null)
        {
            LobbyUi.Centered(GuiData.smallfont, "Waiting for lobby state from the terminal...", cx, 140f, Color.Gray);
            return;
        }

        LobbyUi.Centered(GuiData.smallfont, "Clients: " + LobbyUi.CapacityText(state), cx, 88f,
            state.Capacity > 0 && state.Count >= state.Capacity ? Color.LightGreen : Color.White);

        bool prompt = ServerLobby.ForcePrompt.HasValue && state.Phase == LobbyPhase.Lobby;
        bool canAct = state.Phase == LobbyPhase.Lobby && !prompt;

        // 左：名单（每个客户端一行，右侧 Kick）；右：聊天
        LobbyUi.TwoColumns(viewport, out var listArea, out var chatArea);
        _chat.Draw(chatArea, ServerLobby.Chat, canSend: true);
        int listCx = listArea.X + listArea.Width / 2;
        float y = LobbyUi.DrawRoster(state, listArea, selfPlayerId: int.MinValue, (m, row) =>
        {
            if (!canAct) return;
            if (Button.doButton(IdKickBase + m.PlayerId, row.Right - 70, row.Y + 2, 64, 20, "Kick", new Color(200, 80, 80)))
                ServerLobby.Kick(m.PlayerId);
        });

        // 状态说明
        string status = state.Phase switch
        {
            LobbyPhase.Lobby when state.AllReady && !state.IsFull => "Everyone is ready, but the lobby is not full.",
            LobbyPhase.Lobby => $"Waiting for everyone to get ready ({ReadyCount(state)}/{state.Count + 1}).",
            LobbyPhase.Launching => "Starting the game...",
            LobbyPhase.InGame => "Game in progress.",
            _ => ""
        };
        LobbyUi.Centered(GuiData.smallfont, status, listCx, y + 16f, Color.Gray);

        // 准备 / 取消准备
        if (canAct)
        {
            bool ready = state.HostReady;
            if (Button.doButton(IdReady, listCx - 110, viewport.Height - 80, 220, 36,
                ready ? "Cancel Ready" : "Ready", ready ? Color.Gray : Color.LightGreen))
            {
                ServerLobby.SetReady(!ready);
            }
        }

        if (prompt)
            DrawForcePrompt(viewport, ServerLobby.ForcePrompt!.Value);
    }

    /// <summary>全员已准备但人数不足：询问是否强制开始（不建议）</summary>
    private static void DrawForcePrompt(Rectangle viewport, (int Count, int Capacity) p)
    {
        var box = LobbyUi.DialogBox(viewport, 620, 210);
        int cx = box.X + box.Width / 2;
        LobbyUi.Centered(GuiData.font, "NOT ENOUGH PLAYERS", cx, box.Y + 16, Color.OrangeRed);
        LobbyUi.Centered(GuiData.smallfont, $"Clients: {p.Count} / {p.Capacity}. Everyone in the lobby is ready.", cx, box.Y + 62, Color.White);
        LobbyUi.Centered(GuiData.smallfont, "Force start anyway? NOT recommended:", cx, box.Y + 88, Color.White);
        LobbyUi.Centered(GuiData.smallfont, "the extension's story presets usually break with fewer players.", cx, box.Y + 110, Color.Gray);

        if (Button.doButton(IdForce, cx - 200, box.Bottom - 52, 180, 34, "Force Start", new Color(220, 120, 40)))
            ServerLobby.ForceStart();
        if (Button.doButton(IdDecline, cx + 20, box.Bottom - 52, 180, 34, "Wait for players", Color.Gray))
            ServerLobby.DeclineForceStart();
    }

    private static int ReadyCount(LobbyState s)
    {
        int n = s.HostReady ? 1 : 0;
        foreach (var m in s.Members) if (m.Ready) n++;
        return n;
    }
}
