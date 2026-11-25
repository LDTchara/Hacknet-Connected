using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HNC.Sync
{
    /// <summary>
    /// 方式 A（特性）的实现：扫描 [Sync] 成员（可选再加 [XMLStorage] 成员），
    /// 以"成员名 → 序列化字节"为单位做快照/增量。线上协议按成员名而非下标寻址，
    /// 因此两端成员集合略有出入（版本不一致）时只会忽略未知成员，而不会错位。
    /// </summary>
    public sealed class ReflectionTarget : ISyncTarget
    {
        private sealed class Member
        {
            public string Name;
            public Type Type;
            public SyncScope Scope;
            public SyncMode Mode;
            public Func<object, object> Get;
            public Action<object, object> Set;
            public byte[] Baseline;
        }

        private readonly WeakReference _instance;
        private readonly SerializerRegistry _ser;
        private readonly Dictionary<string, Member> _members;
        private readonly List<Member> _ordered;

        public string Key { get; private set; }
        public bool IsAlive { get { return _instance.IsAlive; } }
        public int MemberCount { get { return _ordered.Count; } }

        public ReflectionTarget(object instance, string key, SerializerRegistry ser, Action<string> log)
        {
            _instance = new WeakReference(instance);
            _ser = ser;
            Key = key;
            _ordered = Scan(instance.GetType(), ser, log);
            _members = _ordered.ToDictionary(m => m.Name, StringComparer.Ordinal);

            // 初始基线 = 当前值：初始世界由存档传输负责，这里只管之后的变化
            foreach (var m in _ordered)
                m.Baseline = Read(instance, m);
        }

        /// <summary>类型上是否有可同步成员（用于决定是否值得创建目标）。</summary>
        public static bool HasSyncMembers(Type t, SerializerRegistry ser)
        {
            return Scan(t, ser, null).Count > 0;
        }

        private static List<Member> Scan(Type type, SerializerRegistry ser, Action<string> log)
        {
            var typeAttr = type.GetCustomAttributes(typeof(SyncTypeAttribute), true)
                               .Cast<SyncTypeAttribute>().FirstOrDefault();
            var defaultScope = typeAttr != null ? typeAttr.Scope : SyncScope.World;
            bool useXml = typeAttr != null && typeAttr.UseXmlStorage;

            var result = new Dictionary<string, Member>(StringComparer.Ordinal);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                                     | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            // 逐层遍历继承链，才能拿到基类里的 private 成员
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var mi in t.GetMembers(flags))
                {
                    if (!(mi is FieldInfo) && !(mi is PropertyInfo)) continue;
                    if (mi.GetCustomAttributes(typeof(NoSyncAttribute), true).Length > 0) continue;

                    var sync = mi.GetCustomAttributes(typeof(SyncAttribute), true)
                                 .Cast<SyncAttribute>().FirstOrDefault();
                    bool xml = useXml && mi.GetCustomAttributes(true)
                                           .Any(a => a.GetType().Name == "XMLStorageAttribute");
                    if (sync == null && !xml) continue;

                    var scope = (sync != null && sync.ScopeOverride.HasValue) ? sync.ScopeOverride.Value : defaultScope;
                    if (scope == SyncScope.Local) continue;

                    var m = new Member
                    {
                        Name = mi.Name,
                        Scope = scope,
                        Mode = sync != null ? sync.Mode : SyncMode.Auto,
                    };

                    var fi = mi as FieldInfo;
                    var pi = mi as PropertyInfo;
                    if (fi != null)
                    {
                        if (fi.IsInitOnly || fi.IsLiteral) continue;
                        m.Type = fi.FieldType;
                        m.Get = fi.GetValue;
                        m.Set = fi.SetValue;
                    }
                    else
                    {
                        if (!pi.CanRead || !pi.CanWrite || pi.GetIndexParameters().Length > 0) continue;
                        m.Type = pi.PropertyType;
                        m.Get = o => pi.GetValue(o, null);
                        m.Set = (o, v) => pi.SetValue(o, v, null);
                    }

                    if (!ser.CanSerialize(m.Type))
                    {
                        if (log != null) log("[Sync] 跳过 " + type.Name + "." + m.Name + "：类型 " + m.Type.Name + " 没有序列化器");
                        continue;
                    }

                    if (result.ContainsKey(m.Name)) continue; // 子类同名成员优先
                    result[m.Name] = m;
                }
            }

            return result.Values.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        }

        private byte[] Read(object instance, Member m)
        {
            return _ser.ToBytes(m.Get(instance), m.Type);
        }

        public bool HasScope(SyncScope scope) { return _ordered.Any(m => m.Scope == scope); }

        public byte[] Snapshot(SyncScope scope)
        {
            var inst = _instance.Target;
            if (inst == null) return null;
            var picked = _ordered.Where(m => m.Scope == scope).ToList();
            var w = new SyncWriter();
            w.WriteInt(picked.Count);
            foreach (var m in picked)
            {
                w.WriteString(m.Name);
                w.WriteBytes(Read(inst, m));
            }
            return w.ToArray();
        }

        public void Apply(byte[] data, bool isDelta, bool updateBaseline)
        {
            var inst = _instance.Target;
            if (inst == null) return;
            var r = new SyncReader(data);
            int n = r.ReadInt();
            for (int i = 0; i < n; i++)
            {
                string name = r.ReadString();
                byte[] bytes = r.ReadBytes();
                Member m;
                if (!_members.TryGetValue(name, out m)) continue; // 对端多出来的成员：忽略
                m.Set(inst, _ser.Read(new SyncReader(bytes), m.Type));
                if (updateBaseline) m.Baseline = bytes;
            }
        }

        public void CollectChanges(List<SyncChange> output, bool forceCheck, string onlyMember)
        {
            var inst = _instance.Target;
            if (inst == null) return;

            foreach (SyncScope scope in new[] { SyncScope.World, SyncScope.Player })
            {
                var changed = new List<KeyValuePair<string, byte[]>>();
                foreach (var m in _ordered)
                {
                    if (m.Scope != scope) continue;
                    if (m.Mode == SyncMode.Manual && !forceCheck) continue;
                    if (onlyMember != null && m.Name != onlyMember) continue;

                    var cur = Read(inst, m);
                    if (BytesEqual(cur, m.Baseline)) continue;
                    m.Baseline = cur; // 发出即视为已同步，避免重复发送
                    changed.Add(new KeyValuePair<string, byte[]>(m.Name, cur));
                }
                if (changed.Count == 0) continue;

                var w = new SyncWriter();
                w.WriteInt(changed.Count);
                foreach (var kv in changed) { w.WriteString(kv.Key); w.WriteBytes(kv.Value); }
                output.Add(new SyncChange { Scope = scope, Data = w.ToArray() });
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    /// <summary>方式 B（ISyncable）的目标包装。</summary>
    public sealed class SyncableTarget : ISyncTarget
    {
        private readonly ISyncable _s;
        public SyncableTarget(ISyncable s) { _s = s; }

        public string Key { get { return _s.SyncId; } }
        public bool HasScope(SyncScope scope) { return _s.Scope == scope; }

        public byte[] Snapshot(SyncScope scope)
        {
            if (_s.Scope != scope) return null;
            var w = new SyncWriter();
            _s.WriteState(w);
            return w.ToArray();
        }

        public void Apply(byte[] data, bool isDelta, bool updateBaseline)
        {
            var r = new SyncReader(data);
            if (isDelta) _s.ReadDelta(r); else _s.ReadState(r);
        }

        public void CollectChanges(List<SyncChange> output, bool forceCheck, string onlyMember)
        {
            if (_s.Scope == SyncScope.Local) return;
            var w = new SyncWriter();
            if (_s.TryWriteDelta(w))
                output.Add(new SyncChange { Scope = _s.Scope, Data = w.ToArray() });
        }
    }
}
