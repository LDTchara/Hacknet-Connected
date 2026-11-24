using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hacknet.Extensions;

namespace ClientPlugin;

/// <summary>
/// 让 HNC.Sync.*.dll 可以作为独立文件放在扩展的 Plugins 目录里。
///
/// 为什么需要它：BepInEx 解析依赖只搜 BepInEx 自己的目录和 BepInEx/plugins，不搜
/// Extensions/&lt;名&gt;/Plugins，所以放在那里的 dll 会报
/// "Could not load file or assembly 'HNC.Sync.Abstractions'"。
///
/// 搜索目录（按顺序）：
///   1. ExtensionLoader.ActiveExtensionInfo 的扩展根目录下的 plugins（目录名不区分大小写）
///   2. 本插件 dll 自己所在的目录——实测 BepInEx 加载扩展插件时 Location 指向 BepInEx/core，
///      所以只能当兜底，不能当主要来源
/// 每次解析时才计算目录，因为安装时 ActiveExtensionInfo 可能还没设置。
///
/// 为什么不直接嵌进插件：HncSync.Runtime 是静态字段，插件和第三方 mod 必须引用同一个程序集实例。
/// 这里只有一个解析器，加载过一次就缓存，之后谁来要都给同一个。
/// 必须在碰到任何 HNC 类型之前安装——放在插件类的静态构造函数里。
/// </summary>
internal static class HncAssemblyResolver
{
    private static bool _installed;
    private static string? _anchorDir;
    private static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.OrdinalIgnoreCase);

    public static void Install(Assembly anchor)
    {
        if (_installed) return;
        _installed = true;

        try
        {
            string loc = anchor.Location;
            if (string.IsNullOrEmpty(loc))
                loc = new Uri(anchor.CodeBase).LocalPath;
            _anchorDir = Path.GetDirectoryName(loc);
        }
        catch { /* 兜底来源，失败就算了 */ }

        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        Console.WriteLine("[HNC] HNC.Sync 程序集解析器已安装");
    }

    private static List<string> CandidateDirs()
    {
        var dirs = new List<string>();

        try
        {
            var info = ExtensionLoader.ActiveExtensionInfo;
            string? root = info?.GetFullFolderPath();
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                string? plugins = Directory.GetDirectories(root)
                    .FirstOrDefault(d => string.Equals(Path.GetFileName(d), "plugins", StringComparison.OrdinalIgnoreCase));
                if (plugins != null) dirs.Add(plugins);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HNC] 读取扩展目录失败: {ex.Message}");
        }

        if (!string.IsNullOrEmpty(_anchorDir) && !dirs.Contains(_anchorDir!))
            dirs.Add(_anchorDir!);

        return dirs;
    }

    private static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        string? name = new AssemblyName(args.Name).Name;
        if (name == null || !name.StartsWith("HNC.Sync.", StringComparison.OrdinalIgnoreCase))
            return null;

        lock (Loaded)
        {
            if (Loaded.TryGetValue(name, out var cached)) return cached;

            // 已经被别人加载过就复用，保证全进程只有一份
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                    return Loaded[name] = asm;
            }

            var tried = new List<string>();
            foreach (var dir in CandidateDirs())
            {
                string path = Path.Combine(dir, name + ".dll");
                tried.Add(path);
                if (!File.Exists(path)) continue;

                var loaded = Assembly.LoadFrom(path);
                Console.WriteLine($"[HNC] 已加载 {path}");
                return Loaded[name] = loaded;
            }

            Console.WriteLine($"[HNC] 找不到 {name}.dll，已尝试: {(tried.Count == 0 ? "(没有可用的搜索目录)" : string.Join("；", tried))}");
            return null;
        }
    }
}
