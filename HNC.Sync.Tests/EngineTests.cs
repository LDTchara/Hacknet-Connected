using HNC.Sync;
using Xunit;

namespace HNC.Sync.Tests;

public class EngineTests
{
    private sealed class Rig
    {
        public readonly Hub Hub = new();
        public readonly SyncEngine Server;
        public readonly Dictionary<string, SyncEngine> Clients = new();
        public readonly List<string> Logs = new();

        public Rig(params string[] clientIds)
        {
            Server = new SyncEngine(Hub.Server) { Log = Logs.Add };
            foreach (var id in clientIds)
                Clients[id] = new SyncEngine(Hub.AddClient(id)) { Log = Logs.Add };
        }
    }

    [Fact]
    public void Server_change_reaches_all_clients()
    {
        var rig = new Rig("p1", "p2");
        var s = new SampleDaemon(); var c1 = new SampleDaemon(); var c2 = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        rig.Clients["p1"].Register(c1, "d:1");
        rig.Clients["p2"].Register(c2, "d:1");

        s.Score = 5; s.Title = "hi";
        rig.Server.Tick();

        Assert.Equal(5, c1.Score); Assert.Equal("hi", c1.Title);
        Assert.Equal(5, c2.Score);
    }

    [Fact]
    public void Remote_apply_does_not_echo_back()
    {
        var rig = new Rig("p1");
        var s = new SampleDaemon(); var c = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        rig.Clients["p1"].Register(c, "d:1");

        s.Score = 5;
        rig.Server.Tick();
        rig.Clients["p1"].Tick();

        Assert.Empty(rig.Hub.ClientToServer);
    }

    [Fact]
    public void Client_change_is_proposed_applied_and_rebroadcast_to_everyone()
    {
        var rig = new Rig("p1", "p2");
        var s = new SampleDaemon(); var c1 = new SampleDaemon(); var c2 = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        rig.Clients["p1"].Register(c1, "d:1");
        rig.Clients["p2"].Register(c2, "d:1");

        c1.Score = 9;
        rig.Clients["p1"].Tick();
        Assert.Single(rig.Hub.ClientToServer);
        Assert.StartsWith("SYNC:PROPOSE:", rig.Hub.ClientToServer[0]);
        Assert.Equal(9, s.Score);   // 服务端已应用
        Assert.Equal(0, c2.Score);  // 还没广播

        rig.Server.Tick();          // 下一轮检测到差异 → 广播
        Assert.Equal(9, c2.Score);
        Assert.Equal(9, c1.Score);
    }

    [Fact]
    public void Missed_delta_triggers_full_resync()
    {
        var rig = new Rig("p1");
        var s = new SampleDaemon(); var c = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        rig.Clients["p1"].Register(c, "d:1");

        bool dropFirst = true;
        rig.Hub.Drop = (msg, to) =>
        {
            if (dropFirst && msg.StartsWith("SYNC:DELTA:")) { dropFirst = false; return true; }
            return false;
        };

        s.Score = 1; rig.Server.Tick();          // v1 丢失
        Assert.Equal(0, c.Score);

        s.Score = 2; rig.Server.Tick();          // v2 到达 → 缺口 → REQ → FULL
        Assert.Equal(2, c.Score);
        Assert.Contains(rig.Hub.ClientToServer, m => m.StartsWith("SYNC:REQ:"));
        Assert.Contains(rig.Hub.ServerToClient, m => m.StartsWith("SYNC:FULL:"));
    }

    [Fact]
    public void Stale_or_duplicate_delta_is_ignored()
    {
        var rig = new Rig("p1");
        var s = new SampleDaemon(); var c = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        rig.Clients["p1"].Register(c, "d:1");

        s.Score = 1; rig.Server.Tick();
        var deltaMsg = rig.Hub.ServerToClient.Last(m => m.StartsWith("SYNC:DELTA:"));
        c.Score = 100; // 本地被改（会在 Tick 里变成 PROPOSE，这里不 Tick）
        rig.Hub.Clients[0].Raise(deltaMsg, "server"); // 重放旧消息

        Assert.Equal(100, c.Score);
        Assert.DoesNotContain(rig.Hub.ClientToServer, m => m.StartsWith("SYNC:REQ:"));
    }

    [Fact]
    public void Delta_arriving_before_registration_is_replayed_on_register()
    {
        var rig = new Rig("p1");
        var s = new SampleDaemon();
        rig.Server.Register(s, "d:late");
        s.Score = 4;
        rig.Server.Tick(); // 客户端还没注册 d:late

        var c = new SampleDaemon();
        rig.Clients["p1"].Register(c, "d:late");
        Assert.Equal(4, c.Score);
    }

    [Fact]
    public void Player_connect_sends_full_state_to_that_player_only()
    {
        var rig = new Rig("p1", "p2");
        var s = new SampleDaemon { Score = 8, Title = "boot" };
        s.Log.Add("entry");
        rig.Server.Register(s, "d:1");
        var c1 = new SampleDaemon(); var c2 = new SampleDaemon();
        rig.Clients["p1"].Register(c1, "d:1");
        rig.Clients["p2"].Register(c2, "d:1");

        rig.Server.OnPlayerConnected("p1");

        Assert.Equal(8, c1.Score);
        Assert.Equal("entry", c1.Log[0]);
        Assert.Equal(0, c2.Score);
    }

    [Fact]
    public void Dead_unregistered_and_keyless_instances_are_handled()
    {
        var rig = new Rig("p1");
        var s = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        rig.Server.Unregister(s);
        s.Score = 1;
        rig.Server.Tick();
        Assert.Empty(rig.Hub.ServerToClient);

        rig.Server.Register(new SampleDaemon(), null);
        Assert.Contains(rig.Logs, l => l.Contains("没有 key"));

        rig.Server.Register(new PlainClass(), "p:1");
        Assert.Contains(rig.Logs, l => l.Contains("没有可同步成员"));
    }

    [Fact]
    public void ISyncable_uses_its_own_SyncId_and_delta()
    {
        var rig = new Rig("p1");
        var s = new SyncableThing { Value = 1 };
        var c = new SyncableThing();
        rig.Server.Register(s);                 // key 取自 SyncId
        rig.Clients["p1"].Register(c);

        s.Value = 6;
        rig.Server.Tick();
        Assert.Equal(6, c.Value);
    }

    [Fact]
    public void MarkDirty_pushes_manual_members_immediately()
    {
        var rig = new Rig("p1");
        var s = new SampleDaemon(); var c = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        rig.Clients["p1"].Register(c, "d:1");

        s.ManualValue = 3;
        rig.Server.Tick();
        Assert.Equal(0, c.ManualValue);          // Manual 不会被轮询带出

        rig.Server.MarkDirty(s, "ManualValue");
        Assert.Equal(3, c.ManualValue);
    }

    [Fact]
    public void Adapter_provides_key_and_target()
    {
        var rig = new Rig("p1");
        rig.Server.RegisterAdapter(new SampleAdapter());
        rig.Clients["p1"].RegisterAdapter(new SampleAdapter());
        var s = new SampleDaemon(); var c = new SampleDaemon();
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        s.Score = 12;
        rig.Server.Tick();
        Assert.Equal(12, c.Score);
    }

    private sealed class SampleAdapter : ISyncAdapter
    {
        public string Kind => "sample";
        public bool TryGetKey(object instance, out string key)
        {
            key = "sample:auto";
            return instance is SampleDaemon;
        }
        public ISyncTarget CreateTarget(object instance, string key) =>
            new ReflectionTarget(instance, key, new SerializerRegistry(), null!);
        public IEnumerable<ISyncTarget> Enumerate() => Array.Empty<ISyncTarget>();
    }

    [Fact]
    public void Channel_message_reaches_listeners()
    {
        var rig = new Rig("p1", "p2");
        var got = new List<(string from, byte[] data)>();
        rig.Clients["p1"].Channel("vote").Received += (f, d) => got.Add((f, d));
        rig.Clients["p2"].Channel("vote");

        rig.Server.Channel("vote").Send(new byte[] { 1, 2 }, SyncScope.World);

        var m = Assert.Single(got);
        Assert.Equal(new byte[] { 1, 2 }, m.data);
    }

    [Fact]
    public void Channel_from_client_reaches_server()
    {
        var rig = new Rig("p1");
        string? from = null;
        rig.Server.Channel("chat").Received += (f, d) => from = f;
        rig.Clients["p1"].Channel("chat").Send(new byte[] { 7 }, SyncScope.World);
        Assert.Equal("p1", from);
    }

    [Fact]
    public void Client_ignores_authoritative_messages_on_server_and_vice_versa()
    {
        var rig = new Rig("p1");
        var s = new SampleDaemon();
        rig.Server.Register(s, "d:1");
        var msg = SyncProtocol.Encode(new SyncMessage
        {
            Op = SyncOp.Delta, Scope = SyncScope.World, Key = "d:1", Version = 1,
            Payload = new SyncWriter().ToArray(),
        });
        rig.Hub.Server.Raise(msg, "p1"); // 恶意/错误客户端把 DELTA 发给服务端
        Assert.Equal(0, s.Score);
    }

    [Fact]
    public void Malformed_payload_is_logged_not_thrown()
    {
        var rig = new Rig("p1");
        var c = new SampleDaemon();
        rig.Clients["p1"].Register(c, "d:1");
        var msg = SyncProtocol.Encode(new SyncMessage
        {
            Op = SyncOp.Full, Scope = SyncScope.World, Key = "d:1", Version = 1,
            Payload = new byte[] { 1 }, // 截断的数据
        });
        rig.Hub.Clients[0].Raise(msg, "server");
        Assert.Contains(rig.Logs, l => l.Contains("处理失败"));
    }

    [Fact]
    public void ApplyingRemote_is_true_only_during_remote_apply()
    {
        var rig = new Rig("p1");
        var s = new ProbeDaemon(); var c = new ProbeDaemon();
        rig.Server.Register(s, "d:p");
        rig.Clients["p1"].Register(c, "d:p");
        c.Engine = rig.Clients["p1"];

        s.Value = 1;
        rig.Server.Tick();

        Assert.True(c.SawApplyingRemote);
        Assert.False(rig.Clients["p1"].ApplyingRemote);
    }

    [SyncType]
    private sealed class ProbeDaemon
    {
        public SyncEngine? Engine;
        public bool SawApplyingRemote;
        private int _value;
        [Sync] public int Value
        {
            get => _value;
            set { _value = value; if (Engine != null && Engine.ApplyingRemote) SawApplyingRemote = true; }
        }
    }
}

public class FacadeTests
{
    [Fact]
    public void HncSync_is_inert_without_runtime()
    {
        var old = HncSync.Runtime;
        HncSync.Runtime = null;
        try
        {
            Assert.False(HncSync.IsActive);
            Assert.False(HncSync.IsServer);
            Assert.False(HncSync.ApplyingRemote);
            Assert.Null(HncSync.LocalPlayerId);
            HncSync.Register(new SampleDaemon(), "x");
            HncSync.MarkDirty(new SampleDaemon());
            HncSync.Unregister(new SampleDaemon());
            var ch = HncSync.Channel("any");
            ch.Send(new byte[] { 1 }, SyncScope.World); // 不抛异常
        }
        finally { HncSync.Runtime = old; }
    }

    [Fact]
    public void HncSync_forwards_to_runtime()
    {
        var hub = new Hub();
        var engine = new SyncEngine(hub.Server);
        var old = HncSync.Runtime;
        HncSync.Runtime = engine;
        try
        {
            Assert.True(HncSync.IsActive);
            Assert.True(HncSync.IsServer);
            Assert.Equal("server", HncSync.LocalPlayerId);
        }
        finally { HncSync.Runtime = old; }
    }
}
