using Hacknet;
using HarmonyLib;

namespace ServerClientEventsMod.Events;

/// <summary>每帧检测委托，在 OS.Update Postfix 中执行</summary>
public delegate void FrameDetector(OS os);

/// <summary>
/// 全局检测器管理器。唯一含 [HarmonyPatch] 的入口，挂载 OS.Update Postfix。
/// DLL 加载时由 PatchAll 自动触发静态构造完成全部自举初始化。
/// </summary>
[HarmonyPatch]
internal static class EventDetectorManager
{
    private static readonly List<FrameDetector> _detectors = new();
    private static bool _initialized;

    static EventDetectorManager()
    {
        AutoRegister();
        GlobalEventBus.Activate();
    }

    // ---- 公开 API ----

    /// <summary>手动注册额外检测器（仅当无法贴 [FrameDetect] 属性时使用）</summary>
    public static void Register(FrameDetector detector)
    {
        _detectors.Add(detector);
    }

    // ---- 内部 ----

    /// <summary>反射扫描程序集中所有 [FrameDetect] 标记的静态方法并注册。</summary>
    static void AutoRegister()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (var t in typeof(EventDetectorManager).Assembly.GetTypes())
        {
            foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Static |
                                           System.Reflection.BindingFlags.NonPublic |
                                           System.Reflection.BindingFlags.Public))
            {
                if (m.GetCustomAttributes(typeof(FrameDetectAttribute), false).Length > 0 &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(OS))
                {
                    var d = (FrameDetector)Delegate.CreateDelegate(typeof(FrameDetector), m);
                    _detectors.Add(d);
                }
            }
        }
    }

    static void RunAll(OS os)
    {
        for (int i = 0; i < _detectors.Count; i++)
            _detectors[i](os);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), nameof(OS.Update))]
    static void OnGlobalDetect(OS __instance) => RunAll(__instance);
}
