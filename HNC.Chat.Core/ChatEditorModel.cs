using System;

namespace HNC.Chat
{
    /// <summary>
    /// 多行输入框的编辑模型：文本 + 光标 + 增删 / 移动。不依赖游戏，界面只负责把按键翻译成这里的调用并把结果画出来。
    /// 换行用 '\n'；Shift+Enter 插入换行、Enter 发送由界面层决定。
    /// </summary>
    public sealed class ChatEditorModel
    {
        public const int MaxLines = 8;

        private string _text = "";
        private int _cursor;

        public int MaxLength { get; set; } = MessageParser.MaxLength;
        public string Text { get { return _text; } }
        /// <summary>光标位置：0 .. Text.Length，表示"插在第几个字符之前"。</summary>
        public int Cursor { get { return _cursor; } }
        public bool IsEmpty { get { return _text.Length == 0; } }

        public int LineCount
        {
            get
            {
                int n = 1;
                foreach (char c in _text) if (c == '\n') n++;
                return n;
            }
        }

        public string[] Lines() { return _text.Split('\n'); }

        /// <summary>光标所在的行号和行内列号（都从 0 开始）。</summary>
        public void GetCursorPosition(out int line, out int col)
        {
            line = 0;
            int lastBreak = -1;
            for (int i = 0; i < _cursor; i++)
            {
                if (_text[i] == '\n') { line++; lastBreak = i; }
            }
            col = _cursor - (lastBreak + 1);
        }

        public void Set(string text, int cursor)
        {
            _text = text ?? "";
            _cursor = Math.Max(0, Math.Min(cursor, _text.Length));
        }

        public void Clear() { Set("", 0); }

        /// <summary>
        /// 在光标处插入文字（输入法 / 粘贴 / 打字）。控制字符被丢弃；\r\n 和 \r 当作换行；
        /// 超过 MaxLength 或 MaxLines 的部分被忽略。返回是否真的插入了字符。
        /// </summary>
        public bool Insert(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Replace("\r\n", "\n").Replace('\r', '\n');

            bool any = false;
            foreach (char c in s)
            {
                if (_text.Length >= MaxLength) break;
                if (c == '\n')
                {
                    if (LineCount >= MaxLines) continue;
                }
                else if (char.IsControl(c) || c == '\u007f')
                {
                    continue;
                }
                _text = _text.Insert(_cursor, c.ToString());
                _cursor++;
                any = true;
            }
            return any;
        }

        public bool InsertNewline() { return Insert("\n"); }

        public void Backspace()
        {
            if (_cursor <= 0) return;
            _text = _text.Remove(_cursor - 1, 1);
            _cursor--;
        }

        public void Delete()
        {
            if (_cursor >= _text.Length) return;
            _text = _text.Remove(_cursor, 1);
        }

        public void Left() { if (_cursor > 0) _cursor--; }
        public void Right() { if (_cursor < _text.Length) _cursor++; }

        /// <summary>移到当前行行首。</summary>
        public void Home()
        {
            int line, col;
            GetCursorPosition(out line, out col);
            _cursor -= col;
        }

        /// <summary>移到当前行行尾。</summary>
        public void End()
        {
            int nl = _text.IndexOf('\n', _cursor);
            _cursor = nl < 0 ? _text.Length : nl;
        }

        /// <summary>上一行，尽量保持列；已经在第一行就移到文本开头。</summary>
        public void Up()
        {
            int line, col;
            GetCursorPosition(out line, out col);
            if (line == 0) { _cursor = 0; return; }
            MoveTo(line - 1, col);
        }

        /// <summary>下一行，尽量保持列；已经在最后一行就移到文本末尾。</summary>
        public void Down()
        {
            int line, col;
            GetCursorPosition(out line, out col);
            if (line >= LineCount - 1) { _cursor = _text.Length; return; }
            MoveTo(line + 1, col);
        }

        /// <summary>移到指定行列（行 / 列越界时夹到合法范围）。鼠标点击也用它。</summary>
        public void MoveTo(int line, int col)
        {
            string[] lines = Lines();
            line = Math.Max(0, Math.Min(line, lines.Length - 1));
            col = Math.Max(0, Math.Min(col, lines[line].Length));
            int start = 0;
            for (int i = 0; i < line; i++) start += lines[i].Length + 1;
            _cursor = start + col;
        }
    }

    /// <summary>消息里的 @ 提及判断，用来给提到自己（或所有人）的那一行加红色高亮。</summary>
    public static class Mentions
    {
        /// <summary>这一行里有没有 @玩家名 或 @everyone。</summary>
        public static bool IsMentioned(string line, string playerName)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (Contains(line, "everyone")) return true;
            return !string.IsNullOrEmpty(playerName) && Contains(line, playerName);
        }

        // 名字以英文字母 / 数字 / 下划线结尾时，后面不能紧跟同类字符（@Bob 不算提到 Bobby）；
        // 中文名后面通常直接接文字，没有空格，所以不做这个限制。@ 前面不能是字母数字（排除邮箱 a@b）。
        private static bool Contains(string line, string name)
        {
            string needle = "@" + name;
            bool needBoundary = IsAsciiWord(needle[needle.Length - 1]);
            int i = 0;
            while ((i = line.IndexOf(needle, i, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = i + needle.Length;
                bool startOk = i == 0 || !char.IsLetterOrDigit(line[i - 1]);
                bool endOk = !needBoundary || end >= line.Length || !IsAsciiWord(line[end]);
                if (startOk && endOk) return true;
                i++;
            }
            return false;
        }

        private static bool IsAsciiWord(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
        }
    }
}
