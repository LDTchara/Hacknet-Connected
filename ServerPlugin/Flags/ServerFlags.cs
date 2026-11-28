using HarmonyLib;
using HNC.Flags;
using HNC.Pause;

namespace ServerPlugin;

/// <summary>服务端主机接入全局 flag 同步：主机经管道收发，Scope 判断用服务端的 ScopeManager</summary>
internal static class ServerFlags
{
    /// <summary>PostLoad 调用（其它 mod 的动作类型此时已加载）</summary>
    public static void Setup(Harmony harmony)
    {
        FlagSync.IsOnline = () => ServerManager.IsConnected && HncPause.GameRunning();
        FlagSync.Send = ServerManager.SendToConsole;
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
