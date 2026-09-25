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
            while (_netManager is { IsRunning: true })
            {
                _netManager.PollEvents();
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
        var client = _clients.Values.FirstOrDefault(c => c.PlayerId == playerId);
        if (client?.Peer != null)
            SendTo(client.Peer, msg);
    }

    // ── 连接管理 ──

    /// <summary>注册客户端身份（由 HELLO 握手触发）</summary>
    /// <param name="overrideId">指定 PlayerId（来自 PlayerDataManager），不指定则用内部自增计数器</param>
    public static ClientInfo RegisterClient(NetPeer peer, string playerName, int? overrideId = null)
    {
        int pid = overrideId ?? _nextPlayerId++;
        var info = new ClientInfo
        {
            PeerId = peer.Id,
            PlayerId = pid,
            PlayerName = playerName,
            Peer = peer
        };
        _clients[peer.Id] = info;
        Console.WriteLine($"[网络] 注册: {playerName} → PlayerId={info.PlayerId} (Peer={peer.Id})");
        return info;
    }

    public static void RemoveClient(int peerId)
    {
        if (_clients.Remove(peerId, out var info))
            Console.WriteLine($"[网络] 移除: {info.PlayerName} (PlayerId={info.PlayerId})");
    }

    public static ClientInfo? GetClient(int peerId) =>
        _clients.TryGetValue(peerId, out var info) ? info : null;

    public static List<ClientInfo> GetAllClients() => _clients.Values.ToList();

    /// <summary>广播玩家列表给所有客户端</summary>
    public static void BroadcastPlayerList()
    {
        var players = GetAllClients();
        var names = string.Join(",", players.Select(c => $"{c.PlayerId}:{c.PlayerName}"));
        Broadcast($"PLAYER_LIST:{names}");
    }

    /// <summary>net_log.txt 写锁（防后台 Pipe 线程和 LiteNetLib 线程并发写）</summary>
    private static readonly object _netLogLock = new();

    internal static void Log(string tag, string msg)
    {
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
        NetworkManager.RemoveClient(peer.Id);
        NetworkManager.BroadcastPlayerList();
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte ch, DeliveryMethod dm)
    {
        var msg = reader.GetString();
        NetworkManager.Log("NET_RCV", $"Peer={peer.Id}: {msg}");

        // ── 握手：HELLO:playerName ──
        if (msg.StartsWith("HELLO:"))
        {
            var playerName = msg.Substring("HELLO:".Length);
            var info = NetworkManager.RegisterClient(peer, playerName);

            // 使用 PlayerDataManager 创建/获取玩家记录
            string serverName = CommandHandler.CurrentServerName;
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

            // 检查本地是否有该玩家的存档（上传时存到 Storage/Saves/）
            string? playerSavePath = GetPlayerSavePath(playerName);
            if (playerSavePath != null)
            {
                NetworkManager.SendTo(peer, "HAS_SAVE");
                SendSaveFileToPlayer(peer, playerSavePath, playerName);
            }
            else
            {
                NetworkManager.SendTo(peer, "NO_SAVE");
            }

            // 用 PlayerData.Id 重新注册（覆盖 RegisterClient 的内部自增 ID）
            if (info.PlayerId != playerData.Id)
            {
                NetworkManager.RemoveClient(peer.Id);
                info = NetworkManager.RegisterClient(peer, playerName, playerData.Id);
            }

            // WELCOME + PLAYER_DATA（两个 ID 一致）
            NetworkManager.SendTo(peer, $"WELCOME:{info.PlayerId}");
            NetworkManager.SendTo(peer, $"PLAYER_DATA:{playerData.Id}:{playerData.Name}");
            Console.WriteLine($"[网络] 已回复 WELCOME:PlayerId={info.PlayerId}");

            // 立即发送世界种子
            SendWorldSeedToClient(peer, info);

            NetworkManager.BroadcastPlayerList();
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
            Console.WriteLine("[心跳] " + msg);
            return;
        }

        // ── 已注册客户端的消息 ──
        var client2 = NetworkManager.GetClient(peer.Id);
        var source = client2 != null ? $"{client2.PlayerName}(P{client2.PlayerId})" : $"Peer{peer.Id}";
        if (!msg.StartsWith("EVENT:NODE_IP:") && !msg.StartsWith("PING:"))
            Console.WriteLine($"[网络] ← {source}: {msg}");

        // 广播给其他客户端（除发送者）
        if (client2 != null && msg.StartsWith("EVENT:"))
        {
            BroadcastExceptSender(peer, msg);

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
        NetworkManager.Broadcast(msg);
    }

    public void OnNetworkError(System.Net.IPEndPoint ep, System.Net.Sockets.SocketError err) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint rep, NetPacketReader r, UnconnectedMessageType t) { }
    public void OnConnectionRequest(ConnectionRequest req)
    {
        req.AcceptIfKey("HacknetServer");
    }
}