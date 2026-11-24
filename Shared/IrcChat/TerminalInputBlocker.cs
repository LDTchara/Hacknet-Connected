using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using HNC.Chat;

namespace HNC.IrcChat;

/// <summary>
/// 输入栏有焦点时屏蔽游戏自带的终端输入。
///
/// 原因：TextBox.doTerminalTextField 每一帧都**无条件**读键盘（它里面判断焦点的那行被编译器优化成了空语句 `_ = GuiData.active`），
/// 字符来自 GuiData.getFilteredKeys——一个读一次就清空的全局缓冲区。所以输入栏和终端会抢同一批字符，
/// Enter 还会同时让终端执行当前那一行命令。
///
/// 做法（两处补丁，都只在 IrcInputBar.IsTyping 时生效）：
///   1. getFilteredStringInput 直接返回原文本：终端读不到字符，缓冲区留给输入栏。
///   2. doTerminalTextField 之后把它在这一帧里设置的 Enter / 上 / 下 / Tab 标志清掉，并恢复光标位置
///      （它检测到 Enter 会把共享的光标位置重置为 0）。
/// </summary>
[HarmonyPatch]
internal static class TerminalInputBlocker
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TextBox), "getFilteredStringInput")]
    private static bool SkipTerminalTyping(string s, ref string __result)
    {
        if (!IrcInputBar.IsTyping) return true;
        __result = s;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TextBox), nameof(TextBox.doTerminalTextField))]
    private static void BeforeTerminalField(out int[] __state)
    {
        __state = new[] { TextBox.cursorPosition, TextBox.textDrawOffsetPosition };
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextBox), nameof(TextBox.doTerminalTextField))]
    private static void AfterTerminalField(int[] __state)
    {
        if (!IrcInputBar.IsTyping) return;
        TextBox.BoxWasActivated = false;
        TextBox.UpWasPresed = false;
        TextBox.DownWasPresed = false;
        TextBox.TabWasPresed = false;
        TextBox.cursorPosition = __state[0];
        TextBox.textDrawOffsetPosition = __state[1];
    }
}

/// <summary>
/// @ 提及的红色高亮：提到自己（@你的名字）或 @everyone 的那一行，画上和原版公告（!ANNOUNCEMENT!）一样的红色底。
/// 原版聊天里唯一的红色高亮就是公告那一种，这里沿用同样的矩形和透明度。
/// DrawLine 画的是自动换行之后的一"行"，所以一条跨多行的消息只有包含 @ 的那一行会高亮。
/// </summary>
[HarmonyPatch(typeof(Hacknet.Daemons.Helpers.IRCSystem), "DrawLine")]
internal static class MentionHighlightPatch
{
    [HarmonyPrefix]
    private static void Prefix(string line, Microsoft.Xna.Framework.Rectangle dest,
        Microsoft.Xna.Framework.Graphics.SpriteBatch sb)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (line.StartsWith(Attachments.Prefix) || line.StartsWith("!ANNOUNCEMENT!")) return; // 这两种原版自己处理

        var os = OS.currentInstance;
        if (os == null || !Mentions.IsMentioned(line, os.defaultUser.name)) return;

        // 和 IRCSystem.DrawLine 里公告那一支完全相同的位置和大小
        var pos = Utils.ClipVec2ForTextRendering(new Microsoft.Xna.Framework.Vector2(
            dest.X, dest.Y + dest.Height - (GuiData.ActiveFontConfig.tinyFontCharHeight + 1f)));
        var rect = new Microsoft.Xna.Framework.Rectangle((int)pos.X - 2, (int)pos.Y, dest.Width + 2,
            (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 6f));
        sb.Draw(Utils.white, rect, Microsoft.Xna.Framework.Color.Red * 0.22f);
    }
}
