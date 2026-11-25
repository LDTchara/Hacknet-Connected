using System;
using System.IO;
using System.Xml.Serialization;

namespace ClientPlugin;

/// <summary>
/// 客户端玩家数据管理器 — 只存当前玩家自己。
/// 存储在 {GameDir}/OnlineSaves/PlayerData.xml。
/// 与服务端共享同一种 PlayerData 格式，但只存单条（不含 List 包装）。
/// </summary>
internal static class PlayerDataManager
{
    private static readonly string StorageDir;
    private static readonly string ConfigFile;
    private static readonly XmlSerializer _serializer = new(typeof(PlayerData));

    private static PlayerData? _current;

    static PlayerDataManager()
    {
        StorageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OnlineSaves");
        ConfigFile = Path.Combine(StorageDir, "PlayerData.xml");
    }

    /// <summary>当前玩家数据（可能为 null）</summary>
    public static PlayerData? Current => _current;

    /// <summary>从磁盘加载本地玩家数据</summary>
    public static PlayerData? Load()
    {
        if (!File.Exists(ConfigFile))
        {
            Console.WriteLine("[PlayerDataMgr] 本地 PlayerData.xml 不存在");
            return null;
        }

        try
        {
            using var reader = new StreamReader(ConfigFile);
            _current = (PlayerData?)_serializer.Deserialize(reader);
            // 同步 SelectPlayerConfig 到内存
            ClientPluginMain.SelectPlayerConfig = _current?.SelectPlayerConfig;
            Console.WriteLine($"[PlayerDataMgr] 已加载本地玩家: Id={_current?.Id} Name={_current?.Name}" +
                (!string.IsNullOrEmpty(_current?.SelectPlayerConfig) ? $" Select={_current.SelectPlayerConfig}" : ""));
            return _current;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerDataMgr] 加载失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>保存玩家数据到磁盘（写入 + 更新内存）</summary>
    public static void Save(PlayerData data)
    {
        _current = data;
        try
        {
            if (!Directory.Exists(StorageDir))
                Directory.CreateDirectory(StorageDir);

            using var writer = new StreamWriter(ConfigFile);
            _serializer.Serialize(writer, data);
            Console.WriteLine($"[PlayerDataMgr] 已保存本地玩家: Id={data.Id} Name={data.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerDataMgr] 保存失败: {ex.Message}");
        }
    }

    /// <summary>创建或更新本地玩家数据（首次从服务端收到时调用）</summary>
    public static void CreateOrUpdate(int id, string name)
    {
        var data = new PlayerData
        {
            Id = id,
            Name = name,
            LastLogin = DateTime.Now
        };
        Save(data);
    }

    /// <summary>更新种子同步相关字段</summary>
    public static void UpdateSeedData(int worldSeed, int childCounter, string randomState, int ipCounter)
    {
        if (_current == null) return;
        _current.WorldSeed = worldSeed;
        _current.ChildSeedCounter = childCounter;
        _current.RandomState = randomState;
        _current.IpCounter = ipCounter;
        Save(_current);
    }

    /// <summary>更新存档路径</summary>
    public static void UpdateSavePaths(string nativePath, string onlinePath)
    {
        if (_current == null) return;
        _current.NativeSavePath = nativePath;
        _current.OnlineSavePath = onlinePath;
        Save(_current);
    }

    /// <summary>
    /// 更新客户端角色选择配置并持久化。
    /// 对应 Players-Cli.xml 中 Globle SelectedName 的值。
    /// 同时更新 ClientPluginMain.SelectPlayerConfig 以保持内存同步。
    /// </summary>
    /// <param name="selectedName">选中角色的 name，如 "p1"、"p2"、"p3"。</param>
    public static void UpdateSelectPlayerConfig(string selectedName)
    {
        if (_current == null)
        {
            Console.WriteLine("[PlayerDataMgr] 玩家数据未初始化，无法保存 SelectPlayerConfig");
            return;
        }
        _current.SelectPlayerConfig = selectedName;
        ClientPluginMain.SelectPlayerConfig = selectedName;
        Save(_current);
        Console.WriteLine($"[PlayerDataMgr] SelectPlayerConfig 已更新: {selectedName}");
    }
}
