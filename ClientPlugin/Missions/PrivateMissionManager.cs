using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Hacknet;

namespace ClientPlugin;

/// <summary>
/// 管理私有支线任务的角色分配。
/// 读取 Players-Cli.xml 中 <Private> 下的 PlayerReplacementsGroup，
/// 维护"哪些角色 slot 被谁选了"的状态。
///
/// 典型用法:
///   PrivateMissionManager mgr = PrivateMissionManager.ForGroup("mission1-private");
///   mgr.ListAvailableSlots(os);           // 显示可选角色
///   mgr.TrySelect(1, "playerName", os);   // 选一个
/// </summary>
public class PrivateMissionManager
{
    public string GroupName { get; }
    public int TotalSlots { get; }
    public List<string> SlotNames { get; } // slot 名字列表（如 "1", "2", "3"）
    public List<PTRMapping> PtrMappings { get; }          // 原始 PTR 定义
    public Dictionary<string, string> Assignments { get; } // slot → playerName

    /// <summary>当前组已选中的数量</summary>
    public int SelectedCount => Assignments.Count;

    /// <summary>是否已满员</summary>
    public bool IsFull => SelectedCount >= TotalSlots;

    private PrivateMissionManager(string groupName, int totalSlots,
        List<string> slotNames, List<PTRMapping> ptrs)
    {
        GroupName = groupName;
        TotalSlots = totalSlots;
        SlotNames = slotNames;
        PtrMappings = ptrs;
        Assignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>从 Players-Cli.xml 加载指定 group 的管理器</summary>
    public static PrivateMissionManager ForGroup(string groupName)
    {
        string xmlPath = FindPlayersCliXml();
        if (xmlPath == null) return null;

        var doc = XDocument.Load(xmlPath);
        var group = doc.Root?
            .Element("PlayerToReplacements")?
            .Element("Private")?
            .Elements("PlayerReplacementsGroup")
            .FirstOrDefault(g =>
                g.Attribute("name")?.Value.Equals(groupName, StringComparison.OrdinalIgnoreCase) == true);

        if (group == null) return null;

        int num = 0;
        var numAttr = group.Attribute("num");
        if (numAttr != null) int.TryParse(numAttr.Value, out num);

        var slots = new List<string>();
        var ptrs = new List<PTRMapping>();

        foreach (var player in group.Elements("Player"))
        {
            string name = player.Attribute("name")?.Value ?? "";
            if (string.IsNullOrEmpty(name)) continue;
            slots.Add(name);

            foreach (var ptr in player.Elements("PTR"))
            {
                ptrs.Add(new PTRMapping
                {
                    PlayerName = name,
                    Org = ptr.Attribute("Org")?.Value ?? "",
                    Curr = ptr.Attribute("Curr")?.Value ?? ""
                });
            }
        }

        int totalSlots = num > 0 ? num : slots.Count;
        return new PrivateMissionManager(groupName, totalSlots, slots, ptrs);
    }

    /// <summary>获取 slot 的显示名称（优先使用 role 属性，回退到 name）</summary>
    public string GetSlotDisplayName(int index)
    {
        if (index < 0 || index >= SlotNames.Count)
            return $"#{index + 1}";
        return SlotNames[index];
    }

    /// <summary>指定序号是否已被选择</summary>
    public bool IsSlotTaken(int index)
    {
        if (index < 0 || index >= SlotNames.Count) return true;
        return Assignments.ContainsKey(SlotNames[index]);
    }

    /// <summary>谁选择了指定序号（空字符串表示未选）</summary>
    public string GetSlotTaker(int index)
    {
        if (index < 0 || index >= SlotNames.Count) return "";
        return Assignments.TryGetValue(SlotNames[index], out var taker) ? taker : "";
    }

    /// <summary>列出可用角色序号</summary>
    public void ListAvailableSlots(OS os)
    {
        os.write($"═══ 支线任务 [{GroupName}] 角色选择 ═══");
        os.write($"总人数: {TotalSlots}");

        for (int i = 0; i < SlotNames.Count; i++)
        {
            string slot = SlotNames[i];
            string display = GetSlotDisplayName(i);
            bool taken = Assignments.ContainsKey(slot);
            string status = taken ? $"[已选: {Assignments[slot]}]" : "[可选]";
            os.write($"  [{i + 1}]  {display}  {status}");
        }

        if (IsFull)
            os.write("[!] 已满员！");
        else
            os.write($"使用 select <序号> 选择角色（剩余 {TotalSlots - SelectedCount} 个空位）");
    }

    /// <summary>尝试选择指定角色</summary>
    public bool TrySelect(int slotIndex, string playerName, OS os)
    {
        if (slotIndex < 0 || slotIndex >= SlotNames.Count)
        {
            os.write($"[!] 无效序号（1~{SlotNames.Count}）");
            return false;
        }

        string slot = SlotNames[slotIndex];
        if (Assignments.ContainsKey(slot))
        {
            os.write($"[!] 角色 [{slot}] 已被选走");
            return false;
        }

        Assignments[slot] = playerName;
        os.write($"[{playerName}] 选择了角色 [{slot}]");

        // 同步到 MyPrivateIdentities
        ScopePatches.MyPrivateIdentities[GroupName] = slot;

        // 更新 PTR 映射：该 slot 的 Curr→Org 现在需要填入玩家真实值
        // #M-P1NAME# 应该被替换为 playerName
        // #M-P1_IP# 应该被替换为 os.thisComputer.ip
        var myPtrs = PtrMappings
            .Where(p => p.PlayerName.Equals(slot, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var ptr in myPtrs)
        {
            // 将 Curr→Org 注入 PlayerConfigLoader.CurrToOrgMappings，
            // PlayerPtrFilterPatch 会在 ComputerLoader.filter 之前做字符串替换：
            // #M-P1NAME# → #PLAYERNAME#，#M-P1_IP# → #PLAYER_IP#，
            // 再由原版 filter 解析为当前玩家的名字 / IP。
            if (string.IsNullOrEmpty(ptr.Curr) || string.IsNullOrEmpty(ptr.Org)) continue;
            PlayerConfigLoader.CurrToOrgMappings[ptr.Curr] = ptr.Org;
        }

        // 广播选择结果给其他客户端
        BroadcastSelection(slot, playerName);

        return true;
    }

    private void BroadcastSelection(string slot, string playerName)
    {
        string msg = $"EVENT:PLAYER_SELECT:{GroupName}:{slot}:{playerName}";
        ClientNetworkManager.Send(msg);
    }

    /// <summary>处理来自其他客户端的角色选择广播</summary>
    public static void HandleRemoteSelection(string group, string slot, string playerName)
    {
        // 同步到本地的 Assignments（防止他人选已被占的 slot）
        var session = GetOrCreate(group);
        if (session != null)
            session.Assignments[slot] = playerName;

        Console.WriteLine($"[PrivateMission] 远程: [{playerName}] 选择了组 [{group}] 的角色 [{slot}]");
    }

    /// <summary>获取所有活跃组的会话列表</summary>
    public static List<PrivateMissionManager> AllSessions { get; } = new();

    /// <summary>获取或创建指定组的会话</summary>
    public static PrivateMissionManager GetOrCreate(string groupName)
    {
        var existing = AllSessions.FirstOrDefault(s =>
            s.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        var mgr = ForGroup(groupName);
        if (mgr != null) AllSessions.Add(mgr);
        return mgr;
    }

    private static string FindPlayersCliXml()
    {
        // 优先扩展目录（和 PlayerConfigLoader 一致），再回退到游戏目录
        string ext = Hacknet.Extensions.ExtensionLoader.ActiveExtensionInfo?.GetFullFolderPath() ?? "";
        if (ext.Length > 0)
        {
            foreach (var p in new[] { Path.Combine(ext, "Players-Cli.xml"), Path.Combine(ext, "Content", "Players-Cli.xml") })
                if (File.Exists(p)) return p;
        }

        string[] candidates =
        {
            "Players-Cli.xml",
            Path.Combine(Environment.CurrentDirectory, "Players-Cli.xml"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Players-Cli.xml")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}

/// <summary>一次 PTR 映射定义</summary>
public class PTRMapping
{
    public string PlayerName { get; set; }  // 属于哪个 player name（"1", "2"）
    public string Org { get; set; }          // 原版标记（#PLAYERNAME#）
    public string Curr { get; set; }         // 自定义标记（#M-P1NAME#）
}
