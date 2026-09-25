using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Hacknet;

namespace ServerPlugin;

/// <summary>
/// 世界种子同步管理器
/// 
/// 职责：
/// 1. 保存主种子（masterSeed）和已注入的 Utils.random 引用
/// 2. Harmony Prefix 拦截 new Random() 无参构造（覆盖所有未来创建）
/// 3. PostLoad() 中反射枚举所有 static Random 字段并 ReSeed（覆盖字段初始化器）
/// 4. Utils.random 守卫（防止外部 mod 偷换）
/// 5. 种子持久化到 Storage/Storages.ini（与 HacknetServerTerminal 共用配置）
/// 
/// INI 格式（每个 ServerProfile 一段）：
///   [ServerProfileStart]
///   [Options]
///   HostAccountName=...
///   HostAccountPassword=...
///   ServerName=...
///   WorldSeed=12345         ← 可选，持久化的种子
///   [ServerProfileEnd]
/// </summary>
internal static class WorldSeedSync
{
    private const string StorageDir = "Storage";
    private const string ConfigFile = "Storage/Storages.ini";

    // ── 持久化 ──

    /// <summary>
    /// 从 Storage/Storages.ini 中查找当前账户对应的 ServerProfile，
    /// 读取其中 WorldSeed 字段。没找到返回 -1。
    /// </summary>
    public static int LoadPersistedSeed(string accountUser)
    {
        try
        {
            if (!File.Exists(ConfigFile))
                return -1;

            var lines = File.ReadAllLines(ConfigFile);
            bool inProfile = false;
            bool inOptions = false;
            bool foundAccount = false;
            int? foundSeed = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line == "[ServerProfileStart]")
                {
                    inProfile = true;
                    inOptions = false;
                    foundAccount = false;
                    foundSeed = null;
                    continue;
                }

                if (line == "[ServerProfileEnd]")
                {
                    if (foundAccount && foundSeed.HasValue)
                        return foundSeed.Value;
                    inProfile = false;
                    inOptions = false;
                    foundAccount = false;
                    foundSeed = null;
                    continue;
                }

                if (!inProfile) continue;

                if (line == "[Options]")
                {
                    inOptions = true;
                    continue;
                }

                if (!inOptions) continue;
                if (line.StartsWith("[") || line.StartsWith("#") || line == "")
                    continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                if (key == "HostAccountName" && val == accountUser)
                {
                    foundAccount = true;
                }
                else if (key == "WorldSeed" && foundAccount)
                {
                    if (int.TryParse(val, out int s))
                        foundSeed = s;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[WorldSeedSync] Load persisted seed failed: " + ex.Message);
        }
        return -1;
    }

    /// <summary>
    /// 将种子持久化到 Storage/Storages.ini 中当前扩展对应的 ServerProfile。
    /// 如果找不到对应 profile，在末尾追加一个新段。
    /// </summary>
    public static void PersistSeed(string accountUser, int seed)
    {
        try
        {
            if (!Directory.Exists(StorageDir))
                Directory.CreateDirectory(StorageDir);

            string[] lines;
            if (File.Exists(ConfigFile))
                lines = File.ReadAllLines(ConfigFile);
            else
                lines = new string[] { "[Servers]" };

            bool foundProfile = false;
            bool inProfile = false;
            bool inOptions = false;
            bool hasWorldSeed = false;
            var result = new List<string>();

            foreach (var rawLine in lines)
            {
                string trimmed = rawLine.Trim();

                if (trimmed == "[ServerProfileStart]")
                {
                    inProfile = true;
                    inOptions = false;
                    hasWorldSeed = false;
                }

                // 在匹配的 profile 中替换 WorldSeed 行
                if (inProfile && inOptions && foundProfile && !hasWorldSeed)
                {
                    if (trimmed.StartsWith("WorldSeed="))
                    {
                        result.Add("WorldSeed=" + seed);
                        hasWorldSeed = true;
                        foundProfile = true;
                        continue;
                    }
                }

                result.Add(rawLine);

                if (trimmed == "[Options]")
                {
                    inOptions = true;
                }

                if (trimmed == "[ServerProfileEnd]")
                {
                    // profile 末尾没有 WorldSeed → 追加
                    if (foundProfile && !hasWorldSeed)
                    {
                        result.Add("WorldSeed=" + seed);
                        hasWorldSeed = true;
                    }
                    foundProfile = false;
                    inProfile = false;
                    inOptions = false;
                    hasWorldSeed = false;
                }

                // 检测当前 profile 目标账户
                if (inProfile && inOptions && !foundProfile)
                {
                    int eq = trimmed.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = trimmed.Substring(0, eq).Trim();
                        string v = trimmed.Substring(eq + 1).Trim();
                        if (k == "HostAccountName" && v == accountUser)
                            foundProfile = true;
                    }
                }
            }

            // 找不到 → 追加
            if (!foundProfile)
            {
                result.Add("[ServerProfileStart]");
                result.Add("[Options]");
                result.Add("HostAccountName=" + accountUser);
                result.Add("WorldSeed=" + seed);
                result.Add("[ServerProfileEnd]");
            }

            File.WriteAllLines(ConfigFile, result);
            Console.WriteLine("[WorldSeedSync] Seed persisted to " + ConfigFile + ": " + seed);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[WorldSeedSync] Persist seed failed: " + ex.Message);
        }
    }

    // ── 状态 ──

    public static bool IsInitialized { get; private set; }
    public static int MasterSeed { get; private set; }
    public static Random ExpectedRandom { get { return _injectedRandom; } }
    private static Random _injectedRandom;
    public static bool SeedSent { get; set; }
    private static int _childSeedCounter;
    public static int ChildSeedCounter { get => _childSeedCounter; set => _childSeedCounter = value; }
    private static bool _pendingFixStaticFields;

    // ── 初始化 ──

    public static void Initialize(int masterSeed)
    {
        MasterSeed = masterSeed;
        _childSeedCounter = 0;
        SeedSent = false;

        Utils.random.ReSeed(masterSeed);
        _injectedRandom = Utils.random;

        IsInitialized = true;
        Console.WriteLine("[WorldSeedSync] Initialized with seed=" + masterSeed);

        // 如果有延迟的 static 字段修复，现在执行
        if (_pendingFixStaticFields)
        {
            _pendingFixStaticFields = false;
            FixAllStaticRandomFields();
        }
    }

    public static void Reset()
    {
        IsInitialized = false;
        MasterSeed = 0;
        _injectedRandom = null;
        SeedSent = false;
        _childSeedCounter = 0;
    }

    // ── 子种子分配 ──

    public static int GetNextChildSeed()
    {
        int idx = _childSeedCounter++;
        return RandomSeedHelper.DeriveChildSeed(MasterSeed, "ctor:" + idx);
    }

    // ── 反射修复 static Random 字段 ──

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

            string asmName = asm.GetName().Name;
            if (asmName == null) continue;

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

                        Random rng = (Random)field.GetValue(null);
                        if (rng == null) continue;
                        if (rng == Utils.random) continue;

                        string identifier = asmName + "." + type.FullName + "." + field.Name;
                        int childSeed = RandomSeedHelper.DeriveChildSeed(MasterSeed, identifier);
                        rng.ReSeed(childSeed);
                        fixedCount++;
                        Console.WriteLine("[WorldSeedSync] Fixed " + identifier + " -> seed=" + childSeed);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }
        }

        Console.WriteLine("[WorldSeedSync] Static Random fields fixed: " + fixedCount + " (skipped " + skippedSystem + " system assemblies)");
    }
}

// ════════════════════════════════════════════════════════════════
// Harmony Patches
// ════════════════════════════════════════════════════════════════

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
            Console.WriteLine("[RandomCtorGuard] ReSeed(" + childSeed + ") FAILED, letting original ctor run.");
            return true; // 失败放行原构造函数，避免留下 SeedArray == null 的坏对象
        }
        Console.WriteLine("[RandomCtorGuard] Intercepted new Random() -> ReSeed(" + childSeed + ")");
        return false;
    }
}
