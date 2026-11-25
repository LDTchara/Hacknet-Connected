using System;
using HarmonyLib;
using Hacknet;

namespace ServerPlugin;

/// <summary>
/// 拦截 Utils.random 和所有 new Random() 无参构造
/// 统一使用固定种子 114514，确保服务端和客户端完全同步
/// </summary>

/// <summary>
/// 拦截 Utils 静态构造函数，在 Utils.random 创建后立即用固定种子替换
/// </summary>
[HarmonyPatch(typeof(Utils), MethodType.StaticConstructor)]
internal static class UtilsRandomPatch
{
    private const int FixedSeed = 114514;
    private static bool _hasBeenPatched = false;

    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    internal static void Postfix()
    {
        if (_hasBeenPatched) return;
        _hasBeenPatched = true;

        try
        {
            Utils.random.ReSeed(FixedSeed);
            Console.WriteLine($"[UtilsRandomPatch] Utils.random set to fixed seed: {FixedSeed}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UtilsRandomPatch] ERROR: {ex.Message}");
        }
    }
}

/// <summary>
/// 拦截所有 new Random() 无参构造，强制使用固定种子。
///
/// 注意：不能使用 __instance = new Random(seed) 替换引用，
/// 因为 newobj IL 指令始终返回原始分配的对象，替换会被丢弃。
/// 改用 ReSeed 直接初始化 __instance 的内部字段。
/// </summary>
[HarmonyPatch(typeof(Random), MethodType.Constructor, new Type[0])]
internal static class RandomCtorPatch
{
    private const int FixedSeed = 114514;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    internal static bool Prefix(Random __instance)
    {
        if (!__instance.ReSeed(FixedSeed))
        {
            Console.WriteLine("[RandomCtorPatch] ReSeed FAILED, letting original ctor run.");
            return true; // 失败放行原构造函数，避免留下 SeedArray == null 的坏对象
        }
        return false; // 成功才阻止原构造函数执行
    }
}
