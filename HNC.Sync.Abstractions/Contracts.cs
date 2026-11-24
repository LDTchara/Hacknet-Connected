using System;
using System.Collections.Generic;

namespace HNC.Sync
{
    /// <summary>方式 B：作者自己控制序列化。对应设计文档里的 ISyncableState。</summary>
    public interface ISyncable
    {
        /// <summary>跨进程稳定的标识，如 "daemon:myNode:MyDaemon"。不得使用 IP 或对象下标。</summary>
        string SyncId { get; }
        SyncScope Scope { get; }
        void WriteState(ISyncWriter w);
        void ReadState(ISyncReader r);
        /// <summary>无变化返回 false；有则写入增量。默认实现可直接返回 false 并只靠全量。</summary>
        bool TryWriteDelta(ISyncWriter w);
        void ReadDelta(ISyncReader r);
    }

    /// <summary>引擎产出的一次变化。</summary>
    public sealed class SyncChange
    {
        public SyncScope Scope;
        /// <summary>Player 范围时的目标玩家；World 为 null。</summary>
        public string PlayerId;
        public byte[] Data;
    }

    /// <summary>一个可同步的对象（适配器返回）。</summary>
    public interface ISyncTarget
    {
        string Key { get; }
        /// <summary>该对象是否含指定范围的成员。</summary>
        bool HasScope(SyncScope scope);
        /// <summary>全量快照，只含指定范围。</summary>
        byte[] Snapshot(SyncScope scope);
        /// <summary>
        /// 应用远端数据。updateBaseline=true 表示这是"收到的权威值"，
        /// 应更新比较基线以免被下一轮轮询当成本地变化回传。
        /// </summary>
        void Apply(byte[] data, bool isDelta, bool updateBaseline);
        /// <summary>轮询差异；forceCheck=true 时也检查 Manual 成员（MarkDirty 触发）。</summary>
        void CollectChanges(List<SyncChange> output, bool forceCheck, string onlyMember);
    }

    /// <summary>适配器：解决"怎么找到对象、怎么给它起 key"。</summary>
    public interface ISyncAdapter
    {
        /// <summary>种类名，如 "daemon"、"module"。</summary>
        string Kind { get; }
        /// <summary>该适配器是否认识这个实例；认识则给出 key。</summary>
        bool TryGetKey(object instance, out string key);
        /// <summary>为实例创建同步目标。</summary>
        ISyncTarget CreateTarget(object instance, string key);
        /// <summary>全量同步/重连时枚举当前所有存活目标。</summary>
        IEnumerable<ISyncTarget> Enumerate();
    }

    /// <summary>传输层：由 ClientPlugin / ServerPlugin 各实现一份。</summary>
    public interface ISyncTransport
    {
        bool IsServer { get; }
        string LocalPlayerId { get; }
        /// <summary>toPlayerId 为 null 表示广播（客户端发出时恒送服务端）。</summary>
        void Send(string message, string toPlayerId);
        event Action<string, string> MessageReceived; // (message, fromPlayerId)
    }

    /// <summary>方式 C：自由消息通道。</summary>
    public interface IHncChannel
    {
        void Send(byte[] payload, SyncScope scope);
        event Action<string, byte[]> Received; // (fromPlayerId, payload)
    }

    /// <summary>Core 实现，通过 <see cref="HncSync.Runtime"/> 注入。</summary>
    public interface IHncSyncRuntime
    {
        bool IsActive { get; }
        bool IsServer { get; }
        string LocalPlayerId { get; }
        bool ApplyingRemote { get; }
        void Register(object instance, string key);
        void Unregister(object instance);
        void MarkDirty(object instance, string member);
        void RegisterAdapter(ISyncAdapter adapter);
        void RegisterSerializer(ISyncValueSerializer serializer);
        IHncChannel Channel(string name);
    }
}
