using System;
using System.Collections.Generic;
using System.Text;
using Hacknet;
using HNC.IrcChat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HNC.Lobby;

internal sealed class LobbyChatLine
{
    public int PlayerId;
    public string Name = "";
    public string Text = "";
}

/// <summary>大厅聊天记录（服务端、客户端各一份）</summary>
internal sealed class LobbyChatLog
{
    private const int Max = 100;

    public readonly List<LobbyChatLine> Lines = new();

    /// <summary>每次变化加一，聊天框据此决定是否重新排版</summary>
    public int Version { get; private set; }

    public void Clear()
    {
        Lines.Clear();
        Version++;
    }

    /// <summary>解析 LOBBY_CHAT_MSG:{id}:{b64 名字}:{b64 文字}</summary>
    public void Add(string msg)
    {
        if (!msg.StartsWith(LobbyProtocol.ChatMsg, StringComparison.Ordinal)) return;
        var p = msg.Substring(LobbyProtocol.ChatMsg.Length).Split(new[] { ':' }, 3);
        if (p.Length != 3 || !int.TryParse(p[0], out int id)) return;

        Lines.Add(new LobbyChatLine
        {
            PlayerId = id,
            Name = LobbyProtocol.FromB64(p[1]),
            Text = LobbyProtocol.FromB64(p[2])
        });
        if (Lines.Count > Max) Lines.RemoveAt(0);
        Version++;
    }
}

/// <summary>
/// 大厅界面里的聊天框：右侧一块区域，上面是记录（自动换行，最新的在下面），下面是输入行。
/// 大厅界面打开时游戏里没有终端在读键盘，所以输入一直有焦点：直接打字，Enter 发送，Backspace 删除。
/// 文字读取用 IrcChat 的 ChatInput（读底层缓冲区，支持输入法中文和 Ctrl+V）。
/// </summary>
internal sealed class LobbyChatBox
{
    private readonly StringBuilder _input = new();
    private KeyboardState _last;

    // 排版缓存
    private int _layoutVersion = -1;
    private int _layoutWidth = -1;
    private readonly List<(string Text, Color Color)> _wrapped = new();

    public LobbyChatBox()
    {
        ChatInput.TakeText();   // 丢掉之前界面残留在缓冲区里的输入
        ChatInput.ResetRepeat();
        _last = Keyboard.GetState();
    }

    /// <summary>每帧调用（界面在最上层时）。按 Enter 时返回要发送的文字，否则返回 null。</summary>
    public string? Update()
    {
        var kb = Keyboard.GetState();
        string? send = null;

        foreach (char c in ChatInput.TakeText())
        {
            if (char.IsControl(c)) continue;   // 回车、Tab 等由按键处理
            if (_input.Length < LobbyProtocol.ChatMaxLength) _input.Append(c);
        }

        if (ChatInput.Triggered(Keys.Back, kb, _last) && _input.Length > 0)
            _input.Length--;

        if (ChatInput.Pressed(Keys.Enter, kb, _last))
        {
            string text = LobbyProtocol.CleanChat(_input.ToString());
            _input.Clear();
            if (text.Length > 0) send = text;
        }

        _last = kb;
        return send;
    }

    /// <summary>画聊天框。canSend=false 时输入行显示提示（例如还没加入大厅）。</summary>
    public void Draw(Rectangle area, LobbyChatLog log, bool canSend)
    {
        var font = GuiData.tinyfont;
        int lineH = (int)Math.Ceiling(font.LineSpacing * 1.05f);

        LobbyUi.Fill(area, new Color(8, 10, 16));
        LobbyUi.Outline(area, Color.Gray * 0.6f);
        LobbyUi.Text(GuiData.smallfont, "CHAT", area.X + 8, area.Y + 4, Color.LightGreen);

        // 输入行
        var inputRect = new Rectangle(area.X + 6, area.Bottom - lineH - 10, area.Width - 12, lineH + 4);
        LobbyUi.Fill(inputRect, Color.White * 0.08f);
        string shown;
        if (!canSend)
        {
            shown = "(join the lobby to chat)";
        }
        else
        {
            bool caret = DateTime.Now.Millisecond < 500;
            shown = "> " + LobbyUi.Clean(_input.ToString()) + (caret ? "_" : " ");
            // 太长时只显示末尾
            while (shown.Length > 3 && font.MeasureString(shown).X > inputRect.Width - 8)
                shown = "> ~" + shown.Substring(4);
        }
        GuiData.spriteBatch.DrawString(font, shown, new Vector2(inputRect.X + 4, inputRect.Y + 2),
            canSend ? Color.White : Color.Gray);

        // 记录：从下往上画
        int textWidth = area.Width - 16;
        Relayout(log, font, textWidth);
        int top = area.Y + 30;
        float y = inputRect.Y - 6 - lineH;
        for (int i = _wrapped.Count - 1; i >= 0 && y >= top; i--)
        {
            GuiData.spriteBatch.DrawString(font, _wrapped[i].Text, new Vector2(area.X + 8, y), _wrapped[i].Color);
            y -= lineH;
        }
    }

    private void Relayout(LobbyChatLog log, SpriteFont font, int width)
    {
        if (log.Version == _layoutVersion && width == _layoutWidth) return;
        _layoutVersion = log.Version;
        _layoutWidth = width;
        _wrapped.Clear();

        foreach (var line in log.Lines)
        {
            bool isServer = line.PlayerId == 0;
            string text = (isServer ? "[SERVER] " : "") + line.Name + ": " + line.Text;
            Color color = isServer ? Color.Orange : Color.White;
            foreach (var part in Wrap(font, LobbyUi.Clean(text), width))
                _wrapped.Add((part, color));
        }
    }

    /// <summary>按像素宽度逐字换行（中文没有空格，按字切）</summary>
    private static IEnumerable<string> Wrap(SpriteFont font, string text, int width)
    {
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            sb.Append(c);
            if (sb.Length > 1 && font.MeasureString(sb).X > width)
            {
                sb.Length--;
                yield return sb.ToString();
                sb.Clear();
                if (c != ' ') sb.Append(c);
            }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
