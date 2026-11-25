using System;
using System.Reflection;

namespace ClientPlugin;

/// <summary>
/// 反射重置 System.Random 内部状态的扩展方法
/// 
/// .NET Framework 4.7.2 的 Random 内部字段（自 .NET 1.1 冻结）：
///   private int[] SeedArray (长度 56)
///   private int   inext      (初始值 0)
///   private int   inextp     (初始值 21)
///   
/// ReSeed() 将任意已有 Random 实例的内部状态重置为指定种子，
/// 使其后续输出与 new Random(seed) 完全一致。
/// </summary>
internal static class RandomSeedHelper
{
    private static readonly FieldInfo SeedArrayField =
        typeof(Random).GetField("SeedArray", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo InextField =
        typeof(Random).GetField("inext", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo InextpField =
        typeof(Random).GetField("inextp", BindingFlags.NonPublic | BindingFlags.Instance);

    private const int MSEED = 161803398;
    private const int MBIG = int.MaxValue;

    /// <summary>ReSeed 是否可用（反射字段都存在）</summary>
    public static bool IsAvailable =>
        SeedArrayField != null && InextField != null && InextpField != null;

    /// <summary>
    /// 将 Random 实例的内部状态重置为指定种子。
    /// 与 .NET Framework 4.x 的 new Random(seed) 算法完全一致。
    /// </summary>
    /// <returns>成功返回 true；反射字段不可用、实例为 null 或写入抛异常时返回 false。</returns>
    /// <remarks>
    /// 调用方（尤其 Harmony Prefix 拦截 new Random() 的代码）必须检查返回值：
    /// 失败时绝不能跳过原构造函数，否则会留下 SeedArray == null 的未初始化对象，
    /// 后续 NextDouble()/Next() 将抛出 NullReferenceException。
    /// </remarks>
    public static bool ReSeed(this Random rng, int seed)
    {
        if (rng == null)
            return false;

        if (!IsAvailable)
        {
            Console.WriteLine("[RandomSeedHelper] Reflection fields not available, ReSeed FAILED (caller must not skip ctor).");
            return false;
        }

        try
        {
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

            SeedArrayField!.SetValue(rng, seedArray);
            InextField!.SetValue(rng, 0);
            InextpField!.SetValue(rng, 21);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RandomSeedHelper] ReSeed threw {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    // ── 状态序列化 ──

    /// <summary>保存 Random 实例的完整内部状态为紧凑字符串</summary>
    public static string SaveState(this Random rng)
    {
        if (rng == null || !IsAvailable)
            return "";

        int inext = (int)InextField!.GetValue(rng);
        int inextp = (int)InextpField!.GetValue(rng);
        int[] seedArray = (int[])SeedArrayField!.GetValue(rng);
        if (seedArray == null || seedArray.Length != 56)
            return "";

        // 格式：inext,inextp,sa0,sa1,...,sa55
        var parts = new string[58];
        parts[0] = inext.ToString();
        parts[1] = inextp.ToString();
        for (int i = 0; i < 56; i++)
            parts[i + 2] = seedArray[i].ToString();

        return string.Join(",", parts);
    }

    /// <summary>从紧凑字符串恢复 Random 实例的完整内部状态</summary>
    public static void RestoreState(this Random rng, string state)
    {
        if (rng == null || string.IsNullOrEmpty(state) || !IsAvailable)
            return;

        var parts = state.Split(',');
        if (parts.Length < 58) return;

        int inext = int.Parse(parts[0]);
        int inextp = int.Parse(parts[1]);
        var seedArray = new int[56];
        for (int i = 0; i < 56; i++)
            seedArray[i] = int.Parse(parts[i + 2]);

        InextField!.SetValue(rng, inext);
        InextpField!.SetValue(rng, inextp);
        SeedArrayField!.SetValue(rng, seedArray);
    }

    /// <summary>
    /// 从种子 + 标识符派生一个确定性子种子。
    /// 保证相同的 (masterSeed, identifier) 在任何机器上产生相同的子种子。
    /// </summary>
    public static int DeriveChildSeed(int masterSeed, string identifier)
    {
        // FNV-1a 哈希保证确定性
        uint hash = 2166136261;
        unchecked
        {
            foreach (byte b in BitConverter.GetBytes(masterSeed))
            {
                hash ^= b;
                hash *= 16777619;
            }
            foreach (char c in identifier)
            {
                hash ^= c;
                hash *= 16777619;
            }
        }
        return (int)(hash & 0x7FFFFFFF);
    }
}
