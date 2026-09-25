using System.Linq;
using System.Reflection;
using Hacknet;
using HarmonyLib;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerActionTriggeredEvent : PathfinderEvent
{
    /// <summary>触发的动作实例，可强制转换为具体 SA* 类型</summary>
    public SerializableAction Action { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    /// <summary>SA 类型名称（如 ChangeIP、AddIRCMessage）</summary>
    public string ActionType { get; }
    /// <summary>动作的完整 XML 表示（含所有字段）</summary>
    public string XmlContent { get; }

    public PlayerActionTriggeredEvent(SerializableAction action, OS os)
    {
        Action = action;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        ActionType = action.GetType().Name;
        XmlContent = action.GetSaveString();
    }
}

/// <summary>
/// 拦截所有 SerializableAction.Trigger() 调用。
/// 覆盖条件触发（RunnableConditionalActions）、延时触发（DelayableActionSystem）等全部路径。
/// </summary>
internal static class ActionTriggerInterceptor
{
    private static bool _patched;
    private static Harmony? _actionHarmony;

    public static void EnsurePatched()
    {
        if (_patched) return;
        _patched = true;

        _actionHarmony = new Harmony("com.ExtraEventsPlugin.Actions");

        // 扫描所有已加载程序集中 SerializableAction 子类的 Trigger 方法
        int count = 0;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var t in asm.GetTypes())
            {
                if (t.IsAbstract || t == typeof(SerializableAction))
                    continue;
                if (!typeof(SerializableAction).IsAssignableFrom(t))
                    continue;

                var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(m => m.Name == "Trigger" && !m.IsAbstract && m.DeclaringType == t);
                foreach (var method in methods)
                {
                    try
                    {
                        _actionHarmony.Patch(method, prefix: new HarmonyMethod(
                            typeof(ActionTriggerInterceptor).GetMethod(nameof(OnAnyTrigger),
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)));
                        count++;
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine($"[ActionTriggerInterceptor] Skip {t.Name}::Trigger — {ex.Message}");
                    }
                }
            }
        }

        Console.WriteLine($"[ActionTriggerInterceptor] Patched {count} SA* Triggers across all assemblies.");
    }

    private static void OnAnyTrigger(SerializableAction __instance)
    {
        // 不依赖原方法参数名，直接取 currentInstance（单 OS 实例，完全准确）
        var os = OS.currentInstance;
        if (os == null || __instance == null) return;
        EventManager<PlayerActionTriggeredEvent>.InvokeAll(
            new PlayerActionTriggeredEvent(__instance, os));
    }
}
