using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;

namespace ClientPlugin;

/// <summary>
/// 客户端网络管理器 — LiteNetLib 连接服务端，收发消息
/// </summary>
internal static class ClientNetworkManager
{
    private static EventBasedNetListener? _listener;
    private static NetManager? _netManager;
    private static NetPeer? _serverPeer;

    public static bool IsConnected => _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected;
    public static bool IsConnecting;
    public static int PlayerId;
    public static string PlayerName = "";
    public static string ServerAddress = "";
    public static int ServerPort = 9050;

    /// <summary>收到的消息队列（主线程消费）</summary>
    private static readonly Queue<string> _messageQueue = new();
    private static readonly object _queueLock = new();

    /// <summary>连接状态变化回调</summary>
    public static Action<string>? OnStatusChanged;

    public static void Connect(string ip, int port, string playerName)
    {
        // 如果已经连接到同一个服务器，不再重复连接
        if (IsConnected && ServerAddress == ip && ServerPort == port) return;

        // 如果已连接到不同的服务器，先断开
        if (IsConnected || IsConnecting)
        {
            Console.WriteLine($"[ClientNet] Switching server from {ServerAddress}:{ServerPort} to {ip}:{port}");
            Disconnect();
        }

        ServerAddress = ip;
        ServerPort = port;
        PlayerName = playerName;
        IsConnecting = true;

        Console.WriteLine($"[ClientNet] Connecting to {ip}:{port} as {playerName}...");
        OnStatusChanged?.Invoke("Connecting...");

        _listener = new EventBasedNetListener();

        _listener.PeerConnectedEvent += peer =>
        {
            Console.WriteLine($"[ClientNet] Connected to server, sending HELLO...");
            _serverPeer = peer;

            // 连接成功后订阅本地事件转发器
            EventForwarderClient.Subscribe();

            var writer = new NetDataWriter();
            writer.Put($"HELLO:{playerName}");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);

            // 连接成功后显式请求世界种子
            // 终端在 HELLO 时也会自动发送，此处作为补充确保必达
            writer = new NetDataWriter();
            writer.Put("REQUEST_SEED");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            Console.WriteLine($"[ClientNet] Seed request sent to server.");
        };

        _listener.PeerDisconnectedEvent += (peer, info) =>
        {
            Console.WriteLine($"[ClientNet] Disconnected: {info.Reason}");
            _serverPeer = null;
            IsConnecting = false;
            PlayerId = 0;
            OnStatusChanged?.Invoke($"Disconnected: {info.Reason}");
        };

        _listener.NetworkReceiveEvent += (peer, reader, ch, dm) =>
        {
            var msg = reader.GetString();
            lock (_queueLock)
            {
                _messageQueue.Enqueue(msg);
            }
        };

        _listener.NetworkErrorEvent += (ep, err) =>
        {
            Console.WriteLine($"[ClientNet] Network error: {err}");
            OnStatusChanged?.Invoke($"Error: {err}");
        };

        _netManager = new NetManager(_listener)
        {
            AutoRecycle = true,
            UpdateTime = 15,
            DisconnectTimeout = 5000
        };
        _netManager.Start();
        _netManager.Connect(ip, port, "HacknetServer");
    }

    public static void Disconnect()
    {
        _netManager?.Stop();
        _netManager = null;
        _serverPeer = null;
        IsConnecting = false;
        PlayerId = 0;
        Console.WriteLine("[ClientNet] Disconnected.");
    }

    /// <summary>主线程调用 — 驱动网络轮询 + 消息分发</summary>
    public static void PollEvents()
    {
        _netManager?.PollEvents();

        // 消费消息队列
        while (true)
        {
            string? msg;
            lock (_queueLock)
            {
                if (_messageQueue.Count == 0) break;
                msg = _messageQueue.Dequeue();
            }
            if (msg != null)
                EventReceiver.HandleMessage(msg);
        }
    }

    public static void Send(string msg)
    {
        if (_serverPeer == null) return;
        var writer = new NetDataWriter();
        writer.Put(msg);
        _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>向服务端显式请求世界种子</summary>
    public static void RequestSeed()
    {
        if (_serverPeer == null)
        {
            Console.WriteLine("[ClientNet] Cannot request seed: not connected");
            return;
        }
        Console.WriteLine("[ClientNet] Requesting seed from server...");
        var writer = new NetDataWriter();
        writer.Put("REQUEST_SEED");
        _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
