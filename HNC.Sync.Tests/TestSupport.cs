using HNC.Sync;

namespace HNC.Sync.Tests;

/// <summary>模拟 Pathfinder 的 [XMLStorage]：Core 只按类型名匹配，所以测试里自己定义一个同名特性即可。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class XMLStorageAttribute : Attribute { }

public enum Mood { Calm, Angry }

[SyncType]
public class SampleDaemon
{
    [Sync] public int Score;
    [Sync] public string? Title;
    [Sync] public Mood Mood;
    [Sync] public List<string> Log = new();
    [Sync] public Dictionary<string, int> Counters = new();
    [Sync(Mode = SyncMode.Manual)] public int ManualValue;
    [NoSync] public int Hidden;
    public int Unmarked;
    [Sync(Scope = SyncScope.Local)] public int LocalOnly;
    [Sync] public int Prop { get; set; }
}

[SyncType(UseXmlStorage = true)]
public class XmlDaemon
{
    [XMLStorage] public string Saved = "";
    [XMLStorage, NoSync] public string SavedButExcluded = "";
    public string NotSaved = "";
}

public class PlainClass { public int X; }

public class NoSerializerHolder
{
    [Sync] public object? Weird;
    [Sync] public int Fine;
}

public class SyncableThing : ISyncable
{
    public string SyncId { get { return "thing:1"; } }
    public SyncScope Scope { get { return SyncScope.World; } }
    public int Value;
    public int LastSent = int.MinValue;
    public void WriteState(ISyncWriter w) { w.WriteInt(Value); }
    public void ReadState(ISyncReader r) { Value = r.ReadInt(); }
    public bool TryWriteDelta(ISyncWriter w)
    {
        if (Value == LastSent) return false;
        LastSent = Value; w.WriteInt(Value); return true;
    }
    public void ReadDelta(ISyncReader r) { Value = r.ReadInt(); LastSent = Value; }
}

/// <summary>内存里的一台服务端 + 若干客户端，消息同步投递，可按需丢包。</summary>
public sealed class Hub
{
    public readonly MemTransport Server;
    public readonly List<MemTransport> Clients = new();
    public readonly List<string> ClientToServer = new();
    public readonly List<string> ServerToClient = new();
    /// <summary>返回 true 则丢弃（参数：消息、目标玩家）。</summary>
    public Func<string, string, bool>? Drop;

    public Hub() { Server = new MemTransport(this, true, "server"); }

    public MemTransport AddClient(string id)
    {
        var c = new MemTransport(this, false, id);
        Clients.Add(c);
        return c;
    }

    internal void ServerSend(string msg, string? to)
    {
        foreach (var c in Clients.ToList())
        {
            if (to != null && to != c.LocalPlayerId) continue;
            if (Drop != null && Drop(msg, c.LocalPlayerId)) continue;
            ServerToClient.Add(msg);
            c.Raise(msg, "server");
        }
    }

    internal void ClientSend(MemTransport from, string msg)
    {
        ClientToServer.Add(msg);
        Server.Raise(msg, from.LocalPlayerId);
    }
}

public sealed class MemTransport : ISyncTransport
{
    private readonly Hub _hub;
    public bool IsServer { get; }
    public string LocalPlayerId { get; }
    public event Action<string, string>? MessageReceived;

    public MemTransport(Hub hub, bool isServer, string id) { _hub = hub; IsServer = isServer; LocalPlayerId = id; }

    public void Send(string message, string toPlayerId)
    {
        if (IsServer) _hub.ServerSend(message, toPlayerId);
        else _hub.ClientSend(this, message);
    }

    internal void Raise(string msg, string from) { MessageReceived?.Invoke(msg, from); }
}
