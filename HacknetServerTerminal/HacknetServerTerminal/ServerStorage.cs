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
                    if (current != null) profiles.Add(current);
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
            }
        }

        if (current != null && !string.IsNullOrWhiteSpace(current.ServerName))
            profiles.Add(current);

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

        foreach (var p in profiles)
        {
            sb.AppendLine("[ServerProfileStart]");
            sb.AppendLine("[Options]");
            sb.AppendLine($"HostAccountName={p.HostAccountName}");
            sb.AppendLine($"HostAccountPassword={p.HostAccountPassword}");
            sb.AppendLine($"Port={p.Port}");
            sb.AppendLine($"ServerName={p.ServerName}");
            sb.AppendLine($"ServerProfilesPath={p.ServerProfilesPath}");
            sb.AppendLine($"ServerSavesPath={p.ServerSavesPath}");
            sb.AppendLine($"WorldSeed={p.WorldSeed}");
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
