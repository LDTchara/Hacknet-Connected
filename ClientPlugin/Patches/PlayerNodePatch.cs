using System;
using Hacknet;
using HarmonyLib;

namespace ClientPlugin;

/// <summary>
/// 玩家节点归属补丁（客户端）：
///
/// 1. 节点加载（ComputerLoader.loadComputer）后：
///    - 所有玩家节点：用"主种子 + 节点 id"派生确定性 ip/位置（两端必然一致，
///      不受 Utils.random 流状态漂移影响）
///    - 属于当前客户端角色（Config.tmp → SelectPlayerConfig）→ 设为 os.thisComputer（"我的电脑"）
///    - 其他玩家节点 → 普通节点（地图可见）
///
/// 2. OS 构造完成后：强制设置 thisComputer 为作者指定的节点（Players-Cli.xml
///    PlayersCompConfig 中当前角色对应的 Comp），兜底任何时序问题。
/// </summary>
[HarmonyPatch]
internal static class PlayerNodePatch
{
    // ── 1. 节点加载后处理 ──

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ComputerLoader), nameof(ComputerLoader.loadComputer))]
    public static void NodeLoadPostfix(object __result)
    {
        if (__result is not Computer comp) return;

        string players = PlayerNodeManager.GetPlayerForNode(comp.idName);
        if (string.IsNullOrEmpty(players)) return; // 非玩家节点 → 原逻辑

        var os = OS.currentInstance;
        if (os == null) return;

        // 所有玩家节点：确定性 ip/位置（主种子派生，两端一致，不消耗 Utils.random）
        int masterSeed = WorldSeedSync.MasterSeed;
        comp.ip = PlayerNodeManager.DeriveNodeIp(masterSeed, comp.idName);
        comp.location = PlayerNodeManager.DeriveNodePosition(masterSeed, comp.idName, os.netMap);

        string myRole = ClientPluginMain.SelectPlayerConfig ?? "";
        if (string.Equals(players, myRole, StringComparison.OrdinalIgnoreCase))
        {
            // 我的玩家节点 → 替换为 my computer
            var old = os.thisComputer;
            os.thisComputer = comp;

            // 把原版默认 playerComp 从地图移除（仅当它不是其他玩家节点时）
            if (old != null && old != comp
                && string.IsNullOrEmpty(PlayerNodeManager.GetPlayerForNode(old.idName)))
            {
                os.netMap.nodes.Remove(old);
                Console.WriteLine($"[PlayerNode] 已移除原版默认节点 {old.idName}");
            }

            Console.WriteLine($"[PlayerNode] 节点 {comp.idName}（players={players}）替换为 thisComputer ip={comp.ip}");
        }
        else
        {
            Console.WriteLine($"[PlayerNode] 节点 {comp.idName}（players={players}）普通节点 ip={comp.ip}");
        }
    }

    // ── 2. 世界加载完成后强制设置 thisComputer ──
    // 注意：不能挂在 OS 构造函数上——OS ctor 只设了 currentInstance 就返回，
    // 世界构建（netMap、节点加载）发生在 LoadContent()，ctor 后 netMap 还是 null。

    private static bool _applied;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), nameof(OS.LoadContent))]
    public static void ForceSetThisComputerPostfix(OS __instance)
    {
        if (__instance == null || _applied) return;
        _applied = true;

        try
        {
            string myRole = ClientPluginMain.SelectPlayerConfig ?? "";
            if (string.IsNullOrEmpty(myRole)) return;

            string nodeId = PlayerNodeManager.GetNodeForPlayer(myRole);
            if (string.IsNullOrEmpty(nodeId)) return;

            if (__instance.netMap == null || __instance.netMap.nodes == null) return;

            // 在地图上找作者指定的玩家节点
            Computer target = null;
            foreach (var node in __instance.netMap.nodes)
            {
                if (node != null && string.Equals(node.idName, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    target = node;
                    break;
                }
            }

            if (target == null)
            {
                Console.WriteLine($"[PlayerNode] 强制设置：地图上未找到节点 {nodeId}");
                return;
            }

            __instance.thisComputer = target;
            Console.WriteLine($"[PlayerNode] 强制设置 thisComputer = {target.idName}（ip={target.ip}）");

            // ⚠ 不能把 idName 改成 "playerComp"：
            // 两端各自的玩家节点 id 必须保持 PlayerCompP1/P2 一致，否则按 idName
            // 同步文件事件时失配（客户端发 PlayerCompP1 事件 → 服务端节点已改名 →
            // "Computer not found" → 实时同步失效 → refresh 时文件被清）。
            // "我的电脑"语义由 os.defaultUser + 权限给予承担。

            // 权限给予（模拟 fh 效果 + 原版玩家电脑）：
            // 1. 节点所有账户 known + admin —— 注意原版语义：type=0 才是 admin
            //    （"admin"→0、"all"→1 普通、"mail"→2、"missionlist"→3，见 ComputerLoader.cs:478）
            // 2. 玩家的 os.defaultUser 加入节点账户（known + admin）——否则玩家连接时
            //    用 defaultUser 匹配节点 users，找不到就无权限（要手动 fh）
            // 3. defaultUser 指向节点第一个账户
            try
            {
                if (target.users != null)
                {
                    for (int i = 0; i < target.users.Count; i++)
                    {
                        var u = target.users[i];
                        u.known = true;
                        u.type = 0; // admin（原版：0=admin）
                        target.users[i] = u;
                    }

                    // 玩家的连接身份加入节点账户（匹配 os.defaultUser.name）
                    var du = __instance.defaultUser;
                    if (du.name != null &&
                        !target.users.Any(u => u.name.Equals(du.name, StringComparison.OrdinalIgnoreCase)))
                    {
                        du.known = true;
                        du.type = 0; // admin
                        target.users.Add(du);
                    }

                    // defaultUser 指向"玩家的账户"（非 admin 的 XML 账户）：
                    // os.defaultUser 是连接身份，指向 admin 的话玩家用自己名字登录
                    // 不匹配 defaultUser → 无绿圈；指向玩家账户则用名字登录就有绿圈 + admin
                    var playerAccount = target.users.FirstOrDefault(u =>
                        u.name != null && !u.name.Equals("admin", StringComparison.OrdinalIgnoreCase));
                    if (playerAccount.name != null)
                        __instance.defaultUser = playerAccount;
                    else if (target.users.Count > 0)
                        __instance.defaultUser = target.users[0];

                    Console.WriteLine($"[PlayerNode] 玩家节点 {target.idName} 权限给予完成（users={target.users.Count}，defaultUser={__instance.defaultUser.name}）");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerNode] 设置节点权限失败: {ex.Message}");
            }

            // 移除所有残留玩家电脑节点（原版 playerComp + Pathfinder 生成的 "xxx Workstation"，
            // 它们的 idName 都是 "playerComp"），只保留我们的玩家节点
            for (int i = __instance.netMap.nodes.Count - 1; i >= 0; i--)
            {
                var node = __instance.netMap.nodes[i];
                if (node == null || node == target) continue;
                if (string.Equals(node.idName, "playerComp", StringComparison.OrdinalIgnoreCase))
                {
                    __instance.netMap.nodes.RemoveAt(i);
                    Console.WriteLine($"[PlayerNode] 已移除残留玩家电脑节点 {node.name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerNode] 强制设置异常: {ex.Message}");
        }
    }
}
