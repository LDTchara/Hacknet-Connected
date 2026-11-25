using Hacknet;
using HarmonyLib;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

// ================================================================
// 委托定义
// ================================================================
public delegate InterceptResult ConnectInterceptor(Computer target, OS os);
public delegate InterceptResult DisconnectInterceptor(string ipFrom, OS os);
public delegate InterceptResult LoginInterceptor(Computer target, OS os, string username, bool isAdmin);

// ================================================================
// 管理器（每个入口一个）
// ================================================================
internal static class ConnectInterceptorManager
{
    private static readonly List<ConnectInterceptor> _list = new();
    public static void Add(ConnectInterceptor i) => _list.Add(i);

    internal static InterceptResult RunAll(Computer c, OS os)
    {
        foreach (var i in _list) { var r = i(c, os); if (r != InterceptResult.PassThrough) return r; }
        return InterceptResult.PassThrough;
    }
}

internal static class DisconnectInterceptorManager
{
    private static readonly List<DisconnectInterceptor> _list = new();
    public static void Add(DisconnectInterceptor i) => _list.Add(i);

    internal static InterceptResult RunAll(string ipFrom, OS os)
    {
        foreach (var i in _list) { var r = i(ipFrom, os); if (r != InterceptResult.PassThrough) return r; }
        return InterceptResult.PassThrough;
    }
}

internal static class LoginInterceptorManager
{
    private static readonly List<LoginInterceptor> _list = new();
    public static void Add(LoginInterceptor i) => _list.Add(i);

    internal static InterceptResult RunAll(Computer c, OS os, string user, bool admin)
    {
        foreach (var i in _list) { var r = i(c, os, user, admin); if (r != InterceptResult.PassThrough) return r; }
        return InterceptResult.PassThrough;
    }
}

// ================================================================
// Harmony Patch —— 每个入口一个
// ================================================================
internal static class ConnectInterceptPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Computer), nameof(Computer.connect))]
    static bool OnConnect(Computer __instance, string ipFrom, ref bool __result)
    {
        if (__instance.os == null || ipFrom != __instance.os.thisComputer.ip) return true;
        var r = ConnectInterceptorManager.RunAll(__instance, __instance.os);
        if (r == InterceptResult.Handled) { __result = true;  return false; }
        if (r == InterceptResult.Cancelled) { __result = false; return false; }
        return true;
    }
}

internal static class DisconnectInterceptPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Programs), nameof(Programs.disconnect))]
    static bool OnDisconnect(string[] args, OS os, ref bool __result)
    {
        if (os?.connectedComp == null) return true;
        var r = DisconnectInterceptorManager.RunAll(os.connectedComp.ip, os);
        if (r == InterceptResult.Handled) { __result = true;  return false; }
        if (r == InterceptResult.Cancelled) { __result = false; return false; }
        return true;
    }
}

internal static class LoginInterceptPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Computer), nameof(Computer.login))]
    static void OnLoginPostfix(Computer __instance, string username, ref int __result)
    {
        if (__instance.os == null) return;
        if (__result <= 0) return;

        var r = LoginInterceptorManager.RunAll(__instance, __instance.os, username, __result == 1);
        if (r == InterceptResult.Handled)
        {
            // 保持登录结果，标记为已处理
        }
        else if (r == InterceptResult.Cancelled)
        {
            __result = 0; // 阻止登录
        }
    }
}
