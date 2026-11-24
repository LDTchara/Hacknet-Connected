using System;
using System.Collections.Generic;
using System.Linq;
using Hacknet;

namespace HNC.Sync.Adapters
{
    /// <summary>
    /// 把 Hacknet 的 <see cref="Folder"/> / <see cref="FileEntry"/> 树适配成 <see cref="IFolderModel"/>。
    /// 关键约束：所有修改都是"原地"的——Daemon 在 initFiles/loadInit 里把子文件夹和文件对象缓存成字段
    /// （MailServer.accounts、IRCSystem.ActiveLogFile 等），替换对象会让这些引用悬空。
    /// </summary>
    public sealed class HacknetFolderModel : IFolderModel
    {
        private readonly Func<Folder> _root;
        private readonly HashSet<string> _excluded;

        /// <param name="root">每次调用时解析根文件夹（Daemon 的文件夹可能在加载后才出现）。返回 null 表示还没有。</param>
        /// <param name="excludedFolderNames">任意层级里同名的文件夹整棵跳过（不枚举、不删除）。</param>
        public HacknetFolderModel(Func<Folder> root, IEnumerable<string> excludedFolderNames)
        {
            _root = root;
            _excluded = new HashSet<string>(excludedFolderNames ?? new string[0], StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<FolderEntry> Entries()
        {
            var root = _root();
            if (root == null) yield break;
            foreach (var e in Walk(root, "")) yield return e;
        }

        private IEnumerable<FolderEntry> Walk(Folder folder, string prefix)
        {
            // ToArray：枚举期间游戏逻辑可能改列表，拷一份避免 InvalidOperationException
            foreach (var f in folder.files.ToArray())
            {
                if (f == null) continue;
                yield return new FolderEntry(FolderPath.Join(prefix, f.name), false, f.data);
            }
            foreach (var sub in folder.folders.ToArray())
            {
                if (sub == null || _excluded.Contains(sub.name)) continue;
                string path = FolderPath.Join(prefix, sub.name);
                yield return new FolderEntry(path, true, null);
                foreach (var e in Walk(sub, path)) yield return e;
            }
        }

        public void EnsureFolder(string path)
        {
            var root = _root();
            if (root == null) return;
            Navigate(root, FolderPath.Split(path), true);
        }

        public void SetFile(string path, string data)
        {
            var root = _root();
            if (root == null) return;
            var names = FolderPath.Split(path);
            if (names.Count == 0) return;

            var parent = Navigate(root, names.Take(names.Count - 1).ToList(), true);
            string name = names[names.Count - 1];
            data = data ?? "";

            var existing = parent.files.FirstOrDefault(x => x != null && x.name == name);
            if (existing == null)
            {
                parent.files.Add(new FileEntry(data, name));
            }
            else
            {
                existing.data = data;
                existing.size = data.Length * 8; // 与 FileEntry 构造函数里的算法一致
            }
        }

        public void DeleteFile(string path)
        {
            var root = _root();
            if (root == null) return;
            var names = FolderPath.Split(path);
            if (names.Count == 0) return;
            var parent = Navigate(root, names.Take(names.Count - 1).ToList(), false);
            if (parent == null) return;
            string name = names[names.Count - 1];
            parent.files.RemoveAll(x => x != null && x.name == name);
        }

        public void DeleteFolder(string path)
        {
            var root = _root();
            if (root == null) return;
            var names = FolderPath.Split(path);
            if (names.Count == 0) return;
            var parent = Navigate(root, names.Take(names.Count - 1).ToList(), false);
            if (parent == null) return;
            string name = names[names.Count - 1];
            if (_excluded.Contains(name)) return;
            parent.folders.RemoveAll(x => x != null && x.name == name);
        }

        public void ReorderFiles(string folderPath, IList<string> orderedFileNames)
        {
            var root = _root();
            if (root == null) return;
            var folder = Navigate(root, FolderPath.Split(folderPath), false);
            if (folder == null) return;

            var order = new Dictionary<string, int>();
            for (int i = 0; i < orderedFileNames.Count; i++)
                if (!order.ContainsKey(orderedFileNames[i])) order[orderedFileNames[i]] = i;

            // OrderBy 是稳定排序：名单外的文件保持原相对顺序、排在最后
            var sorted = folder.files
                .OrderBy(f => f != null && order.ContainsKey(f.name) ? order[f.name] : int.MaxValue)
                .ToList();
            folder.files.Clear();
            folder.files.AddRange(sorted); // 复用原对象，不新建
        }

        /// <summary>沿名字链走到目标文件夹；create=true 时补建缺失的；否则缺失返回 null。</summary>
        private Folder Navigate(Folder start, IList<string> names, bool create)
        {
            var cur = start;
            foreach (var n in names)
            {
                if (_excluded.Contains(n)) return create ? cur : null;
                Folder next = cur.folders.FirstOrDefault(x => x != null && x.name == n);
                if (next == null)
                {
                    if (!create) return null;
                    next = new Folder(n);
                    cur.folders.Add(next);
                }
                cur = next;
            }
            return cur;
        }
    }
}
