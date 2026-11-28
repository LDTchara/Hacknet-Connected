using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Hacknet;
using HarmonyLib;
using HNC.Pause;

namespace HNC.Flags;

/// <summary>
/// flag 同步（客户端和服务端主机共用；主机经管道，客户端经 LiteNetLib）。
///
/// 按 flag 是"在哪里"被增删的来判断，不维护系统 flag 名单：
/// - 剧情执行中（任何动作的 Trigger、任务函数 MissionFunctions.runCommand、任务起始 / 结束函数）→ 全局，发给终端；
/// - 其中带 Scope 的（动作带 players / group，或任务的根 / 起始 / 结束带私有 scope）→ 只给本人，不发；
/// - 不在剧情执行中（Hacknet 引擎自己加的：BootFailure、TutorialComplete、startVer:…、提示图标、成就……）→ 只在本机。
/// 收到别人的全局 flag 时在本机增删（不会再报回去）。
///
/// 宿主插件设置钩子并在 PostLoad 调用 PatchStoryContexts（那时其它 mod 的动作类型都已加载）。都在游戏主线程。
/// </summary>
internal static class FlagSync
{
    // ═══════════════════════════════════════════════ 宿主钩子

    public static Func<bool> IsOnline = () => false;
    public static Action<string> Send = _ => { };

    /// <summary>这个动作对象是否带私有 scope（players / group）</summary>
    public static Func<object, bool> IsPrivateScope = _ => false;

    /// <summary>这个任务的起始（true）/ 结束（false）函数是否带私有 scope（含任务根元素）</summary>
    public static Func<ActiveMission, bool, bool> IsPrivateMissionFunction = (_, _) => false;

    // ═══════════════════════════════════════════════ 剧情上下文

    /// <summary>
    /// 剧情执行栈：每层记"是否私有"。
    /// - 动作、任务函数：继承外层（私有动作里执行的东西也是私有的）；
    /// - 任务的起始 / 结束函数、读任务文件时直接运行的起始函数：只看这个任务自己的 scope 标签，不继承外层。
    /// </summary>
    private static readonly List<bool> _stack = new();

    /// <summary>正在应用别人发来的 flag（不再报回去）</summary>
    private static bool _applyingRemote;

    private static readonly List<(bool Add, string Flag)> _pending = new();

    private static void Push(bool isPrivate) => _stack.Add(isPrivate || (_stack.Count > 0 && _stack[_stack.Count - 1]));

    /// <summary>不继承外层：只由自己的 scope 决定</summary>
    private static void PushOwn(bool isPrivate) => _stack.Add(isPrivate);

    private static void Pop()
    {
        if (_stack.Count > 0) _stack.RemoveAt(_stack.Count - 1);
    }

    /// <summary>flag 实际发生了变化（ProgressionFlags 补丁调用）</summary>
    private static void OnChanged(ProgressionFlags flags, string flag, bool add)
    {
        if (_applyingRemote || _stack.Count == 0 || _stack[_stack.Count - 1]) return;
        if (flags != OS.currentInstance?.Flags || !IsOnline()) return;
        Send(FlagProtocol.EncodeSet(add, flag));
    }

    // ═══════════════════════════════════════════════ 收到别人的 flag

    public static bool TryHandle(string msg)
    {
        if (!msg.StartsWith(FlagProtocol.Prefix, StringComparison.Ordinal)) return false;

        if (FlagProtocol.TryDecodeSet(msg, out bool add, out string flag))
            _pending.Add((add, flag));
        else if (msg.StartsWith(FlagProtocol.All, StringComparison.Ordinal))
            foreach (var f in FlagProtocol.DecodeAll(msg)) _pending.Add((true, f));

        Update();
        return true;
    }

    /// <summary>每帧：游戏加载后应用收到的 flag（进游戏前收到的先存着）</summary>
    public static void Update()
    {
        if (_pending.Count == 0 || !HncPause.GameRunning()) return;
        var flags = OS.currentInstance?.Flags;
        if (flags == null) return;

        _applyingRemote = true;
        try
        {
            foreach (var (add, flag) in _pending)
            {
                if (add) flags.AddFlag(flag);
                else flags.RemoveFlag(flag);
            }
        }
        finally
        {
            _applyingRemote = false;
            _pending.Clear();
        }
    }

    /// <summary>进入大厅 / 退出扩展页时清空（终端会在加入时发 FLAG_ALL）</summary>
    public static void Reset()
    {
        _pending.Clear();
        _stack.Clear();
    }

    // ═══════════════════════════════════════════════ 补丁

    private static bool _patched;

    /// <summary>
    /// 给"剧情执行"的入口打补丁（PostLoad 调用，其它 mod 的动作类型此时都已加载）：
    /// 所有 SerializableAction 子类的 Trigger、MissionFunctions.runCommand、任务起始 / 结束函数。
    /// 前置补丁压栈，Finalizer 出栈（原方法被 Scope 门控跳过、或抛异常时也一定出栈）。
    /// </summary>
    public static void PatchStoryContexts(Harmony harmony)
    {
        if (_patched) return;
        _patched = true;

        var pop = new HarmonyMethod(typeof(FlagSync), nameof(FinalizerPop));
        int count = 0;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            catch { continue; }

            foreach (var t in types)
            {
                if (t == null || t.IsAbstract || !typeof(SerializableAction).IsAssignableFrom(t)) continue;
                var m = AccessTools.DeclaredMethod(t, "Trigger", new[] { typeof(object) });
                if (m == null || m.IsAbstract) continue;
                try
                {
                    harmony.Patch(m, prefix: new HarmonyMethod(typeof(FlagSync), nameof(ActionPrefix)), finalizer: pop);
                    count++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FlagSync] 跳过 {t.Name}.Trigger: {ex.Message}");
                }
            }
        }

        harmony.Patch(AccessTools.Method(typeof(MissionFunctions), nameof(MissionFunctions.runCommand)),
            prefix: new HarmonyMethod(typeof(FlagSync), nameof(InheritPrefix)), finalizer: pop);
        harmony.Patch(AccessTools.Method(typeof(ActiveMission), nameof(ActiveMission.ActivateSuppressedStartFunctionIfPresent)),
            prefix: new HarmonyMethod(typeof(FlagSync), nameof(MissionStartPrefix)), finalizer: pop);
        harmony.Patch(AccessTools.Method(typeof(ActiveMission), "finish"),
            prefix: new HarmonyMethod(typeof(FlagSync), nameof(MissionEndPrefix)), finalizer: pop);

        // 读任务文件（Pathfinder 统一入口）：missionStart 写了 suppress="false" 时，起始函数在解析时就直接运行，
        // 那时任务对象还没挂上 scope，所以直接读这个任务文件自己的 scope 标签
        var load = AccessTools.Method(typeof(Pathfinder.Replacements.MissionLoader), nameof(Pathfinder.Replacements.MissionLoader.LoadContentMission));
        if (load != null)
            harmony.Patch(load, prefix: new HarmonyMethod(typeof(FlagSync), nameof(MissionLoadPrefix)), finalizer: pop);

        Console.WriteLine($"[FlagSync] 已标记剧情入口：{count} 个动作 Trigger + 任务函数");
    }

    private static void ActionPrefix(object __instance) => Push(SafePrivate(() => IsPrivateScope(__instance)));
    private static void InheritPrefix() => Push(false);
    private static void MissionStartPrefix(ActiveMission __instance) => PushOwn(SafePrivate(() => IsPrivateMissionFunction(__instance, true)));
    private static void MissionEndPrefix(ActiveMission __instance) => PushOwn(SafePrivate(() => IsPrivateMissionFunction(__instance, false)));
    private static void MissionLoadPrefix(string filename) => PushOwn(SafePrivate(() => StartIsPrivateInFile(filename)));

    /// <summary>
    /// 任务文件里起始函数是否私有：&lt;missionStart&gt; 或根元素带 players 且不是 PUBLIC
    /// （和 Scope 预扫描的规则一致：只写 group 不写 players 视为公开）。
    /// </summary>
    private static bool StartIsPrivateInFile(string filename)
    {
        string path = LocalizedFileLoader.GetLocalizedFilepath(filename);
        if (!File.Exists(path)) return false;
        var root = XDocument.Load(path).Root;
        if (root == null) return false;
        var start = root.Elements().FirstOrDefault(e => e.Name.LocalName == "missionStart");
        return IsPrivatePlayers((string?)start?.Attribute("players")) || IsPrivatePlayers((string?)root.Attribute("players"));
    }

    private static bool IsPrivatePlayers(string? players) =>
        !string.IsNullOrEmpty(players) && !string.Equals(players, "PUBLIC", StringComparison.OrdinalIgnoreCase);

    private static Exception? FinalizerPop(Exception? __exception)
    {
        Pop();
        return __exception;
    }

    private static bool SafePrivate(Func<bool> f)
    {
        try { return f(); }
        catch { return false; }
    }

    // ProgressionFlags 是 public 类，直接用特性打补丁：前置记下"之前有没有"，后置判断是否真的变了
    [HarmonyPatch(typeof(ProgressionFlags), nameof(ProgressionFlags.AddFlag))]
    private static class AddFlagPatch
    {
        private static void Prefix(ProgressionFlags __instance, string flag, out bool __state) => __state = !__instance.HasFlag(flag);

        private static void Postfix(ProgressionFlags __instance, string flag, bool __state)
        {
            if (__state && __instance.HasFlag(flag)) OnChanged(__instance, flag, true);
        }
    }

    [HarmonyPatch(typeof(ProgressionFlags), nameof(ProgressionFlags.RemoveFlag))]
    private static class RemoveFlagPatch
    {
        private static void Prefix(ProgressionFlags __instance, string flag, out bool __state) => __state = __instance.HasFlag(flag);

        private static void Postfix(ProgressionFlags __instance, string flag, bool __state)
        {
            if (__state && !__instance.HasFlag(flag)) OnChanged(__instance, flag, false);
        }
    }
}
