using System;
using ClientPlugin.Listing;
using Hacknet;
using HNC.Lobby;

namespace ClientPlugin;

internal enum ClientLobbyStatus
{
    /// <summary>正在连接服务器</summary>
    Connecting,
    /// <summary>已发出加入请求，等待终端回复</summary>
    Joining,
    /// <summary>终端还没 start，大厅未开放（开放时自动重新加入）</summary>
    NotOpen,
    /// <summary>满员被拒</summary>
    Full,
    /// <summary>已在大厅里</summary>
    Joined,
    /// <summary>被服务端移出</summary>
    Kicked,
    /// <summary>与服务器断开</summary>
    Disconnected
}

/// <summary>
/// 客户端这一侧的准备大厅：账户列表点"登录"后进入，
/// 向终端发 LOBBY_JOIN（带真实账户名，终端据此确定玩家身份和存档），
/// 收到 LOBBY_START（或游戏已开始时玩家点 Start Game）后才真正进入游戏。
/// 所有方法都在游戏主线程调用（界面 Update / Draw、EventReceiver）。
/// </summary>
internal static class ClientLobby
{
    public static bool Active { get; private set; }
    public static ClientLobbyStatus Status { get; private set; }

    /// <summary>终端发来的最新大厅状态（只在已加入时有效）</summary>
    public static LobbyState? State { get; private set; }

    public static int FullCount { get; private set; }
    public static int FullCapacity { get; private set; }

    public static AvailableServer? Server { get; private set; }
    public static ClientAccountStructure? Account { get; private set; }

    /// <summary>大厅聊天记录</summary>
    public static readonly LobbyChatLog Chat = new();

    /// <summary>上一局在游戏中掉线、重连后发现服务器已重新开局，被送回大厅（界面上提示"进度已存在本地"）</summary>
    public static bool ReturnedFromGame { get; private set; }

    /// <summary>通过大厅进入了游戏（ClientPauseSync 据此判断是否在游戏中）</summary>
    public static bool InSession { get; private set; }

    /// <summary>断线后自动重连的间隔</summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);
    private static DateTime _nextRetry = DateTime.MinValue;

    private static bool _joinSent;
    private static bool _startRequested;

    /// <summary>账户列表点"登录"时调用</summary>
    public static void Begin(AvailableServer server, ClientAccountStructure account)
    {
        Server = server;
        Account = account;
        Active = true;
        State = null;
        Chat.Clear();
        ReturnedFromGame = false;
        HNC.Pause.HncPause.ClearAll();   // 终端会在加入时补发当前的全员暂停状态
        ClientPhases.ResetForLobby();    // 同上，阶段进度也会补发
        ClientNodeSync.Reset();
        HNC.Flags.FlagSync.Reset();       // 全局 flag：加入时终端会发 FLAG_ALL          // 共享节点状态：加入时服务端会全部重发
        _startRequested = false;
        _joinSent = false;
        _nextRetry = DateTime.MinValue;
        Status = ClientLobbyStatus.Connecting;

        ClientPluginMain.SelectedServer = server;
        ClientPluginMain.SelectedAccount = account;

        // 存档标志由本次加入时终端回的 HAS_SAVE / NO_SAVE 重新确定（加入时才按真实账户名查存档）
        ClientPluginMain.HasServerSave = false;
        ClientPluginMain.ServerReportedNoSave = false;
        ClientPluginMain.IsSaveFileReady = false;
        ClientPluginMain.PendingSaveFile = null;
        ClientPluginMain.PendingSaveChunks = null;
        ClientPluginMain.PendingSaveFileB64 = null;

        EnsureConnected();
        Console.WriteLine($"[Lobby] 进入准备大厅: {account.Name} @ {server.HostName}");
    }

    /// <summary>每帧（大厅界面 Update）：连上后发加入请求，检测断线</summary>
    public static void Update()
    {
        if (!Active) return;

        bool connected = ClientNetworkManager.IsConnected;
        if (connected && !_joinSent &&
            (Status == ClientLobbyStatus.Connecting || Status == ClientLobbyStatus.Joining))
        {
            _joinSent = true;
            Status = ClientLobbyStatus.Joining;
            ClientNetworkManager.Send(LobbyProtocol.Join + LobbyProtocol.ToB64(Account!.Name));
        }

        if (!connected && !ClientNetworkManager.IsConnecting &&
            Status != ClientLobbyStatus.Kicked && Status != ClientLobbyStatus.Disconnected)
        {
            Status = ClientLobbyStatus.Disconnected;
            State = null;
            _joinSent = false;
            _nextRetry = DateTime.UtcNow + RetryInterval;
            Console.WriteLine("[Lobby] 与服务器断开，稍后自动重连");
        }

        // 选中的阶段预设已经开始：自动进入游戏
        if (Status == ClientLobbyStatus.Joined && State?.Phase == LobbyPhase.InGame && ClientPhases.ChosenReady)
            _startRequested = true;

        // 断线后每隔几秒自动重连（连上后会自动重新加入大厅）
        if (Status == ClientLobbyStatus.Disconnected && DateTime.UtcNow >= _nextRetry)
        {
            _nextRetry = DateTime.UtcNow + RetryInterval;
            Retry();
        }
    }

    /// <summary>距离下次自动重连的秒数（界面显示用）</summary>
    public static int SecondsUntilRetry =>
        Math.Max(0, (int)Math.Ceiling((_nextRetry - DateTime.UtcNow).TotalSeconds));

    // ═══════════════════════════════════════════════ 游戏中掉线、无法原地继续 → 退回大厅

    /// <summary>
    /// 游戏中掉线后重连，发现服务器已经不在这一局（终端重开等），由 ClientPauseSync 调用：
    /// 关掉游戏界面（掉线时已本地存过档），回到准备大厅重新加入。
    /// 正常掉线不走这里——ClientPauseSync 会原地冻结、重连后原地继续。
    /// </summary>
    public static void ReturnToLobby()
    {
        InSession = false;

        var os = OS.currentInstance;
        var sman = Game1.getSingleton()?.sman;
        var server = Server;
        var account = Account;
        if (os == null || sman == null || server == null || account == null) return;

        // 玩家已经正常退回主菜单（OS.quitGame 不会清空 currentInstance）：不再弹回大厅
        if (os.HasExitedAndEnded || Array.IndexOf(sman.GetScreens(), os) < 0) return;

        try
        {
            OS.currentInstance = null;
            sman.RemoveScreen(os);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lobby] 关闭游戏界面失败: {ex.Message}");
        }

        Begin(server, account);
        ReturnedFromGame = true;
        sman.AddScreen(new ClientLobbyScreen());
    }

    /// <summary>大厅界面进入游戏时调用</summary>
    public static void MarkInSession() => InSession = true;

    /// <summary>处理终端发来的 LOBBY_* 消息；不是大厅消息返回 false。不在大厅界面时大厅消息一律忽略。</summary>
    public static bool TryHandle(string msg)
    {
        if (!msg.StartsWith(LobbyProtocol.Prefix, StringComparison.Ordinal)) return false;
        if (!Active) return true;

        if (msg.StartsWith(LobbyProtocol.Joined, StringComparison.Ordinal))
        {
            if (int.TryParse(msg.Substring(LobbyProtocol.Joined.Length), out int id))
                ClientNetworkManager.PlayerId = id;
            Status = ClientLobbyStatus.Joined;
        }
        else if (msg == LobbyProtocol.Closed)
        {
            // 不再重发，等 LOBBY_OPENED
            Status = ClientLobbyStatus.NotOpen;
        }
        else if (msg == LobbyProtocol.Opened)
        {
            if (Status == ClientLobbyStatus.NotOpen)
            {
                Status = ClientLobbyStatus.Joining;
                _joinSent = false;
            }
        }
        else if (msg.StartsWith(LobbyProtocol.Full, StringComparison.Ordinal))
        {
            var p = msg.Substring(LobbyProtocol.Full.Length).Split(':');
            FullCount = p.Length > 0 && int.TryParse(p[0], out int n) ? n : 0;
            FullCapacity = p.Length > 1 && int.TryParse(p[1], out int c) ? c : 0;
            Status = ClientLobbyStatus.Full;
            State = null;
        }
        else if (msg == LobbyProtocol.Kicked)
        {
            Status = ClientLobbyStatus.Kicked;
            State = null;
        }
        else if (msg.StartsWith(LobbyProtocol.State, StringComparison.Ordinal))
        {
            if (Status == ClientLobbyStatus.Joined)
                State = LobbyState.Decode(msg) ?? State;
        }
        else if (msg == LobbyProtocol.Start)
        {
            if (Status == ClientLobbyStatus.Joined)
                _startRequested = true;
        }
        else if (msg.StartsWith(LobbyProtocol.ChatMsg, StringComparison.Ordinal))
        {
            Chat.Add(msg);
        }
        return true;
    }

    /// <summary>发聊天（终端盖上发送者后再发回来，所以这里不直接加进记录）</summary>
    public static void SendChat(string text)
    {
        if (Status != ClientLobbyStatus.Joined) return;
        ClientNetworkManager.Send(LobbyProtocol.Chat + LobbyProtocol.ToB64(text));
    }

    /// <summary>终端通知可以进入游戏（界面每帧检查一次，在主循环里而不是网络回调里启动游戏）</summary>
    public static bool ConsumeStartRequest()
    {
        if (!_startRequested) return false;
        _startRequested = false;
        return true;
    }

    /// <summary>游戏已开始时（中途加入、重连），玩家可以直接点 Start Game</summary>
    public static bool CanStartNow =>
        Status == ClientLobbyStatus.Joined && State?.Phase == LobbyPhase.InGame;

    public static LobbyMember? Self
    {
        get
        {
            var s = State;
            if (s == null) return null;
            foreach (var m in s.Members)
                if (m.PlayerId == ClientNetworkManager.PlayerId) return m;
            return null;
        }
    }

    public static void SetReady(bool ready)
    {
        if (Status != ClientLobbyStatus.Joined) return;
        ClientNetworkManager.Send($"{LobbyProtocol.Ready}{(ready ? 1 : 0)}");
    }

    /// <summary>满员 / 被移出 / 断线后再试一次</summary>
    public static void Retry()
    {
        State = null;
        _joinSent = false;
        Status = ClientLobbyStatus.Connecting;
        EnsureConnected();
    }

    /// <summary>离开大厅（返回账户列表）</summary>
    public static void Leave()
    {
        if (Status == ClientLobbyStatus.Joined && ClientNetworkManager.IsConnected)
            ClientNetworkManager.Send(LobbyProtocol.Leave);
        End();
    }

    /// <summary>进入游戏或离开时结束大厅（之后的大厅消息忽略）</summary>
    public static void End()
    {
        Active = false;
        State = null;
        _startRequested = false;
    }

    private static void EnsureConnected()
    {
        var server = Server;
        if (server == null || ClientNetworkManager.IsConnected || ClientNetworkManager.IsConnecting) return;
        ClientNetworkManager.Connect(server.IP, server.Port, Account?.Name ?? server.HostName);
    }
}
