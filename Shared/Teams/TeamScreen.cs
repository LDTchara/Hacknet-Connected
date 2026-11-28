using System;
using System.Collections.Generic;
using System.Linq;
using Hacknet;
using Hacknet.Gui;
using HNC.Lobby;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HNC.Teams;

/// <summary>
/// 组队面板（team ui，或任务界面组队条上的 "Teams..."）：弹出层，游戏在下面照常运行。
/// 左边列出所有队伍，右边是选中队伍的位置、成员、确认情况和操作按钮。
/// 按钮做的事和 team 命令完全一样（由终端执行），状态来自终端广播的 TEAM_STATE。
/// 弹出层拿走输入，所以打开时终端不会收到按键；Esc 或 Close 关闭。
/// </summary>
internal class TeamScreen : GameScreen
{
    private const int IdClose = 8960001;
    private const int IdListBase = 8960100;
    private const int IdActionBase = 8960300;
    private const int IdSlotBase = 8960400;
    private const int IdKickBase = 8960500;

    private string? _selectedKey;
    private string _localNote = "";
    private KeyboardState _lastKb;

    public TeamScreen(string? focusKey = null)
    {
        IsPopup = true;
        TransitionOnTime = TimeSpan.FromSeconds(0.15);
        TransitionOffTime = TimeSpan.FromSeconds(0.15);
        _selectedKey = focusKey;
        _lastKb = Keyboard.GetState();
    }

    /// <summary>已打开时再次请求打开：切到指定任务的队伍</summary>
    public void Focus(string? key)
    {
        if (key != null) _selectedKey = key;
    }

    public override void HandleInput(InputState input)
    {
        base.HandleInput(input);
        GuiData.doInput(input);
    }

    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
        var kb = Keyboard.GetState();
        if (!otherScreenHasFocus && kb.IsKeyDown(Keys.Escape) && !_lastKb.IsKeyDown(Keys.Escape))
            Close();
        _lastKb = kb;

        // 游戏不在了（退回菜单、回大厅）就关掉
        if (!IsExiting && OS.currentInstance == null) Close();
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GuiData.startDraw();
        try
        {
            DrawPanel();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Team] 面板绘制错误: {ex.Message}");
        }
        GuiData.endDraw();
    }

    private void Close()
    {
        if (!IsExiting) ExitScreen();
    }

    // ═══════════════════════════════════════════════ 绘制

    private void DrawPanel()
    {
        var vp = ScreenManager.GraphicsDevice.Viewport.Bounds;
        LobbyUi.Fill(vp, Color.Black * 0.55f);

        int w = Math.Min(960, vp.Width - 60);
        int h = Math.Min(580, vp.Height - 60);
        var panel = new Rectangle((vp.Width - w) / 2, (vp.Height - h) / 2, w, h);
        LobbyUi.Fill(panel, new Color(10, 12, 18));
        LobbyUi.Outline(panel, Color.OrangeRed);

        LobbyUi.Text(GuiData.font, "TEAMS", panel.X + 16, panel.Y + 10, Color.White);
        LobbyUi.Text(GuiData.tinyfont, "Same as the team command. Esc to close.", panel.X + 120, panel.Y + 22, Color.Gray);
        if (Button.doButton(IdClose, panel.Right - 96, panel.Y + 10, 80, 26, "Close", Color.Gray))
            Close();

        var teams = TeamPlayer.Teams;
        int me = TeamPlayer.MyPlayerId();

        // 默认选中：指定的任务 → 我的组队中队伍 → 我所在的已开始队伍 → 第一支
        var selected = teams.FirstOrDefault(t => t.Key == _selectedKey)
                    ?? TeamPlayer.MyFormingTeam
                    ?? teams.FirstOrDefault(t => t.Find(me) != null)
                    ?? teams.FirstOrDefault();

        int top = panel.Y + 50;
        int bottom = panel.Bottom - 40;
        int listW = (int)(w * 0.40f);
        var listArea = new Rectangle(panel.X + 12, top, listW, bottom - top);
        var detailArea = new Rectangle(listArea.Right + 14, top, panel.Right - 12 - (listArea.Right + 14), bottom - top);

        DrawList(listArea, teams, selected, me);
        LobbyUi.Fill(new Rectangle(listArea.Right + 6, top, 1, bottom - top), Color.Gray * 0.4f);

        if (selected != null)
        {
            DrawDetail(detailArea, selected, me);
        }
        else if (_selectedKey != null)
        {
            LobbyUi.Text(GuiData.smallfont, "Nobody has locked this mission yet.", detailArea.X, detailArea.Y, Color.Gray);
            LobbyUi.Text(GuiData.tinyfont, "Use Form Team on the mission page (or Accept) to lock it and lead a team.",
                detailArea.X, detailArea.Y + 26, Color.Gray);
        }
        else
        {
            LobbyUi.Text(GuiData.smallfont, "No teams yet.", detailArea.X, detailArea.Y, Color.Gray);
            LobbyUi.Text(GuiData.tinyfont, "Accept a team mission on a mission board to lock it and lead a team.",
                detailArea.X, detailArea.Y + 26, Color.Gray);
        }

        // 底部：最近的提示
        string note = _localNote.Length > 0 ? _localNote : TeamPlayer.LastMessage;
        if (note.Length > 0)
            LobbyUi.Text(GuiData.tinyfont, TeamStrip.Fit(GuiData.tinyfont, note, w - 30), panel.X + 16, panel.Bottom - 28, Color.LightGray);
    }

    private void DrawList(Rectangle area, List<TeamView> teams, TeamView? selected, int me)
    {
        LobbyUi.Text(GuiData.smallfont, "ALL TEAMS", area.X, area.Y, Color.LightGreen);
        int y = area.Y + 28;
        for (int i = 0; i < teams.Count && y + 44 <= area.Bottom; i++)
        {
            var t = teams[i];
            bool isSel = t == selected;
            bool mineTeam = t.Find(me) != null;
            string state = t.Started ? "STARTED" : $"{t.Members.Count}/{t.Slots}";
            string label = TeamStrip.Fit(GuiData.tinyfont, $"[{t.Name}] {t.Title}", area.Width - 70);

            var row = new Rectangle(area.X, y, area.Width - 6, 40);
            if (isSel) LobbyUi.Fill(row, Color.White * 0.08f);
            if (Button.doButton(IdListBase + i, row.X, row.Y, row.Width, 20, label, mineTeam ? Color.LightGreen * 0.6f : (Color?)null))
            {
                _selectedKey = t.Key;
                _localNote = "";
            }
            LobbyUi.Text(GuiData.tinyfont, $"{state}  leader: {Leader(t)}{(mineTeam ? "  (you)" : "")}",
                row.X + 4, row.Y + 22, t.Started ? Color.Gray : Color.White);
            y += 46;
        }
        if (teams.Count == 0)
            LobbyUi.Text(GuiData.tinyfont, "(none)", area.X, y, Color.Gray);
    }

    private void DrawDetail(Rectangle area, TeamView t, int me)
    {
        var mine = t.Find(me);
        bool leader = t.LeaderId == me;
        bool forming = !t.Started;

        LobbyUi.Text(GuiData.smallfont, TeamStrip.Fit(GuiData.smallfont, $"[{t.Name}]  {t.Title}", area.Width), area.X, area.Y, Color.White);
        string sub = t.Started
            ? "Mission started."
            : $"{t.Members.Count}/{t.Slots} players, confirmed {t.ConfirmedCount}/{t.Slots}.  Leader: {Leader(t)}";
        LobbyUi.Text(GuiData.tinyfont, sub, area.X, area.Y + 26, t.Started ? Color.Gray : Color.LightGray);

        // 位置
        int y = area.Y + 54;
        for (int s = 0; s < t.Slots; s++)
        {
            var m = t.Members.FirstOrDefault(x => x.Slot == s);
            var row = new Rectangle(area.X, y, area.Width, 26);
            LobbyUi.Fill(row, Color.White * (m?.PlayerId == me ? 0.08f : 0.03f));
            LobbyUi.Text(GuiData.tinyfont, $"Slot {s + 1}", row.X + 6, row.Y + 6, Color.Gray);

            if (m == null)
            {
                LobbyUi.Text(GuiData.tinyfont, "(empty)", row.X + 70, row.Y + 6, Color.DimGray);
                if (forming && mine != null && Button.doButton(IdSlotBase + s, row.Right - 90, row.Y + 3, 84, 20, "Take slot", Color.LightBlue))
                    Run($"slot {s + 1}");
            }
            else
            {
                string who = m.Name + (m.PlayerId == t.LeaderId ? "  [leader]" : "") + (m.PlayerId == me ? "  (you)" : "");
                LobbyUi.Text(GuiData.tinyfont, TeamStrip.Fit(GuiData.tinyfont, who, row.Width - 230), row.X + 70, row.Y + 6, Color.White);
                if (forming)
                    LobbyUi.Text(GuiData.tinyfont, m.Confirmed ? "CONFIRMED" : "not confirmed", row.Right - 190, row.Y + 6,
                        m.Confirmed ? Color.LightGreen : Color.Gray);
                if (forming && leader && m.PlayerId != me &&
                    Button.doButton(IdKickBase + s, row.Right - 70, row.Y + 3, 64, 20, "Kick", new Color(200, 80, 80)))
                    Run($"kick P{m.PlayerId}");
            }
            y += 30;
        }

        if (!forming) return;

        // 操作按钮（和组队条一致）
        var actions = new List<(string Label, Color Color, string Command)>();
        if (mine != null)
        {
            actions.Add(mine.Confirmed ? ("Unconfirm", Color.Gray, "unconfirm") : ("Confirm", Color.LightGreen, "confirm"));
            if (leader)
            {
                bool ready = t.IsFull && t.ConfirmedCount == t.Slots;
                actions.Add(("Submit", ready ? Color.Orange : Color.DimGray, ready ? "submit" : ""));
                actions.Add(("Disband", new Color(200, 80, 80), "disband"));
            }
            else
            {
                actions.Add(("Leave", new Color(200, 80, 80), "leave"));
            }
        }
        else if (!t.IsFull && TeamPlayer.MyFormingTeam == null)
        {
            actions.Add(("Join team", Color.LightBlue, "join " + t.Name));
        }

        int bx = area.X;
        int by = Math.Min(y + 12, area.Bottom - 30);
        for (int i = 0; i < actions.Count; i++)
        {
            var (label, color, cmd) = actions[i];
            if (Button.doButton(IdActionBase + i, bx, by, 130, 28, label, color))
            {
                if (cmd.Length == 0) _localNote = "Submit needs a full team and everyone confirmed.";
                else Run(cmd);
            }
            bx += 140;
        }

        if (leader)
            LobbyUi.Text(GuiData.tinyfont, "Rename: team name <new-name>", area.X, by + 36, Color.Gray);
    }

    private void Run(string line)
    {
        _localNote = TeamPlayer.SendCommand(line) ?? "";
    }

    private static string Leader(TeamView t) =>
        t.Find(t.LeaderId)?.Name ?? (t.LeaderId == 0 ? "Server" : $"P{t.LeaderId}");
}
