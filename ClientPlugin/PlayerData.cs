using System;
using System.Xml.Serialization;

namespace ClientPlugin;

/// <summary>
/// 玩家数据模型 — 客户端侧只存自己。
/// 存储在 OnlineSaves/PlayerData.xml。
/// 
/// 数据来源：
/// - Id, Name: 服务端 HELLO 握手时分配
/// - GameUsername: 登录时填的账户名
/// - NativeSavePath, OnlineSavePath: 启动游戏后解析
/// - WorldSeed, RandomState, ChildSeedCounter: 来自服务端 WORLD_SEED 消息
/// </summary>
[XmlRoot("PlayerData")]
public class PlayerData
{
    [XmlElement("Id")]
    public int Id { get; set; }

    [XmlElement("Name")]
    public string Name { get; set; } = "";

    [XmlElement("GameUsername")]
    public string GameUsername { get; set; } = "";

    [XmlElement("NativeSavePath")]
    public string NativeSavePath { get; set; } = "";

    [XmlElement("OnlineSavePath")]
    public string OnlineSavePath { get; set; } = "";

    [XmlElement("WorldSeed")]
    public int WorldSeed { get; set; }

    [XmlElement("ChildSeedCounter")]
    public int ChildSeedCounter { get; set; }

    [XmlElement("RandomState")]
    public string RandomState { get; set; } = "";

    [XmlElement("IpCounter")]
    public int IpCounter { get; set; }

    [XmlElement("LastLogin")]
    public DateTime LastLogin { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 客户端所选角色的配置标识。
    /// 对应 Players-Cli.xml 中 Globle SelectedName 的值（如 "p1"、"p2"、"p3"）。
    /// 由客户端在角色选择界面设定，持久化到本地 PlayerData.xml。
    /// </summary>
    [XmlElement("SelectPlayerConfig")]
    public string SelectPlayerConfig { get; set; } = "";
}
