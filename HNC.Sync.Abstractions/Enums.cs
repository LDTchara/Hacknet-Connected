namespace HNC.Sync
{
    /// <summary>同步范围。对应 ReadMe/05-状态模型.md 的三层。</summary>
    public enum SyncScope
    {
        /// <summary>世界层：服务端权威，广播给所有玩家。</summary>
        World = 0,
        /// <summary>玩家层：每个玩家一份，只发给对应玩家。</summary>
        Player = 1,
        /// <summary>临时层：不同步，仅用于显式声明。</summary>
        Local = 2,
    }

    /// <summary>变化检测方式。</summary>
    public enum SyncMode
    {
        /// <summary>引擎按固定频率轮询比较，作者无需额外代码。</summary>
        Auto = 0,
        /// <summary>只在作者调用 <see cref="HncSync.MarkDirty"/> 时才检查。</summary>
        Manual = 1,
    }
}
