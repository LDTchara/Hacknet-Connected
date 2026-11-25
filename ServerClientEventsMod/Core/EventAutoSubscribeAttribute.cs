namespace ServerClientEventsMod.Events;

/// <summary>标记 PathfinderEvent 子类可被 GlobalEventBus 自动发现并纳入事件转发管道。</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class EventAutoSubscribeAttribute : Attribute { }
