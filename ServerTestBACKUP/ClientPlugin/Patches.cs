using HarmonyLib;
using Hacknet;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using ClientPlugin.Listing;
using Hacknet.Screens;

namespace ClientPlugin;

// 注：ClientPlugin 不需要 [BepInDependency] 控制加载顺序。
// 它的 Harmony Patch 在 Load() 中注册，PostLoad 有延迟机制。
// 如果种子在 PostLoad 时未到达，FixAllStaticRandomFields 会
// 延迟到 Initialize() 时执行，不会漏掉 static Random 字段。

/// <summary>
/// 扩展详情页：在 ServerPlugin 的 [Start Server] 按钮下方加 [Connect to Server]
/// </summary>
[HarmonyPatch(typeof(ExtensionsMenuScreen), "DrawExtensionInfoDetail")]
internal static class ConnectButtonPatch
{
    [HarmonyPostfix]
    public static void Postfix(Vector2 drawpos)
    {
        float x = drawpos.X;
        // 偏移到 ServerPlugin 按钮下方：原版内容 + ServerPlugin 按钮(25px) + 间距(10px)
        float y = drawpos.Y + 40f;

        if (Button.doButton(8899001, (int)x, (int)y, 450, 25,
            "[ Connect to Server ]", Hacknet.MainMenu.buttonColor))
        {
            Console.WriteLine("[ClientPlugin] Connect to Server clicked, opening ServerListScreen...");
            Game1.getSingleton().sman.AddScreen(new ServerListScreen());
        }
    }
}

/// <summary>
/// Game1.Update 后缀：驱动 LiteNetLib 网络轮询 + 心跳
/// </summary>
[HarmonyPatch(typeof(Game1), "Update", new Type[] { typeof(GameTime) })]
internal static class GameUpdatePatch
{
    private static double _lastHeartbeat;

    [HarmonyPostfix]
    public static void Postfix()
    {
        ClientNetworkManager.PollEvents();

        // 心跳：每 2 秒发一次 PING 给服务端，测试是否一直在线
        if (ClientNetworkManager.IsConnected)
        {
            double now = OS.currentElapsedTime;
            if (now - _lastHeartbeat >= 2.0)
            {
                _lastHeartbeat = now;
                ClientNetworkManager.Send("PING:" + ClientNetworkManager.PlayerId);
            }
        }
    }
}
