using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace HacknetServerTerminal;

/// <summary>
/// 玩家数据管理器 — 服务端侧。
/// 按服务器名分文件存储到 Storage/Players/{serverName}.xml。
/// 每台服务器独立管理玩家列表和 ID 递增。
/// </summary>
internal static class PlayerDataManager
{
    private const string PlayersDir = "Storage/Players";
    private static readonly XmlSerializer _listSerializer = new(typeof(PlayerDataList));

    /// <summary>获取指定服务器的玩家配置文件路径</summary>
    private static string GetFilePath(string serverName)
    {
        return Path.Combine(PlayersDir, $"{serverName}.xml");
    }

    /// <summary>加载指定服务器的玩家列表</summary>
    private static List<PlayerData> LoadPlayers(string serverName)
    {
        string file = GetFilePath(serverName);
        if (!File.Exists(file))
            return new List<PlayerData>();

        try
        {
            using var reader = new StreamReader(file);
            if (_listSerializer.Deserialize(reader) is PlayerDataList list && list.Items != null)
                return new List<PlayerData>(list.Items);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerDataMgr] 加载失败 ({serverName}): {ex.Message}");
        }
        return new List<PlayerData>();
    }

    /// <summary>保存指定服务器的玩家列表</summary>
    private static void SavePlayers(string serverName, List<PlayerData> players)
    {
        try
        {
            if (!Directory.Exists(PlayersDir))
                Directory.CreateDirectory(PlayersDir);

            using var writer = new StreamWriter(GetFilePath(serverName));
            _listSerializer.Serialize(writer, new PlayerDataList { Items = players.ToArray() });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerDataMgr] 保存失败 ({serverName}): {ex.Message}");
        }
    }

    /// <summary>按名称查找玩家（限指定服务器）</summary>
    public static PlayerData? FindByName(string playerName, string serverName)
    {
        var players = LoadPlayers(serverName);
        return players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>按 ID 查找玩家（限指定服务器）</summary>
    public static PlayerData? FindById(int id, string serverName)
    {
        var players = LoadPlayers(serverName);
        return players.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>是否为已知玩家（限指定服务器）</summary>
    public static bool IsKnownPlayer(string playerName, string serverName)
    {
        return FindByName(playerName, serverName) != null;
    }

    /// <summary>
    /// 首次登录时创建新玩家记录。
    /// 分配递增 ID，写入 Name、ServerName 和 LastLogin。
    /// </summary>
    public static PlayerData CreatePlayer(string playerName, string serverName)
    {
        var players = LoadPlayers(serverName);

        var existing = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.LastLogin = DateTime.Now;
            SavePlayers(serverName, players);
            return existing;
        }

        int nextId = players.Count > 0 ? players.Max(p => p.Id) + 1 : 1;
        var player = new PlayerData
        {
            Id = nextId,
            Name = playerName,
            ServerName = serverName,
            LastLogin = DateTime.Now
        };
        players.Add(player);
        SavePlayers(serverName, players);
        Console.WriteLine($"[PlayerDataMgr] 新玩家: Id={player.Id} Name={playerName} @ {serverName}");
        return player;
    }

    /// <summary>更新玩家字段（按 Id 匹配，限指定服务器）</summary>
    public static void UpdatePlayer(PlayerData updated, string serverName)
    {
        var players = LoadPlayers(serverName);
        int idx = players.FindIndex(p => p.Id == updated.Id);
        if (idx >= 0)
        {
            players[idx] = updated;
            SavePlayers(serverName, players);
        }
    }

    /// <summary>删除玩家（限指定服务器）</summary>
    public static bool RemovePlayer(string playerName, string serverName)
    {
        var players = LoadPlayers(serverName);
        int removed = players.RemoveAll(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SavePlayers(serverName, players);
            return true;
        }
        return false;
    }

    /// <summary>重命名（保留 Id 和其他字段，限指定服务器）</summary>
    public static bool RenamePlayer(string oldName, string newName, string serverName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var players = LoadPlayers(serverName);

        var player = players.FirstOrDefault(p => p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
        if (player == null) return false;

        if (players.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) && p.Id != player.Id))
            return false;

        player.Name = newName;
        SavePlayers(serverName, players);
        return true;
    }

    /// <summary>获取指定服务器的所有玩家列表</summary>
    public static List<PlayerData> GetAllPlayers(string serverName)
    {
        return LoadPlayers(serverName);
    }
}
