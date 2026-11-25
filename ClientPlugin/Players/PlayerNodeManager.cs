using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Hacknet;
using Microsoft.Xna.Framework;

namespace ClientPlugin;

/// <summary>
/// 玩家节点管理器（客户端）：
///
/// 从 Players-Cli.xml 读取"节点 id → 玩家角色"映射，支持两种声明格式：
///
/// 格式 1（Globle 下，推荐）：
///   <Globle>
///     ...
///     <PlayersCompConfig>
///       <PlayerComp name="p1" PlayerID="1" Comp="playerCompP1" />
///       <PlayerComp name="p2" PlayerID="2" Comp="playerCompP2" />
///     </PlayersCompConfig>
///   </Globle>
///
/// 格式 2（根下）：
///   <PlayerNodes>
///     <PlayerNode id="playerCompP1" players="p1" />
///   </PlayerNodes>
///
/// 节点处理（见 PlayerNodePatch）：
///   - 所有玩家节点：用"主种子 + 节点 id"派生确定性 ip/位置（两端必然一致，
///     不依赖 Utils.random 流状态，不受任何随机消耗偏移影响）
///   - 匹配当前客户端角色的节点 → 设为 os.thisComputer（我的电脑）
///   - 其他玩家的节点 → 普通节点（地图可见）
/// </summary>
internal static class PlayerNodeManager
{
    private static readonly Dictionary<string, string> NodeToPlayer =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>从 Players-Cli.xml 加载节点映射</summary>
    public static void LoadFromPlayersXml(string playersXmlPath)
    {
        NodeToPlayer.Clear();
        try
        {
            if (!File.Exists(playersXmlPath)) return;

            var doc = XDocument.Load(playersXmlPath);
            var root = doc.Root;
            if (root == null) return;

            // ── 格式 1：Globle/PlayersCompConfig ──
            var compCfg = root.Element("PlayerToReplacements")?
                .Element("Globle")?
                .Element("PlayersCompConfig");
            if (compCfg != null)
            {
                foreach (var el in compCfg.Elements())
                {
                    // 兼容 name / Name 两种属性大小写
                    var players = (string)(el.Attribute("name") ?? el.Attribute("Name"));
                    var comp = (string)el.Attribute("Comp");
                    if (!string.IsNullOrEmpty(players) && !string.IsNullOrEmpty(comp))
                    {
                        NodeToPlayer[comp] = players;
                        Console.WriteLine($"[PlayerNodeManager] {comp} -> players={players}");
                    }
                }
            }

            // ── 格式 2：根下 PlayerNodes ──
            var playerNodes = root.Element("PlayerNodes");
            if (playerNodes != null)
            {
                foreach (var el in playerNodes.Elements("PlayerNode"))
                {
                    var id = (string)el.Attribute("id");
                    var players = (string)el.Attribute("players");
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(players))
                    {
                        NodeToPlayer[id] = players;
                        Console.WriteLine($"[PlayerNodeManager] {id} -> players={players}");
                    }
                }
            }

            Console.WriteLine($"[PlayerNodeManager] 玩家节点映射共 {NodeToPlayer.Count} 条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerNodeManager] 加载失败: {ex.Message}");
        }
    }

    /// <summary>查询节点归属的玩家角色（无映射返回 null）</summary>
    public static string GetPlayerForNode(string idName) =>
        !string.IsNullOrEmpty(idName) && NodeToPlayer.TryGetValue(idName, out var p) ? p : null;

    /// <summary>反查：当前角色对应的作者指定节点 id（无映射返回 null）</summary>
    public static string GetNodeForPlayer(string role)
    {
        if (string.IsNullOrEmpty(role)) return null;
        foreach (var kv in NodeToPlayer)
        {
            if (string.Equals(kv.Value, role, StringComparison.OrdinalIgnoreCase))
                return kv.Key;
        }
        return null;
    }

    // ── 确定性派生（主种子 + 节点 id，两端必然一致） ──

    /// <summary>玩家节点确定性 IP（与 NetworkMap.generateRandomIP 同格式，避开 0/255）</summary>
    public static string DeriveNodeIp(int masterSeed, string nodeId)
    {
        int childSeed = RandomSeedHelper.DeriveChildSeed(masterSeed, "nodeip:" + nodeId);
        var rng = new Random(childSeed);
        return (rng.Next(254) + 1) + "." + (rng.Next(254) + 1) + "." +
               (rng.Next(254) + 1) + "." + (rng.Next(254) + 1);
    }

    /// <summary>玩家节点确定性位置（0~1 归一化，防重叠）</summary>
    public static Vector2 DeriveNodePosition(int masterSeed, string nodeId, NetworkMap map)
    {
        int childSeed = RandomSeedHelper.DeriveChildSeed(masterSeed, "nodepos:" + nodeId);
        var rng = new Random(childSeed);

        for (int i = 0; i < 8; i++)
        {
            var pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
            if (!map.collides(pos))
                return pos;
        }
        return new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
    }
}
