using System;

namespace HNC.Sync
{
    /// <summary>标记一个类型参与同步（显式模式：只同步带 [Sync] 的成员）。</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
    public sealed class SyncTypeAttribute : Attribute
    {
        /// <summary>成员未单独指定时使用的默认范围。</summary>
        public SyncScope Scope { get; set; } = SyncScope.World;

        /// <summary>
        /// 额外把带有 Pathfinder [XMLStorage] 特性的成员也纳入同步（零代码模式）。
        /// 通过特性类型名匹配，因此本程序集不依赖 Pathfinder。
        /// </summary>
        public bool UseXmlStorage { get; set; }
    }

    /// <summary>标记需要同步的字段或属性。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SyncAttribute : Attribute
    {
        /// <summary>未设置（null）时继承类型上 <see cref="SyncTypeAttribute.Scope"/>。</summary>
        public SyncScope? ScopeOverride { get; private set; }

        public SyncScope Scope
        {
            get { return ScopeOverride ?? SyncScope.World; }
            set { ScopeOverride = value; }
        }

        public SyncMode Mode { get; set; } = SyncMode.Auto;
    }

    /// <summary>显式排除成员（即使类型启用了 UseXmlStorage）。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class NoSyncAttribute : Attribute { }
}
