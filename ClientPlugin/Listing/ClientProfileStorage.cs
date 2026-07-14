using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClientPlugin.Listing;

/// <summary>
/// INI 格式存储管理 — 参考 ExampleStorage/ClientProfiles.ini
/// 
/// 格式:
/// [ClientsStorageStart]
/// [Client Name="Server1" Start]
/// [Folder Path="..."]
/// [PlayersStart]
/// [Player id="..." name="..." password="..." savefile="..."]
/// [PlayersEnd]
/// [Client Name="Server1" End]
/// [Client Name="Server2" Start]
/// [Client Name="Server2" End]
/// [ClientsStorageEnd]
/// </summary>
public static class ClientProfileStorage
{
    public static string StorageDir => Path.Combine(ClientPluginMain.EnvPath, "OnlineSaves");
    public static string ProfilesFile => Path.Combine(StorageDir, "ClientProfiles.ini");

    /// <summary>所有已知服务器</summary>
    public static readonly List<AvailableServer> Servers = new();

    /// <summary>加载 INI 配置</summary>
    public static void Load()
    {
        Servers.Clear();

        // 确保目录存在
        Directory.CreateDirectory(StorageDir);

        if (!File.Exists(ProfilesFile))
        {
            // 创建空 INI 文件
            File.WriteAllText(ProfilesFile,
                "[ClientsStorageStart]" + Environment.NewLine +
                "[ClientsStorageEnd]" + Environment.NewLine,
                Encoding.UTF8);
            Console.WriteLine($"[ProfileStorage] Created empty config: {ProfilesFile}");
            return;
        }

        try
        {
            var lines = File.ReadAllLines(ProfilesFile, Encoding.UTF8);
            AvailableServer? current = null;
            bool inPlayers = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // [Client Name="xxx" Start]
                if (line.StartsWith("[Client Name=\"") && line.Contains("Start]"))
                {
                    // HostName 是 [Client Name=" 之后到第一个 " 之间的内容
                    // 不能用 " Start] 做定界——属性值中也包含 "，会吞掉所有属性
                    int nameStart = "[Client Name=\"".Length;
                    int nameEnd = line.IndexOf('"', nameStart);
                    string name = nameEnd > nameStart
                        ? line.Substring(nameStart, nameEnd - nameStart)
                        : "";
                    // 清理空白（防止 INI 中 HostName 前后有空格）
                    name = name.Trim();
                    current = new AvailableServer
                    {
                        LocalID = Guid.NewGuid().ToString("N").Substring(0, 8),
                        HostName = name
                    };

                    // 解析 ExtensionName="..." 属性
                    var extMatch = System.Text.RegularExpressions.Regex.Match(line,
                        "ExtensionName=\"([^\"]+)\"");
                    if (extMatch.Success)
                        current.ExtensionName = extMatch.Groups[1].Value;

                    // 解析 ip="..." 属性
                    var ipMatch = System.Text.RegularExpressions.Regex.Match(line,
                        "ip=\"([^\"]+)\"");
                    if (ipMatch.Success)
                        current.IP = ipMatch.Groups[1].Value;

                    // 解析 Port="..." 属性
                    var portMatch = System.Text.RegularExpressions.Regex.Match(line,
                        "Port=\"([^\"]+)\"");
                    if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int p))
                        current.Port = p;

                    // 解析 WorldSeed="..." 属性
                    var seedMatch = System.Text.RegularExpressions.Regex.Match(line,
                        "WorldSeed=\"([^\"]+)\"");
                    if (seedMatch.Success && int.TryParse(seedMatch.Groups[1].Value, out int ws))
                        current.WorldSeed = ws;

                    // 解析 ChildSeedCounter="..." 属性
                    var cscMatch = System.Text.RegularExpressions.Regex.Match(line,
                        "ChildSeedCounter=\"([^\"]+)\"");
                    if (cscMatch.Success && int.TryParse(cscMatch.Groups[1].Value, out int csc))
                        current.ChildSeedCounter = csc;

                    inPlayers = false;
                    continue;
                }

                // [Client Name="xxx" End]
                if (line.StartsWith("[Client Name=\"") && line.Contains("End]"))
                {
                    if (current != null)
                    {
                        Servers.Add(current);
                        current = null;
                    }
                    inPlayers = false;
                    continue;
                }

                // [Folder Path="..."]
                if (line.StartsWith("[Folder Path=\""))
                {
                    var path = ExtractBetween(line, "[Folder Path=\"", "\"]");
                    if (current != null) current.FolderPath = path;
                    continue;
                }

                // [RandomState="..."]
                if (line.StartsWith("[RandomState=\""))
                {
                    var state = ExtractBetween(line, "[RandomState=\"", "\"]");
                    if (current != null) current.RandomState = state;
                    continue;
                }

                // [PlayersStart]
                if (line == "[PlayersStart]")
                {
                    inPlayers = true;
                    continue;
                }

                // [PlayersEnd]
                if (line == "[PlayersEnd]")
                {
                    inPlayers = false;
                    continue;
                }

                // [Player id="..." name="..." password="..." savefile="..."]
                if (inPlayers && line.StartsWith("[Player "))
                {
                    var player = ParsePlayer(line);
                    if (player != null && current != null)
                        current.Accounts.Add(player);
                    continue;
                }
            }

            Console.WriteLine($"[ProfileStorage] Loaded {Servers.Count} servers.");
            foreach (var s in Servers)
                Console.WriteLine($"  [{s.HostName}] {s.Accounts.Count} accounts, folder={s.FolderPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProfileStorage] Load error: {ex.Message}");
        }
    }

    /// <summary>保存所有配置到 INI</summary>
    public static void Save()
    {
        Directory.CreateDirectory(StorageDir);
        var sb = new StringBuilder();

        sb.AppendLine("[ClientsStorageStart]");

        foreach (var server in Servers)
        {
            string ipAttr = !string.IsNullOrEmpty(server.IP) ? $" ip=\"{server.IP}\"" : "";
            string portAttr = server.Port > 0 ? $" Port=\"{server.Port}\"" : "";
            string seedAttr = server.WorldSeed > 0 ? $" WorldSeed=\"{server.WorldSeed}\"" : "";
            string cscAttr = server.ChildSeedCounter > 0 ? $" ChildSeedCounter=\"{server.ChildSeedCounter}\"" : "";
            sb.AppendLine($"[Client Name=\"{server.HostName}\"{ipAttr}{portAttr}{seedAttr}{cscAttr} Start]");

            if (!string.IsNullOrEmpty(server.FolderPath))
                sb.AppendLine($"[Folder Path=\"{server.FolderPath}\"]");

            // 随机状态序列化（可能很长，单独一行）
            if (!string.IsNullOrEmpty(server.RandomState))
                sb.AppendLine($"[RandomState=\"{server.RandomState}\"]");

            if (server.Accounts.Count > 0)
            {
                sb.AppendLine("[PlayersStart]");
                foreach (var acc in server.Accounts)
                {
                    sb.AppendLine($"[Player id=\"{acc.Id}\" name=\"{acc.Name}\" password=\"{acc.Password}\" savefile=\"{acc.SaveFilePath}\"]");
                }
                sb.AppendLine("[PlayersEnd]");
            }

            sb.AppendLine($"[Client Name=\"{server.HostName}\" End]");
        }

        sb.AppendLine("[ClientsStorageEnd]");

        File.WriteAllText(ProfilesFile, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"[ProfileStorage] Saved {Servers.Count} servers to {ProfilesFile}");
    }

    /// <summary>添加服务器</summary>
    public static void AddServer(AvailableServer server)
    {
        // 检查重名
        if (Servers.Any(s => s.HostName.Equals(server.HostName, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"[ProfileStorage] Server name '{server.HostName}' already exists.");
            return;
        }

        // 确保 FolderPath 存在
        if (string.IsNullOrEmpty(server.FolderPath))
            server.FolderPath = Path.Combine(StorageDir, "Client", server.HostName);

        if (!Directory.Exists(server.FolderPath))
            Directory.CreateDirectory(server.FolderPath);

        Servers.Add(server);
        Save();
    }

    /// <summary>删除服务器</summary>
    public static bool RemoveServer(string hostName)
    {
        var server = Servers.FirstOrDefault(s => s.HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase));
        if (server == null) return false;

        Servers.Remove(server);

        // 删除文件夹
        if (!string.IsNullOrEmpty(server.FolderPath) && Directory.Exists(server.FolderPath))
        {
            try
            {
                Directory.Delete(server.FolderPath, true);
                Console.WriteLine($"[ProfileStorage] Deleted folder: {server.FolderPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProfileStorage] Delete folder error: {ex.Message}");
            }
        }

        Save();
        return true;
    }

    /// <summary>添加账户到指定服务器</summary>
    public static void AddAccount(string hostName, ClientAccountStructure account)
    {
        var server = Servers.FirstOrDefault(s => s.HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase));
        if (server == null) return;

        // 确保账户名不重复
        if (server.Accounts.Any(a => a.Name.Equals(account.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"[ProfileStorage] Account '{account.Name}' already exists on '{hostName}'.");
            return;
        }

        server.Accounts.Add(account);
        Save();
    }

    /// <summary>从指定服务器删除账户</summary>
    public static bool RemoveAccount(string hostName, string accountName)
    {
        var server = Servers.FirstOrDefault(s => s.HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase));
        if (server == null) return false;

        var acc = server.Accounts.FirstOrDefault(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));
        if (acc == null) return false;

        // 删除存档文件
        if (!string.IsNullOrEmpty(acc.SaveFilePath) && File.Exists(acc.SaveFilePath))
        {
            try { File.Delete(acc.SaveFilePath); } catch { }
        }

        server.Accounts.Remove(acc);
        Save();
        return true;
    }

    // ── 解析工具 ──

    private static string ExtractBetween(string s, string start, string end)
    {
        var i = s.IndexOf(start, StringComparison.Ordinal);
        if (i < 0) return "";
        i += start.Length;
        var j = s.IndexOf(end, i, StringComparison.Ordinal);
        if (j < 0) return s.Substring(i);
        return s.Substring(i, j - i);
    }

    private static ClientAccountStructure? ParsePlayer(string line)
    {
        // [Player id="..." name="..." password="..." savefile="..."]
        var id = ExtractAttribute(line, "id");
        var name = ExtractAttribute(line, "name");
        var password = ExtractAttribute(line, "password");
        var savefile = ExtractAttribute(line, "savefile");

        if (string.IsNullOrEmpty(name)) return null;

        return new ClientAccountStructure
        {
            Id = id,
            Name = name,
            Password = password,
            SaveFilePath = savefile
        };
    }

    private static string ExtractAttribute(string s, string attrName)
    {
        return ExtractBetween(s, $"{attrName}=\"", "\"");
    }
}
