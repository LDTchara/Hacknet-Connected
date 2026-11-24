using HNC.Sync;
using Xunit;

namespace HNC.Sync.Tests;

/// <summary>内存里的文件夹树，实现 IFolderModel，用来脱离游戏测试文件夹同步逻辑。</summary>
public sealed class MemTree : IFolderModel
{
    private sealed class Dir
    {
        public string Name = "";
        public List<KeyValuePair<string, string>> Files = new();
        public List<Dir> Dirs = new();
    }

    private readonly Dir _root = new();
    public string Name;
    public HashSet<string> Excluded = new();

    public MemTree(string name) { Name = name; }

    private Dir? Nav(IList<string> names, bool create)
    {
        var cur = _root;
        foreach (var n in names)
        {
            var next = cur.Dirs.FirstOrDefault(d => d.Name == n);
            if (next == null)
            {
                if (!create) return null;
                next = new Dir { Name = n };
                cur.Dirs.Add(next);
            }
            cur = next;
        }
        return cur;
    }

    public IEnumerable<FolderEntry> Entries() => Walk(_root, "").ToList();

    private IEnumerable<FolderEntry> Walk(Dir d, string prefix)
    {
        foreach (var f in d.Files)
            yield return new FolderEntry(FolderPath.Join(prefix, f.Key), false, f.Value);
        foreach (var s in d.Dirs)
        {
            if (Excluded.Contains(s.Name)) continue;
            var p = FolderPath.Join(prefix, s.Name);
            yield return new FolderEntry(p, true, null);
            foreach (var e in Walk(s, p)) yield return e;
        }
    }

    public void EnsureFolder(string path) => Nav(FolderPath.Split(path), true);

    public void SetFile(string path, string data)
    {
        var names = FolderPath.Split(path);
        var parent = Nav(names.Take(names.Count - 1).ToList(), true)!;
        var name = names[names.Count - 1];
        int i = parent.Files.FindIndex(f => f.Key == name);
        if (i < 0) parent.Files.Add(new KeyValuePair<string, string>(name, data));
        else parent.Files[i] = new KeyValuePair<string, string>(name, data);
    }

    public void DeleteFile(string path)
    {
        var names = FolderPath.Split(path);
        var parent = Nav(names.Take(names.Count - 1).ToList(), false);
        parent?.Files.RemoveAll(f => f.Key == names[names.Count - 1]);
    }

    public void DeleteFolder(string path)
    {
        var names = FolderPath.Split(path);
        var parent = Nav(names.Take(names.Count - 1).ToList(), false);
        parent?.Dirs.RemoveAll(d => d.Name == names[names.Count - 1]);
    }

    public void ReorderFiles(string folderPath, IList<string> orderedFileNames)
    {
        var d = Nav(FolderPath.Split(folderPath), false);
        if (d == null) return;
        d.Files = d.Files
            .OrderBy(f => { int i = orderedFileNames.IndexOf(f.Key); return i < 0 ? int.MaxValue : i; })
            .ToList();
    }

    // ── 测试辅助 ──
    public MemTree Put(string path, string data) { SetFile(path, data); return this; }
    public string? Get(string path)
    {
        var names = FolderPath.Split(path);
        var parent = Nav(names.Take(names.Count - 1).ToList(), false);
        var i = parent?.Files.FindIndex(f => f.Key == names[names.Count - 1]) ?? -1;
        return i < 0 ? null : parent!.Files[i].Value;
    }
    public bool HasFolder(string path) => Nav(FolderPath.Split(path), false) != null;
    public List<string> FileOrder(string folderPath) =>
        Nav(FolderPath.Split(folderPath), false)?.Files.Select(f => f.Key).ToList() ?? new();
    /// <summary>排序后的 "d:路径" / "f:路径=内容" 行，用来比较两棵树是否一致。</summary>
    public string Dump() => string.Join("\n",
        Entries().Select(e => e.IsFolder ? "d:" + e.Path : "f:" + e.Path + "=" + e.Data).OrderBy(s => s, StringComparer.Ordinal));
}

public class FolderSyncTests
{
    private sealed class TreeAdapter : ISyncAdapter
    {
        public string Kind => "tree";
        public bool TryGetKey(object instance, out string key)
        {
            key = instance is MemTree t ? "tree:" + t.Name : null!;
            return instance is MemTree;
        }
        public ISyncTarget CreateTarget(object instance, string key) => new FolderSyncTarget(key, (MemTree)instance);
        public IEnumerable<ISyncTarget> Enumerate() => Array.Empty<ISyncTarget>();
    }

    private sealed class Rig
    {
        public readonly Hub Hub = new();
        public readonly SyncEngine Server;
        public readonly Dictionary<string, SyncEngine> Clients = new();
        public readonly List<string> Logs = new();

        public Rig(params string[] clientIds)
        {
            Server = new SyncEngine(Hub.Server) { Log = Logs.Add };
            Server.RegisterAdapter(new TreeAdapter());
            foreach (var id in clientIds)
            {
                var c = new SyncEngine(Hub.AddClient(id)) { Log = Logs.Add };
                c.RegisterAdapter(new TreeAdapter());
                Clients[id] = c;
            }
        }
    }

    [Fact]
    public void Full_sync_on_connect_makes_client_match_server_and_drops_extras()
    {
        var rig = new Rig("p1");
        var s = new MemTree("mail").Put("accounts/bob/inbox/m1", "hello").Put("accounts/bob/AccountInfo", "pw");
        var c = new MemTree("mail").Put("junk/leftover", "random junk"); // 客户端自己随机生成的内容
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        rig.Server.OnPlayerConnected("p1");

        Assert.Equal(s.Dump(), c.Dump());
        Assert.Null(c.Get("junk/leftover"));
        Assert.False(c.HasFolder("junk"));
    }

    [Fact]
    public void Delta_add_modify_delete_reach_clients()
    {
        var rig = new Rig("p1", "p2");
        var s = new MemTree("irc").Put("active.log", "#");
        var c1 = new MemTree("irc").Put("active.log", "#");
        var c2 = new MemTree("irc").Put("active.log", "#");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c1);
        rig.Clients["p2"].Register(c2);

        s.Put("active.log", "#12:00//a//hi");   // 改
        s.Put("users.cfg", "x");                 // 增
        rig.Server.Tick();
        Assert.Equal(s.Dump(), c1.Dump());
        Assert.Equal(s.Dump(), c2.Dump());

        s.DeleteFile("users.cfg");               // 删
        rig.Server.Tick();
        Assert.Null(c1.Get("users.cfg"));
        Assert.Equal(s.Dump(), c2.Dump());
    }

    [Fact]
    public void Empty_folders_are_synced()
    {
        var rig = new Rig("p1");
        var s = new MemTree("mail");
        var c = new MemTree("mail");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        s.EnsureFolder("accounts/alice/inbox");
        rig.Server.Tick();

        Assert.True(c.HasFolder("accounts/alice/inbox"));
    }

    [Fact]
    public void Deleting_a_folder_removes_its_contents_on_clients()
    {
        var rig = new Rig("p1");
        var s = new MemTree("board").Put("Threads/t1", "a").Put("Threads/t2", "b").Put("keep", "k");
        var c = new MemTree("board").Put("Threads/t1", "a").Put("Threads/t2", "b").Put("keep", "k");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        s.DeleteFolder("Threads");
        rig.Server.Tick();

        Assert.False(c.HasFolder("Threads"));
        Assert.Equal("k", c.Get("keep"));
    }

    [Fact]
    public void Client_edit_is_proposed_applied_on_server_and_broadcast_to_others()
    {
        var rig = new Rig("p1", "p2");
        var s = new MemTree("mail");
        var c1 = new MemTree("mail");
        var c2 = new MemTree("mail");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c1);
        rig.Clients["p2"].Register(c2);

        c1.Put("accounts/bob/inbox/new", "from p1");
        rig.Clients["p1"].Tick();            // PROPOSE → 服务端
        Assert.Equal("from p1", s.Get("accounts/bob/inbox/new"));

        rig.Server.Tick();                   // 服务端检测到变化 → DELTA 广播
        Assert.Equal("from p1", c2.Get("accounts/bob/inbox/new"));
    }

    [Fact]
    public void Two_players_editing_different_files_do_not_overwrite_each_other()
    {
        var rig = new Rig("p1", "p2");
        var s = new MemTree("mail");
        var c1 = new MemTree("mail");
        var c2 = new MemTree("mail");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c1);
        rig.Clients["p2"].Register(c2);

        c1.Put("inbox/a", "A");
        c2.Put("inbox/b", "B");
        rig.Clients["p1"].Tick();
        rig.Clients["p2"].Tick();
        rig.Server.Tick();

        Assert.Equal("A", s.Get("inbox/a"));
        Assert.Equal("B", s.Get("inbox/b"));
    }

    [Fact]
    public void Excluded_subtree_is_neither_sent_nor_deleted()
    {
        var rig = new Rig("p1");
        var s = new MemTree("irc") { Excluded = { "runtime" } }.Put("active.log", "log");
        var c = new MemTree("irc") { Excluded = { "runtime" } }.Put("runtime/queued", "local-only");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        s.Put("runtime/server-queue", "server-only");
        rig.Server.OnPlayerConnected("p1");
        rig.Server.Tick();

        Assert.Equal("log", c.Get("active.log"));
        Assert.Equal("local-only", c.Get("runtime/queued"));   // 没被全量同步删掉
        Assert.Null(c.Get("runtime/server-queue"));            // 也没被收到
    }

    [Fact]
    public void File_order_is_preserved_by_full_sync()
    {
        var rig = new Rig("p1");
        var s = new MemTree("board").Put("listings/c", "1").Put("listings/a", "2").Put("listings/b", "3");
        var c = new MemTree("board").Put("listings/a", "2");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        rig.Server.OnPlayerConnected("p1");

        Assert.Equal(new List<string> { "c", "a", "b" }, c.FileOrder("listings"));
    }

    [Fact]
    public void Remote_apply_does_not_echo_back_to_the_server()
    {
        var rig = new Rig("p1");
        var s = new MemTree("mail");
        var c = new MemTree("mail");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        s.Put("inbox/x", "1");
        rig.Server.Tick();
        rig.Hub.ClientToServer.Clear();
        rig.Clients["p1"].Tick();

        Assert.Empty(rig.Hub.ClientToServer);
    }

    [Fact]
    public void Names_containing_separator_and_percent_roundtrip()
    {
        var rig = new Rig("p1");
        var s = new MemTree("odd");
        var c = new MemTree("odd");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        s.Put(FolderPath.Join("", "a/b%c") + "/" + FolderPath.Escape("x/y"), "v");
        rig.Server.Tick();

        Assert.Equal(s.Dump(), c.Dump());
        Assert.Equal("a/b%c", FolderPath.Split(FolderPath.Join("", "a/b%c"))[0]);
    }

    [Fact]
    public void Unchanged_tree_produces_no_traffic()
    {
        var rig = new Rig("p1");
        var s = new MemTree("mail").Put("x", "1");
        var c = new MemTree("mail").Put("x", "1");
        rig.Server.Register(s);
        rig.Clients["p1"].Register(c);

        rig.Server.Tick();
        rig.Clients["p1"].Tick();

        Assert.Empty(rig.Hub.ServerToClient);
        Assert.Empty(rig.Hub.ClientToServer);
    }
}
