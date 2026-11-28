using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using HNC.Lobby;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HNC.Teams;

/// <summary>
/// 三种任务界面的定向支持（客户端和服务端主机共用）：
/// - 合约板 MissionListingServer、任务中心 MissionHubServer、DLC 任务中心 DHS（DLCHubServer）；
/// - 原生接受：组队提交后，成员走任务来源那个界面自己的接受 / 认领（DHS 的任务必须认领后才能在 DHS 上完成，任务中心会归档合约）；
/// - 组队条：在任务详情里直接显示队伍状态和按钮（组队 / 加入 / 确认 / 提交 / 解散 / 打开面板），不用另开界面。
/// 很多字段 / 方法在 Hacknet 里是 private / internal，统一用反射按名字取。
/// </summary>
internal static class TeamBoards
{
    // ── 反射 ──

    internal static readonly Type? DhsType = AccessTools.TypeByName("Hacknet.DLCHubServer");

    private static readonly FieldInfo? ListingStateF = AccessTools.Field(typeof(MissionListingServer), "state");
    private static readonly FieldInfo? ListingTargetF = AccessTools.Field(typeof(MissionListingServer), "targetIndex");
    private static readonly FieldInfo? ListingMissionsF = AccessTools.Field(typeof(MissionListingServer), "missions");

    private static readonly FieldInfo? HubFolderF = AccessTools.Field(typeof(MissionHubServer), "listingsFolder");
    private static readonly FieldInfo? HubMissionsF = AccessTools.Field(typeof(MissionHubServer), "listingMissions");
    private static readonly FieldInfo? HubSelectedF = AccessTools.Field(typeof(MissionHubServer), "selectedElementIndex");
    private static readonly MethodInfo? HubIdM = AccessTools.Method(typeof(MissionHubServer), "getIDStringForContractFile");
    private static readonly MethodInfo? HubAcceptM = AccessTools.Method(typeof(MissionHubServer), "acceptMission");

    private static readonly FieldInfo? DhsMissionsF = DhsType == null ? null : AccessTools.Field(DhsType, "ActiveMissions");
    private static readonly MethodInfo? DhsAcceptM = DhsType == null ? null : AccessTools.Method(DhsType, "PlayerAcceptMission");

    /// <summary>任务来源：{界面类型}|{电脑id}</summary>
    public static string SourceOf(string kind, Daemon? d) => $"{kind}|{d?.comp?.idName}";

    /// <summary>DHS 的 ClaimableMission（internal 嵌套类）里的任务</summary>
    public static ActiveMission? MissionOfClaimable(object? claimable) =>
        claimable == null ? null : Traverse.Create(claimable).Field("Mission").GetValue<ActiveMission>();

    private static string? HubId(MissionHubServer hub, FileEntry file) => HubIdM?.Invoke(hub, new object[] { file }) as string;

    // ═══════════════════════════════════════════════ 原生接受（TEAM_START）

    /// <summary>
    /// 在任务来源那个界面上执行它自己的接受 / 认领。找到并执行了返回 true；
    /// 合约板（它的接受就是通用步骤）或找不到界面 / 任务时返回 false，由调用方走通用方式。
    /// 调用方负责置 TeamPlayer.Accepting（拦截补丁放行）。
    /// </summary>
    public static bool NativeAccept(OS os, string source, string key)
    {
        var p = (source ?? "").Split(new[] { '|' }, 2);
        if (p.Length < 2 || p[1].Length == 0) return false;
        var comp = os.netMap?.nodes?.FirstOrDefault(c => c.idName == p[1]);
        if (comp?.daemons == null) return false;

        foreach (var d in comp.daemons)
        {
            if (p[0] == "hub" && d is MissionHubServer hub)
            {
                var dict = HubMissionsF?.GetValue(hub) as Dictionary<string, ActiveMission>;
                var folder = HubFolderF?.GetValue(hub) as Folder;
                if (dict == null || folder == null || HubAcceptM == null) continue;
                foreach (var kv in dict)
                {
                    if (TeamPlayer.KeyOf(kv.Value.reloadGoalsSourceFile) != key) continue;
                    int index = folder.files.FindIndex(f => HubId(hub, f) == kv.Key);
                    if (index < 0) continue;
                    HubAcceptM.Invoke(hub, new object[] { kv.Value, index, kv.Key });
                    return true;
                }
            }
            else if (p[0] == "dhs" && DhsType != null && DhsType.IsInstanceOfType(d))
            {
                if (DhsMissionsF?.GetValue(d) is not IList list || DhsAcceptM == null) continue;
                foreach (var cm in list)
                {
                    if (TeamPlayer.KeyOf(MissionOfClaimable(cm)?.reloadGoalsSourceFile) != key) continue;
                    DhsAcceptM.Invoke(d, new[] { cm });
                    return true;
                }
            }
        }
        return false;
    }

    // ═══════════════════════════════════════════════ 组队条：挂到三种界面的任务详情上

    [HarmonyPatch(typeof(MissionListingServer), nameof(MissionListingServer.draw))]
    private static class ListingStrip
    {
        private static void Postfix(MissionListingServer __instance, Rectangle bounds)
        {
            if (ListingStateF?.GetValue(__instance) is not int state || state != 2) return;
            if (ListingMissionsF?.GetValue(__instance) is not List<ActiveMission> missions) return;
            if (ListingTargetF?.GetValue(__instance) is not int idx || idx < 0 || idx >= missions.Count) return;
            // 在底部 Accept 那一行的上方
            var area = new Rectangle(bounds.X + 10, bounds.Y + bounds.Height - 35 - 54, bounds.Width - 20, 50);
            TeamStrip.Draw(area, missions[idx], SourceOf("listing", __instance));
        }
    }

    [HarmonyPatch(typeof(MissionHubServer), "doContractPreviewScreen")]
    private static class HubStrip
    {
        private static void Postfix(MissionHubServer __instance, Rectangle bounds)
        {
            var dict = HubMissionsF?.GetValue(__instance) as Dictionary<string, ActiveMission>;
            var folder = HubFolderF?.GetValue(__instance) as Folder;
            if (dict == null || folder == null || HubSelectedF?.GetValue(__instance) is not int sel) return;
            if (sel < 0 || sel >= folder.files.Count) return;
            string? id = HubId(__instance, folder.files[sel]);
            if (id == null || !dict.TryGetValue(id, out var mission)) return;
            // 在 Accept 按钮那一行的上方（原版：Back 在 bottom-num2，Accept 在它上面 40）
            int num2 = Math.Max(135, bounds.Height / 6);
            var area = new Rectangle(bounds.X + 20, bounds.Y + bounds.Height - num2 - 40 - 56, bounds.Width - 40, 50);
            TeamStrip.Draw(area, mission, SourceOf("hub", __instance));
        }
    }

    [HarmonyPatch]
    private static class DhsStrip
    {
        private static bool Prepare() => DhsType != null;

        private static MethodBase TargetMethod() => AccessTools.Method(DhsType, "DrawMissionPanel");

        private static void Postfix(object __instance, Rectangle bounds, object mission)
        {
            var m = MissionOfClaimable(mission);
            if (m == null) return;
            // DHS 每个合约是一个面板：放在面板底部
            var area = new Rectangle(bounds.X + 8, bounds.Y + bounds.Height - 56, bounds.Width - 16, 50);
            TeamStrip.Draw(area, m, SourceOf("dhs", __instance as Daemon));
        }
    }
}

/// <summary>
/// 任务详情里的组队条：一行状态 + 一行按钮。按钮做的事和 team 命令完全一样（由终端执行）。
/// 在 Hacknet 的界面绘制过程中调用（SpriteBatch 已经开始），用原版 Button 做按钮。
/// </summary>
internal static class TeamStrip
{
    private const int ButtonH = 22;

    public static void Draw(Rectangle area, ActiveMission mission, string source)
    {
        var info = TeamPlayer.GetTeamInfo(mission);
        if (info == null) return;
        var os = OS.currentInstance;
        if (os == null) return;

        // 同一画面可能有好几个组队条（DHS 一次显示几个合约）：按钮 id 按任务区分
        int idBase = 8950000 + (info.Key.GetHashCode() & 0x7FF) * 8;

        LobbyUi.Fill(area, Color.Black * 0.8f);
        LobbyUi.Outline(area, Color.OrangeRed * 0.7f);

        var team = TeamPlayer.TeamForKey(info.Key);
        int me = TeamPlayer.MyPlayerId();
        var mine = team?.Find(me);
        string status;
        Color statusColor = Color.White;
        var buttons = new List<(string Label, Color Color, Action OnClick)>();

        if (team == null)
        {
            status = $"TEAM MISSION - needs {info.Slots} player{(info.Slots > 1 ? "s" : "")}. Not locked yet.";
            buttons.Add(("Form Team", Color.LightGreen, () =>
            {
                if (os.currentMission != null) os.write("[组队] 你当前有任务在进行，先完成或放弃它。");
                else TeamPlayer.RequestLock(info, os, source);
            }));
        }
        else if (team.Started)
        {
            status = mine != null ? $"Your team [{team.Name}] is on this mission." : $"Taken by team [{team.Name}].";
            statusColor = Color.Gray;
        }
        else if (mine != null)
        {
            bool leader = team.LeaderId == me;
            status = $"Team [{team.Name}]  {team.Members.Count}/{team.Slots} players  confirmed {team.ConfirmedCount}/{team.Slots}" +
                     (leader ? "  (you lead)" : "");
            statusColor = Color.LightGreen;
            buttons.Add(mine.Confirmed
                ? ("Unconfirm", Color.Gray, () => Run("unconfirm"))
                : ("Confirm", Color.LightGreen, () => Run("confirm")));
            if (leader)
            {
                bool ready = team.IsFull && team.ConfirmedCount == team.Slots;
                buttons.Add(("Submit", ready ? Color.Orange : Color.DimGray, () =>
                {
                    if (ready) Run("submit");
                    else os.write("[组队] 需要满员且所有人都确认后才能提交。");
                }));
                buttons.Add(("Disband", new Color(200, 80, 80), () => Run("disband")));
            }
            else
            {
                buttons.Add(("Leave", new Color(200, 80, 80), () => Run("leave")));
            }
        }
        else
        {
            status = $"Locked by team [{team.Name}]  {team.Members.Count}/{team.Slots} players";
            statusColor = Color.Yellow;
            if (!team.IsFull && TeamPlayer.MyFormingTeam == null)
                buttons.Add(("Join", Color.LightBlue, () => Run("join " + team.Name)));
        }
        buttons.Add(("Teams...", Color.White, () => TeamPlayer.OpenPanel(info.Key)));

        // 第一行：状态
        string text = Fit(GuiData.tinyfont, status, area.Width - 12);
        GuiData.spriteBatch.DrawString(GuiData.tinyfont, text, new Vector2(area.X + 6, area.Y + 4), statusColor);

        // 第二行：按钮（从左往右）
        int bw = Math.Min(110, (area.Width - 12 - (buttons.Count - 1) * 6) / Math.Max(1, buttons.Count));
        int x = area.X + 6;
        int y = area.Y + area.Height - ButtonH - 4;
        for (int i = 0; i < buttons.Count; i++)
        {
            var (label, color, onClick) = buttons[i];
            if (Button.doButton(idBase + i, x, y, bw, ButtonH, label, color))
                onClick();
            x += bw + 6;
        }
    }

    private static void Run(string line)
    {
        string? error = TeamPlayer.SendCommand(line);
        if (error != null) OS.currentInstance?.write("[组队] " + error);
    }

    /// <summary>超出宽度时截断（先换掉字体画不出的字符）</summary>
    internal static string Fit(SpriteFont font, string text, int width)
    {
        string t = LobbyUi.Clean(text);
        if (font.MeasureString(t).X <= width) return t;
        while (t.Length > 1 && font.MeasureString(t + "...").X > width) t = t.Substring(0, t.Length - 1);
        return t + "...";
    }
}
