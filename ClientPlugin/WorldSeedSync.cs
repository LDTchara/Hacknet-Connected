using System;
using System.Reflection;
using HarmonyLib;
using Hacknet;

namespace ClientPlugin;

/// <summary>
/// 世界种子同步管理器（客户端版）
/// 
/// 职责：
/// 1. 保存从服务端接收的主种子
/// 2. Harmony Prefix 拦截 new Random() 无参构造
/// 3. PostLoad() 中反射枚举所有 static Random 字段并 ReSeed
/// 4. 服务端种子到达后一次性注入
/// </summary>
internal static class WorldSeedSync
{
    // ── 状态 ──

    /// <summary>主种子是否已注入</summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>从服务端接收的主种子</summary>
    public static int MasterSeed { get; private set; }

    /// <summary>我们期望 Utils.random 指向的实例</summary>
    public static Random ExpectedRandom => _injectedRandom;
    private static Random? _injectedRandom;

    /// <summary>子种子计数器（用于 Prefix 中分配唯一子种子）</summary>
    private static int _childSeedCounter;
    public static int ChildSeedCounter { get => _childSeedCounter; set => _childSeedCounter = value; }

    /// <summary>是否需要延迟处理 static Random 字段（在种子到达后执行）</summary>
    private static bool _pendingFixStaticFields;

    // ── 初始化 ──

    /// <summary>
    /// 从服务端接收种子后调用。
    /// 1. 用 ReSeed 重置 Utils.random
    /// 2. 如果之前有延迟的 static 字段修复请求，现在执行
    /// </summary>
    public static void Initialize(int masterSeed)
    {
        MasterSeed = masterSeed;
        _childSeedCounter = 0;

        Utils.random.ReSeed(masterSeed);
        _injectedRandom = Utils.random;

        IsInitialized = true;
        Console.WriteLine($"[WorldSeedSync] Initialized with seed={masterSeed}");

        // 如果有延迟的 static 字段修复，现在执行
        if (_pendingFixStaticFields)
        {
            _pendingFixStaticFields = false;
            FixAllStaticRandomFields();
        }
    }

    // ── 子种子分配 ──

    public static int GetNextChildSeed()
    {
        int idx = _childSeedCounter++;
        return RandomSeedHelper.DeriveChildSeed(MasterSeed, $"ctor:{idx}");
    }

    // ── 反射修复 static Random 字段 ──

    /// <summary>
    /// 在 PostLoad() 或 Initialize() 中调用。
    /// 枚举所有已加载的非系统程序集，找到 static Random 字段，
    /// 用确定性子种子重置它们。
    /// 
    /// 注意：如果是 PostLoad 阶段调用（种子还没到），
    /// 就标记延迟，等 Initialize 时再执行。
    /// </summary>
    public static void FixAllStaticRandomFields()
    {
        if (!IsInitialized)
        {
            Console.WriteLine("[WorldSeedSync] Not initialized yet, deferring static field fix until seed arrives.");
            _pendingFixStaticFields = true;
            return;
        }

        int fixedCount = 0;
        int skippedSystem = 0;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;

            string asmName = asm.GetName().Name ?? "";
            if (asmName.StartsWith("System") ||
                asmName.StartsWith("Microsoft") ||
                asmName.StartsWith("mscorlib") ||
                asmName.StartsWith("NETStandard") ||
                asmName.StartsWith("FNA") ||
                asmName.StartsWith("Mono") ||
                asmName.StartsWith("BepInEx") ||
                asmName.StartsWith("Harmony") ||
                asmName == "HacknetPathfinder" ||
                asmName == "PathfinderAPI")
            {
                skippedSystem++;
                continue;
            }

            try
            {
                foreach (var type in asm.GetTypes())
                {
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        if (field.FieldType != typeof(Random)) continue;

                        var rng = (Random?)field.GetValue(null);
                        if (rng == null) continue;

                        if (rng == Utils.random) continue;

                        string identifier = $"{asmName}.{type.FullName}.{field.Name}";
                        int childSeed = RandomSeedHelper.DeriveChildSeed(MasterSeed, identifier);
                        rng.ReSeed(childSeed);
                        fixedCount++;
                        Console.WriteLine($"[WorldSeedSync] Fixed {identifier} -> seed={childSeed}");
                    }
                }
            }
            catch (ReflectionTypeLoadException) { continue; }
        }

        Console.WriteLine($"[WorldSeedSync] Static Random fields fixed: {fixedCount} (skipped {skippedSystem} system assemblies)");
    }
}

// ════════════════════════════════════════════════════════════════
// Harmony Patches
// ════════════════════════════════════════════════════════════════

/// <summary>
/// 拦截 new Random() 无参构造函数。
///
/// 注意：不能使用 __instance = new Random(seed) 替换引用，
/// 因为 newobj IL 指令始终返回原始分配的对象，替换会被丢弃，
/// 导致调用方持有未初始化的 Random（SeedArray == null）。
/// 改用 ReSeed 直接初始化 __instance 的内部字段。
/// </summary>
[HarmonyPatch(typeof(Random), MethodType.Constructor, new Type[0])]
internal static class RandomCtorGuard
{
    [HarmonyPriority(Priority.High)]
    internal static bool Prefix(Random __instance)
    {
        if (!WorldSeedSync.IsInitialized)
            return true;

        int childSeed = WorldSeedSync.GetNextChildSeed();
        if (!__instance.ReSeed(childSeed))
        {
            Console.WriteLine($"[RandomCtorGuard] ReSeed({childSeed}) FAILED, letting original ctor run.");
            return true; // 失败放行原构造函数，避免留下 SeedArray == null 的坏对象
        }
        Console.WriteLine($"[RandomCtorGuard] Intercepted new Random() -> ReSeed({childSeed})");
        return false;
    }
}
