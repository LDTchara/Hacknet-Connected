using System;
using System.Linq;
using System.Text;
using Hacknet;
using Hacknet.PlatformAPI.Storage;

namespace ServerPlugin;

/// <summary>
/// 存档存储辅助——解决 StorageMethods 顺序不固定导致的"一会 OL 一会原生"问题
/// 
/// 问题根因：
///   StorageMethods[0] 可能是 LocalDocumentsStorageMethod（原生），
///   也可能是 Pathfinder 注册的 OnlineSaveStorage（OL），
///   取决于 Pathfinder 注册存储驱动的顺序。同一个存档文件可能被某个 storage 正确处理，
///   但 StorageMethods[0] 指向的是错误的那个。
/// 
/// 修复：
///   读操作：遍历所有 StorageMethods，找第一个 FileExists 为 true 的
///   写操作：写入所有 StorageMethods（冗余写入）
/// </summary>
internal static class SaveFileHelper
{
    /// <summary>
    /// 遍历所有 StorageMethods，找到第一个真正包含指定存档文件的存储驱动。
    /// 如果都不存在，回退到 StorageMethods[0]（与修改前行为一致）。
    /// </summary>
    public static IStorageMethod? FindStorageWithFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var methods = SaveFileManager.StorageMethods;
        if (methods == null || methods.Count == 0)
            return null;

        // 优先找 FileExists 为 true 的
        foreach (var storage in methods)
        {
            if (storage != null && storage.FileExists(filePath))
                return storage;
        }

        // 都不存在 → 回退到第一个
        return methods[0];
    }

    /// <summary>
    /// 检查存档文件是否存在于任何一个 StorageMethod 中。
    /// 注意：和 FindStorageWithFile 不同，不做 fallback 回退。
    /// </summary>
    public static bool SaveExists(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var methods = SaveFileManager.StorageMethods;
        if (methods == null || methods.Count == 0)
            return false;

        foreach (var storage in methods)
        {
            if (storage != null && storage.FileExists(filePath))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 将存档数据写入所有可用的 StorageMethod（冗余写入）。
    /// 注意：不走 storage.WriteFileData(byte[])，因为 Hacknet 原生的
    /// SafeWriteToFile(byte[], string) 内部用 StreamWriter.Write(byte[])，
    /// 该重载不存在，实际调用的是 TextWriter.Write(object) → .ToString() → "System.Byte[]"。
    /// 改用反射读 FullFolderPath + File.WriteAllBytes 直接写文件系统。
    /// </summary>
    public static bool WriteToAllStorages(string filePath, byte[] data)
    {
        bool anySuccess = false;
        var methods = SaveFileManager.StorageMethods;
        if (methods == null) return false;

        foreach (var storage in methods)
        {
            if (storage == null) continue;
            try
            {
                // 反射读 FullFolderPath（不是 IStorageMethod 接口成员，只能用反射）
                string folder = "";
                var ffpProp = storage.GetType().GetProperty("FullFolderPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ffpProp != null)
                {
                    folder = ffpProp.GetValue(storage)?.ToString() ?? "";
                }

                if (!string.IsNullOrEmpty(folder))
                {
                    // 直接文件系统写入，不走 SafeWriteToFile(byte[])
                    string fullPath = System.IO.Path.Combine(folder, filePath);
                    string? dir = System.IO.Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllBytes(fullPath, data);
                    anySuccess = true;
                }
                else
                {
                    // 没有 FullFolderPath 的存储 — 跳过。
                    // 不走 storage.WriteFileData(byte[])：Hacknet 原生的 SafeWriteToFile(byte[], string)
                    // 用 StreamWriter.Write(byte[]) → 无重载 → .ToString() → "System.Byte[]"。
                    // HHBS 写入已覆盖实际读档路径，这里跳过不影响功能。
                    Console.WriteLine($"[SaveFileHelper] {storage.GetType().Name} 无 FullFolderPath，跳过（HHBS 写入已覆盖）");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveFileHelper] Write failed for {storage.GetType().Name}: {ex.Message}");
            }
        }
        return anySuccess;
    }

    /// <summary>
    /// 从正确的 StorageMethod 获取存档文件的读取流。
    /// 如果所有 StorageMethod 都找不到，回退到 HHBS 文件系统路径。
    /// </summary>
    public static System.IO.Stream? GetReadStream(string filePath)
    {
        // 优先走 StorageMethod 遍历
        var storage = FindStorageWithFile(filePath);
        if (storage != null)
        {
            try
            {
                return storage.GetFileReadStream(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveFileHelper] GetReadStream from {storage.GetType().Name} failed: {ex.Message}");
            }
        }

        // fallback：HHBS 文件系统路径（Pathfinder 把存档写在这里）
        try
        {
            string hhbsPath = Hacknet.HostileHackerBreakinSequence.GetBaseDirectory();
            string fullPath = System.IO.Path.Combine(hhbsPath, System.IO.Path.GetFileName(filePath));
            if (System.IO.File.Exists(fullPath))
            {
                Console.WriteLine($"[SaveFileHelper] GetReadStream fallback to HHBS: {fullPath}");
                return System.IO.File.OpenRead(fullPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveFileHelper] HHBS fallback failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 在所有 StorageMethod 中保存 Manifest（冗余写入）。
    /// </summary>
    public static void SaveManifestToAll()
    {
        var methods = SaveFileManager.StorageMethods;
        if (methods == null) return;

        foreach (var storage in methods)
        {
            if (storage == null) continue;
            try
            {
                storage.GetSaveManifest().Save(storage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveFileHelper] Manifest save failed for {storage.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 打印所有 StorageMethod 的类型和路径到控制台（调试用）。
    /// </summary>
    public static void DebugPrintAllStorages(string? filePath)
    {
        var methods = SaveFileManager.StorageMethods;
        if (methods == null || methods.Count == 0)
        {
            Console.WriteLine("[SaveFileHelper] No StorageMethods available");
            return;
        }

        Console.WriteLine($"[SaveFileHelper] === StorageMethods dump ({methods.Count} total) ===");
        for (int i = 0; i < methods.Count; i++)
        {
            var s = methods[i];
            if (s == null)
            {
                Console.WriteLine($"  [{i}] null");
                continue;
            }

            Console.WriteLine($"  [{i}] Type = {s.GetType().FullName}");
            try
            {
                var ffpProp = s.GetType().GetProperty("FullFolderPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ffpProp != null)
                {
                    string folder = ffpProp.GetValue(s)?.ToString() ?? "(null)";
                    Console.WriteLine($"       FullFolderPath = '{folder}'");
                }
                else
                {
                    Console.WriteLine($"       (no FullFolderPath property)");
                }

                if (!string.IsNullOrEmpty(filePath))
                {
                    bool exists = s.FileExists(filePath);
                    Console.WriteLine($"       FileExists('{filePath}') = {exists}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"       (reflection error: {ex.Message})");
            }
        }
        Console.WriteLine("[SaveFileHelper] === End dump ===");
    }
}
