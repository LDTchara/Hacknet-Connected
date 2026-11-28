using HarmonyLib;
using HNC.Flags;

namespace ClientPlugin;

/// <summary>客户端接入全局 flag 同步：逻辑在共享的 FlagSync，这里接上客户端的收发和 Scope 判断</summary>
internal static class ClientFlags
{
    /// <summary>PostLoad 调用（其它 mod 的动作类型此时已加载）</summary>
    public static void Setup(Harmony harmony)
    {
        FlagSync.IsOnline = () => ClientLobby.InSession && ClientNetworkManager.IsConnected;
        FlagSync.Send = ClientNetworkManager.Send;
        FlagSync.IsPrivateScope = o => ScopeManager.GetScope(o)?.IsPrivate == true;
        FlagSync.IsPrivateMissionFunction = (m, start) =>
        {
            var s = ScopeMissionPatches.GetMissionScope(m);
            if (s == null) return false;
            var fn = start ? s.StartScope : s.EndScope;
            return fn?.IsPrivate == true || s.RootScope?.IsPrivate == true;
        };
        FlagSync.PatchStoryContexts(harmony);
    }
}
