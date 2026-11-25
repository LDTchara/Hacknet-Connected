using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using static HacknetServerTerminal.NetworkManager;

namespace HacknetServerTerminal;

internal static class NetworkManager
{
    private static NetManager? _netManager;
    private static int _nextPlayerId = 1;

    // ── 上传存档分块累积 ──
    internal static readonly System.Collections.Generic.Dictionary<string, ChunkedUpload> Uploads = new();

    internal class ChunkedUpload
    {
        public int Total;
        public string? Username;
        public string[]? Chunks;
    }

    public static int PeerCount => _netManager?.ConnectedPeersCount ?? 0;

    /// <summary>peerId → 玩家信息</summary>
    private static readonly Dictionary<int, ClientInfo> _clients = new();

    /// <summary>_clients 的锁：LiteNetLib 轮询线程、Pipe 读线程、命令行线程都会访问</summary>
    private static readonly object _clientsLock = new();

    public static void Start(int port)
    {
        var listener = new ServerNetListener();
        _netManager = new NetManager(listener)
        {
            AutoRecycle = true,
            UpdateTime = 15,
            PingInterval = 2000,
            DisconnectTimeout = 30000
        };
        _netManager.Start(port);
        Console.WriteLine($"[网络] LiteNetLib 监听端口 {port}");

        _ = Task.Run(async () =>
        {
            var lastTick = DateTime.UtcNow;
            while (_netManager is { IsRunning: true })
            {
                _netManager.PollEvents();

                // 每秒检查一次游戏中客户端的心跳（和网络回调在同一个线程）
                if ((DateTime.UtcNow - lastTick).TotalSeconds >= 1)
                {
                    lastTick = DateTime.UtcNow;
                    try { PauseManager.Tick(); }
                    catch (Exception ex) { Console.WriteLine($"[暂停] 检查心跳出错: {ex.Message}"); }
                }

                await Task.Delay(15);
            }
        });
    }

    public static void Stop()
    {
        _netManager?.Stop();
        Console.WriteLine("[网络] LiteNetLib 已停止");
    }

    // ── 消息发送 ──

    /// <summary>广播给所有已注册身份的客户端</summary>
    public static void Broadcast(string msg)
    {
        if (_netManager is not { ConnectedPeersCount: > 0 }) return;
        var writer = new NetDataWriter();
        writer.Put(msg);
        Log("NET_SEND", $"Broadcast: {msg}");
        _netManager.SendToAll(writer, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>发送给单个客户端</summary>
    public static void SendTo(NetPeer peer, string msg)
    {
        var writer = new NetDataWriter();
        writer.Put(msg);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>发送给指定 playerId</summary>
    public static void SendToPlayer(int playerId, string msg)
    {
        ClientInfo? client;
        lock (_clientsLock)
            client = _clients.Values.FirstOrDefault(c => c.PlayerId == playerId && c.Joined);
        if (client?.Peer != null)
            SendTo(client.Peer, msg);
    }

    /// <summary>
    /// HNC.Sync 消息（服务端游戏 → 客户端）按头部路由，不解析载荷：
    ///   SYNC:{OP}:{scope}:{playerId|*}:{b64 key}:{version}:{b64 payload}
    /// playerId 为 * → 广播；否则只发给该 PlayerId 的客户端。
    /// </summary>
    public static void RouteSync(string msg)
    {
        var parts = msg.Split(':', 5);
        if (parts.Length < 5) return;
        if (parts[3] == "*")
            Broadcast(msg);
        else if (int.TryParse(parts[3], out int pid))
            SendToPlayer(pid, msg);
    }

    /// <summary>
    /// 服务端游戏发来的邮件：MAIL_OUT:{收件人id}:{b64 主题}:{b64 正文}。
    /// 转给在线的收件人（发件人显示为 "Server"），并回 MAIL_RESULT:{id}:{ok|offline} 给游戏进程。
    /// </summary>
    public static void HandleServerMail(string msg)
    {
        var p = msg.Split(':', 4);
        if (p.Length != 4 || !int.TryParse(p[1], out int toId)) return;

        var target = GetAllClients().FirstOrDefault(c => c.PlayerId == toId && c.Joined && c.Peer != null);
        if (target == null || p[2].Length > 1400 || p[3].Length > 12000)
        {
            _ = Program.SendToModFireForget($"MAIL_RESULT:{toId}:offline");
            return;
        }

        string from64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("Server"));
        SendTo(target.Peer!, $"MAIL:0:{from64}:{p[2]}:{p[3]}");
        _ = Program.SendToModFireForget($"MAIL_RESULT:{toId}:ok");
        Console.WriteLine($"[邮件] Server → {target.PlayerName}(P{toId})");
    }

    // ── 连接管理 ──

    /// <summary>
    /// 登记一个连接。
    /// HELLO 时只做临时登记（joined=false，PlayerId 为负数，不进玩家列表）；
    /// 加入大厅时用真实账户名重新登记（joined=true，PlayerId 来自 PlayerDataManager）。
    /// </summary>
    /// <param name="overrideId">指定 PlayerId，不指定则用内部自增计数器</param>
    public static ClientInfo RegisterClient(NetPeer peer, string playerName, int? overrideId = null, bool joined = false)
    {
        int pid;
        lock (_clientsLock) pid = overrideId ?? _nextPlayerId++;
        var info = new ClientInfo
        {
            PeerId = peer.Id,
            PlayerId = pid,
            PlayerName = playerName,
            Peer = peer,
            Joined = joined
        };
        lock (_clientsLock) _clients[peer.Id] = info;
        if (joined)
            Console.WriteLine($"[网络] 注册: {playerName} → PlayerId={info.PlayerId} (Peer={peer.Id})");
        else
            Console.WriteLine($"[网络] 临时登记: {playerName} (Peer={peer.Id})，加入大厅后确定身份");
        return info;
    }

    public static void RemoveClient(int peerId)
    {
        ClientInfo? info;
        bool removed;
        lock (_clientsLock) removed = _clients.Remove(peerId, out info);
        if (removed && info != null)
            Console.WriteLine($"[网络] 移除: {info.PlayerName} (PlayerId={info.PlayerId})");
    }

    /// <summary>离开 / 被移出大厅：保留连接，但撤销玩家身份（不再出现在玩家列表里）</summary>
    public static void MarkLeft(int peerId)
    {
        bool changed = false;
        lock (_clientsLock)
        {
            if (_clients.TryGetValue(peerId, out var info) && info.Joined)
            {
                info.Joined = false;
                changed = true;
            }
        }
        if (changed) BroadcastPlayerList();
    }

    public static ClientInfo? GetClient(int peerId)
    {
        lock (_clientsLock)
            return _clients.TryGetValue(peerId, out var info) ? info : null;
    }

    public static List<ClientInfo> GetAllClients()
    {
        lock (_clientsLock)
            return _clients.Values.ToList();
    }

    /// <summary>广播玩家列表给所有客户端（只含已加入大厅、确定了身份的玩家）</summary>
    public static void BroadcastPlayerList()
    {
        var players = GetAllClients().Where(c => c.Joined).ToList();
        var names = string.Join(",", players.Select(c => $"{c.PlayerId}:{c.PlayerName}"));

        // 服务端游戏（主机玩家）也是邮件的收发对象：给客户端的列表里加上 0 号 "Server"
        string forClients = Program.IsModConnected
            ? (names.Length > 0 ? "0:Server," + names : "0:Server")
            : names;
        Broadcast($"PLAYER_LIST:{forClients}");

        // 服务端游戏的 sendmail list 用（不含它自己）
        _ = Program.SendToModFireForget($"PLAYERS:{names}");
    }

    /// <summary>net_log.txt 写锁（防后台 Pipe 线程和 LiteNetLib 线程并发写）</summary>
    private static readonly object _netLogLock = new();

    internal static void Log(string tag, string msg)
    {
        // 大消息（存档分块、HNC.Sync 全量快照）只记开头，避免每条都把几十 KB 写进日志
        if (msg.Length > 300)
            msg = msg.Substring(0, 300) + $"...(共 {msg.Length} 字符)";
        lock (_netLogLock)
        {
            File.AppendAllText("net_log.txt",
                $"{DateTime.Now:HH:mm:ss.fff} [{tag}] {msg}{Environment.NewLine}");
        }
    }
}

internal class ClientInfo
{
    public int PeerId;
    public int PlayerId;
    public string PlayerName = "";
    public int Ping;
    public NetPeer? Peer;

    /// <summary>已通过 LOBBY_JOIN 确定身份（false = 只是连上来取种子 / 已离开大厅）</summary>
    public bool Joined;

    /// <summary>最近一次心跳时间（PauseManager 判断掉线用）</summary>
    public DateTime LastPing = DateTime.UtcNow;

    /// <summary>心跳报告已进入游戏且加载完成（只有这种客户端掉线才触发全员暂停）</summary>
    public bool InGame;

    /// <summary>种子是否已发送</summary>
    public bool SeedSent = false;
    
    /// <summary>客户端是否已确认种子</summary>
    public bool SeedConfirmed = false;
}

internal class ServerNetListener : INetEventListener
{
    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"[网络] 客户端已连接 (Peer={peer.Id})，等待 HELLO...");
        Program.LogToDesktop("NET", $"Client connected Peer={peer.Id}");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        Console.WriteLine($"[网络] 客户端已断开 (Peer={peer.Id}, {info.Reason})");
        PauseManager.OnPeerDisconnected(peer.Id);
        LobbyManager.OnPeerDisconnected(peer.Id);
        NetworkManager.RemoveClient(peer.Id);
        NetworkManager.BroadcastPlayerList();
    }

    /// <summary>
    /// 确定玩家身份（加入大厅时由 LobbyManager 调用）：
    /// 按真实账户名取得/创建玩家记录，告知客户端服务端有没有它的存档（有则发过去），
    /// 再发 WELCOME / PLAYER_DATA，通知服务端游戏（HNC.Sync 全量）并广播玩家列表。
    ///
    /// 以前这些在 HELLO 时做，但账户列表界面会用列表里第一个账户名先连上来取种子，
    /// 那个名字不一定是最终点"登录"的账户，所以挪到加入大厅（带真实账户名）时做。
    /// </summary>
    internal static ClientInfo CompleteIdentity(NetPeer peer, string playerName, string serverName, bool sendSave = true)
    {
        PlayerData playerData;
        if (PlayerDataManager.IsKnownPlayer(playerName, serverName))
        {
            playerData = PlayerDataManager.FindByName(playerName, serverName)!;
            playerData.LastLogin = DateTime.Now;
            PlayerDataManager.UpdatePlayer(playerData, serverName);
            Console.WriteLine($"[网络] 已知玩家 {playerName} (Id={playerData.Id})");
        }
        else
        {
            playerData = PlayerDataManager.CreatePlayer(playerName, serverName);
            Console.WriteLine($"[网络] 新玩家 {playerName} → 分配 Id={playerData.Id}");
        }

        // 检查本地是否有该玩家的存档（上传时存到 Storage/Saves/）。
        // 游戏中掉线后原地重连（resume）的客户端不需要存档：它没有退出游戏，进度在内存里。
        string? playerSavePath = GetPlayerSavePath(playerName);
        if (!sendSave)
        {
            // 不发 HAS_SAVE / NO_SAVE，也不传存档
        }
        else if (playerSavePath != null)
        {
            NetworkManager.SendTo(peer, "HAS_SAVE");
            SendSaveFileToPlayer(peer, playerSavePath, playerName);
        }
        else
        {
            NetworkManager.SendTo(peer, "NO_SAVE");
        }

        // 用 PlayerData.Id 重新登记（覆盖 HELLO 时的临时登记）
        var info = NetworkManager.RegisterClient(peer, playerName, playerData.Id, joined: true);

        // WELCOME + PLAYER_DATA（两个 ID 一致）
        NetworkManager.SendTo(peer, $"WELCOME:{info.PlayerId}");
        NetworkManager.SendTo(peer, $"PLAYER_DATA:{playerData.Id}:{playerData.Name}");
        Console.WriteLine($"[网络] 已回复 WELCOME:PlayerId={info.PlayerId}");

        // 通知服务端游戏进程：该玩家已连入（HNC.Sync 据此发送全量状态；客户端世界没加载完时会暂存）
        _ = Program.SendToModFireForget($"SYNC_JOIN:{info.PlayerId}");

        NetworkManager.BroadcastPlayerList();
        return info;
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte ch, DeliveryMethod dm)
    {
        var msg = reader.GetString();
        NetworkManager.Log("NET_RCV", $"Peer={peer.Id}: {msg}");

        // ── 握手：HELLO:playerName ──
        // 只做临时登记并请求世界种子。真正的玩家身份（PlayerId、存档、玩家列表）在加入大厅时确定，
        // 见 CompleteIdentity——账户列表界面用来连接的名字不一定是最终登录的账户。
        if (msg.StartsWith("HELLO:"))
        {
            var tempName = msg.Substring("HELLO:".Length);
            var info = NetworkManager.RegisterClient(peer, tempName, -(peer.Id + 1));

            // 立即请求世界种子（服务端还没开局时种子尚未生成，开局后会自动广播）
            SendWorldSeedToClient(peer, info);
            return;
        }

        // ── 准备大厅 ──
        if (msg.StartsWith(HNC.Lobby.LobbyProtocol.Prefix))
        {
            LobbyManager.OnClientMessage(peer, msg);
            return;
        }

        // ── 种子确认：SEED_CONFIRMED ──
        if (msg == "SEED_CONFIRMED")
        {
            var client = NetworkManager.GetClient(peer.Id);
            if (client != null)
            {
                client.SeedConfirmed = true;
                Console.WriteLine($"[网络] 客户端 {client.PlayerName} 已确认种子同步完成");

                // 可以在此处触发后续操作，例如允许创建账户等
                // TODO: 通知游戏逻辑层客户端已准备就绪
            }
            return;
        }

        // ── 显式请求种子 ──
        if (msg == "REQUEST_SEED")
        {
            var client = NetworkManager.GetClient(peer.Id);
            if (client != null)
            {
                Console.WriteLine($"[网络] 客户端 {client.PlayerName} 显式请求种子");
                SendWorldSeedToClient(peer, client);
            }
            else
            {
                Console.WriteLine($"[网络] 未注册客户端请求种子，忽略");
            }
            return;
        }

        // ── 客户端上传存档（分块接收） ──
        // SAVE_DATA_CHUNK:{seq}:{total}:{username}:{base64part}
        // SAVE_DATA_DONE:{total}
        // ── 客户端上传存档（分块接收，uploadSeq 区分新旧） ──
        // SAVE_DATA_CHUNK:{seq}:{total}:{uploadSeq}:{username}:{base64part}
        if (msg.StartsWith("SAVE_DATA_CHUNK:"))
        {
            var parts = msg.Split(':', 6);
            if (parts.Length == 6 &&
                int.TryParse(parts[1], out int seq) &&
                int.TryParse(parts[2], out int total) &&
                int.TryParse(parts[3], out int uploadSeq))
            {
                string username = parts[4];
                string chunkData = parts[5];
                string key = $"{username}_{uploadSeq}";  // uploadSeq 递增，新旧自动隔离

                if (!NetworkManager.Uploads.TryGetValue(key, out var up))
                {
                    up = new ChunkedUpload { Total = total, Username = username, Chunks = new string[total] };
                    NetworkManager.Uploads[key] = up;
                }
                if (seq >= 0 && seq < total)
                    up.Chunks![seq] = chunkData;

                Console.Write($"\r[网络] 存档接收 [{key}]: {up.Chunks!.Count(c => c != null)}/{total}");
            }
            return;
        }
        if (msg.StartsWith("SAVE_DATA_DONE:"))
        {
            // SAVE_DATA_DONE:{total}:{uploadSeq}
            var parts = msg.Split(':');
            if (parts.Length >= 3 && int.TryParse(parts[1], out int total) && int.TryParse(parts[2], out int doneSeq))
            {
                // 找到匹配 uploadSeq 的上传
                string key = NetworkManager.Uploads.Keys
                    .FirstOrDefault(k => k.EndsWith($"_{doneSeq}"));
                if (key != null && NetworkManager.Uploads.TryGetValue(key, out var up))
                {
                    if (up.Chunks != null && up.Chunks.All(c => c != null))
                    {
                        var sb = new System.Text.StringBuilder();
                        for (int i = 0; i < up.Total; i++) sb.Append(up.Chunks[i]);
                        try
                        {
                            string b64 = sb.ToString();
                            byte[] data = Convert.FromBase64String(b64);
                            // LDTchara注：这个地方后期需要改成存到服务器指定目录，而不是桌面
                            string saveDir = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HacknetSaves");
                            Directory.CreateDirectory(saveDir);
                            string savePath = Path.Combine(saveDir, $"save_{up.Username}_uploaded.xml");
                            File.WriteAllBytes(savePath, data);

                            // 同时存入玩家专属存档目录（供重连时直接读取）
                            string perPlayerDir = Path.Combine("Storage", "Saves");
                            Directory.CreateDirectory(perPlayerDir);
                            string perPlayerPath = Path.Combine(perPlayerDir, $"{up.Username}.xml");
                            File.WriteAllBytes(perPlayerPath, data);

                            Console.WriteLine($"\n[网络] 收到存档: {up.Username} ({data.Length} 字节, {total} 块) -> {savePath}");
                            Console.WriteLine($"[网络] 已存入玩家存档: {perPlayerPath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\n[网络] 存档保存失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"\n[网络] 存档接收不完整 [{key}]: 只有 {(up.Chunks?.Count(c => c != null) ?? 0)}/{total} 块");
                    }
                    NetworkManager.Uploads.Remove(key);
                }
            }
            return;
        }

        // ── 客户端上传存档（旧版单条） ──
        if (msg.StartsWith("SAVE_DATA:"))
        {
            var parts = msg.Split(':', 3);
            if (parts.Length >= 3)
            {
                string saveUser = parts[1];
                string b64 = parts[2];
                try
                {
                    byte[] data = Convert.FromBase64String(b64);
                    string saveDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HacknetSaves");
                    Directory.CreateDirectory(saveDir);
                    string savePath = Path.Combine(saveDir, $"save_{saveUser}_uploaded.xml");
                    File.WriteAllBytes(savePath, data);
                    Console.WriteLine($"[网络] 收到存档: {saveUser} ({data.Length} 字节) -> {savePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[网络] 存档保存失败: {ex.Message}");
                }
            }
            return;
        }

        // 心跳 PING——直接打印，不广播
        if (msg.StartsWith("PING:"))
        {
            // 回 PONG 并记录心跳；游戏中超时由 PauseManager.Tick 判定（全员暂停）
            PauseManager.OnPing(peer, msg);
            return;
        }

        // ── 玩家间邮件：MAIL_SEND:{收件人id}:{b64 主题}:{b64 正文} ──
        // 只定向转给收件人，并盖上真实发件人（取自已注册的连接身份，客户端无法伪造）。
        if (msg.StartsWith("MAIL_SEND:"))
        {
            var mailSender = NetworkManager.GetClient(peer.Id);
            var mp = msg.Split(':', 4);
            if (mailSender == null || !mailSender.Joined || mp.Length != 4 || !int.TryParse(mp[1], out int mailToId))
            {
                NetworkManager.SendTo(peer, "MAIL_FAIL:0:invalid");
                return;
            }
            if (mp[2].Length > 1400 || mp[3].Length > 12000)
            {
                NetworkManager.SendTo(peer, $"MAIL_FAIL:{mailToId}:toolong");
                return;
            }
            if (mailToId == mailSender.PlayerId)
            {
                NetworkManager.SendTo(peer, $"MAIL_FAIL:{mailToId}:self");
                return;
            }
            // 收件人 0 = 服务端游戏（主机玩家）：走 Pipe 交给游戏进程，同样由终端盖上真实发件人
            if (mailToId == 0)
            {
                if (!Program.IsModConnected)
                {
                    NetworkManager.SendTo(peer, "MAIL_FAIL:0:offline");
                    return;
                }
                string mailFrom64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(mailSender.PlayerName));
                _ = Program.SendToModFireForget($"MAIL_TO_SERVER:{mailSender.PlayerId}:{mailFrom64}:{mp[2]}:{mp[3]}");
                NetworkManager.SendTo(peer, "MAIL_SENT:0");
                Console.WriteLine($"[邮件] {mailSender.PlayerName}(P{mailSender.PlayerId}) → Server");
                return;
            }

            var mailTarget = NetworkManager.GetAllClients().FirstOrDefault(c => c.PlayerId == mailToId && c.Joined && c.Peer != null);
            if (mailTarget == null)
            {
                NetworkManager.SendTo(peer, $"MAIL_FAIL:{mailToId}:offline");
                return;
            }
            string senderName64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(mailSender.PlayerName));
            NetworkManager.SendTo(mailTarget.Peer!, $"MAIL:{mailSender.PlayerId}:{senderName64}:{mp[2]}:{mp[3]}");
            NetworkManager.SendTo(peer, $"MAIL_SENT:{mailToId}");
            Console.WriteLine($"[邮件] {mailSender.PlayerName}(P{mailSender.PlayerId}) → {mailTarget.PlayerName}(P{mailToId})");
            return;
        }

        // ── HNC.Sync：只转给服务端游戏进程（带发送者 PlayerId），不广播给其他客户端 ──
        if (msg.StartsWith("SYNC:"))
        {
            var syncClient = NetworkManager.GetClient(peer.Id);
            if (syncClient is { Joined: true })
                _ = Program.SendToModFireForget($"SYNC_FROM:{syncClient.PlayerId}:{msg}");
            return;
        }

        // ── 已注册客户端的消息 ──
        var client2 = NetworkManager.GetClient(peer.Id);
        var source = client2 != null ? $"{client2.PlayerName}(P{client2.PlayerId})" : $"Peer{peer.Id}";
        if (!msg.StartsWith("EVENT:NODE_IP:") && !msg.StartsWith("PING:"))
            Console.WriteLine($"[网络] ← {source}: {msg}");

        // 广播给其他客户端（除发送者）；只接受已加入大厅（确定了身份）的客户端的事件
        if (client2 is { Joined: true } && msg.StartsWith("EVENT:"))
        {
            BroadcastExceptSender(peer, msg);

            Console.WriteLine("[NET_RCV] Client event: " + msg.Substring(0, Math.Min(80, msg.Length)));
                    // 转发到 Mod（仅处理有用的事件类型）
            // NODE_IP / FLAG / MISSION 等高频低价值事件不转发给 Mod
            // 使用 FireForget：不阻塞 PipeListenLoop，信号量串行化 + 超时丢弃防背压
            if (!msg.StartsWith("EVENT:NODE_IP:") && !msg.StartsWith("EVENT:FLAG:") && !msg.StartsWith("EVENT:MISSION:"))
            {
                _ = Program.SendToModFireForget(msg);
            }
        }
    }

        /// <summary>
        /// 获取指定玩家的本地存档路径（Storage/Saves/{name}.xml）。
        /// 文件不存在返回 null。
        /// </summary>
        private static string? GetPlayerSavePath(string playerName)
        {
            string path = System.IO.Path.Combine("Storage", "Saves", $"{playerName}.xml");
            return System.IO.File.Exists(path) ? path : null;
        }

        /// <summary>
        /// 将本地存档文件分块发送给指定客户端。
        /// 格式与 PipeListenLoop 中的 S_FILE_INFO/S_FILE/S_FILE_END 一致。
        /// </summary>
        private static void SendSaveFileToPlayer(NetPeer peer, string filePath, string playerName)
        {
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(filePath);
                string b64 = Convert.ToBase64String(data);
                int chunkSize = 60000;
                int total = (b64.Length + chunkSize - 1) / chunkSize;

                NetworkManager.SendTo(peer, $"S_FILE_INFO:{total}");
                for (int i = 0; i < total; i++)
                {
                    int start = i * chunkSize;
                    int len = Math.Min(chunkSize, b64.Length - start);
                    NetworkManager.SendTo(peer, $"S_FILE:{i}:{total}:{b64.Substring(start, len)}");
                }
                NetworkManager.SendTo(peer, $"S_FILE_END:{total}");
                Console.WriteLine($"[网络] 已发送玩家存档 {playerName} ({data.Length} 字节, {total} 块)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[网络] 发送玩家存档失败 {playerName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 向模组请求世界种子。
    /// 种子数据通过 PipeListenLoop → Broadcast 到达客户端，这里只负责触发请求。
    /// </summary>
    private async void SendWorldSeedToClient(NetPeer peer, ClientInfo client)
    {
        try
        {
            await Program.SendToMod("GET_WORLD_SEED");
            Console.WriteLine($"[网络] 已请求世界种子 (通过 Broadcast 到达客户端)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[网络] 请求种子失败: {ex.Message}");
        }
    }

    private void BroadcastExceptSender(NetPeer sender, string msg)
    {
        // TODO: 后续加消息路由（按 isPublic 过滤、按 playerId 定向）
        // 目前先广播给所有人
        Broadcast(msg);
    }

    public void OnNetworkError(System.Net.IPEndPoint ep, System.Net.Sockets.SocketError err) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint rep, NetPacketReader r, UnconnectedMessageType t) { }
    public void OnConnectionRequest(ConnectionRequest req)
    {
        req.AcceptIfKey("HacknetServer");
    }
}