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
    // 注意：全员暂停时原版 Game1.Update 被跳过，但这个后置补丁照常执行（网络、心跳、重连都靠它）
    [HarmonyPostfix]
    public static void Postfix()
    {
        ClientNetworkManager.PollEvents();
        // 心跳（PING/PONG，按真实时间——OS.currentElapsedTime 在暂停时不走）、掉线冻结与重连
        ClientPauseSync.Update();
        ClientPhases.Update();
        ClientSync.Update();
        PlayerMail.Update();
    }
}

/// <summary>
/// ComputerLoader.filter 前缀补丁：将选中角色的 PTR 自替换符注入到 filter 输入中。
/// 
/// 例如选中了角色 "p1"，Players-Cli.xml 中定义：
///   <PTR Org="#PLAYER_IP#" Curr="#P1_IP#" />
/// 
/// 在 filter 执行之前将 #P1_IP# 替换为 #PLAYER_IP#，
/// 由原 filter 解析为 os.thisComputer.ip，不产生循环调用。
/// </summary>
[HarmonyPatch(typeof(ComputerLoader), nameof(ComputerLoader.filter))]
internal static class PlayerPtrFilterPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref string s)
    {
        // 1) 全局直接映射：#P1NAME# → 实际玩家名（Config.tmp @G 段）——全局权威，优先于 PTR
        if (PlayerConfigLoader.DirectMappings.Count > 0)
        {
            foreach (var kv in PlayerConfigLoader.DirectMappings)
            {
                s = s.Replace(kv.Key, kv.Value);
            }
        }

        // 2) 支线直接映射：#M-P1NAME# → 实际玩家名（Config.tmp @P 段，测试期预设）
        if (PlayerConfigLoader.PrivateMappings.Count > 0)
        {
            foreach (var kv in PlayerConfigLoader.PrivateMappings)
            {
                s = s.Replace(kv.Key, kv.Value);
            }
        }

        // 3) PTR：自己角色 IP 占位符 Curr → Org（#P1_IP# → #PLAYER_IP#），由原版 filter 完成最终解析
        //    NAME 占位符已被 DirectMappings 处理，这里只处理 IP 等剩余占位符
        if (PlayerConfigLoader.CurrToOrgMappings.Count > 0)
        {
            foreach (var kv in PlayerConfigLoader.CurrToOrgMappings)
            {
                s = s.Replace(kv.Key, kv.Value);
            }
        }
    }
}
