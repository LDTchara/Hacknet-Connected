using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ServerClientEventsMod.Events;

/// <summary>
/// 每个程序集只 PatchAll 一次。
///
/// 正常部署时 ServerPlugin / ClientPlugin 和 ServerClientEventsMod 是各自独立的 dll，
/// 各自 PatchAll 自己的程序集，互不影响。
/// 但"单 dll 合并构建"（-p:SingleDll=true）把它们编译进同一个程序集，
/// 两个插件的 Load() 都会 PatchAll 同一个程序集——不拦一下，每个补丁都会被打两遍。
/// </summary>
public static class HarmonyPatchOnce
{
    private static readonly HashSet<Assembly> Done = new();

    /// <returns>true = 这次真的打了补丁；false = 该程序集已经打过，跳过。</returns>
    public static bool PatchAll(Harmony harmony, Assembly assembly)
    {
        lock (Done)
        {
            if (!Done.Add(assembly)) return false;
        }
        harmony.PatchAll(assembly);
        return true;
    }
}
