using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using Hacknet;

namespace ServerPlugin;

/// <summary>
/// 服务端主机的 PTR 自替换符处理（复制自 ClientPlugin 的 PlayerConfigLoader + PlayerPtrFilterPatch）。
///
/// 服务端作为"特殊玩家"（Config.tmp 指定角色，如 p0）参与 PTR 替换：
/// 从扩展根 Players-Cli.xml 的 Globle 中加载该角色的 PTR 映射，
/// 在 ComputerLoader.filter 执行前把 Curr（如 #P0NAME#）替换为 Org（如 #PLAYERNAME#），
/// 由原版 filter 解析为服务端玩家名/IP。
/// </summary>
internal static class ServerPtrPatch
{
    /// <summary>当前服务端角色的 PTR 映射：{Curr → Org}</summary>
    public static readonly Dictionary<string, string> CurrToOrgMappings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>全局占位符直接映射：{占位符 → 实际玩家名}（Config.tmp @G 段 + Globle 组）</summary>
    public static readonly Dictionary<string, string> DirectMappings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>支线占位符直接映射：{占位符 → 实际玩家名}（Config.tmp @P 段 + Private 组）</summary>
    public static readonly Dictionary<string, string> PrivateMappings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Config.tmp @G 段：角色 → 玩家名（p1 → asdf）</summary>
    private static readonly Dictionary<string, string> RoleToUsername =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Config.tmp @P 段：组,槽位 → 玩家名（mp1,1 → asdf）</summary>
    private static readonly Dictionary<string, string> PrivateSlotToUsername =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 从扩展目录加载服务端角色的 PTR 映射。
    /// </summary>
    /// <param name="extPath">扩展目录完整路径（ExtensionLoader.ActiveExtensionInfo.GetFullFolderPath()）</param>
    public static void LoadFromExtension(string extPath)
    {
        CurrToOrgMappings.Clear();

        try
        {
            // ── 1. 读 Config.tmp 获取服务端角色名 ──
            string configPath = Path.Combine(extPath, "Config.tmp");
            if (!File.Exists(configPath))
            {
                Console.WriteLine("[ServerPtrPatch] 未找到 Config.tmp，跳过服务端 PTR 映射");
                return;
            }

            string role = File.ReadAllText(configPath).Trim()
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim().TrimEnd(';') ?? "";
            if (string.IsNullOrWhiteSpace(role))
            {
                Console.WriteLine("[ServerPtrPatch] Config.tmp 为空，跳过服务端 PTR 映射");
                return;
            }

            // 解析 Config.tmp @G/@P 段（其他玩家的身份选择结果）
            ParseConfigAssignments(configPath);

            // 同步设置服务端角色（与 CreateAccountPatch 一致）——
            // 否则非"服务端启动"进程（如客户端机器也装了 ServerPlugin）里
            // ServerPlugin.SelectPlayerConfig 为空 → 玩家节点不替换、权限不设
            if (!string.IsNullOrEmpty(role))
            {
                ServerPlugin.SelectPlayerConfig = role;
                Console.WriteLine($"[ServerPtrPatch] ServerPlugin.SelectPlayerConfig = {role}");
            }

            // ── 2. 找 Players-Cli.xml ──
            string[] candidates =
            {
                Path.Combine(extPath, "Players-Cli.xml"),
                Path.Combine(extPath, "Content", "Players-Cli.xml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Players-Cli.xml"),
                Path.Combine(Environment.CurrentDirectory, "Players-Cli.xml")
            };

            string playersXmlPath = null;
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    playersXmlPath = path;
                    break;
                }
            }

            if (playersXmlPath == null)
            {
                Console.WriteLine($"[ServerPtrPatch] 未找到 Players-Cli.xml，角色 \"{role}\" 无 PTR 映射");
                return;
            }

            // 顺带加载玩家节点映射（Players-Cli.xml 的 <PlayerNodes> 字段）
            PlayerNodeManager.LoadFromPlayersXml(playersXmlPath);

            // 基于 @G/@P 段 + Globle/Private 组建立占位符直接映射（全局替换）
            BuildPlaceholderMappings(playersXmlPath);

            // ── 3. 解析 Globle 中该角色的 PTR 组 ──
            var doc = XDocument.Load(playersXmlPath);
            var group = doc.Root?
                .Element("PlayerToReplacements")?
                .Element("Globle")?
                .Elements("PlayerReplacementsGroup")
                .FirstOrDefault(g => g.Attribute("name")?.Value.Equals(role, StringComparison.OrdinalIgnoreCase) == true);

            if (group == null)
            {
                Console.WriteLine($"[ServerPtrPatch] 未找到角色 \"{role}\" 的 PlayerReplacementsGroup，无 PTR 映射");
                return;
            }

            foreach (var ptr in group.Elements("PTR"))
            {
                string org = ptr.Attribute("Org")?.Value ?? "";
                string curr = ptr.Attribute("Curr")?.Value ?? "";
                if (!string.IsNullOrWhiteSpace(org) && !string.IsNullOrWhiteSpace(curr))
                {
                    CurrToOrgMappings[curr] = org;
                    Console.WriteLine($"[ServerPtrPatch] PTR映射: {curr} → {org}");
                }
            }

            Console.WriteLine($"[ServerPtrPatch] 服务端角色 \"{role}\" 的 PTR 映射已加载，共 {CurrToOrgMappings.Count} 条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerPtrPatch] 加载 PTR 映射失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析 Config.tmp 的 @G/@P 段（其他玩家的身份选择结果）。
    /// @G：玩家游戏内用户名:p?（asdf:p1）→ 全局角色分配
    /// @P：玩家游戏内用户名:组名,槽位（asdf:mp1,1）→ 支线槽位分配
    /// </summary>
    public static void ParseConfigAssignments(string configPath)
    {
        RoleToUsername.Clear();
        PrivateSlotToUsername.Clear();

        try
        {
            if (!File.Exists(configPath)) return;

            bool inG = false, inP = false;
            foreach (var rawLine in File.ReadAllLines(configPath))
            {
                var line = rawLine.Trim().TrimEnd(';');
                if (line.StartsWith("@G")) { inG = true; inP = false; continue; }
                if (line.StartsWith("@P")) { inP = true; inG = false; continue; }
                if (line.StartsWith("{") || line.StartsWith("}") || line.Length == 0) continue;
                if (!inG && !inP) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string username = line.Substring(0, colon).Trim();
                string val = line.Substring(colon + 1).Trim();
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(val)) continue;

                if (inG)
                {
                    RoleToUsername[val] = username;
                    Console.WriteLine($"[ServerPtrPatch] @G: 角色 {val} <- 玩家 {username}");
                }
                else
                {
                    PrivateSlotToUsername[val] = username;
                    Console.WriteLine($"[ServerPtrPatch] @P: 槽位 {val} <- 玩家 {username}");
                }
            }

            Console.WriteLine($"[ServerPtrPatch] Config.tmp 身份分配: 全局 {RoleToUsername.Count} 条, 支线 {PrivateSlotToUsername.Count} 条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerPtrPatch] 解析 Config.tmp 分配失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 Players-Cli.xml 的 Globle/Private 组建立占位符直接映射：
    ///   DirectMappings：#P1NAME# → asdf（@G 段角色 → 玩家名）
    ///   PrivateMappings：#M-P1NAME# → asdf（@P 段 组,槽位 → 玩家名）
    /// 供 filter 的 Prefix 补丁做全局替换（所有加载文本）。
    /// </summary>
    public static void BuildPlaceholderMappings(string playersXmlPath)
    {
        DirectMappings.Clear();
        PrivateMappings.Clear();

        try
        {
            if (!File.Exists(playersXmlPath)) return;
            var doc = XDocument.Load(playersXmlPath);
            var root = doc.Root;
            if (root == null) return;

            var globle = root.Element("PlayerToReplacements")?.Element("Globle");
            if (globle != null)
            {
                foreach (var g in globle.Elements("PlayerReplacementsGroup"))
                {
                    string role = (string)g.Attribute("name") ?? "";
                    if (!RoleToUsername.TryGetValue(role, out var uname)) continue;
                    foreach (var ptr in g.Elements("PTR"))
                    {
                        string curr = (string)ptr.Attribute("Curr") ?? "";
                        if (curr.Contains("NAME"))
                        {
                            DirectMappings[curr] = uname;
                            Console.WriteLine($"[ServerPtrPatch] 全局占位符: {curr} -> {uname}");
                        }
                    }
                }
            }

            var priv = root.Element("PlayerToReplacements")?.Element("Private");
            if (priv != null)
            {
                foreach (var g in priv.Elements("PlayerReplacementsGroup"))
                {
                    string group = (string)g.Attribute("name") ?? "";
                    foreach (var pl in g.Elements("Player"))
                    {
                        string slot = (string)pl.Attribute("name") ?? "";
                        string key = group + "," + slot;
                        if (!PrivateSlotToUsername.TryGetValue(key, out var uname)) continue;
                        foreach (var ptr in pl.Elements("PTR"))
                        {
                            string curr = (string)ptr.Attribute("Curr") ?? "";
                            if (curr.Contains("NAME"))
                            {
                                PrivateMappings[curr] = uname;
                                Console.WriteLine($"[ServerPtrPatch] 支线占位符: {curr} -> {uname}");
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"[ServerPtrPatch] 占位符映射: 全局 {DirectMappings.Count} 条, 支线 {PrivateMappings.Count} 条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerPtrPatch] 构建占位符映射失败: {ex.Message}");
        }
    }

    /// <summary>清理 PTR 映射（服务端状态重置时调用）</summary>
    public static void ClearMappings()
    {
        CurrToOrgMappings.Clear();
    }
}

/// <summary>
/// ComputerLoader.filter 前缀补丁：将服务端角色的 PTR 自替换符注入到 filter 输入中。
/// 与 ClientPlugin 的 PlayerPtrFilterPatch 逻辑一致。
/// </summary>
[HarmonyPatch(typeof(ComputerLoader), nameof(ComputerLoader.filter))]
internal static class ServerPtrFilterPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref string s)
    {
        // 1) 全局直接映射：#P1NAME# → 实际玩家名（Config.tmp @G 段）——全局权威，优先于 PTR
        if (ServerPtrPatch.DirectMappings.Count > 0)
        {
            foreach (var kv in ServerPtrPatch.DirectMappings)
            {
                s = s.Replace(kv.Key, kv.Value);
            }
        }

        // 2) 支线直接映射：#M-P1NAME# → 实际玩家名（Config.tmp @P 段，测试期预设）
        if (ServerPtrPatch.PrivateMappings.Count > 0)
        {
            foreach (var kv in ServerPtrPatch.PrivateMappings)
            {
                s = s.Replace(kv.Key, kv.Value);
            }
        }

        // 3) PTR：自己角色 IP 占位符 Curr → Org（#P2_IP# → #PLAYER_IP#），由原版 filter 完成最终解析
        //    NAME 占位符已被 DirectMappings 处理，这里只处理 IP 等剩余占位符
        if (ServerPtrPatch.CurrToOrgMappings.Count > 0)
        {
            foreach (var kv in ServerPtrPatch.CurrToOrgMappings)
            {
                s = s.Replace(kv.Key, kv.Value);
            }
        }
    }
}
