using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Hacknet;

namespace HNC.Sync.Adapters
{
    /// <summary>
    /// 一类 Daemon 的同步规则：它的状态存在电脑文件系统的哪个子文件夹里，哪些子文件夹不同步。
    /// </summary>
    public sealed class DaemonHandler
    {
        public Type DaemonType;
        /// <summary>返回该 Daemon 在 comp.files.root 下的文件夹名。</summary>
        public Func<Daemon, string> RootFolderName;
        public string[] ExcludeFolders = new string[0];
        /// <summary>返回 true 表示这个具体实例不同步（例如玩家自己的邮箱）。可为空。</summary>
        public Func<Daemon, bool> Skip;

        internal Folder ResolveRoot(Daemon d)
        {
            if (d == null || d.comp == null || d.comp.files == null || d.comp.files.root == null) return null;
            string name = RootFolderName(d);
            return string.IsNullOrEmpty(name) ? null : d.comp.files.root.searchForFolder(name);
        }
    }

    /// <summary>
    /// 原版 Daemon 适配器。key = daemon:{computer.idName}:{类型名}:{同类型第几个}。
    /// 不用 Daemon.name（会被本地化）、不用 IP（节点 IP 会轮转），只用 idName + 类型 + 序号，
    /// 前提是两端加载的是同一份扩展（Daemon 顺序一致）。
    /// </summary>
    public sealed class DaemonAdapter : ISyncAdapter
    {
        public string Kind { get { return "daemon"; } }

        public bool TryGetKey(object instance, out string key)
        {
            key = null;
            var d = instance as Daemon;
            if (d == null || d.comp == null || DaemonSync.FindHandler(d) == null) return false;

            int ordinal = 0;
            foreach (var other in d.comp.daemons)
            {
                if (ReferenceEquals(other, d)) break;
                if (other != null && other.GetType() == d.GetType()) ordinal++;
            }
            key = "daemon:" + d.comp.idName + ":" + d.GetType().Name + ":" + ordinal;
            return true;
        }

        public ISyncTarget CreateTarget(object instance, string key)
        {
            var d = (Daemon)instance;
            var handler = DaemonSync.FindHandler(d);
            return new FolderSyncTarget(key, new HacknetFolderModel(() => handler.ResolveRoot(d), handler.ExcludeFolders));
        }

        public IEnumerable<ISyncTarget> Enumerate()
        {
            var os = OS.currentInstance;
            if (os == null || os.netMap == null || os.netMap.nodes == null) yield break;
            foreach (var comp in os.netMap.nodes.ToArray())
            {
                if (comp == null || comp.daemons == null) continue;
                foreach (var d in comp.daemons.ToArray())
                {
                    string key;
                    if (TryGetKey(d, out key)) yield return CreateTarget(d, key);
                }
            }
        }
    }

    /// <summary>
    /// 入口。宿主（ClientPlugin / ServerPlugin）在 HncSync.Runtime 就绪后调用 <see cref="Install"/>，
    /// 世界加载完成后调用 <see cref="RegisterAll"/>，之后可周期性再调用以捕获运行时新增的 Daemon。
    /// 第三方 mod 的"文件夹型 Daemon"用 <see cref="AddHandler"/> 即可接入，无需写适配器。
    /// </summary>
    public static class DaemonSync
    {
        private static readonly List<DaemonHandler> Handlers = new List<DaemonHandler>();
        private static readonly DaemonAdapter Adapter = new DaemonAdapter();
        private static ConditionalWeakTable<Daemon, object> _registered = new ConditionalWeakTable<Daemon, object>();

        // 宿主可能停止后重启（新的 SyncEngine）：适配器和"已注册"记录都属于具体的 Runtime 实例，
        // Runtime 换了就必须重新安装、重新注册，否则新引擎里什么都没有。
        private static IHncSyncRuntime _installedInto;

        static DaemonSync()
        {
            // ── 原版 Daemon：状态 = 电脑文件系统里的一个子文件夹 ──
            // 越具体的类型越先注册（FindHandler 取第一个匹配）。
            // 玩家自己的邮箱（jmail）是"每玩家一份"：账户按玩家名建，收件箱是私人的。
            // 按 World 范围同步会让所有人的邮件互相可见，所以跳过它；NPC 邮件服务器照常同步。
            // （等 Player 范围打通后，可以改成按玩家单独同步。）
            AddHandler(typeof(MailServer), d => "mail").Skip = IsPlayerMailbox;
            AddHandler(typeof(MessageBoardDaemon), d => "ImageBoard");
            // IRC 的 runtime 子文件夹是 DelayableActionSystem 的待执行动作队列：
            // 同步它会让每个客户端都执行一遍，所以排除。
            AddHandler(typeof(IRCDaemon), d => "IRC", "runtime");
            AddHandler(typeof(UploadServerDaemon), d => ((UploadServerDaemon)d).Foldername);
        }

        private static bool IsPlayerMailbox(Daemon d)
        {
            if (d.comp == null) return false;
            if (string.Equals(d.comp.idName, "jmail", StringComparison.OrdinalIgnoreCase)) return true;
            var os = OS.currentInstance;
            return os != null && os.netMap != null && ReferenceEquals(os.netMap.mailServer, d.comp);
        }

        /// <summary>
        /// 声明一类 Daemon 的状态存在 comp.files.root 下的哪个文件夹里。
        /// 同一类型重复声明时，后声明的优先。
        /// </summary>
        public static DaemonHandler AddHandler(Type daemonType, Func<Daemon, string> rootFolderName, params string[] excludeFolders)
        {
            if (daemonType == null || rootFolderName == null) return null;
            var handler = new DaemonHandler
            {
                DaemonType = daemonType,
                RootFolderName = rootFolderName,
                ExcludeFolders = excludeFolders ?? new string[0],
            };
            lock (Handlers) Handlers.Insert(0, handler);
            return handler;
        }

        internal static DaemonHandler FindHandler(Daemon d)
        {
            DaemonHandler h;
            lock (Handlers)
                h = Handlers.FirstOrDefault(x => x.DaemonType.IsInstanceOfType(d));
            return h != null && h.Skip != null && h.Skip(d) ? null : h;
        }

        /// <summary>
        /// 把适配器注册进当前 Runtime。HncSync.Runtime 为空时什么都不做（返回 false），可稍后重试。
        /// 对同一个 Runtime 重复调用是空操作；Runtime 换成新实例时会重新安装并清空"已注册"记录。
        /// </summary>
        public static bool Install()
        {
            var rt = HncSync.Runtime;
            if (rt == null) return false;
            if (ReferenceEquals(_installedInto, rt)) return true;

            HncSync.RegisterAdapter(Adapter);
            _registered = new ConditionalWeakTable<Daemon, object>();
            _installedInto = rt;
            return true;
        }

        /// <summary>
        /// 扫描当前世界所有节点上的 Daemon，把还没注册的注册进同步引擎。可重复调用。
        /// 必须在世界加载完成之后调用（此时 Daemon 的文件夹已经存在，基线才是正确的）。
        /// </summary>
        /// <returns>本次新注册的数量。</returns>
        public static int RegisterAll(OS os)
        {
            if (!HncSync.IsActive || !Install()) return 0;
            if (os == null || os.netMap == null || os.netMap.nodes == null) return 0;

            int count = 0;
            foreach (var comp in os.netMap.nodes.ToArray())
            {
                if (comp == null || comp.daemons == null) continue;
                foreach (var d in comp.daemons.ToArray())
                {
                    if (d == null || FindHandler(d) == null) continue;
                    object tmp;
                    if (_registered.TryGetValue(d, out tmp)) continue;

                    HncSync.Register(d);
                    _registered.Add(d, new object());
                    count++;
                }
            }
            return count;
        }
    }
}
