using HNC.Teams;

namespace ClientPlugin;

/// <summary>
/// 客户端接入组队任务：逻辑在共享的 TeamPlayer（客户端和服务端主机共用），这里只把钩子接到客户端的实现上。
/// </summary>
internal static class ClientTeams
{
    public static void RegisterCommand()
    {
        TeamPlayer.IsOnline = () => ClientLobby.InSession && ClientNetworkManager.IsConnected;
        TeamPlayer.Send = ClientNetworkManager.Send;
        TeamPlayer.MyPlayerId = () => ClientNetworkManager.PlayerId;
        TeamPlayer.SetMyIdentity = (group, slot) => ScopePatches.MyPrivateIdentities[group] = slot;
        TeamPlayer.SetNamePlaceholder = (curr, name) => PlayerConfigLoader.PrivateMappings[curr] = name;
        TeamPlayer.SetMyPlaceholder = (curr, org) => PlayerConfigLoader.CurrToOrgMappings[curr] = org;
        TeamPlayer.RegisterCommand();
    }

    public static bool TryHandle(string msg) => TeamPlayer.TryHandle(msg);

    public static void Update() => TeamPlayer.Update();
}
