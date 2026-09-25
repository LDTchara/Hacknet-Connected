using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HacknetServerTerminal;

/// <summary>
/// Storage/Storages.ini 读写
/// </summary>
internal static class ServerStorage
{
    private const string StorageDir = "Storage";
    private const string ConfigFile = "Storage/Storages.ini";

    public static List<ServerProfile> LoadAll()
    {
        var profiles = new List<ServerProfile>();
        if (!File.Exists(ConfigFile)) return profiles;

        var rawContent = File.ReadAllText(ConfigFile);
        int orphanCount = 0;
        if (rawContent.Contains("[ServerProfileStart]"))
        {
            // 先扫描孤儿条目数量
            var scanLines = File.ReadAllLines(ConfigFile);
            bool inB = false;
            bool hasSN = false;
            foreach (var l in scanLines)
            {
                var t = l.Trim();
                if (t == "[ServerProfileStart]") { inB = true; hasSN = false; }
                else if (t == "[ServerProfileEnd]") { if (inB && !hasSN) orphanCount++; inB = false; }
                else if (inB && t.StartsWith("ServerName=") && t.Length > 11) hasSN = true;
            }
            if (orphanCount > 0)
                Console.WriteLine($"[Storage] Found {orphanCount} orphan entries (missing ServerName), will clean on write");
        }

        var lines = File.ReadAllLines(ConfigFile);
        ServerProfile? current = null;
        bool inOptions = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            switch (line)
            {
                case "[ServerProfileStart]":
                    current = new ServerProfile();
                    inOptions = false;
                    continue;
                case "[ServerProfileEnd]":
                    if (current != null && !string.IsNullOrWhiteSpace(current.ServerName))
                        profiles.Add(current);
                    current = null;
                    continue;
                case "[Options]":
                    inOptions = true;
                    continue;
            }

            if (!inOptions || current == null ||
                line.StartsWith("[") || line.StartsWith("#") || line == "")
                continue;

            var eq = line.IndexOf('=');
            if (eq < 0) continue;

            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();

            switch (key)
            {
                case "HostAccountName": current.HostAccountName = val; break;
                case "HostAccountPassword": current.HostAccountPassword = val; break;
                case "Port": current.Port = val; break;
                case "ServerName": current.ServerName = val; break;
                case "WorldSeed":
                    if (int.TryParse(val, out int seed))
                        current.WorldSeed = seed;
                    break;
                case "PlayersCount":
                    if (int.TryParse(val, out int pc))
                        current.PlayersCount = pc;
                    break;
            }
        }

        if (current != null && !string.IsNullOrWhiteSpace(current.ServerName))
            profiles.Add(current);

        // 有孤儿条目 → 重新写入，自动清除
        if (orphanCount > 0)
        {
            Console.WriteLine($"[Storage] Rewriting Storage/Storages.ini to remove {orphanCount} orphan entries");
            WriteAll(profiles);
        }

        return profiles;
    }

    public static ServerProfile? Find(string serverName)
    {
        return LoadAll().Find(p =>
            p.ServerName.Equals(serverName, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> FindNames()
    {
        return LoadAll().Select(p => p.ServerName).ToList();
    }

    public static void Save(ServerProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ServerName))
        {
            Console.WriteLine("[Storage] WARNING: Save called with null/empty ServerName, skipping");
            return;
        }
        var all = LoadAll();
        all.RemoveAll(p =>
            p.ServerName.Equals(profile.ServerName, StringComparison.OrdinalIgnoreCase));
        all.Add(profile);
        WriteAll(all);
    }

    public static bool Delete(string serverName)
    {
        var all = LoadAll();
        var removed = all.RemoveAll(p =>
            p.ServerName.Equals(serverName, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;

        WriteAll(all);

        var dir = Path.Combine(StorageDir, serverName);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);

        return true;
    }

    public static bool ValidatePassword(ServerProfile profile, string password)
    {
        return profile.HostAccountPassword == password;
    }

    private static void WriteAll(List<ServerProfile> profiles)
    {
        if (!Directory.Exists(StorageDir))
            Directory.CreateDirectory(StorageDir);

        var sb = new StringBuilder();
        sb.AppendLine("[Servers]");

        // 去重：同名服务器只保留最后一个
        var deduped = new List<ServerProfile>();
        foreach (var p in profiles)
        {
            // 跳过没有 ServerName 的孤儿条目（防止 Save() 时重复创建）
            if (string.IsNullOrWhiteSpace(p.ServerName))
                continue;
            deduped.RemoveAll(u => u.ServerName.Equals(p.ServerName, StringComparison.OrdinalIgnoreCase));
            deduped.Add(p);
        }

        foreach (var p in deduped)
        {
            sb.AppendLine("[ServerProfileStart]");
            sb.AppendLine("[Options]");
            // 通用反射：写出所有可读写（有 setter）的非空属性
            var pType = typeof(ServerProfile);
            foreach (var prop in pType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;  // 跳过只读属性（计算属性等）
                var val = prop.GetValue(p)?.ToString();
                if (prop.PropertyType == typeof(string) && string.IsNullOrWhiteSpace(val))
                    continue;
                if (prop.PropertyType == typeof(int) && val == "0")
                    continue;
                sb.AppendLine($"{prop.Name}={val ?? ""}");
            }
            sb.AppendLine("[ServerProfileEnd]");
        }

        File.WriteAllText(ConfigFile, sb.ToString());

        foreach (var p in profiles)
        {
            var dir = Path.Combine(StorageDir, p.ServerName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
