using BepInEx;
using BepInEx.Hacknet;
using HarmonyLib;
using Hacknet;
using System;
using System.IO;
using System.Reflection;

namespace SeedSync.Global;

/// <summary>
/// 全局种子同步插件 - 桌面 .seed 文件版
/// 
/// 在桌面（Desktop）上放置 .seed 文件，文件名就是固定种子值。
/// 例如桌面上有个 42.seed 文件 → FixedSeed = 42
///
/// 安装路径：Hacknet/BepInEx/plugins/SeedSyncGlobal.dll
/// </summary>
[BepInPlugin(ModGUID, ModName, ModVer)]
public class SeedSyncGlobalPlugin : HacknetPlugin
{
    public const string ModGUID = "com.SeedSync.Global";
    public const string ModName = "Seed Sync Global (Fixed Seed)";
    public const string ModVer = "2.1.0";

    /// <summary>从桌面 .seed 文件读取的固定种子，未找到时使用默认值 114514</summary>
    public static int FixedSeed = 114514;

    /// <summary>
    /// NetworkMap.generateRandomIP() 的全局调用计数器。
    /// 两端用同一种子 + 同一计数器 → 同一 IP。
    /// 服务端发送 WORLD_SEED 时带上当前计数值，
    /// 客户端接收后用此值跳过，确保后续调用对齐。
    /// </summary>
    public static int GlobalIpCounter = 0;

    private static Harmony? _harmony;

    public override bool Load()
    {
        // ════════════════════════════════════════════════════════
        // 步骤 1：检索桌面，读取第一个 .seed 文件
        // ════════════════════════════════════════════════════════
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string[] seedFiles = Directory.GetFiles(desktopPath, "*.seed");

            if (seedFiles.Length == 0)
            {
                Console.WriteLine("[SeedSyncGlobal] ⚠ ERROR: No .seed file found on Desktop!");
                Console.WriteLine("[SeedSyncGlobal] ⚠ Please create a file named <number>.seed on your Desktop.");
                Console.WriteLine($"[SeedSyncGlobal] ⚠ Using default seed: {FixedSeed}");
            }
            else
            {
                string firstFile = seedFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(firstFile);

                if (int.TryParse(fileName, out int parsedSeed))
                {
                    FixedSeed = parsedSeed;
                    Console.WriteLine($"[SeedSyncGlobal] ✓ Loaded seed from: {Path.GetFileName(firstFile)} -> {FixedSeed}");
                }
                else
                {
                    Console.WriteLine($"[SeedSyncGlobal] ✗ ERROR: Cannot parse seed from filename '{fileName}'!");
                    Console.WriteLine($"[SeedSyncGlobal] ✗ File: {firstFile}");
                    Console.WriteLine("[SeedSyncGlobal] ✗ Filename must be a valid integer (e.g. 42.seed)");
                    Console.WriteLine($"[SeedSyncGlobal] ✗ Using default seed: {FixedSeed}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeedSyncGlobal] ✗ ERROR reading .seed file: {ex.Message}");
            Console.WriteLine($"[SeedSyncGlobal] ✗ Using default seed: {FixedSeed}");
        }

        // ════════════════════════════════════════════════════════
        // 步骤 2：应用 Harmony 补丁
        // 此时 FixedSeed 已确定，补丁会引用它
        // ════════════════════════════════════════════════════════
        _harmony = new Harmony(ModGUID);
        _harmony.PatchAll();

        // ════════════════════════════════════════════════════════
        // 步骤 3：确保 Utils.random 已注入
        // ════════════════════════════════════════════════════════
        try
        {
            ForceReSeed(FixedSeed);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeedSyncGlobal] ✗ ERROR seeding Utils.random: {ex.Message}");
        }

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"[SeedSyncGlobal] Loaded - Fixed Seed = {FixedSeed}");
        Console.WriteLine("═══════════════════════════════════════════");

        return true;
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        return true;
    }

    /// <summary>内联 ReSeed，不依赖外部 RandomSeedHelper</summary>
    /// <returns>成功返回 true；反射字段不可用或写入失败返回 false。</returns>
    internal static bool ForceReSeed(int seed)
    {
        return ForceReSeed(Utils.random, seed);
    }

    /// <summary>在指定的 Random 实例上执行 ReSeed，通过反射设置内部字段</summary>
    /// <returns>成功返回 true；实例为 null、反射字段不可用或写入失败返回 false。</returns>
    internal static bool ForceReSeed(Random rng, int seed)
    {
        if (rng == null)
            return false;

        var seedArrayField = typeof(Random).GetField("SeedArray",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var inextField = typeof(Random).GetField("inext",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var inextpField = typeof(Random).GetField("inextp",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (seedArrayField == null || inextField == null || inextpField == null)
        {
            Console.WriteLine("[SeedSyncGlobal] Cannot ReSeed: reflection fields not available");
            return false;
        }

        try
        {
            const int MSEED = 161803398;
            const int MBIG = int.MaxValue;
            int num = MSEED - (seed == int.MinValue ? int.MaxValue : Math.Abs(seed));
            var seedArray = new int[56];
            seedArray[55] = num;
            int num2 = 1;
            for (int i = 1; i < 55; i++)
            {
                int idx = 21 * i % 55;
                seedArray[idx] = num2;
                num2 = num - num2;
                if (num2 < 0) num2 += MBIG;
                num = seedArray[idx];
            }
            for (int j = 1; j < 5; j++)
                for (int i = 1; i < 56; i++)
                {
                    seedArray[i] -= seedArray[1 + (i + 30) % 55];
                    if (seedArray[i] < 0) seedArray[i] += MBIG;
                }

            seedArrayField.SetValue(rng, seedArray);
            inextField.SetValue(rng, 0);
            inextpField.SetValue(rng, 21);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeedSyncGlobal] ForceReSeed threw {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// 拦截 Utils 静态构造函数。
/// </summary>
[HarmonyPatch(typeof(Utils), MethodType.StaticConstructor)]
internal static class UtilsRandomGlobalPatch
{
    private static bool _hasBeenPatched = false;

    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    internal static void Postfix()
    {
        if (_hasBeenPatched) return;
        _hasBeenPatched = true;

        try
        {
            SeedSyncGlobalPlugin.ForceReSeed(SeedSyncGlobalPlugin.FixedSeed);
            Console.WriteLine($"[SeedSyncGlobal] Utils.random <- {SeedSyncGlobalPlugin.FixedSeed} (via static ctor patch)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeedSyncGlobal] ERROR in UtilsRandomGlobalPatch: {ex.Message}");
        }
    }
}

/// <summary>
/// 拦截所有 new Random() 无参构造，强制使用固定种子。
///
/// 注意：不能使用 __instance = new Random(seed) 替换引用，
/// 因为 newobj IL 指令始终返回原始分配的对象，替换会被丢弃，
/// 导致调用方持有未初始化的 Random（SeedArray == null），
/// 后续 .Next() 会触发 NullReferenceException。
/// 改用反射直接初始化 __instance 的内部字段。
/// </summary>
[HarmonyPatch(typeof(Random), MethodType.Constructor, new Type[0])]
internal static class RandomCtorGlobalPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    internal static bool Prefix(Random __instance)
    {
        if (!SeedSyncGlobalPlugin.ForceReSeed(__instance, SeedSyncGlobalPlugin.FixedSeed))
        {
            Console.WriteLine("[SeedSyncGlobal] ReSeed FAILED, letting original ctor run.");
            return true; // 失败放行原构造函数，避免留下 SeedArray == null 的坏对象
        }
        return false;
    }
}

/// <summary>
/// 拦截 Computer.generateBinaryString(int length)，
/// 用确定性子种子代替 Utils.random。
///
/// 种子 = FixedSeed + length 的 FNV-1a 哈希。
/// 不依赖调用次数，同一个长度的 #BINARY# 在所有位置产生相同内容。
/// </summary>
[HarmonyPatch(typeof(Hacknet.Computer), "generateBinaryString", new Type[] { typeof(int) })]
internal static class BinaryStringPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(ref string __result, int length)
    {
        uint hash = 2166136261;
        unchecked
        {
            foreach (byte b in BitConverter.GetBytes(SeedSyncGlobalPlugin.FixedSeed))
            { hash ^= b; hash *= 16777619; }
            foreach (byte b in BitConverter.GetBytes(length))
            { hash ^= b; hash *= 16777619; }
        }
        int callSeed = (int)(hash & 0x7FFFFFFF);

        var rng = new Random(callSeed);
        byte[] array = new byte[length / 8];
        rng.NextBytes(array);
        string text = "";
        for (int i = 0; i < array.Length; i++)
            text += Convert.ToString(array[i], 2);
        __result = text;
        return false;
    }
}

/// <summary>
/// 拦截 NetworkMap.generateRandomIP()，
/// 用确定性种子 + 全局计数器代替 Utils.random。
///
/// 计数器 GlobalIpCounter 在两端由同一个 WORLD_SEED 消息同步，
/// 客户端初始化时设置 SeedSyncGlobalPlugin.GlobalIpCounter = 服务端的值。
/// 后续每次调用双方从相同的计数器位置开始，得到相同的随机 IP。
/// </summary>
[HarmonyPatch(typeof(NetworkMap), "generateRandomIP")]
internal static class RandomIPPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(ref string __result)
    {
        int ipSeed = SeedSyncGlobalPlugin.FixedSeed
            ^ (SeedSyncGlobalPlugin.GlobalIpCounter * 397)
            ^ (SeedSyncGlobalPlugin.GlobalIpCounter >> 16);
        ipSeed = ipSeed & 0x7FFFFFFF;
        SeedSyncGlobalPlugin.GlobalIpCounter++;

        var rng = new Random(ipSeed);
        __result = (rng.Next(254) + 1) + "."
                 + (rng.Next(254) + 1) + "."
                 + (rng.Next(254) + 1) + "."
                 + (rng.Next(254) + 1);
        return false;
    }
}
