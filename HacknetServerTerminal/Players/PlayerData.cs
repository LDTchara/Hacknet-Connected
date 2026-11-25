using System;
using System.IO;
using System.Xml.Serialization;

namespace HacknetServerTerminal;

/// <summary>
/// 玩家数据模型 — 服务端和客户端共用格式。
/// 服务端：PlayerDataList 嵌套，存储所有玩家。
/// 客户端：单 PlayerData 节点，只存自己。
/// 
/// XML 字段覆盖：身份、存档路径、种子同步状态。
/// RandomState 即 Utils.random.SaveState() 输出的"一长串"（58 个逗号分隔整数）。
/// </summary>
[XmlRoot("PlayerData")]
public class PlayerData
{
    // ── 身份 ──

    /// <summary>服务端分配的玩家 ID（首次 HELLO 时生成，递增）</summary>
    [XmlElement("Id")]
    public int Id { get; set; }

    /// <summary>玩家名称（来自 HELLO:playerName）</summary>
    [XmlElement("Name")]
    public string Name { get; set; } = "";

    /// <summary>所属服务器名（不同服务器玩家 ID 独立）</summary>
    [XmlElement("ServerName")]
    public string ServerName { get; set; } = "";

    /// <summary>游戏账户用户名</summary>
    [XmlElement("GameUsername")]
    public string GameUsername { get; set; } = "";

    // ── 路径 ──

    /// <summary>原生存档文件路径（LocalDocumentsStorageMethod）</summary>
    [XmlElement("NativeSavePath")]
    public string NativeSavePath { get; set; } = "";

    /// <summary>Pathfinder OL 存档文件路径（HHBS 目录）</summary>
    [XmlElement("OnlineSavePath")]
    public string OnlineSavePath { get; set; } = "";

    // ── 种子同步状态 ──

    /// <summary>世界主种子</summary>
    [XmlElement("WorldSeed")]
    public int WorldSeed { get; set; }

    /// <summary>子种子计数器（对齐 new Random() 分配）</summary>
    [XmlElement("ChildSeedCounter")]
    public int ChildSeedCounter { get; set; }

    /// <summary>
    /// Utils.random 完整内部状态序列化 —— 你朋友说的"那一长串"。
    /// 格式：inext,inextp,sa0,sa1,...,sa55（共 58 个逗号分隔 int）
    /// </summary>
    [XmlElement("RandomState")]
    public string RandomState { get; set; } = "";

    /// <summary>IP 生成计数器（对齐 NetworkMap.generateRandomIP()）</summary>
    [XmlElement("IpCounter")]
    public int IpCounter { get; set; }

    // ── 元信息 ──

    [XmlElement("LastLogin")]
    public DateTime LastLogin { get; set; } = DateTime.MinValue;
}

/// <summary>
/// 服务端专用：多玩家集合包装器。
/// 按服务器分文件存储在 Storage/Players/{serverName}.xml。
/// </summary>
[XmlRoot("PlayerDataList")]
public class PlayerDataList
{
    [XmlElement("PlayerData")]
    public PlayerData[]? Items { get; set; }
}
