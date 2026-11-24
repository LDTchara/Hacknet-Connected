using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hacknet;
using Hacknet.Daemons.Helpers;
using Hacknet.Gui;
using HarmonyLib;
using HNC.Chat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HNC.IrcChat;

/// <summary>
/// 在 IRC / DHS 界面最下面加多行输入框和发送按钮。
/// 两种界面都通过 IRCSystem.Draw 画聊天记录，所以只补这一个方法：
/// Prefix 把记录区缩短（输入框有几行就缩短几行），Postfix 在腾出来的位置画输入栏。
///
/// 操作：Enter 发送，Shift+Enter 换行；@ 弹出候选，Tab 补全（多个候选时循环），也可以点击候选。
/// 输入框是自己实现的（不用原版的 TextBox.doTextBox）：原版那个只能单行，而且用的是 ASCII 过滤过的按键，输入不了中文。
/// 有焦点时会屏蔽终端的输入，见 TerminalInputBlocker。
/// </summary>
[HarmonyPatch(typeof(IRCSystem), nameof(IRCSystem.Draw))]
internal static class IrcInputBar
{
    private const int MaxVisibleLines = 5;
    private const int MinLogHeight = 90;
    private const int RowHeight = 20;
    private const int MaxCandidates = 6;

    // GuiData 的 hot/active 靠整数 id 区分控件，取一段不太可能和原版/其他 mod 撞的
    private const int BoxId = 70113001;
    private const int ButtonId = 70113002;
    private const int CandidateId = 70113010;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly ChatEditorModel Model = new();

    private static WeakReference? _owner;
    private static string _ownerComputerId = "";
    private static bool _focused;
    private static bool _errorLogged;
    private static long _lastDrawMs = long.MinValue / 2;
    private static int _scrollLine;
    private static int _frames;
    private static Rectangle _lastListRect = Rectangle.Empty;

    // Tab 循环：第一次按 Tab 时记下原文和光标，之后每按一次换下一个候选
    private static List<Candidate>? _cycle;
    private static int _cycleIndex;
    private static string _cycleText = "";
    private static string _origText = "";
    private static int _origCursor;

    /// <summary>
    /// 玩家正在这个输入框里打字。要求最近 200 毫秒内画过它：
    /// 切换到别的界面后输入栏不再被画，焦点 id 还留在 GuiData.active 里，不能因此一直屏蔽终端。
    /// </summary>
    public static bool IsTyping => GuiData.active == BoxId && Clock.ElapsedMilliseconds - _lastDrawMs < 200;

    private static int LineHeight() => (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 8f);
    private static int VisibleLines() => Math.Min(Model.LineCount, MaxVisibleLines);
    private static int BarHeight() => VisibleLines() * LineHeight() + 8;

    [HarmonyPrefix]
    private static void Prefix(ref Rectangle dest, bool CanWrite, out Rectangle __state)
    {
        __state = Rectangle.Empty;
        int bar = BarHeight();
        if (CanWrite || dest.Height < MinLogHeight + bar) return;
        __state = dest;
        dest.Height -= bar;
    }

    [HarmonyPostfix]
    private static void Postfix(IRCSystem __instance, Rectangle __state, SpriteBatch sb,
        Dictionary<string, Color> HighlightKeywords)
    {
        if (__state.Width <= 0) return;
        try
        {
            DrawBar(__instance, __state, sb, HighlightKeywords);
        }
        catch (Exception ex)
        {
            // 绘制异常会让游戏崩溃（Game1.Draw 里没有兜底），所以只记一次日志然后跳过
            if (!_errorLogged)
            {
                _errorLogged = true;
                Console.WriteLine($"[IrcChat] 输入栏绘制失败，已跳过: {ex}");
            }
        }
    }

    private static void DrawBar(IRCSystem sys, Rectangle area, SpriteBatch sb, Dictionary<string, Color> users)
    {
        var os = OS.currentInstance;
        if (os == null) return;

        // 换了一个频道就清掉没发出去的内容
        if (_owner == null || !ReferenceEquals(_owner.Target, sys))
        {
            _owner = new WeakReference(sys);
            Model.Clear();
            _scrollLine = 0;
            _ownerComputerId = IrcLocator.FindOwner(os, sys)?.idName ?? "";
            ResetCycle();
        }
        IrcSender.NoteViewing(sys);
        _lastDrawMs = Clock.ElapsedMilliseconds;
        _frames++;

        var font = GuiData.tinyfont;
        int lineH = LineHeight();
        int visible = VisibleLines();
        int boxH = visible * lineH + 4;
        int buttonW = 64;
        int gap = 4;
        int boxW = area.Width - buttonW - gap;
        var boxRect = new Rectangle(area.X, area.Y + area.Height - boxH - 2, boxW, boxH);

        // ── 焦点和鼠标点击 ──
        var mouse = GuiData.getMousePoint();
        bool hot = boxRect.Contains(mouse);
        if (hot) GuiData.hot = BoxId;
        else if (GuiData.hot == BoxId) GuiData.hot = -1;

        bool pressed = GuiData.mouseWasPressed();
        if (pressed)
        {
            if (hot)
            {
                GuiData.active = BoxId;
                _focused = true;
                ClickToCursor(boxRect, mouse, font, lineH, boxW - 10);
                ResetCycle();
            }
            else if (GuiData.active == BoxId && !_lastListRect.Contains(mouse))
            {
                GuiData.active = -1;
            }
        }

        // ── 键盘 ──
        bool send = false;
        bool tab = false;
        if (GuiData.active == BoxId)
        {
            GuiData.willBlockTextInput = true; // 原版的快捷键（Utils.keyPressed）也会因此让开
            HandleKeys(ref send, ref tab);
        }

        // ── Tab 补全 ──
        var source = new GameWorld(os, users.Keys);
        if (tab) DoTab(source);

        // ── 候选列表 ──
        List<Candidate> candidates;
        int selected = -1;
        if (_cycle != null && string.Equals(Model.Text, _cycleText, StringComparison.Ordinal))
        {
            candidates = _cycle;
            selected = _cycleIndex;
        }
        else
        {
            _cycle = null;
            candidates = _focused
                ? Completer.Suggest(Model.Text, Model.Cursor, source, MaxCandidates)
                : new List<Candidate>();
        }

        // ── 画输入框 ──
        DrawBox(sb, font, boxRect, lineH, visible, hot);

        // ── 画候选（在输入框上方） ──
        _lastListRect = Rectangle.Empty;
        if (candidates.Count > 0)
        {
            int shown = Math.Min(candidates.Count, MaxCandidates);
            int width = Math.Min(area.Width, 380);
            var listRect = new Rectangle(area.X, boxRect.Y - shown * RowHeight - 2, width, shown * RowHeight);
            _lastListRect = listRect;
            sb.Draw(Utils.white, listRect, Color.Black * 0.92f);

            for (int i = 0; i < shown; i++)
            {
                string label = (i == selected ? "> " : "  ") + candidates[i].Display;
                if (Button.doButton(CandidateId + i, listRect.X, listRect.Y + i * RowHeight, width, RowHeight - 1, label, null))
                {
                    PickCandidate(candidates[i]);
                    break;
                }
            }
        }

        // 点到输入框和候选列表以外的地方 → 收起候选
        if (pressed && !boxRect.Contains(mouse) && !_lastListRect.Contains(mouse)) _focused = false;

        // ── 发送按钮 ──
        int buttonH = lineH + 4;
        bool clicked = Button.doButton(ButtonId, area.X + boxW + gap, boxRect.Y + boxH - buttonH, buttonW, buttonH, "Send", null);
        if (send || clicked)
        {
            if (!string.IsNullOrWhiteSpace(Model.Text) && IrcSender.TrySend(sys, os, Model.Text))
            {
                Model.Clear();
                _scrollLine = 0;
                ResetCycle();
            }
            // 发完（或出错）后继续在里面打字
            GuiData.active = BoxId;
            _focused = true;
        }
    }

    // ═══════════════════════ 键盘 ═══════════════════════

    private static void HandleKeys(ref bool send, ref bool tab)
    {
        var kb = GuiData.getKeyboadState();
        var last = GuiData.getLastKeyboadState();

        // 文字（输入法、粘贴）
        if (Model.Insert(ChatInput.TakeText())) ResetCycle();

        // Enter 发送；Shift+Enter 换行
        if (ChatInput.Pressed(Keys.Enter, kb, last))
        {
            if (ChatInput.ShiftDown(kb)) { if (Model.InsertNewline()) ResetCycle(); }
            else send = true;
        }

        if (ChatInput.Pressed(Keys.Tab, kb, last)) tab = true;

        // 编辑和移动（支持按住重复）
        if (ChatInput.Triggered(Keys.Back, kb, last)) { Model.Backspace(); ResetCycle(); }
        if (ChatInput.Triggered(Keys.Delete, kb, last)) { Model.Delete(); ResetCycle(); }
        if (ChatInput.Triggered(Keys.Left, kb, last)) Model.Left();
        if (ChatInput.Triggered(Keys.Right, kb, last)) Model.Right();
        if (ChatInput.Triggered(Keys.Up, kb, last)) Model.Up();
        if (ChatInput.Triggered(Keys.Down, kb, last)) Model.Down();
        if (ChatInput.Triggered(Keys.Home, kb, last)) Model.Home();
        if (ChatInput.Triggered(Keys.End, kb, last)) Model.End();
    }

    // ═══════════════════════ 绘制 ═══════════════════════

    private static void DrawBox(SpriteBatch sb, SpriteFont font, Rectangle box, int lineH, int visible, bool hot)
    {
        bool active = GuiData.active == BoxId;
        sb.Draw(Utils.white, box,
            active ? GuiData.Default_Lit_Backing_Color : (hot ? GuiData.Default_Selected_Color : GuiData.Default_Dark_Background_Color));
        var inner = new Rectangle(box.X + 2, box.Y + 2, box.Width - 4, box.Height - 4);
        sb.Draw(Utils.white, inner, GuiData.Default_Light_Backing_Color);

        string[] lines = Model.Lines();
        Model.GetCursorPosition(out int curLine, out int curCol);

        // 让光标所在行始终可见
        if (curLine < _scrollLine) _scrollLine = curLine;
        if (curLine >= _scrollLine + visible) _scrollLine = curLine - visible + 1;
        _scrollLine = Math.Max(0, Math.Min(_scrollLine, Math.Max(0, lines.Length - visible)));

        int maxW = box.Width - 10;

        if (Model.IsEmpty && !active)
        {
            bool muted = _ownerComputerId.Length > 0 &&
                         ChatBus.IsMuted(_ownerComputerId, OS.currentInstance?.defaultUser.name ?? "");
            string hint = muted
                ? "You are not allowed to send messages here"
                : "message…  Enter: send   Shift+Enter: new line   @: mention (Tab)";
            sb.DrawString(font, hint, new Vector2(box.X + 6, box.Y + 4), muted ? Color.IndianRed * 0.9f : Color.Gray * 0.7f);
            return;
        }

        for (int i = 0; i < visible; i++)
        {
            int lineIndex = _scrollLine + i;
            if (lineIndex >= lines.Length) break;
            string line = lines[lineIndex];
            int y = box.Y + 2 + i * lineH + 2;

            int start = lineIndex == curLine ? WindowStart(line, curCol, maxW, font) : 0;
            string shown = FitRight(line.Substring(start), maxW, font);
            sb.DrawString(font, shown, new Vector2(box.X + 5, y), Color.White);

            if (active && lineIndex == curLine && (_frames % 60 < 40))
            {
                float cx = box.X + 5 + font.MeasureString(line.Substring(start, curCol - start)).X;
                sb.Draw(Utils.white, new Rectangle((int)cx, y, 1, lineH - 4), Color.White);
            }
        }
    }

    /// <summary>光标所在行太长时，从哪个字符开始画才能让光标可见。</summary>
    private static int WindowStart(string line, int col, int maxW, SpriteFont font)
    {
        int start = 0;
        while (start < col && font.MeasureString(line.Substring(start, col - start)).X > maxW - 6) start++;
        return start;
    }

    /// <summary>把文字截到不超过 maxW 宽。</summary>
    private static string FitRight(string text, int maxW, SpriteFont font)
    {
        while (text.Length > 0 && font.MeasureString(text).X > maxW) text = text.Substring(0, text.Length - 1);
        return text;
    }

    private static void ClickToCursor(Rectangle box, Point mouse, SpriteFont font, int lineH, int maxW)
    {
        string[] lines = Model.Lines();
        int lineIndex = _scrollLine + (mouse.Y - (box.Y + 4)) / Math.Max(1, lineH);
        lineIndex = Math.Max(0, Math.Min(lineIndex, lines.Length - 1));
        string line = lines[lineIndex];

        Model.GetCursorPosition(out int curLine, out int curCol);
        int start = lineIndex == curLine ? WindowStart(line, curCol, maxW, font) : 0;

        int col = start;
        float target = mouse.X - (box.X + 5);
        while (col < line.Length && font.MeasureString(line.Substring(start, col + 1 - start)).X <= target) col++;
        Model.MoveTo(lineIndex, col);
    }

    // ═══════════════════════ 补全 ═══════════════════════

    private static void DoTab(ICompletionSource source)
    {
        int cursor = Model.Cursor;

        if (_cycle != null && string.Equals(Model.Text, _cycleText, StringComparison.Ordinal))
        {
            _cycleIndex = (_cycleIndex + 1) % _cycle.Count;
            ApplyCycle();
            return;
        }

        var list = Completer.Suggest(Model.Text, cursor, source, 20);
        if (list.Count == 0) return;

        if (list.Count == 1)
        {
            string text = Completer.Apply(Model.Text, cursor, list[0], true, out int newCursor);
            Model.Set(text, newCursor);
            ResetCycle();
            return;
        }

        _cycle = list;
        _cycleIndex = 0;
        _origText = Model.Text;
        _origCursor = cursor;
        ApplyCycle();
    }

    private static void ApplyCycle()
    {
        if (_cycle == null) return;
        string text = Completer.Apply(_origText, _origCursor, _cycle[_cycleIndex], false, out int newCursor);
        Model.Set(text, newCursor);
        _cycleText = Model.Text;
    }

    /// <summary>鼠标点了一个候选：补全并加空格，焦点回到输入框。</summary>
    private static void PickCandidate(Candidate c)
    {
        string text;
        int newCursor;
        if (_cycle != null && string.Equals(Model.Text, _cycleText, StringComparison.Ordinal))
            text = Completer.Apply(_origText, _origCursor, c, true, out newCursor);
        else
            text = Completer.Apply(Model.Text, Model.Cursor, c, true, out newCursor);

        Model.Set(text, newCursor);
        ResetCycle();
        GuiData.active = BoxId;
        _focused = true;
    }

    private static void ResetCycle()
    {
        _cycle = null;
        _cycleIndex = 0;
        _cycleText = "";
    }
}
