using System.Collections.Generic;

namespace ClientPlugin.Listing;

public class AvailableServer
{
    public string LocalID = "";
    public string HostName = "";
    public string IP = "";
    public int Port;
    public string FolderPath = "";

    /// <summary>扩展名，如 "tmod"；为空则由 HostName/FolderPath 自动推断</summary>
    public string ExtensionName = "";

    /// <summary>从服务端接收的世界种子，0 表示未接收</summary>
    public int WorldSeed = 0;

    /// <summary>服务端 Utils.random 完整内部状态（58 个逗号分隔 int）</summary>
    public string RandomState = "";

    /// <summary>服务端 _childSeedCounter，用于对齐 new Random() 子种子分配</summary>
    public int ChildSeedCounter = 0;

    public HashSet<ClientAccountStructure> Accounts = new();
}
