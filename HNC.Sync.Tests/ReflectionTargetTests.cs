using HNC.Sync;
using Xunit;

namespace HNC.Sync.Tests;

public class ReflectionTargetTests
{
    private static ReflectionTarget Make(object o, string key = "k") =>
        new ReflectionTarget(o, key, new SerializerRegistry(), null!);

    private static Dictionary<string, byte[]> ReadPayload(byte[] data)
    {
        var r = new SyncReader(data);
        int n = r.ReadInt();
        var d = new Dictionary<string, byte[]>();
        for (int i = 0; i < n; i++) d[r.ReadString()!] = r.ReadBytes()!;
        return d;
    }

    [Fact]
    public void Scans_only_marked_serializable_members()
    {
        var snap = ReadPayload(Make(new SampleDaemon()).Snapshot(SyncScope.World)!);
        Assert.Contains("Score", snap.Keys);
        Assert.Contains("Prop", snap.Keys);
        Assert.Contains("ManualValue", snap.Keys);
        Assert.DoesNotContain("Hidden", snap.Keys);
        Assert.DoesNotContain("Unmarked", snap.Keys);
        Assert.DoesNotContain("LocalOnly", snap.Keys);
    }

    [Fact]
    public void No_changes_right_after_creation()
    {
        var t = Make(new SampleDaemon { Score = 5, Title = "x" });
        var changes = new List<SyncChange>();
        t.CollectChanges(changes, false, null);
        Assert.Empty(changes);
    }

    [Fact]
    public void Detects_only_changed_members()
    {
        var d = new SampleDaemon();
        var t = Make(d);
        d.Score = 3;
        d.Log.Add("hello");

        var changes = new List<SyncChange>();
        t.CollectChanges(changes, false, null);

        var c = Assert.Single(changes);
        Assert.Equal(SyncScope.World, c.Scope);
        var p = ReadPayload(c.Data);
        Assert.Equal(new[] { "Log", "Score" }, p.Keys.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Change_is_reported_once()
    {
        var d = new SampleDaemon();
        var t = Make(d);
        d.Score = 1;
        var first = new List<SyncChange>();
        t.CollectChanges(first, false, null);
        var second = new List<SyncChange>();
        t.CollectChanges(second, false, null);
        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Collection_mutation_in_place_is_detected()
    {
        var d = new SampleDaemon();
        var t = Make(d);
        d.Counters["a"] = 1;
        var changes = new List<SyncChange>();
        t.CollectChanges(changes, false, null);
        Assert.Single(changes);
    }

    [Fact]
    public void Manual_members_are_skipped_unless_forced()
    {
        var d = new SampleDaemon();
        var t = Make(d);
        d.ManualValue = 9;

        var auto = new List<SyncChange>();
        t.CollectChanges(auto, false, null);
        Assert.Empty(auto);

        var forced = new List<SyncChange>();
        t.CollectChanges(forced, true, "ManualValue");
        Assert.Single(forced);
    }

    [Fact]
    public void OnlyMember_filter_limits_the_check()
    {
        var d = new SampleDaemon();
        var t = Make(d);
        d.Score = 1; d.Prop = 2;
        var changes = new List<SyncChange>();
        t.CollectChanges(changes, true, "Prop");
        var p = ReadPayload(Assert.Single(changes).Data);
        Assert.Equal(new[] { "Prop" }, p.Keys.ToArray());
    }

    [Fact]
    public void Apply_writes_values_and_updates_baseline()
    {
        var src = new SampleDaemon { Score = 7, Title = "T", Mood = Mood.Angry };
        src.Log.Add("a"); src.Counters["c"] = 3;
        var data = Make(src).Snapshot(SyncScope.World)!;

        var dst = new SampleDaemon();
        var t = Make(dst);
        t.Apply(data, false, updateBaseline: true);

        Assert.Equal(7, dst.Score);
        Assert.Equal("T", dst.Title);
        Assert.Equal(Mood.Angry, dst.Mood);
        Assert.Equal("a", dst.Log[0]);
        Assert.Equal(3, dst.Counters["c"]);

        var changes = new List<SyncChange>();
        t.CollectChanges(changes, false, null);
        Assert.Empty(changes); // 基线已更新，不会回传
    }

    [Fact]
    public void Apply_without_baseline_update_is_seen_as_a_change()
    {
        var src = new SampleDaemon { Score = 7 };
        var data = Make(src).Snapshot(SyncScope.World)!;
        var dst = new SampleDaemon();
        var t = Make(dst);
        t.Apply(data, false, updateBaseline: false);

        var changes = new List<SyncChange>();
        t.CollectChanges(changes, false, null);
        Assert.Single(changes); // 服务端处理 PROPOSE 时依赖这一点来广播
    }

    [Fact]
    public void Unknown_members_from_peer_are_ignored()
    {
        var w = new SyncWriter();
        w.WriteInt(2);
        w.WriteString("DoesNotExist"); w.WriteBytes(new byte[] { 1, 2, 3 });
        w.WriteString("Score"); w.WriteBytes(new SerializerRegistry().ToBytes(11, typeof(int)));

        var d = new SampleDaemon();
        Make(d).Apply(w.ToArray(), true, true);
        Assert.Equal(11, d.Score);
    }

    [Fact]
    public void Members_without_serializer_are_skipped_and_logged()
    {
        var logs = new List<string>();
        var h = new NoSerializerHolder();
        var t = new ReflectionTarget(h, "k", new SerializerRegistry(), logs.Add);
        Assert.Equal(1, t.MemberCount);
        Assert.Contains(logs, l => l.Contains("Weird"));
    }

    [Fact]
    public void XmlStorage_members_included_only_when_enabled_and_not_excluded()
    {
        var snap = ReadPayload(Make(new XmlDaemon()).Snapshot(SyncScope.World)!);
        Assert.Contains("Saved", snap.Keys);
        Assert.DoesNotContain("SavedButExcluded", snap.Keys);
        Assert.DoesNotContain("NotSaved", snap.Keys);
    }

    [Fact]
    public void HasSyncMembers_distinguishes_types()
    {
        var reg = new SerializerRegistry();
        Assert.True(ReflectionTarget.HasSyncMembers(typeof(SampleDaemon), reg));
        Assert.False(ReflectionTarget.HasSyncMembers(typeof(PlainClass), reg));
    }

    [Fact]
    public void Dead_instance_is_reported_and_ops_are_safe()
    {
        var t = MakeDead();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        // GC 行为不能保证，所以只断言：无论是否已回收，调用都不抛异常
        var changes = new List<SyncChange>();
        t.CollectChanges(changes, false, null);
        t.Apply(new SyncWriter().ToArray(), false, true);
    }

    private static ReflectionTarget MakeDead() => Make(new SampleDaemon());

    [Fact]
    public void SyncableTarget_wraps_interface()
    {
        var thing = new SyncableThing { Value = 5 };
        var t = new SyncableTarget(thing);
        Assert.Equal("thing:1", t.Key);

        var changes = new List<SyncChange>();
        t.CollectChanges(changes, false, null);
        Assert.Single(changes);

        var other = new SyncableThing();
        new SyncableTarget(other).Apply(t.Snapshot(SyncScope.World)!, false, true);
        Assert.Equal(5, other.Value);
        Assert.Null(t.Snapshot(SyncScope.Player));
    }
}
