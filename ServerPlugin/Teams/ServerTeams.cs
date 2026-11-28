using HNC.Pause;
using HNC.Teams;

namespace ServerPlugin;

/// <summary>
/// 服务端主机接入组队任务：主机是 0 号玩家，经管道和终端通信（和玩家邮件、分享一样）。
/// 逻辑在共享的 TeamPlayer；这里把钩子接到服务端的实现上：
/// 支线身份写进 TeamPlayer.MyIdentities（服务端的 Scope 门控读它），占位符写进 ServerPtrPatch 的映射。
/// </summary>
internal static class ServerTeams
{
    public static void RegisterCommand()
    {
        TeamPlayer.IsOnline = () => ServerManager.IsConnected && HncPause.GameRunning();
        TeamPlayer.Send = ServerManager.SendToConsole;
        TeamPlayer.MyPlayerId = () => 0;
        TeamPlayer.SetMyIdentity = (_, _) => { };   // TeamPlayer.MyIdentities 已记录，门控直接读
        TeamPlayer.SetNamePlaceholder = (curr, name) => ServerPtrPatch.PrivateMappings[curr] = name;
        TeamPlayer.SetMyPlaceholder = (curr, org) => ServerPtrPatch.CurrToOrgMappings[curr] = org;
        TeamPlayer.RegisterCommand();
    }

    /// <summary>管道消息：TEAM_MSG / TEAM_START / TEAM_APPLY</summary>
    public static bool TryHandlePipeMessage(string msg) => TeamPlayer.TryHandle(msg);

    public static void Update() => TeamPlayer.Update();
}
