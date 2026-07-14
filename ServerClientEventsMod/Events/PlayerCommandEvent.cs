using System.Reflection;
using Hacknet;
using HarmonyLib;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

public enum InterceptResult
{
    PassThrough,  // 放行给原版
    Handled,      // 已处理，阻止原版
    Cancelled     // 阻止，无输出
}

[EventAutoSubscribe]
public class PlayerCommandEvent : PathfinderEvent
{
    /// <summary>命令名（如 "ls"、"connect"）</summary>
    public string Command { get; }
    /// <summary>命令参数列表</summary>
    public string[] Args { get; }
    /// <summary>玩家 OS 实例</summary>
    public OS Os { get; }
    public string CurrentPlayerIP { get; }

    public PlayerCommandEvent(string command, string[] args, OS os)
    {
        Command = command;
        Args = args;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
    }
}

/// <summary>
/// 命令拦截器委托。return PassThrough 放行，Handled/Cancelled 阻止。
/// </summary>
public delegate InterceptResult CommandInterceptor(string command, string[] args, OS os);

internal static class CommandInterceptorManager
{
    private static readonly List<CommandInterceptor> _interceptors = new();

    public static void Add(CommandInterceptor interceptor)
    {
        _interceptors.Add(interceptor);
    }

    internal static InterceptResult RunAll(string cmd, string[] args, OS os)
    {
        foreach (var itcp in _interceptors)
        {
            var result = itcp(cmd, args, os);
            if (result != InterceptResult.PassThrough)
                return result;
        }
        return InterceptResult.PassThrough;
    }
}

internal static class CommandInterceptorPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(ProgramRunner), nameof(ProgramRunner.ExecuteProgram));
    }

    [HarmonyPrefix]
    static bool OnExecuteProgram(object os_object, string[] arguments, ref bool __result)
    {
        if (os_object is OS os && arguments.Length > 0)
        {
            EventManager<PlayerCommandEvent>.InvokeAll(
                new PlayerCommandEvent(arguments[0], arguments, os));

            var result = CommandInterceptorManager.RunAll(arguments[0], arguments, os);
            if (result == InterceptResult.Handled) { __result = true;  return false; }
            if (result == InterceptResult.Cancelled) { __result = false; return false; }
        }
        return true;
    }
}
