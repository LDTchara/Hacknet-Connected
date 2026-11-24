using System;
using System.Collections.Generic;
using System.Linq;

namespace HNC.Sync
{
    /// <summary>
    /// 中间层运行时。由 ClientPlugin / ServerPlugin 各创建一个，注入各自的 <see cref="ISyncTransport"/>，
    /// 并在每帧（或固定频率）调用 <see cref="Tick"/>。
    ///
    /// 第一期权威模型：服务端权威。
    ///   服务端：检测变化 → 版本号 +1 → 广播 DELTA。
    ///   客户端：检测变化 → PROPOSE 给服务端；收到 DELTA/FULL 才是权威值。
    ///   版本号有缺口（ver != 本地+1）→ 发 REQ 要全量。
    /// 已知限制：Player 范围目前只支持 服务端 → 指定玩家 的推送；
    ///   服务端对单个对象只有一份字段值，没有"每玩家一份"的存储，客户端的 Player 范围 PROPOSE 会被忽略。
    /// </summary>
    public sealed class SyncEngine : IHncSyncRuntime
    {
        private sealed class Entry
        {
            public string Key;
            public WeakReference Instance;
            public ISyncTarget Target;
            public int WorldVersion;
            public int PlayerVersion;
        }

        private readonly ISyncTransport _t;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<ISyncAdapter> _adapters = new List<ISyncAdapter>();
        private readonly Dictionary<string, List<SyncMessage>> _pending = new Dictionary<string, List<SyncMessage>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ChannelImpl> _channels = new Dictionary<string, ChannelImpl>(StringComparer.Ordinal);
        private readonly object _lock = new object();

        [ThreadStatic] private static int _applying;

        public SerializerRegistry Serializers { get; private set; }
        public Action<string> Log = delegate { };

        public SyncEngine(ISyncTransport transport)
        {
            _t = transport;
            Serializers = new SerializerRegistry();
            _t.MessageReceived += OnRaw;
        }

        // ── IHncSyncRuntime ──────────────────────────────────────────────

        public bool IsActive { get { return _t != null; } }
        public bool IsServer { get { return _t.IsServer; } }
        public string LocalPlayerId { get { return _t.LocalPlayerId; } }
        public bool ApplyingRemote { get { return _applying > 0; } }

        public void RegisterAdapter(ISyncAdapter adapter)
        {
            lock (_lock) _adapters.Add(adapter);
        }

        public void RegisterSerializer(ISyncValueSerializer s) { Serializers.Add(s); }

        public void Register(object instance, string key = null)
        {
            if (instance == null) return;
            ISyncAdapter owner = null;

            lock (_lock)
            {
                if (key == null)
                {
                    var syncable = instance as ISyncable;
                    if (syncable != null) key = syncable.SyncId;
                }
                if (key == null)
                {
                    foreach (var a in _adapters)
                    {
                        string k;
                        if (a.TryGetKey(instance, out k)) { key = k; owner = a; break; }
                    }
                }
                if (key == null)
                {
                    Log("[Sync] 无法注册 " + instance.GetType().Name + "：没有 key（请传 key、实现 ISyncable，或注册适配器）");
                    return;
                }

                ISyncTarget target;
                if (owner != null) target = owner.CreateTarget(instance, key);
                else if (instance is ISyncable) target = new SyncableTarget((ISyncable)instance);
                else if (ReflectionTarget.HasSyncMembers(instance.GetType(), Serializers))
                    target = new ReflectionTarget(instance, key, Serializers, Log);
                else
                {
                    Log("[Sync] " + instance.GetType().Name + " 没有可同步成员（缺 [Sync] 或类型无序列化器）");
                    return;
                }

                _entries[key] = new Entry { Key = key, Instance = new WeakReference(instance), Target = target };
            }

            ReplayPending(key);
        }

        public void Unregister(object instance)
        {
            lock (_lock)
            {
                var dead = _entries.Where(kv => ReferenceEquals(kv.Value.Instance.Target, instance))
                                   .Select(kv => kv.Key).ToList();
                foreach (var k in dead) _entries.Remove(k);
            }
        }

        public void MarkDirty(object instance, string member)
        {
            if (ApplyingRemote) return;
            Entry e;
            lock (_lock)
                e = _entries.Values.FirstOrDefault(x => ReferenceEquals(x.Instance.Target, instance));
            if (e != null) Flush(e, true, member);
        }

        public IHncChannel Channel(string name)
        {
            lock (_lock)
            {
                ChannelImpl c;
                if (!_channels.TryGetValue(name, out c)) _channels[name] = c = new ChannelImpl(this, name);
                return c;
            }
        }

        // ── 驱动 ─────────────────────────────────────────────────────────

        /// <summary>建议 ~10Hz 调用。轮询所有已注册对象的变化并清理已被回收的对象。</summary>
        public void Tick()
        {
            if (ApplyingRemote) return;
            List<Entry> snapshot;
            lock (_lock)
            {
                foreach (var k in _entries.Where(kv => !kv.Value.Instance.IsAlive).Select(kv => kv.Key).ToList())
                    _entries.Remove(k);
                snapshot = _entries.Values.ToList();
            }
            foreach (var e in snapshot) Flush(e, false, null);
        }

        /// <summary>服务端调用：某玩家连入/重连，把当前全部状态发给他。</summary>
        public void OnPlayerConnected(string playerId)
        {
            if (!IsServer) return;
            Send(new SyncMessage { Op = SyncOp.Hello, Scope = SyncScope.World, PlayerId = playerId,
                                   Key = "", Payload = System.Text.Encoding.UTF8.GetBytes(SyncProtocol.ProtocolVersion) });
            SendFull(playerId, null);
        }

        // ── 发送 ─────────────────────────────────────────────────────────

        private void Flush(Entry e, bool force, string member)
        {
            var changes = new List<SyncChange>();
            try { e.Target.CollectChanges(changes, force, member); }
            catch (Exception ex) { Log("[Sync] 检测失败 " + e.Key + ": " + ex.Message); return; }

            foreach (var c in changes)
            {
                if (c.Scope == SyncScope.Local) continue;
                if (IsServer)
                {
                    // Player 范围：服务端不知道该推给谁（单份存储），M0 只广播 World
                    if (c.Scope != SyncScope.World) continue;
                    int ver = ++e.WorldVersion;
                    Send(new SyncMessage { Op = SyncOp.Delta, Scope = c.Scope, PlayerId = null,
                                           Key = e.Key, Version = ver, Payload = c.Data });
                }
                else
                {
                    if (c.Scope != SyncScope.World) continue; // 见类注释的已知限制
                    Send(new SyncMessage { Op = SyncOp.Propose, Scope = c.Scope, PlayerId = null,
                                           Key = e.Key, Version = e.WorldVersion, Payload = c.Data });
                }
            }
        }

        private void SendFull(string toPlayer, string onlyKey)
        {
            List<Entry> list;
            lock (_lock)
                list = _entries.Values.Where(x => onlyKey == null || x.Key == onlyKey).ToList();

            foreach (var e in list)
            {
                foreach (SyncScope scope in new[] { SyncScope.World, SyncScope.Player })
                {
                    if (!e.Target.HasScope(scope)) continue;
                    var data = e.Target.Snapshot(scope);
                    if (data == null) continue;
                    Send(new SyncMessage { Op = SyncOp.Full, Scope = scope, PlayerId = toPlayer,
                                           Key = e.Key,
                                           Version = scope == SyncScope.World ? e.WorldVersion : e.PlayerVersion,
                                           Payload = data });
                }
            }
        }

        private void Send(SyncMessage m)
        {
            try { _t.Send(SyncProtocol.Encode(m), m.PlayerId); }
            catch (Exception ex) { Log("[Sync] 发送失败: " + ex.Message); }
        }

        // ── 接收 ─────────────────────────────────────────────────────────

        private void OnRaw(string raw, string fromPlayerId)
        {
            SyncMessage m;
            if (!SyncProtocol.TryDecode(raw, out m)) return;
            try { Handle(m, fromPlayerId); }
            catch (Exception ex) { Log("[Sync] 处理失败 " + m.Op + " " + m.Key + ": " + ex.Message); }
        }

        private void Handle(SyncMessage m, string from)
        {
            switch (m.Op)
            {
                case SyncOp.Hello:
                    if (System.Text.Encoding.UTF8.GetString(m.Payload) != SyncProtocol.ProtocolVersion)
                        Log("[Sync] 协议版本不一致，对端=" + System.Text.Encoding.UTF8.GetString(m.Payload));
                    return;

                case SyncOp.Msg:
                    ChannelImpl c;
                    lock (_lock) _channels.TryGetValue(m.Key, out c);
                    if (c != null) c.Raise(from, m.Payload);
                    return;

                case SyncOp.Req:
                    if (IsServer) SendFull(from, m.Key);
                    return;

                case SyncOp.Propose:
                    if (!IsServer) return;
                    ApplyPropose(m);
                    return;

                case SyncOp.Full:
                case SyncOp.Delta:
                    if (IsServer) return; // 服务端不接收权威数据
                    ApplyAuthoritative(m);
                    return;
            }
        }

        private void ApplyPropose(SyncMessage m)
        {
            Entry e;
            lock (_lock) _entries.TryGetValue(m.Key, out e);
            if (e == null) { Log("[Sync] PROPOSE 找不到 " + m.Key); return; }
            // 刻意不进入 ApplyingRemote，也不更新基线：
            // 让服务端游戏逻辑正常响应这次写入，下一轮 Tick 检测到差异后自然广播给所有人（含提议者）。
            e.Target.Apply(m.Payload, true, false);
        }

        private void ApplyAuthoritative(SyncMessage m)
        {
            Entry e;
            lock (_lock) _entries.TryGetValue(m.Key, out e);
            if (e == null)
            {
                // 对象还没注册（加载顺序）：暂存，Register 时重放
                lock (_lock)
                {
                    List<SyncMessage> q;
                    if (!_pending.TryGetValue(m.Key, out q)) _pending[m.Key] = q = new List<SyncMessage>();
                    if (q.Count < 64) q.Add(m);
                }
                return;
            }

            bool world = m.Scope == SyncScope.World;
            int cur = world ? e.WorldVersion : e.PlayerVersion;
            if (m.Op == SyncOp.Delta && m.Version != cur + 1)
            {
                if (m.Version <= cur) return; // 过期/重复
                Send(new SyncMessage { Op = SyncOp.Req, Scope = m.Scope, Key = e.Key });
                return; // 缺口：等全量
            }

            _applying++;
            try { e.Target.Apply(m.Payload, m.Op == SyncOp.Delta, true); }
            finally { _applying--; }

            if (world) e.WorldVersion = m.Version; else e.PlayerVersion = m.Version;
        }

        private void ReplayPending(string key)
        {
            List<SyncMessage> q;
            lock (_lock)
            {
                if (!_pending.TryGetValue(key, out q)) return;
                _pending.Remove(key);
            }
            foreach (var m in q) ApplyAuthoritative(m);
        }

        internal void SendChannel(string name, byte[] payload, SyncScope scope)
        {
            Send(new SyncMessage { Op = SyncOp.Msg, Scope = scope, PlayerId = null, Key = name, Payload = payload });
        }

        private sealed class ChannelImpl : IHncChannel
        {
            private readonly SyncEngine _e;
            private readonly string _name;
            public ChannelImpl(SyncEngine e, string name) { _e = e; _name = name; }
            public void Send(byte[] payload, SyncScope scope) { _e.SendChannel(_name, payload, scope); }
            public event Action<string, byte[]> Received;
            internal void Raise(string from, byte[] p) { var h = Received; if (h != null) h(from, p); }
        }
    }
}
