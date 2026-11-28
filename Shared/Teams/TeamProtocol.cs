using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HNC.Teams;

// 组队任务协议：终端（权威）、ClientPlugin 共用。纯 C#，不依赖游戏程序集。
//
// 流程：玩家在任务板点 Accept（组队任务）→ 客户端不接任务，发 TEAM_LOCK → 终端先到先得锁定该任务并建队（点的人是队长）
//      → 其他人 team join → 每人 team confirm → 队长 team submit → 终端发 TEAM_START，各成员本机接受任务。
//      提交前队长 team disband（或队长 team leave）→ 解散并解除锁定。
//
// 客户端 → 终端
//   TEAM_LOCK:{b64 任务key}:{b64 group}:{人数}:{b64 标题}:{b64 游戏内名字}:{b64 来源}
//       来源 = {界面类型}|{电脑id}：listing（合约板）/ hub（任务中心）/ dhs（DLC 任务中心），成员接任务时走同一个界面的原生接受
//   TEAM_CMD:{b64 "team" 后面的参数}:{b64 游戏内名字}      所有 team 子命令都交给终端执行
//   TEAM_ACK:{b64 任务key}                                   已在本机接受任务
// 终端 → 客户端
//   TEAM_MSG:{b64 文字}                                      显示在游戏终端里（可多行）
//   TEAM_START:{b64 key}:{b64 group}:{分配}:{b64 来源}        队伍已提交：成员本机接受任务并设置支线身份
//   TEAM_APPLY:{b64 key}:{b64 group}:{分配}:{b64 来源}        已开始的队伍的分配（只设置身份 / 占位符，不接任务）
//   TEAM_STATE:{队伍;队伍;…}                                  所有队伍的结构化状态（有变化就发给所有人，界面用）
//   分配 = {位置下标}.{playerId}.{b64 游戏内名字}，逗号分隔（位置下标从 0 开始，对应 Players-*.xml 里 Private 组的 Player 顺序）
//
// 任务 key：任务文件相对扩展根目录的路径，小写、用 / 分隔（各端扩展目录的绝对路径不同）。

internal sealed class TeamAssignment
{
    public int Slot;
    public int PlayerId;
    public string Name = "";
}

/// <summary>界面用的队伍快照（TEAM_STATE）</summary>
internal sealed class TeamView
{
    public string Name = "";
    public string Key = "";
    public string Group = "";
    public string Title = "";
    public int Slots;
    public int LeaderId;
    public bool Started;
    public string Source = "";
    public List<TeamMemberView> Members = new();

    public bool IsFull => Members.Count >= Slots;
    public TeamMemberView? Find(int pid) => Members.FirstOrDefault(m => m.PlayerId == pid);
    public int ConfirmedCount => Members.Count(m => m.Confirmed);
}

internal sealed class TeamMemberView
{
    public int PlayerId;
    public int Slot;
    public bool Confirmed;
    public string Name = "";
}

internal static class TeamProtocol
{
    public const string Prefix = "TEAM_";
    public const string Lock = "TEAM_LOCK:";
    public const string Cmd = "TEAM_CMD:";
    public const string Ack = "TEAM_ACK:";
    public const string Msg = "TEAM_MSG:";
    public const string Start = "TEAM_START:";
    public const string Apply = "TEAM_APPLY:";
    public const string State = "TEAM_STATE:";

    public static string ToB64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));

    public static string FromB64(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch { return ""; }
    }

    public static string EncodeAssignments(IEnumerable<TeamAssignment> list) =>
        string.Join(",", list.Select(a => $"{a.Slot}.{a.PlayerId}.{ToB64(a.Name)}"));

    public static List<TeamAssignment> DecodeAssignments(string s)
    {
        var result = new List<TeamAssignment>();
        foreach (var item in (s ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = item.Split('.');
            if (f.Length != 3 || !int.TryParse(f[0], out int slot) || !int.TryParse(f[1], out int pid)) continue;
            result.Add(new TeamAssignment { Slot = slot, PlayerId = pid, Name = FromB64(f[2]) });
        }
        return result;
    }

    /// <summary>TEAM_START / TEAM_APPLY</summary>
    public static string EncodeStart(string prefix, string key, string group, IEnumerable<TeamAssignment> list, string source = "") =>
        $"{prefix}{ToB64(key)}:{ToB64(group)}:{EncodeAssignments(list)}:{ToB64(source)}";

    public static bool TryDecodeStart(string msg, string prefix, out string key, out string group, out List<TeamAssignment> list) =>
        TryDecodeStart(msg, prefix, out key, out group, out list, out _);

    public static bool TryDecodeStart(string msg, string prefix, out string key, out string group,
        out List<TeamAssignment> list, out string source)
    {
        key = group = source = "";
        list = new List<TeamAssignment>();
        if (!msg.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var p = msg.Substring(prefix.Length).Split(new[] { ':' }, 4);
        if (p.Length < 2) return false;
        key = FromB64(p[0]);
        group = FromB64(p[1]);
        if (p.Length > 2) list = DecodeAssignments(p[2]);
        if (p.Length > 3) source = FromB64(p[3]);
        return true;
    }

    // ── TEAM_STATE：队伍之间用 ;，字段之间用 ,，成员之间用 |，成员字段之间用 ~（都不会出现在 base64 里） ──

    public static string EncodeState(IEnumerable<TeamView> teams) =>
        State + string.Join(";", teams.Select(t => string.Join(",",
            ToB64(t.Name), ToB64(t.Key), ToB64(t.Group), ToB64(t.Title),
            t.Slots, t.LeaderId, t.Started ? 1 : 0, ToB64(t.Source),
            string.Join("|", t.Members.Select(m => $"{m.PlayerId}~{m.Slot}~{(m.Confirmed ? 1 : 0)}~{ToB64(m.Name)}")))));

    public static List<TeamView> DecodeState(string msg)
    {
        var result = new List<TeamView>();
        if (!msg.StartsWith(State, StringComparison.Ordinal)) return result;
        foreach (var item in msg.Substring(State.Length).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = item.Split(',');
            if (f.Length < 9) continue;
            var t = new TeamView
            {
                Name = FromB64(f[0]), Key = FromB64(f[1]), Group = FromB64(f[2]), Title = FromB64(f[3]),
                Slots = int.TryParse(f[4], out int s) ? s : 0,
                LeaderId = int.TryParse(f[5], out int l) ? l : 0,
                Started = f[6] == "1",
                Source = FromB64(f[7])
            };
            foreach (var mm in f[8].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var g = mm.Split('~');
                if (g.Length < 4 || !int.TryParse(g[0], out int pid) || !int.TryParse(g[1], out int slot)) continue;
                t.Members.Add(new TeamMemberView { PlayerId = pid, Slot = slot, Confirmed = g[2] == "1", Name = FromB64(g[3]) });
            }
            result.Add(t);
        }
        return result;
    }
}
