using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

/// <summary>
/// 玩家事件 API —— 唯一公共入口。外部代码仅通过此类订阅事件。
/// 由 GlobalEventBus 自动绑定转发，无需任何手动初始化。
/// </summary>
public static class PlayerEventAPI
{
    // ---- 连接 ----
    public static event Action<Computer, OS> OnConnected;
    public static event Action<string, OS> OnDisconnected;

    // ---- 登录 ----
    public static event Action<Computer, OS, string, bool> OnLoggedIn;

    // ---- 端口 ----
    public static event Action<Computer, OS, int, int, bool> OnPortChanged;

    // ---- 代理 ----
    public static event Action<Computer, OS, ProxyState, float, float> OnProxyChanged;

    // ---- 防火墙 ----
    public static event Action<Computer, OS, FirewallState, Firewall> OnFirewallChanged;

    // ---- 拦截器 ----
    public static void InterceptConnect(ConnectInterceptor i) => ConnectInterceptorManager.Add(i);
    public static void InterceptDisconnect(DisconnectInterceptor i) => DisconnectInterceptorManager.Add(i);
    public static void InterceptLogin(LoginInterceptor i) => LoginInterceptorManager.Add(i);
    public static void InterceptCommand(CommandInterceptor i) => CommandInterceptorManager.Add(i);

    // ---- 节点可见性 ----
    public static event Action<Computer, OS, bool> OnNodeVisibilityChanged;

    // ---- 节点 IP 变更 ----
    public static event Action<Computer, OS, string, string> OnNodeIPChanged;

    // ---- 文件系统 ----
    public static event Action<Computer, OS, FileSystemAction, string, string> OnFileSystemChanged;

    // ---- 游戏保存 ----
    public static event Action<OS> OnSaveGame;

    // ---- 条件动作触发 ----
    public static event Action<SerializableAction, OS, string, string> OnActionTriggered;

    /// <summary>自定义扩展事件订阅入口</summary>
    public static void On<T>(Action<T> handler) where T : PathfinderEvent
    {
        EventManager<T>.AddHandler(handler);
    }

    // ---- 仅 GlobalEventBus 调用 ----

    internal static void RaiseConnected(Computer c, OS os) => OnConnected?.Invoke(c, os);
    internal static void RaiseDisconnected(string ip, OS os) => OnDisconnected?.Invoke(ip, os);
    internal static void RaiseLoggedIn(Computer c, OS os, string u, bool a) => OnLoggedIn?.Invoke(c, os, u, a);
    internal static void RaisePortChanged(Computer c, OS os, int d, int r, bool o) => OnPortChanged?.Invoke(c, os, d, r, o);
    internal static void RaiseProxyChanged(Computer c, OS os, ProxyState s, float t, float st) => OnProxyChanged?.Invoke(c, os, s, t, st);
    internal static void RaiseFirewallChanged(Computer c, OS os, FirewallState s, Firewall f) => OnFirewallChanged?.Invoke(c, os, s, f);
    internal static void RaiseNodeVisibilityChanged(Computer c, OS os, bool v) => OnNodeVisibilityChanged?.Invoke(c, os, v);
    internal static void RaiseNodeIPChanged(Computer c, OS os, string oldIP, string newIP) => OnNodeIPChanged?.Invoke(c, os, oldIP, newIP);
    internal static void RaiseFileSystemChanged(Computer c, OS os, FileSystemAction a, string p, string ip) => OnFileSystemChanged?.Invoke(c, os, a, p, ip);
    internal static void RaiseSaveGame(OS os) => OnSaveGame?.Invoke(os);
    internal static void RaiseActionTriggered(SerializableAction a, OS os, string t, string x) => OnActionTriggered?.Invoke(a, os, t, x);
}
