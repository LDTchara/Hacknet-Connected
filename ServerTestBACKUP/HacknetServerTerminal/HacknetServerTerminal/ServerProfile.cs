namespace HacknetServerTerminal;

/// <summary>
/// 服务端配置数据
/// </summary>
internal class ServerProfile
{
    public string HostAccountName { get; set; } = "";
    public string HostAccountPassword { get; set; } = "";
    public string Port { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ServerProfilesPath => $"{ServerName}/{ServerName}_Profile.xml";
    public string ServerSavesPath => $"{ServerName}/save_{HostAccountName}.xml";

    /// <summary>世界种子，0 表示由服务端自动生成</summary>
    public int WorldSeed { get; set; } = 0;

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(HostAccountName) &&
        !string.IsNullOrWhiteSpace(HostAccountPassword) &&
        !string.IsNullOrWhiteSpace(Port) &&
        !string.IsNullOrWhiteSpace(ServerName);

    /// <summary>
    /// 配置文件保存路径：{BaseDir}/Storage/{ServerName}/{ServerName}_Profile.xml
    /// </summary>
    public static string GetStoragePath(string serverName)
    {
        string dir = Path.Combine("Storage", serverName);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{serverName}_Profile.xml");
    }

    public ServerProfile Clone() => new()
    {
        HostAccountName = HostAccountName,
        HostAccountPassword = HostAccountPassword,
        Port = Port,
        ServerName = ServerName,
        WorldSeed = WorldSeed
    };
}
