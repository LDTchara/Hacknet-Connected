using System;
using Hacknet;
using Pathfinder.Command;

namespace ClientPlugin;

/// <summary>
/// selectplayer 命令：管理支线任务角色选择。
///
/// 在接取了一个私有支线任务后，使用此命令进行角色分配。
/// 上下文（group）由最近接取的私有任务自动确定。
///
/// 用法:
///   selectplayer list              — 列出当前支线的可选角色
///   selectplayer <序号>            — 选择指定序号的角色
///
/// 示例:
///   > selectplayer list
///   ═══ 角色列表 ═══
///   [1]  战士       [可选]
///   [2]  法师       [可选]
///   [3]  刺客       [可选]
///
///   > selectplayer 1
///   [OK] 你选择了 [战士]
///
///   > selectplayer 1
///   [ERROR] 角色已被其他玩家选择！
/// </summary>
public static class SelectPlayerCommand
{
    private const string CommandName = "selectplayer";

    /// <summary>当前活跃的私有支线 group 名（由任务接取时设定）</summary>
    public static string CurrentGroup { get; set; } = "";

    /// <summary>是否正在等待角色选择</summary>
    public static bool IsSelecting => !string.IsNullOrEmpty(CurrentGroup);

    public static void Register()
    {
        CommandManager.RegisterCommand(CommandName, Handle);
    }

    private static void Handle(OS os, string[] args)
    {
        if (args.Length < 2)
        {
            // selectplayer list — 无参数直接显示帮助
            if (string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            {
                if (!IsSelecting)
                {
                    os.write("当前没有活跃的支线任务角色选择。");
                    os.write("用法:");
                    os.write($"  {CommandName} list              — 列出可选角色");
                    os.write($"  {CommandName} <序号>            — 选择角色");
                    return;
                }
                HandleList(os);
                return;
            }

            os.write($"用法:");
            os.write($"  {CommandName} list              — 列出可选角色");
            os.write($"  {CommandName} <序号>            — 选择角色");
            return;
        }

        // selectplayer list
        if (string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
        {
            HandleList(os);
            return;
        }

        // selectplayer <数字>
        if (int.TryParse(args[1], out int slotIndex) && slotIndex >= 1)
        {
            HandleSelect(os, slotIndex);
            return;
        }

        os.write($"未知子命令: {args[1]}");
    }

    private static void HandleList(OS os)
    {
        if (!IsSelecting)
        {
            os.write("当前没有活跃的支线任务角色选择。");
            return;
        }

        var mgr = PrivateMissionManager.GetOrCreate(CurrentGroup);
        if (mgr == null)
        {
            os.write($"[!] 上下文错误：未找到组 [{CurrentGroup}]");
            CurrentGroup = "";
            return;
        }

        mgr.ListAvailableSlots(os);
    }

    private static void HandleSelect(OS os, int slotIndex)
    {
        if (!IsSelecting)
        {
            os.write("当前没有活跃的支线任务角色选择。");
            return;
        }

        var mgr = PrivateMissionManager.GetOrCreate(CurrentGroup);
        if (mgr == null)
        {
            os.write($"[!] 上下文错误：未找到组 [{CurrentGroup}]");
            CurrentGroup = "";
            return;
        }

        if (slotIndex < 1 || slotIndex > mgr.TotalSlots)
        {
            os.write($"[!] 无效序号（1~{mgr.TotalSlots}）");
            return;
        }

        string slotDisplay = mgr.GetSlotDisplayName(slotIndex - 1);
        string playerName = os.defaultUser.name;

        // 先检查其他客户端是否已选了该角色（通过 Assignments 同步）
        if (mgr.IsSlotTaken(slotIndex - 1))
        {
            string takenBy = mgr.GetSlotTaker(slotIndex - 1);
            os.write($"[!] 角色 [{slotDisplay}] 已被 [{takenBy}] 选择！");
            return;
        }

        if (mgr.TrySelect(slotIndex - 1, playerName, os))
        {
            os.write($"[OK] 你选择了 [{slotDisplay}]");

            // 检查是否已满员
            if (mgr.IsFull)
            {
                os.write("═══════════════════════════════════");
                os.write("   所有角色已分配完毕！");
                os.write("   任务即将开始...");
                os.write("═══════════════════════════════════");
                // 广播满员事件，触发其他客户端的倒计时
                BroadcastMissionReady();
            }
        }
    }

    /// <summary>广播任务就绪（所有角色已选完）</summary>
    private static void BroadcastMissionReady()
    {
        if (string.IsNullOrEmpty(CurrentGroup)) return;
        ClientNetworkManager.Send($"EVENT:MISSION_READY:{CurrentGroup}");
    }

    /// <summary>处理远程满员事件</summary>
    public static void HandleMissionReady(string group)
    {
        if (string.IsNullOrEmpty(group)) return;
        OS os = OS.currentInstance;
        if (os != null)
        {
            os.write("═══════════════════════════════════");
            os.write("  所有玩家已完成角色选择！");
            os.write("  任务即将开始...");
            os.write("═══════════════════════════════════");
        }
    }

    /// <summary>处理远程 PLAYER_SELECT 事件</summary>
    public static void OnPlayerSelectEvent(string[] parts)
    {
        if (parts.Length < 5) return;
        string group = parts[2];
        string slot = parts[3];
        string playerName = parts[4];
        PrivateMissionManager.HandleRemoteSelection(group, slot, playerName);
    }
}
