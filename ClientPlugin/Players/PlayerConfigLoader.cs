using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ClientPlugin;

/// <summary>
/// 从拓展目录的 Config.tmp 读取玩家选择的 GlobleName，
/// 并验证该名称是否在 Players-Cli.xml 的 Globle 列表中，
/// 最终存入 PlayerDataManager 持久化。
/// 
/// 同时解析选中角色的 PTR（Player To Replacements）映射，
/// 供 ComputerLoader.filter 的 Prefix 补丁使用。
/// </summary>
internal static class PlayerConfigLoader
{
    private const string ConfigFileName = "Config.tmp";
    private const string PlayersXmlFileName = "Players-Cli.xml";

    /// <summary>
    /// 当前选中角色的 PTR 映射：{Curr → Org}。
    /// 例如：{"#P1_IP#" → "#PLAYER_IP#"}
    /// 由 PlayerAndPrtPatch 的 Prefix 补丁读取，在 filter 之前将 Curr 替换为 Org。
    /// </summary>
    public static readonly Dictionary<string, string> CurrToOrgMappings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 全局占位符直接映射：{占位符 → 实际玩家名}。
    /// 来自 Config.tmp @G 段（asdf:p1）+ Players-Cli.xml Globle 组 Curr（#P1NAME#）。
    /// 由 filter 的 Prefix 补丁做全局替换（节点名、文件内容、邮件等全部加载文本）。
    /// </summary>
    public static readonly Dictionary<string, string> DirectMappings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 支线占位符直接映射：{占位符 → 实际玩家名}。
    /// 来自 Config.tmp @P 段（asdf:mp1,1）+ Players-Cli.xml Private 组 Curr（#M-P1NAME#）。
    /// 测试期由 Config.tmp 预设支线身份；生产环境需等游戏内选择后再填充。
    /// </summary>
    public static readonly Dictionary<string, string> PrivateMappings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Config.tmp @G 段：角色 → 玩家名（p1 → asdf）</summary>
    private static readonly Dictionary<string, string> RoleToUsername =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Config.tmp @P 段：组,槽位 → 玩家名（mp1,1 → asdf）</summary>
    private static readonly Dictionary<string, string> PrivateSlotToUsername =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 从拓展目录加载玩家角色配置。
    /// </summary>
    /// <param name="extPath">拓展目录完整路径（ExtensionLoader.ActiveExtensionInfo.GetFullFolderPath()）</param>
    /// <returns>true = 成功加载并设置了角色；false = 未找到或验证失败</returns>
    public static bool LoadFromExtension(string extPath)
    {
        try
        {
            // ─── 1. 读取 Config.tmp ───
            string configPath = Path.Combine(extPath, ConfigFileName);
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[PlayerConfig] 未找到 {configPath}，跳过角色选择配置");
                return false;
            }

            string rawContent = File.ReadAllText(configPath).Trim();
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                Console.WriteLine($"[PlayerConfig] {configPath} 内容为空，跳过");
                return false;
            }

            // 只取第一行，去除可能的 BOM 和空白（容忍行尾分号，如 "p1;"）
            string globleName = rawContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                          .FirstOrDefault()?.Trim().TrimEnd(';') ?? "";

            // 解析 Config.tmp @G/@P 段（其他玩家的身份选择结果）
            ParseConfigAssignments(configPath);

            if (string.IsNullOrWhiteSpace(globleName))
            {
                Console.WriteLine($"[PlayerConfig] {configPath} 未找到有效内容");
                return false;
            }

            Console.WriteLine($"[PlayerConfig] 读取 Config.tmp 得到 globleName = \"{globleName}\"");

            // ─── 2. 查找 Players-Cli.xml ───
            string? playersXmlPath = LocatePlayersXml(extPath);
            if (playersXmlPath == null)
            {
                Console.WriteLine($"[PlayerConfig] 未找到 {PlayersXmlFileName}，跳过验证。直接使用: {globleName}");
                // 没找到校验文件，直接信任 Config.tmp 的内容
                PlayerDataManager.UpdateSelectPlayerConfig(globleName);
                return true;
            }

            // 顺带加载玩家节点映射（Players-Cli.xml 的 <PlayerNodes> 字段）
            PlayerNodeManager.LoadFromPlayersXml(playersXmlPath);

            // ─── 3. 解析 Globle 列表并验证 ───
            var doc = XDocument.Load(playersXmlPath);
            var globle = doc.Root?
                .Element("PlayerToReplacements")?
                .Element("Globle");

            if (globle == null)
            {
                Console.WriteLine($"[PlayerConfig] {playersXmlPath} 中未找到 Globle 节点，跳过验证");
                PlayerDataManager.UpdateSelectPlayerConfig(globleName);
                return true;
            }

            // 收集所有 Globle 下的 <PlayerReplacementsGroup name="..."> 的 name 属性
            var validNames = globle.Elements("PlayerReplacementsGroup")
                                   .Attributes("name")
                                   .Select(a => a.Value)
                                   .Where(n => !string.IsNullOrWhiteSpace(n))
                                   .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (validNames.Count == 0)
            {
                Console.WriteLine($"[PlayerConfig] {playersXmlPath} 的 Globle 中没有有效的 PlayerReplacementsGroup，跳过验证");
                PlayerDataManager.UpdateSelectPlayerConfig(globleName);
                return true;
            }

            // ─── 4. 校验 ───
            if (validNames.Contains(globleName))
            {
                Console.WriteLine($"[PlayerConfig] ✓ 验证通过: \"{globleName}\" 是合法的角色名（可选: {string.Join(", ", validNames)}）");
                PlayerDataManager.UpdateSelectPlayerConfig(globleName);

                // ─── 5. 加载选中角色的 PTR 映射 ───
                LoadPrtMappings(doc, globleName);

                // ─── 6. 基于 @G/@P 段 + Globle/Private 组建立占位符直接映射（全局替换） ───
                BuildPlaceholderMappings(playersXmlPath);
                return true;
            }
            else
            {
                Console.WriteLine($"[PlayerConfig] ✗ 验证失败: \"{globleName}\" 不在可用角色列表 [{string.Join(", ", validNames)}] 中");
                Console.WriteLine($"[PlayerConfig]   请确保 Config.tmp 中的值与 Players-Cli.xml Globle 的 name 属性一致");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerConfig] 加载配置时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 按优先级搜索 Players-Cli.xml。
    /// 搜索路径：扩展目录根 → 扩展 Content 目录 → 当前工作目录。
    /// </summary>
    private static string? LocatePlayersXml(string extPath)
    {
        string[] candidates =
        {
            Path.Combine(extPath, PlayersXmlFileName),
            Path.Combine(extPath, "Content", PlayersXmlFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlayersXmlFileName),
            Path.Combine(Environment.CurrentDirectory, PlayersXmlFileName)
        };

        foreach (var path in candidates)
        {
            string full = Path.GetFullPath(path);
            if (File.Exists(full))
            {
                Console.WriteLine($"[PlayerConfig] 找到 {PlayersXmlFileName}: {full}");
                return full;
            }
        }

        return null;
    }

    /// <summary>
    /// 从已加载的 Players-Cli.xml 文档中，解析指定角色的 PTR 映射。
    /// 填充 CurrToOrgMappings: {Curr → Org}，供 Prefix 补丁使用。
    /// </summary>
    /// <param name="doc">已加载的 Players-Cli.xml 文档</param>
    /// <param name="globleName">选中的角色名（如 "p1"）</param>
    public static void LoadPrtMappings(XDocument doc, string globleName)
    {
        CurrToOrgMappings.Clear();

        try
        {
            var group = doc.Root?
                .Element("PlayerToReplacements")?
                .Element("Globle")?
                .Elements("PlayerReplacementsGroup")
                .FirstOrDefault(g => g.Attribute("name")?.Value.Equals(globleName, StringComparison.OrdinalIgnoreCase) == true);

            if (group == null)
            {
                Console.WriteLine($"[PlayerConfig] 未找到角色 \"{globleName}\" 的 PlayerReplacementsGroup，无 PTR 映射");
                return;
            }

            foreach (var ptr in group.Elements("PTR"))
            {
                string org = ptr.Attribute("Org")?.Value ?? "";
                string curr = ptr.Attribute("Curr")?.Value ?? "";
                if (!string.IsNullOrWhiteSpace(org) && !string.IsNullOrWhiteSpace(curr))
                {
                    CurrToOrgMappings[curr] = org;
                    Console.WriteLine($"[PlayerConfig] PTR映射: {curr} → {org}");
                }
            }

            Console.WriteLine($"[PlayerConfig] 角色 \"{globleName}\" 的 PTR 映射已加载，共 {CurrToOrgMappings.Count} 条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerConfig] 加载 PTR 映射失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析 Config.tmp 的 @G/@P 段（其他玩家的身份选择结果）。
    /// @G：玩家游戏内用户名:p?（asdf:p1）→ 全局角色分配
    /// @P：玩家游戏内用户名:组名,槽位（asdf:mp1,1）→ 支线槽位分配
    /// 测试期支线身份由 Config.tmp 预设；生产环境支线应在游戏内选择后调用 FillPrivateMappings 更新。
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
                    Console.WriteLine($"[PlayerConfig] @G: 角色 {val} <- 玩家 {username}");
                }
                else
                {
                    PrivateSlotToUsername[val] = username;
                    Console.WriteLine($"[PlayerConfig] @P: 槽位 {val} <- 玩家 {username}");
                }
            }

            Console.WriteLine($"[PlayerConfig] Config.tmp 身份分配: 全局 {RoleToUsername.Count} 条, 支线 {PrivateSlotToUsername.Count} 条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerConfig] 解析 Config.tmp 分配失败: {ex.Message}");
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

            // 全局：Globle 组 Curr（#P1NAME#）→ RoleToUsername[组名]
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
                            Console.WriteLine($"[PlayerConfig] 全局占位符: {curr} -> {uname}");
                        }
                    }
                }
            }

            // 支线：Private 组 Player name="1"/"2" 的 Curr → PrivateSlotToUsername["组,槽位"]
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
                                Console.WriteLine($"[PlayerConfig] 支线占位符: {curr} -> {uname}");
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"[PlayerConfig] 占位符映射: 全局 {DirectMappings.Count} 条, 支线 {PrivateMappings.Count} 条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerConfig] 构建占位符映射失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清理 PTR 映射（角色切换或关闭时调用）。
    /// </summary>
    public static void ClearMappings()
    {
        CurrToOrgMappings.Clear();
        Console.WriteLine("[PlayerConfig] PTR 映射已清理");
    }
}
