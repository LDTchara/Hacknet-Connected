using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

Console.Title = "TestClient";
Console.WriteLine("=== TestClient ===");
Console.WriteLine("Connecting to localhost:9050...");
Console.WriteLine("Type a message and press Enter to send.");
Console.WriteLine("Ctrl+C to exit.");

var listener = new ClientListener();
var manager = new NetManager(listener);
manager.Start();
manager.Connect("localhost", 9050, "HacknetServer");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    manager.Stop();
    Environment.Exit(0);
};

// 后台轮询
_ = Task.Run(async () =>
{
    while (manager.IsRunning)
    {
        manager.PollEvents();
        await Task.Delay(15);
    }
});

// 发送线程
while (true)
{
    var input = Console.ReadLine();
    if (input == null || input == "/exit") break;
    if (string.IsNullOrWhiteSpace(input)) continue;

    if (listener.Peer != null)
    {
        var writer = new NetDataWriter();
        writer.Put(input);
        listener.Peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
    else
    {
        Console.WriteLine("[!] Not connected yet.");
    }
}

manager.Stop();

internal class ClientListener : INetEventListener
{
    public NetPeer? Peer;

    public void OnPeerConnected(NetPeer peer)
    {
        Peer = peer;
        Console.WriteLine($"[+] Connected (ID={peer.Id})");

        // 注册
        var writer = new NetDataWriter();
        writer.Put("REGISTER:testTool:ConsoleClient");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        Console.WriteLine($"[-] Disconnected: {info.Reason}");
        Peer = null;
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte ch, DeliveryMethod dm)
    {
        var msg = reader.GetString();
        Console.WriteLine($"[←] {msg}");
    }

    public void OnNetworkError(IPEndPoint ep, SocketError err) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnNetworkReceiveUnconnected(IPEndPoint rep, NetPacketReader r, UnconnectedMessageType t) { }
    public void OnConnectionRequest(ConnectionRequest req) { }
}
