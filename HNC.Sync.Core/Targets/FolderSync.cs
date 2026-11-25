using System;
using System.Collections.Generic;
using System.Linq;

namespace HNC.Sync
{
    /// <summary>文件夹树里的一个条目（扁平化）。Path 是相对根的编码路径，见 <see cref="FolderPath"/>。</summary>
    public sealed class FolderEntry
    {
        public string Path;
        public bool IsFolder;
        /// <summary>文件内容；文件夹为 null。</summary>
        public string Data;

        public FolderEntry() { }
        public FolderEntry(string path, bool isFolder, string data) { Path = path; IsFolder = isFolder; Data = data; }
    }

    /// <summary>
    /// 对"一棵文件夹树"的抽象。Hacknet 的 Folder/FileEntry 由 HNC.Sync.Adapters 实现它；
    /// 单元测试用内存实现。这样差异检测/增量/全量合并这些容易出错的逻辑可以脱离游戏测试。
    /// </summary>
    public interface IFolderModel
    {
        /// <summary>
        /// 按存储顺序枚举整棵树：父文件夹先于其内容；不含根自身；被排除的子树不出现。
        /// 根不存在（尚未创建）时返回空序列。
        /// </summary>
        IEnumerable<FolderEntry> Entries();

        /// <summary>创建文件夹（含缺失的父级）；已存在则什么都不做。</summary>
        void EnsureFolder(string path);

        /// <summary>创建或覆盖文件（含缺失的父文件夹）。必须原地修改已存在的文件对象，不得替换。</summary>
        void SetFile(string path, string data);

        void DeleteFile(string path);

        /// <summary>递归删除文件夹。</summary>
        void DeleteFolder(string path);

        /// <summary>
        /// 把 folderPath 下的文件按给定顺序重排（folderPath 为 "" 表示根）。
        /// 名单里没有的文件保持相对顺序、排在最后。
        /// </summary>
        void ReorderFiles(string folderPath, IList<string> orderedFileNames);
    }

    /// <summary>路径编码：用 '/' 分隔，名字里的 '/' 与 '%' 被转义，因此任何名字都是安全的。</summary>
    public static class FolderPath
    {
        public static string Escape(string name)
        {
            return (name ?? "").Replace("%", "%25").Replace("/", "%2F");
        }

        public static string Unescape(string s)
        {
            return (s ?? "").Replace("%2F", "/").Replace("%25", "%");
        }

        public static string Join(string parent, string name)
        {
            var e = Escape(name);
            return string.IsNullOrEmpty(parent) ? e : parent + "/" + e;
        }

        public static IList<string> Split(string path)
        {
            if (string.IsNullOrEmpty(path)) return new List<string>();
            return path.Split('/').Select(Unescape).ToList();
        }

        /// <summary>父路径；顶层条目的父路径是 ""。</summary>
        public static string Parent(string path)
        {
            int i = path.LastIndexOf('/');
            return i < 0 ? "" : path.Substring(0, i);
        }

        /// <summary>最后一段（已反转义）。</summary>
        public static string Name(string path)
        {
            int i = path.LastIndexOf('/');
            return Unescape(i < 0 ? path : path.Substring(i + 1));
        }

        public static int Depth(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            int d = 1;
            foreach (char c in path) if (c == '/') d++;
            return d;
        }
    }

    /// <summary>
    /// 文件夹树的同步目标：World 范围、以"文件/文件夹"为粒度做增量。
    /// 两个玩家改不同的文件互不覆盖；同一文件以服务端最终结果为准。
    /// 载荷是一串操作：[kind, path, data]，kind = 0 建文件夹 / 1 写文件 / 2 删文件夹 / 3 删文件。
    /// 全量快照就是"整棵树的全部写操作"，应用时再额外删掉本地多出来的条目。
    /// </summary>
    public sealed class FolderSyncTarget : ISyncTarget
    {
        private const int OpUpsertFolder = 0;
        private const int OpUpsertFile = 1;
        private const int OpDeleteFolder = 2;
        private const int OpDeleteFile = 3;

        private struct Op
        {
            public int Kind;
            public string Path;
            public string Data;
        }

        private readonly IFolderModel _model;
        private Dictionary<string, string> _baseline;

        public string Key { get; private set; }

        /// <summary>创建时把当前内容记为基线：初始世界由存档传输负责，这里只管之后的变化。</summary>
        public FolderSyncTarget(string key, IFolderModel model)
        {
            Key = key;
            _model = model;
            _baseline = Capture();
        }

        public bool HasScope(SyncScope scope) { return scope == SyncScope.World; }

        public byte[] Snapshot(SyncScope scope)
        {
            if (scope != SyncScope.World) return null;
            var ops = new List<Op>();
            foreach (var e in _model.Entries())
                ops.Add(new Op { Kind = e.IsFolder ? OpUpsertFolder : OpUpsertFile, Path = e.Path, Data = e.IsFolder ? null : e.Data });
            return Encode(ops);
        }

        public void Apply(byte[] data, bool isDelta, bool updateBaseline)
        {
            var ops = Decode(data);
            if (isDelta) ApplyOrdered(ops);
            else ApplyFull(ops);

            // 收到的是权威值：把基线对齐到"应用后的真实状态"，避免下一轮轮询把它当成本地改动回传
            if (updateBaseline) _baseline = Capture();
        }

        public void CollectChanges(List<SyncChange> output, bool forceCheck, string onlyMember)
        {
            var cur = Capture();
            var ops = Diff(_baseline, cur);
            if (ops.Count == 0) return;
            _baseline = cur; // 发出即视为已同步
            output.Add(new SyncChange { Scope = SyncScope.World, Data = Encode(ops) });
        }

        // ── 内部 ─────────────────────────────────────────────────────────

        private Dictionary<string, string> Capture()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in _model.Entries())
                map[MapKey(e.IsFolder, e.Path)] = e.IsFolder ? null : e.Data;
            return map;
        }

        private static string MapKey(bool isFolder, string path) { return (isFolder ? "d:" : "f:") + path; }

        private static string MapKey(Op o)
        {
            return MapKey(o.Kind == OpUpsertFolder || o.Kind == OpDeleteFolder, o.Path);
        }

        private static List<Op> Diff(Dictionary<string, string> old, Dictionary<string, string> cur)
        {
            var ops = new List<Op>();
            foreach (var kv in cur)
            {
                string o;
                if (old.TryGetValue(kv.Key, out o) && string.Equals(o, kv.Value, StringComparison.Ordinal)) continue;
                bool folder = kv.Key[0] == 'd';
                ops.Add(new Op { Kind = folder ? OpUpsertFolder : OpUpsertFile, Path = kv.Key.Substring(2), Data = kv.Value });
            }
            foreach (var kv in old)
            {
                if (cur.ContainsKey(kv.Key)) continue;
                bool folder = kv.Key[0] == 'd';
                ops.Add(new Op { Kind = folder ? OpDeleteFolder : OpDeleteFile, Path = kv.Key.Substring(2) });
            }
            return Order(ops);
        }

        /// <summary>删除先于写入；删除按"深的在前、文件在文件夹前"；写入按"文件夹在前、浅的在前"。</summary>
        private static List<Op> Order(IEnumerable<Op> ops)
        {
            var list = ops.ToList();
            var deletes = list.Where(o => o.Kind == OpDeleteFolder || o.Kind == OpDeleteFile)
                              .OrderByDescending(o => FolderPath.Depth(o.Path))
                              .ThenBy(o => o.Kind == OpDeleteFile ? 0 : 1);
            var upserts = list.Where(o => o.Kind == OpUpsertFolder || o.Kind == OpUpsertFile)
                              .OrderBy(o => o.Kind == OpUpsertFolder ? 0 : 1)
                              .ThenBy(o => o.Kind == OpUpsertFolder ? FolderPath.Depth(o.Path) : 0); // OrderBy 稳定：同一文件夹内文件保持原顺序
            return deletes.Concat(upserts).ToList();
        }

        private void ApplyOrdered(IEnumerable<Op> ops)
        {
            foreach (var o in Order(ops))
            {
                switch (o.Kind)
                {
                    case OpUpsertFolder: _model.EnsureFolder(o.Path); break;
                    case OpUpsertFile: _model.SetFile(o.Path, o.Data ?? ""); break;
                    case OpDeleteFolder: _model.DeleteFolder(o.Path); break;
                    case OpDeleteFile: _model.DeleteFile(o.Path); break;
                }
            }
        }

        private void ApplyFull(List<Op> remote)
        {
            var remoteMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var o in remote)
                remoteMap[MapKey(o)] = o.Kind == OpUpsertFolder ? null : o.Data;

            var cur = Capture();
            var ops = new List<Op>();

            // 本地多出来的 → 删除
            foreach (var kv in cur)
            {
                if (remoteMap.ContainsKey(kv.Key)) continue;
                bool folder = kv.Key[0] == 'd';
                ops.Add(new Op { Kind = folder ? OpDeleteFolder : OpDeleteFile, Path = kv.Key.Substring(2) });
            }
            // 缺失或内容不同的 → 写入（相同的不动，避免无谓地改写对象）
            foreach (var o in remote)
            {
                string c;
                if (cur.TryGetValue(MapKey(o), out c) && string.Equals(c, remoteMap[MapKey(o)], StringComparison.Ordinal)) continue;
                ops.Add(o);
            }
            ApplyOrdered(ops);

            // 文件顺序：任务板/邮件列表按存储顺序显示
            foreach (var g in remote.Where(o => o.Kind == OpUpsertFile).GroupBy(o => FolderPath.Parent(o.Path)))
            {
                var names = g.Select(o => FolderPath.Name(o.Path)).ToList();
                if (names.Count > 1) _model.ReorderFiles(g.Key, names);
            }
        }

        private static byte[] Encode(List<Op> ops)
        {
            var w = new SyncWriter();
            w.WriteInt(ops.Count);
            foreach (var o in ops)
            {
                w.WriteInt(o.Kind);
                w.WriteString(o.Path);
                w.WriteString(o.Data);
            }
            return w.ToArray();
        }

        private static List<Op> Decode(byte[] data)
        {
            var r = new SyncReader(data);
            int n = r.ReadInt();
            var list = new List<Op>(Math.Max(0, n));
            for (int i = 0; i < n; i++)
            {
                var o = new Op { Kind = r.ReadInt(), Path = r.ReadString(), Data = r.ReadString() };
                list.Add(o);
            }
            return list;
        }
    }
}
