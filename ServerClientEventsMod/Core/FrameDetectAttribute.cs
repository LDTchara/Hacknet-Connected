namespace ServerClientEventsMod;

/// <summary>标记一个静态方法为帧检测器，由 EventDetectorManager.AutoRegister 自动发现并注册。</summary>
[AttributeUsage(AttributeTargets.Method)]
internal class FrameDetectAttribute : Attribute { }
