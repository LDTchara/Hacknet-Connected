using System;
using System.Collections.Generic;
using System.Linq;
using HNC.Lobby;
using LiteNetLib;

namespace HacknetServerTerminal;

/// <summary>
/// 准备大厅（终端持有权威状态）。
///
/// 流程：start → 大厅开放（服务端游戏弹出大厅界面）→ 客户端登录后加入大厅 → 全员准备
/// → 满员则直接开局；人数不足则询问服务端是否强制开始 → 终端发 LAUNCH_SERVER，服务端先进游戏
/// → 服务端准备完毕（LOBBY_GAME_READY）→ 通知客户端进入。之后进来的客户端（中途加入、掉线重连）直接可以开始。
///
/// 名额只算客户端，上限是 profile 的 PlayersCount（0 = 不限）。
/// 线程：LiteNetLib 轮询线程、Pipe 读线程、命令行线程都会调到这里，所有状态都在 _lock 里改。
/// </summary>
internal static class LobbyManager
{
    private static readonly object _lock = new();
    private static readonly LobbyState _state = new();

    /// <summary>peerId → 成员（和 _state.Members 里是同一批对象）</summary>
    private static readonly Dictionary<int, LobbyMember> _byPeer = new();

    /// <summary>peerId → 连接（用于给成员发消息）</summary>
    private static readonly Dictionary<int, NetPeer> _peers = new();

    /// <summary>全员准备后发给游戏进程的开局消息（start 时生成好）</summary>
    private static string _launchMsg = "";

    /// <summary>大厅所属的服务器名（玩家数据按服务器分文件）</summary>
    private static string _serverName = "";

    /// <summary>正在询问"人数不足是否强制开始"时记下当时的人数；-1 = 没在询问</summary>
    private static int _promptCount = -1;

    /// <summary>服务端游戏最近报告的已开始阶段（HNC_PHASES:...），新成员加入时补发</summary>
    private static string _phasesMsg = HNC.Pause.PauseProtocol.Phases;

    /// <summary>最近的聊天（已编码的 LOBBY_CHAT_MSG），新成员加入时补发</summary>
    private static readonly List<string> _chatHistory = new();
    private const int ChatHistoryMax = 50;

    public static LobbyPhase Phase
    {
        get { lock (_lock) return _state.Phase; }
    }

    /// <summary>服务端主机的账户名（大厅名单上显示的；还没 start 时为空）</summary>
    public static string HostName
    {
        get { lock (_lock) return _state.HostName; }
    }

    // ═══════════════════════════════════════════════ 开放大厅

    /// <summary>start 命令调用：开放大厅（不立即开局）。已经开放过返回 false。</summary>
    public static bool Open(ServerProfile profile, string launchMsg)
    {
        lock (_lock)
        {
            if (_state.Phase != LobbyPhase.Closed) return false;

            _state.Phase = LobbyPhase.Lobby;
            _state.Capacity = Math.Max(0, profile.PlayersCount);
            _state.HostReady = false;
            _state.HostName = profile.HostAccountName;
            _state.Members.Clear();
            _byPeer.Clear();
            _peers.Clear();
            _launchMsg = launchMsg;
            _serverName = profile.ServerName;
            _promptCount = -1;
            _chatHistory.Clear();
            _phasesMsg = HNC.Pause.PauseProtocol.Phases;
            PauseManager.Reset();
            TeamManager.Open(profile.ServerName);
            FlagRelay.Open(profile.ServerName);

            SendToMod($"{LobbyProtocol.Open}{_state.Capacity}:{LobbyProtocol.ToB64(profile.ServerName)}");
            SendToMod(_state.Encode());
            // 已经连上、停在"大厅未开放"界面的客户端收到后会自动重新加入
            NetworkManager.Broadcast(LobbyProtocol.Opened);

            Console.WriteLine($"[大厅] 已开放，客户端上限 {CapText()}。等待玩家加入并准备（服务端在游戏里的大厅界面操作）。");
            return true;
        }
    }

    // ═══════════════════════════════════════════════ 客户端消息

    /// <summary>LiteNetLib 线程调用：处理客户端发来的 LOBBY_* 消息</summary>
    public static void OnClientMessage(NetPeer peer, string msg)
    {
        if (msg.StartsWith(LobbyProtocol.Join, StringComparison.Ordinal))
        {
            // LOBBY_JOIN:{b64 账户名}[:resume]——resume = 游戏中掉线后原地重连，不需要存档
            var p = msg.Substring(LobbyProtocol.Join.Length).Split(':');
            string name = LobbyProtocol.FromB64(p[0]).Trim();
            bool resume = p.Length > 1 && p[1] == "resume";
            HandleJoin(peer, name, resume);
        }
        else if (msg == LobbyProtocol.Leave)
        {
            lock (_lock)
            {
                var m = RemoveMember(peer.Id);
                if (m == null) return;
                Console.WriteLine($"[大厅] {m.Name}(P{m.PlayerId}) 离开大厅 ({_state.Count}/{CapText()})");
                BroadcastState();
                Evaluate();
            }
        }
        else if (msg.StartsWith(LobbyProtocol.Chat, StringComparison.Ordinal))
        {
            lock (_lock)
            {
                if (!_byPeer.TryGetValue(peer.Id, out var m)) return;
                AddChat(m.PlayerId, m.Name, msg);
            }
        }
        else if (msg.StartsWith(LobbyProtocol.Ready, StringComparison.Ordinal))
        {
            bool ready = msg.Substring(LobbyProtocol.Ready.Length) == "1";
            lock (_lock)
            {
                if (_state.Phase != LobbyPhase.Lobby) return;
                if (!_byPeer.TryGetValue(peer.Id, out var m) || m.Ready == ready) return;
                m.Ready = ready;
                Console.WriteLine($"[大厅] {m.Name}(P{m.PlayerId}) {(ready ? "已准备" : "取消准备")}");
                BroadcastState();
                Evaluate();
            }
        }
    }

    private static void HandleJoin(NetPeer peer, string name, bool resume)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        lock (_lock)
        {
            if (_state.Phase == LobbyPhase.Closed)
            {
                NetworkManager.SendTo(peer, LobbyProtocol.Closed);
                return;
            }

            // 同一个连接重复发 JOIN（客户端重试）：同名就只补发一次状态
            if (_byPeer.TryGetValue(peer.Id, out var self))
            {
                if (self.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    NetworkManager.SendTo(peer, $"{LobbyProtocol.Joined}{self.PlayerId}");
                    NetworkManager.SendTo(peer, _state.Encode());
                    return;
                }
                RemoveMember(peer.Id);
            }

            // 同名账户已在大厅里（多半是掉线后旧连接还没超时）：踢掉旧连接，让新连接接替
            int? oldPeerId = FindPeerId(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (oldPeerId is int oldId)
            {
                _peers.TryGetValue(oldId, out var oldPeer);
                RemoveMember(oldId);
                NetworkManager.RemoveClient(oldId);
                try { oldPeer?.Disconnect(); } catch { }
                Console.WriteLine($"[大厅] {name} 从新连接重新加入，已断开旧连接 (Peer={oldId})");
            }

            if (_state.Capacity > 0 && _state.Count >= _state.Capacity)
            {
                NetworkManager.SendTo(peer, $"{LobbyProtocol.Full}{_state.Count}:{_state.Capacity}");
                Console.WriteLine($"[大厅] 已满员 ({_state.Count}/{_state.Capacity})，拒绝 {name} 加入");
                return;
            }

            // 确定玩家身份：PlayerId、存档（HAS_SAVE/NO_SAVE + 存档传输）、WELCOME、玩家列表
            var info = ServerNetListener.CompleteIdentity(peer, name, _serverName, sendSave: !resume);

            var member = new LobbyMember { PlayerId = info.PlayerId, Name = name, Ready = false };
            _state.Members.Add(member);
            _byPeer[peer.Id] = member;
            _peers[peer.Id] = peer;

            NetworkManager.SendTo(peer, $"{LobbyProtocol.Joined}{member.PlayerId}");
            Console.WriteLine($"[大厅] {name}(P{member.PlayerId}) 加入 ({_state.Count}/{CapText()})" +
                (_state.Phase == LobbyPhase.InGame ? " — 游戏已开始，可直接进入" : ""));
            BroadcastState();
            foreach (var line in _chatHistory)
                NetworkManager.SendTo(peer, line);
            // 当前的全员暂停状态和已开始的阶段（中途加入的人据此选预设、进游戏后照此暂停）
            PauseManager.SendStateTo(peer);
            NetworkManager.SendTo(peer, _phasesMsg);
            // 已开始的组队任务：补发支线身份分配；他是成员但还没接任务的补发 TEAM_START
            TeamManager.OnPlayerJoined(peer, member.PlayerId);
            FlagRelay.SendAllTo(peer);
            Evaluate();
        }
    }

    /// <summary>LiteNetLib 线程调用：连接断开（在 NetworkManager.RemoveClient 之前）</summary>
    public static void OnPeerDisconnected(int peerId)
    {
        lock (_lock)
        {
            var m = RemoveMember(peerId);
            if (m == null) return;
            Console.WriteLine($"[大厅] {m.Name}(P{m.PlayerId}) 断开连接，空出名额 ({_state.Count}/{CapText()})");
            BroadcastState();
            Evaluate();
        }
    }

    // ═══════════════════════════════════════════════ 服务端游戏消息

    /// <summary>Pipe 读线程调用：处理服务端游戏发来的 LOBBY_* 消息</summary>
    public static void OnModMessage(string msg)
    {
        if (msg.StartsWith(LobbyProtocol.Ready, StringComparison.Ordinal))
        {
            bool ready = msg.Substring(LobbyProtocol.Ready.Length) == "1";
            lock (_lock)
            {
                if (_state.Phase != LobbyPhase.Lobby || _state.HostReady == ready) return;
                _state.HostReady = ready;
                Console.WriteLine($"[大厅] 服务端 {(ready ? "已准备" : "取消准备")}");
                BroadcastState();
                Evaluate();
            }
        }
        else if (msg.StartsWith(LobbyProtocol.Kick, StringComparison.Ordinal))
        {
            if (int.TryParse(msg.Substring(LobbyProtocol.Kick.Length), out int pid))
                Kick(pid);
        }
        else if (msg.StartsWith(LobbyProtocol.Chat, StringComparison.Ordinal))
        {
            lock (_lock)
            {
                if (_state.Phase == LobbyPhase.Closed) return;
                AddChat(0, _state.HostName, msg);
            }
        }
        else if (msg == LobbyProtocol.ForceStart)
        {
            lock (_lock)
            {
                if (_state.Phase == LobbyPhase.Lobby && _state.AllReady)
                {
                    Launch($"服务端确认强制开始（人数 {_state.Count}/{CapText()}）");
                }
                else
                {
                    // 确认到达前状态已经变了（有人取消准备、加入或离开）
                    CancelPrompt();
                }
            }
        }
        else if (msg == LobbyProtocol.GameReady)
        {
            bool ready = false;
            lock (_lock)
            {
                if (_state.Phase != LobbyPhase.Launching) return;
                ready = true;
                _state.Phase = LobbyPhase.InGame;
                Console.WriteLine($"[大厅] 服务端已准备完毕，通知 {_state.Count} 个客户端进入游戏");
                BroadcastState();
                foreach (var peer in _peers.Values)
                    NetworkManager.SendTo(peer, LobbyProtocol.Start);
            }
            // 主机也是玩家：补发它在已开始组队任务里的分配（在大厅锁外调用）
            if (ready)
            {
                TeamManager.OnHostReady();
                FlagRelay.SendAllToHost();
            }
        }
    }

    /// <summary>Pipe 读线程：服务端游戏报告已开始的阶段（HNC_PHASES:...），记下并转给所有成员</summary>
    public static void OnPhases(string msg)
    {
        lock (_lock)
        {
            if (msg == _phasesMsg) return;
            _phasesMsg = msg;
            foreach (var peer in _peers.Values)
                NetworkManager.SendTo(peer, msg);
            var ids = msg.Substring(HNC.Pause.PauseProtocol.Phases.Length)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(LobbyProtocol.FromB64);
            Console.WriteLine($"[阶段] 已开始: {string.Join(", ", ids)}");
        }
    }

    /// <summary>把客户端移出大厅（服务端界面或终端 lobby kick 命令）。只在开局前可用。</summary>
    public static bool Kick(int playerId)
    {
        lock (_lock)
        {
            if (_state.Phase != LobbyPhase.Lobby) return false;
            if (FindPeerId(m => m.PlayerId == playerId) is not int peerId) return false;

            if (_peers.TryGetValue(peerId, out var peer))
                NetworkManager.SendTo(peer, LobbyProtocol.Kicked);
            var m = RemoveMember(peerId);
            Console.WriteLine($"[大厅] 已将 {m?.Name}(P{playerId}) 移出大厅 ({_state.Count}/{CapText()})");
            BroadcastState();
            Evaluate();
            return true;
        }
    }

    // ═══════════════════════════════════════════════ 内部

    /// <summary>检查是否全员准备；满员直接开局，人数不足则询问服务端（必须在 _lock 内调用）</summary>
    private static void Evaluate()
    {
        if (_state.Phase != LobbyPhase.Lobby) return;

        if (!_state.AllReady)
        {
            CancelPrompt();
            return;
        }

        if (_state.IsFull)
        {
            Launch(_state.Capacity > 0 ? "全员已准备且满员" : "全员已准备（不限人数）");
            return;
        }

        if (_promptCount != _state.Count)
        {
            _promptCount = _state.Count;
            SendToMod($"{LobbyProtocol.ConfirmForce}{_state.Count}:{_state.Capacity}");
            Console.WriteLine($"[大厅] 全员已准备，但人数不足 ({_state.Count}/{_state.Capacity})，等待服务端确认是否强制开始");
        }
    }

    private static void CancelPrompt()
    {
        if (_promptCount < 0) return;
        _promptCount = -1;
        SendToMod(LobbyProtocol.ConfirmCancel);
    }

    /// <summary>开局：服务端先进游戏，等它回 LOBBY_GAME_READY 再放客户端（必须在 _lock 内调用）</summary>
    private static void Launch(string reason)
    {
        _state.Phase = LobbyPhase.Launching;
        _promptCount = -1;
        Console.WriteLine($"[大厅] {reason}，服务端开始进入游戏...");
        SendToMod(_launchMsg);
        BroadcastState();
    }

    /// <summary>按条件找成员所在的连接号；找不到返回 null（必须在 _lock 内调用）</summary>
    private static int? FindPeerId(Func<LobbyMember, bool> match)
    {
        foreach (var kv in _byPeer)
            if (match(kv.Value)) return kv.Key;
        return null;
    }

    /// <summary>移出成员并撤销其玩家身份（必须在 _lock 内调用）</summary>
    private static LobbyMember? RemoveMember(int peerId)
    {
        if (!_byPeer.TryGetValue(peerId, out var m)) return null;
        _byPeer.Remove(peerId);
        _peers.Remove(peerId);
        _state.Members.Remove(m);
        NetworkManager.MarkLeft(peerId);
        return m;
    }

    /// <summary>
    /// 收到一条聊天（LOBBY_CHAT:{b64}）：盖上发送者，转给所有成员和服务端游戏，记入历史（必须在 _lock 内调用）。
    /// 发送者由终端按连接身份决定，客户端无法冒充别人。
    /// </summary>
    private static void AddChat(int playerId, string name, string rawMsg)
    {
        string text = LobbyProtocol.CleanChat(LobbyProtocol.FromB64(rawMsg.Substring(LobbyProtocol.Chat.Length)));
        if (text.Length == 0) return;

        string line = LobbyProtocol.EncodeChat(playerId, name, text);
        _chatHistory.Add(line);
        if (_chatHistory.Count > ChatHistoryMax) _chatHistory.RemoveAt(0);

        SendToMod(line);
        foreach (var peer in _peers.Values)
            NetworkManager.SendTo(peer, line);
        Console.WriteLine($"[大厅聊天] {(playerId == 0 ? "[服务端] " : "")}{name}: {text}");
    }

    private static void BroadcastState()
    {
        string s = _state.Encode();
        SendToMod(s);
        foreach (var peer in _peers.Values)
            NetworkManager.SendTo(peer, s);
    }

    private static void SendToMod(string msg) => _ = Program.SendToModFireForget(msg);

    private static string CapText() => _state.Capacity > 0 ? _state.Capacity.ToString() : "不限";

    /// <summary>lobby 命令：打印大厅状态</summary>
    public static void PrintStatus()
    {
        lock (_lock)
        {
            string phase = _state.Phase switch
            {
                LobbyPhase.Closed => "未开放（使用 start 开放大厅）",
                LobbyPhase.Lobby => "等待准备",
                LobbyPhase.Launching => "服务端正在进入游戏",
                LobbyPhase.InGame => "游戏中（客户端可直接进入）",
                _ => _state.Phase.ToString()
            };
            Console.WriteLine($"大厅: {phase}");
            if (_state.Phase == LobbyPhase.Closed) return;
            Console.WriteLine($"  客户端: {_state.Count}/{CapText()}");
            Console.WriteLine($"  [服务端] {_state.HostName}  {(_state.HostReady ? "已准备" : "未准备")}");
            foreach (var m in _state.Members)
                Console.WriteLine($"  [P{m.PlayerId}] {m.Name}  {(m.Ready ? "已准备" : "未准备")}");
        }
    }
}
