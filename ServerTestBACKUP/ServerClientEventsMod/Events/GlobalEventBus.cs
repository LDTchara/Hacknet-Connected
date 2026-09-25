using Hacknet;
using Pathfinder.Event;
using Pathfinder.Event.Loading;

namespace ServerClientEventsMod.Events;

internal static class GlobalEventBus
{
    private static bool _activated;

    internal static void Activate()
    {
        if (_activated) return;
        _activated = true;

        var assembly = typeof(GlobalEventBus).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !typeof(PathfinderEvent).IsAssignableFrom(type))
                continue;
            if (type.GetCustomAttributes(typeof(EventAutoSubscribeAttribute), false).Length == 0)
                continue;

            ForwardEvent(type);
        }

        BindCoreEvents();
    }

    private static void ForwardEvent(Type eventType) { /* 预留总线层扩展 */ }

    private static void BindCoreEvents()
    {
        EventManager<PlayerConnectToCompEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseConnected(e.Computer, e.Os));

        EventManager<PlayerDisconnectFromCompEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseDisconnected(e.IPFrom, e.Os));

        EventManager<PlayerLoginCompEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseLoggedIn(e.Computer, e.Os, e.Username, e.IsAdmin));

        EventManager<PlayerPortEvent>.AddHandler(e =>
            PlayerEventAPI.RaisePortChanged(e.Computer, e.Os, e.DisplayPort, e.CodePort, e.IsOpen));

        EventManager<PlayerProxyEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseProxyChanged(e.Computer, e.Os, e.State, e.OverloadTicksRemaining, e.StartingOverloadTicks));

        EventManager<PlayerFirewallEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseFirewallChanged(e.Computer, e.Os, e.State, e.Firewall));

        EventManager<PlayerNodeVisibilityEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseNodeVisibilityChanged(e.Computer, e.Os, e.Visible));

        EventManager<PlayerNodeIPChangedEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseNodeIPChanged(e.Computer, e.Os, e.OldIP, e.NewIP));

        EventManager<PlayerFileSystemEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseFileSystemChanged(e.Computer, e.Os, e.Action, e.Path, e.SourceIP));

        EventManager<PlayerActionTriggeredEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseActionTriggered(e.Action, e.Os, e.ActionType, e.XmlContent));

        EventManager<PlayerMissionEvent>.AddHandler(e =>
        {
            // ActiveMission 为 internal，不由快捷入口转发
        });

        EventManager<PlayerFunctionEvent>.AddHandler(e =>
        {
            // 快捷入口不需要，handler 通过 On<T> 订阅
        });

        EventManager<PlayerFactionEvent>.AddHandler(e =>
        {
            // 快捷入口不需要，handler 通过 On<T> 订阅
        });

        EventManager<PlayerFlagEvent>.AddHandler(e =>
        {
            // 快捷入口不需要，handler 通过 On<T> 订阅
        });

        EventManager<PlayerMailEvent>.AddHandler(e =>
        {
            // 快捷入口不需要，handler 通过 On<T> 订阅
        });

        EventManager<PlayerModuleEvent>.AddHandler(e =>
        {
            // 快捷入口不需要，handler 通过 On<T> 订阅
        });

        EventManager<PlayerCommandEvent>.AddHandler(e =>
        {
            // 快捷入口不需要，handler 通过 On<T> 订阅
        });

        // ── 游戏保存事件 ──
        EventManager<Pathfinder.Event.Saving.SaveEvent>.AddHandler(e =>
            PlayerEventAPI.RaiseSaveGame(e.Os));

        // ── Pathfinder 自定义文本替换 → 写入全局替换字典 ──
        EventManager<TextReplaceEvent>.AddHandler(e =>
        {
            GlobalReplaceDict.Set(e.Original, e.Replacement);
        });
    }
}
