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
            DisconnectTimeout = 5000
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
    public static ClientInfo RegisterClient(NetPeer peer, string playerName)
    {
        var info = new ClientInfo
        {
            PeerId = peer.Id,
            PlayerId = _nextPlayerId++,
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

    private static void Log(string tag, string msg)
    {
        File.AppendAllText("net_log.txt",
            $"{DateTime.Now:HH:mm:ss.fff} [{tag}] {msg}{Environment.NewLine}");
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
        File.AppendAllText("net_log.txt",
            $"{DateTime.Now:HH:mm:ss.fff} [NET_RCV] Peer={peer.Id}: {msg}{Environment.NewLine}");

        // ── 握手：HELLO:playerName ──
        if (msg.StartsWith("HELLO:"))
        {
            var playerName = msg.Substring("HELLO:".Length);
            var info = NetworkManager.RegisterClient(peer, playerName);
            NetworkManager.SendTo(peer, $"WELCOME:{info.PlayerId}");
            Console.WriteLine($"[网络] 已回复 WELCOME:PlayerId={info.PlayerId}");
            
            // 立即发送世界种子
            SendWorldSeedToClient(peer, info);

            // 请求服务端存档文件（终端读盘 → 分块 LiteNetLib 广播）
            _ = Task.Run(async () =>
            {
                try { await Program.SendToMod("GET_SAVE_FILE", false); }
                catch { }
            });

            // 请求服务端存档列表（供客户端对比）
            _ = Task.Run(async () =>
            {
                try { await Program.SendToMod($"GET_SAVE_LIST:{playerName}", false); }
                catch { }
            });

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
        if (msg.StartsWith("SAVE_DATA_CHUNK:"))
        {
            var parts = msg.Split(':', 5);
            if (parts.Length == 5 &&
                int.TryParse(parts[1], out int seq) &&
                int.TryParse(parts[2], out int total))
            {
                string username = parts[3];
                string chunkData = parts[4];
                if (!NetworkManager.Uploads.TryGetValue(username, out var up))
                {
                    up = new ChunkedUpload { Total = total, Username = username, Chunks = new string[total] };
                    NetworkManager.Uploads[username] = up;
                }
                if (seq >= 0 && seq < total)
                    up.Chunks![seq] = chunkData;

                Console.Write($"\r[网络] 存档接收: {up.Chunks!.Count(c => c != null)}/{total}");
            }
            return;
        }
        if (msg.StartsWith("SAVE_DATA_DONE:"))
        {
            var totalStr = msg.Substring("SAVE_DATA_DONE:".Length);
            if (int.TryParse(totalStr, out int total) && NetworkManager.Uploads.Count > 0)
            {
                // 找 username（从最后一个上传）
                var kv = NetworkManager.Uploads.Last();
                var up = kv.Value;
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
                        Console.WriteLine($"\n[网络] 收到存档: {up.Username} ({data.Length} 字节, {total} 块) -> {savePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n[网络] 存档保存失败: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"\n[网络] 存档接收不完整: 只有 {(up.Chunks?.Count(c => c != null) ?? 0)}/{total} 块");
                }
                NetworkManager.Uploads.Remove(kv.Key);
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
            if (!msg.StartsWith("EVENT:NODE_IP:") && !msg.StartsWith("EVENT:FLAG:") && !msg.StartsWith("EVENT:MISSION:"))
            {
                Task.Run(async () =>
                {
                    try { await Program.SendToMod(msg); }
                    catch { }
                });
            }
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
