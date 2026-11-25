using System;
using Hacknet;
using Hacknet.Gui;
using HNC.Lobby;
using Microsoft.Xna.Framework;

namespace ClientPlugin;

/// <summary>
/// 客户端的准备大厅界面：账户列表点"登录"后打开。
/// 显示名单和准备状态，等待其他玩家和服务端开始；服务端准备完毕后自动进入游戏。
/// 游戏已经开始时（中途加入、掉线重连）直接显示 Start Game 按钮。
/// </summary>
internal class ClientLobbyScreen : GameScreen
{
    // 按钮 id 段：8940000 起
    private const int IdReady = 8940001;
    private const int IdStart = 8940002;
    private const int IdBack = 8940003;
    private const int IdRetry = 8940004;
    private const int IdPhaseBase = 8940100;

    private bool _leaving;

    private readonly LobbyChatBox _chat = new();

    public ClientLobbyScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(0.3);
        TransitionOffTime = TimeSpan.FromSeconds(0.3);
        IsPopup = false;
    }

    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
        if (_leaving) return;

        ClientLobby.Update();
        if (ClientLobby.ConsumeStartRequest())
        {
            StartGame();
            return;
        }

        if (!otherScreenHasFocus)
        {
            string? text = _chat.Update();
            if (text != null) ClientLobby.SendChat(text);
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
        int bottomY = viewport.Height - 80;

        var server = ClientLobby.Server;
        var account = ClientLobby.Account;
        LobbyUi.Centered(GuiData.font, "PREPARATION LOBBY", cx, 20f, Color.White);
        LobbyUi.Centered(GuiData.smallfont,
            $"{server?.HostName}  ({server?.IP}:{server?.Port})   Account: {account?.Name}", cx, 62f, Color.Gray);

        if (_leaving) return;

        // 左：名单 / 状态；右：聊天
        LobbyUi.TwoColumns(viewport, out var listArea, out var chatArea);
        _chat.Draw(chatArea, ClientLobby.Chat, canSend: ClientLobby.Status == ClientLobbyStatus.Joined);
        int lx = listArea.X + listArea.Width / 2;

        if (ClientLobby.ReturnedFromGame)
            LobbyUi.Centered(GuiData.smallfont, "Connection lost during the game. Your progress was saved locally.",
                cx, 88f, Color.Orange);

        switch (ClientLobby.Status)
        {
            case ClientLobbyStatus.Connecting:
            case ClientLobbyStatus.Joining:
                LobbyUi.Centered(GuiData.smallfont, "Connecting to the lobby...", lx, 160f, Color.Yellow);
                break;

            case ClientLobbyStatus.NotOpen:
                LobbyUi.Centered(GuiData.smallfont, "The server has not opened the lobby yet.", lx, 160f, Color.Yellow);
                LobbyUi.Centered(GuiData.smallfont, "You will join automatically when it opens.", lx, 186f, Color.Gray);
                break;

            case ClientLobbyStatus.Full:
                LobbyUi.Centered(GuiData.smallfont,
                    $"The server is full ({ClientLobby.FullCount} / {ClientLobby.FullCapacity}).", lx, 160f, Color.OrangeRed);
                DrawRetry(lx, 210, "Try Again");
                break;

            case ClientLobbyStatus.Kicked:
                LobbyUi.Centered(GuiData.smallfont, "You were removed from the lobby by the server.", lx, 160f, Color.OrangeRed);
                DrawRetry(lx, 210, "Rejoin");
                break;

            case ClientLobbyStatus.Disconnected:
                LobbyUi.Centered(GuiData.smallfont, "Disconnected from the server.", lx, 160f, Color.OrangeRed);
                LobbyUi.Centered(GuiData.smallfont, $"Reconnecting automatically in {ClientLobby.SecondsUntilRetry}s...", lx, 186f, Color.Gray);
                DrawRetry(lx, 230, "Reconnect Now");
                break;

            case ClientLobbyStatus.Joined:
                DrawJoined(listArea, lx, bottomY);
                break;
        }

        // 返回账户列表（开局中不允许离开，避免服务端已在进游戏时名单变化）
        if (ClientLobby.State?.Phase != LobbyPhase.Launching &&
            Button.doButton(IdBack, viewport.Width - 190, bottomY, 150, 36, "Back", Color.Gray))
        {
            ClientLobby.Leave();
            Leave();
        }
    }

    private void DrawJoined(Rectangle listArea, int cx, int bottomY)
    {
        var state = ClientLobby.State;
        if (state == null)
        {
            LobbyUi.Centered(GuiData.smallfont, "Joined. Waiting for lobby state...", cx, 160f, Color.Gray);
            return;
        }

        if (!ClientLobby.ReturnedFromGame)
            LobbyUi.Centered(GuiData.smallfont, "Clients: " + LobbyUi.CapacityText(state), cx, 88f, Color.White);

        float y = LobbyUi.DrawRoster(state, listArea, ClientNetworkManager.PlayerId);

        string status;
        Color statusColor = Color.Gray;
        switch (state.Phase)
        {
            case LobbyPhase.Lobby when state.AllReady && !state.IsFull:
                status = "Everyone is ready. Waiting for the server to decide whether to start.";
                break;
            case LobbyPhase.Lobby when !state.HostReady && AllClientsReady(state):
                status = "Waiting for the server to get ready.";
                break;
            case LobbyPhase.Lobby:
                status = "Waiting for everyone to get ready.";
                break;
            case LobbyPhase.Launching:
                status = "The server is starting the game. You will enter automatically...";
                statusColor = Color.Yellow;
                break;
            case LobbyPhase.InGame:
                status = "The game is already running. You can enter now.";
                statusColor = Color.LightGreen;
                break;
            default:
                status = "";
                break;
        }
        LobbyUi.Centered(GuiData.smallfont, status, cx, y + 16f, statusColor);

        if (state.Phase == LobbyPhase.Lobby)
        {
            bool ready = ClientLobby.Self?.Ready == true;
            if (Button.doButton(IdReady, cx - 110, bottomY, 220, 36,
                ready ? "Cancel Ready" : "Ready", ready ? Color.Gray : Color.LightGreen))
            {
                ClientLobby.SetReady(!ready);
            }
        }
        else if (ClientLobby.CanStartNow)
        {
            bool offer = ClientPhases.ShouldOffer(state);
            if (offer)
                DrawPhasePicker(listArea, (int)y + 50, bottomY - 10);

            // 选了阶段预设时由预设自动进入；没选时照常可以直接开始（从扩展默认起点开新游戏）
            if (ClientPhases.Chosen == null &&
                Button.doButton(IdStart, cx - 110, bottomY, 220, 36,
                    offer ? "Start without preset" : "Start Game", offer ? Color.Gray : Color.LightGreen))
            {
                StartGame();
            }
        }
    }

    /// <summary>
    /// 中途加入时的阶段预设列表（本角色可用的）：已经过去的阶段不能选；
    /// 正在进行的阶段点 Join now 立即进入；还没开始的点 Wait for it，开始后自动进入。
    /// </summary>
    private static void DrawPhasePicker(Rectangle area, int top, int bottom)
    {
        var font = GuiData.smallfont;
        var options = ClientPhases.OptionsForRole();
        var current = ClientPhases.Current;
        var chosen = ClientPhases.Chosen;

        LobbyUi.Text(font, $"Choose a starting point (role: {ClientPhases.Role})", area.X + 8, top, Color.LightGreen);
        int y = top + LobbyUi.RowHeight + 4;

        for (int i = 0; i < options.Count && y + LobbyUi.RowHeight <= bottom; i++)
        {
            var p = options[i];
            bool started = ClientPhases.IsStarted(p);
            bool isCurrent = p == current;
            bool passed = started && !isCurrent;

            string status = passed ? "PASSED" : isCurrent ? "IN PROGRESS" : "NOT STARTED";
            Color color = passed ? Color.Gray : isCurrent ? Color.LightGreen : Color.White;
            LobbyUi.Text(font, $"[{p.Id}] {p.Title}", area.X + 8, y, color);
            LobbyUi.Text(font, status, area.X + area.Width * 0.5f, y, color * 0.8f);

            int bx = area.Right - 130;
            if (chosen == p)
            {
                if (Button.doButton(IdPhaseBase + i, bx, y, 124, 22, "Cancel", Color.Gray))
                    ClientPhases.ClearChoice();
            }
            else if (chosen == null && ClientPhases.CanChoose(p))
            {
                if (Button.doButton(IdPhaseBase + i, bx, y, 124, 22,
                    started ? "Join now" : "Wait for it", started ? Color.LightGreen : Color.LightBlue))
                    ClientPhases.Choose(p);
            }
            y += LobbyUi.RowHeight;

            if (!string.IsNullOrEmpty(p.Description) && y + LobbyUi.RowHeight <= bottom)
            {
                LobbyUi.Text(GuiData.tinyfont, p.Description, area.X + 24, y, Color.Gray);
                y += LobbyUi.RowHeight - 6;
            }
        }

        if (chosen != null && !ClientPhases.ChosenReady)
            LobbyUi.Text(font, $"Waiting for phase {chosen.Id} to start. You will enter automatically.",
                area.X + 8, Math.Min(y + 6, bottom), Color.Yellow);
    }

    private static void DrawRetry(int cx, int y, string label)
    {
        if (Button.doButton(IdRetry, cx - 90, y, 180, 32, label, Color.LightBlue))
            ClientLobby.Retry();
    }

    private static bool AllClientsReady(LobbyState s)
    {
        foreach (var m in s.Members) if (!m.Ready) return false;
        return true;
    }

    /// <summary>进入游戏：关掉大厅界面，走原有的 GameLauncher 流程（种子、存档在加入大厅时已经收到）</summary>
    private void StartGame()
    {
        var server = ClientLobby.Server;
        var account = ClientLobby.Account;
        ClientLobby.End();
        Leave();
        if (server != null && account != null)
        {
            ClientPhases.PrepareForStart();   // 选了阶段预设就加载它（没选则什么都不做）
            ClientLobby.MarkInSession();
            GameLauncher.LoginAndStart(server, account);
        }
    }

    private void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        ExitScreen();
    }
}
