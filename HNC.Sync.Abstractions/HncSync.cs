using System;

namespace HNC.Sync
{
    /// <summary>
    /// 公开外观。Runtime 为 null（未安装 HNC / 单机）时所有调用都是安全的空操作，
    /// 因此引用本程序集的 mod 在单机下零影响。
    /// </summary>
    public static class HncSync
    {
        public static IHncSyncRuntime Runtime { get; set; }

        public static bool IsActive { get { return Runtime != null && Runtime.IsActive; } }
        public static bool IsServer { get { return Runtime != null && Runtime.IsServer; } }
        public static string LocalPlayerId { get { return Runtime != null ? Runtime.LocalPlayerId : null; } }
        /// <summary>true 表示远端数据正在写入本地对象——此时 mod 自己的 setter/钩子不应再触发同步。</summary>
        public static bool ApplyingRemote { get { return Runtime != null && Runtime.ApplyingRemote; } }

        /// <summary>注册实例。key 为空时依次尝试 ISyncable.SyncId、适配器的 TryGetKey。</summary>
        public static void Register(object instance, string key = null)
        {
            if (Runtime != null) Runtime.Register(instance, key);
        }

        public static void Unregister(object instance)
        {
            if (Runtime != null) Runtime.Unregister(instance);
        }

        /// <summary>立即检查并推送。member 为 null 表示该对象所有成员。</summary>
        public static void MarkDirty(object instance, string member = null)
        {
            if (Runtime != null) Runtime.MarkDirty(instance, member);
        }

        public static void RegisterAdapter(ISyncAdapter adapter)
        {
            if (Runtime != null) Runtime.RegisterAdapter(adapter);
        }

        public static void RegisterSerializer(ISyncValueSerializer serializer)
        {
            if (Runtime != null) Runtime.RegisterSerializer(serializer);
        }

        public static IHncChannel Channel(string name)
        {
            return Runtime != null ? Runtime.Channel(name) : NullChannel.Instance;
        }

        private sealed class NullChannel : IHncChannel
        {
            public static readonly NullChannel Instance = new NullChannel();
            public void Send(byte[] payload, SyncScope scope) { }
            public event Action<string, byte[]> Received { add { } remove { } }
        }
    }
}
