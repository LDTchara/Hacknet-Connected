using System;
using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HNC.Lobby;

/// <summary>
/// 服务端、客户端两个大厅界面共用的绘制代码（只编进两个插件，终端不编译这个文件）。
/// 界面文字用英文：Hacknet 默认字体不一定带中文字形。
/// 名字等外来文字一律先经过 Clean：字体里没有的字符换成 '?'，否则 SpriteFont 会直接抛异常。
/// </summary>
internal static class LobbyUi
{
    public const int RowHeight = 26;

    public static string Clean(string? s) =>
        string.IsNullOrEmpty(s) ? "" : Utils.CleanStringToLanguageRenderable(s!);

    public static void Fill(Rectangle r, Color c) => GuiData.spriteBatch.Draw(Utils.white, r, c);

    public static void Outline(Rectangle r, Color c)
    {
        Fill(new Rectangle(r.X, r.Y, r.Width, 1), c);
        Fill(new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        Fill(new Rectangle(r.X, r.Y, 1, r.Height), c);
        Fill(new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    public static void Text(SpriteFont font, string text, float x, float y, Color c) =>
        GuiData.spriteBatch.DrawString(font, Clean(text), new Vector2(x, y), c);

    public static void Centered(SpriteFont font, string text, int centerX, float y, Color c)
    {
        string t = Clean(text);
        var size = font.MeasureString(t);
        GuiData.spriteBatch.DrawString(font, t, new Vector2(centerX - size.X / 2f, y), c);
    }

    /// <summary>大厅界面布局：左边名单（约一半宽），右边聊天框；上方留标题，下方留按钮行</summary>
    public static void TwoColumns(Rectangle viewport, out Rectangle roster, out Rectangle chat)
    {
        int margin = Math.Max(16, (int)(viewport.Width * 0.05f));
        int top = 130;
        int bottom = viewport.Height - 110;
        int rosterWidth = (int)(viewport.Width * 0.5f);
        roster = new Rectangle(margin, top, rosterWidth, bottom - top);
        int chatX = margin + rosterWidth + 30;
        chat = new Rectangle(chatX, top, viewport.Width - margin - chatX, bottom - top);
    }

    public static string CapacityText(LobbyState s) =>
        s.Capacity > 0 ? $"{s.Count} / {s.Capacity}" : $"{s.Count} (no limit)";

    /// <summary>
    /// 画玩家名单：表头、服务端主机一行、每个客户端一行。
    /// rowExtra 用来在客户端那一行右侧加按钮（服务端的 Kick）。返回名单下方的 y。
    /// </summary>
    public static float DrawRoster(LobbyState state, Rectangle area, int selfPlayerId,
        Action<LobbyMember, Rectangle>? rowExtra = null)
    {
        var font = GuiData.smallfont;
        float statusX = area.X + area.Width * 0.55f;
        float y = area.Y;
        bool showReady = state.Phase == LobbyPhase.Lobby;

        Text(font, "PLAYER", area.X + 8, y, Color.LightGreen);
        Text(font, showReady ? "STATUS" : "", statusX, y, Color.LightGreen);
        y += RowHeight;
        Fill(new Rectangle(area.X, (int)y - 4, area.Width, 1), Color.Gray * 0.5f);

        // 服务端主机
        Text(font, "[SERVER] " + state.HostName, area.X + 8, y, Color.White);
        DrawStatus(font, statusX, y, showReady, state.HostReady, state.Phase);
        y += RowHeight;

        if (state.Members.Count == 0)
        {
            Text(font, "(no clients yet)", area.X + 8, y, Color.Gray);
            y += RowHeight;
        }

        foreach (var m in state.Members)
        {
            var row = new Rectangle(area.X, (int)y - 2, area.Width, RowHeight);
            if (m.PlayerId == selfPlayerId)
                Fill(row, Color.White * 0.06f);

            string label = $"[P{m.PlayerId}] {m.Name}" + (m.PlayerId == selfPlayerId ? "  (you)" : "");
            Text(font, label, area.X + 8, y, Color.White);
            DrawStatus(font, statusX, y, showReady, m.Ready, state.Phase);
            rowExtra?.Invoke(m, row);
            y += RowHeight;
        }
        return y;
    }

    private static void DrawStatus(SpriteFont font, float x, float y, bool showReady, bool ready, LobbyPhase phase)
    {
        if (showReady)
            Text(font, ready ? "READY" : "NOT READY", x, y, ready ? Color.LightGreen : Color.Gray);
        else if (phase == LobbyPhase.Launching)
            Text(font, "STARTING", x, y, Color.Yellow);
        else if (phase == LobbyPhase.InGame)
            Text(font, "IN GAME", x, y, Color.LightBlue);
    }

    /// <summary>屏幕中央的对话框底板（深色底 + 边框），返回框的矩形</summary>
    public static Rectangle DialogBox(Rectangle viewport, int width, int height)
    {
        Fill(viewport, Color.Black * 0.6f);
        var box = new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
        Fill(box, new Color(12, 12, 20));
        Outline(box, Color.OrangeRed);
        return box;
    }
}
