using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HacknetServerTerminal;

/// <summary>
/// Tab 自动补全
/// </summary>
internal static class CommandAutocomplete
{
    private static readonly string[] SetParams = { "Port", "ServerName", "HostAccountName", "HostAccountPassword" };

    public static string? GetCompletion(string input, string state, bool isLoggedIn)
    {
        if (string.IsNullOrEmpty(input)) return null;

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var endsWithSpace = input.EndsWith(" ");

        // 空 + 空格 = 补命令列表
        if (endsWithSpace && parts.Length == 0) return null;

        string prefix;
        string[] candidates;

        if (parts.Length == 1 && !endsWithSpace)
        {
            // ── 正在补全第一个词（命令） ──
            prefix = parts[0];
            candidates = GetCommands(state, isLoggedIn);
        }
        else
        {
            // ── 正在补全第二个词（参数） ──
            var command = parts[0];
            prefix = endsWithSpace || parts.Length == 1 ? "" : parts[^1];

            candidates = command switch
            {
                "set" => SetParams,
                "login" or "create" or "delete" => ServerStorage.FindNames().ToArray(),
                _ => Array.Empty<string>()
            };

            // 第三个词及以后不补全（自由值）
            if (parts.Length > 2 && !endsWithSpace) return null;
            if (endsWithSpace && parts.Length >= 2 && command != "set") return null;
        }

        if (candidates.Length == 0) return null;

        var matches = candidates
            .Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c)
            .ToArray();

        if (matches.Length == 0) return null;

        if (matches.Length == 1)
        {
            if (endsWithSpace || parts.Length > 1)
                return input + matches[0];
            else
                return matchFirstWord(input, prefix, matches[0]);
        }

        // 多个匹配：找公共前缀，自动补上公共部分
        var common = FindCommonPrefix(matches);
        if (common.Length > prefix.Length)
        {
            if (endsWithSpace || parts.Length > 1)
                return null; // 无法安全补全，显示候选项
            else
                return matchFirstWord(input, prefix, common);
        }

        return null; // 需要用户选择
    }

    private static string matchFirstWord(string input, string prefix, string replacement)
    {
        var start = input.Length - prefix.Length;
        return input.Substring(0, start) + replacement;
    }

    private static string[] GetCommands(string state, bool isLoggedIn)
    {
        if (isLoggedIn) return new[] { "start", "lobby", "pause", "resume", "options", "logout", "help", "exit" };

        return state switch
        {
            "create" => new[] { "set", "options", "save", "back", "help" },
            _ => new[] { "list", "login", "create", "delete", "broadcast", "status", "help", "exit" }
        };
    }

    private static string FindCommonPrefix(string[] strs)
    {
        var first = strs[0];
        for (int i = 0; i < first.Length; i++)
        {
            if (!strs.All(s => s.Length > i && s[i] == first[i]))
                return first.Substring(0, i);
        }
        return first;
    }
}

/// <summary>
/// 交互式控制台输入（支持 Tab 补全、命令历史）
/// </summary>
internal static class ConsoleHelper
{
    private static readonly List<string> History = new();
    private static int _historyIndex = -1;

    public static string ReadLine(string prompt)
    {
        Console.Write(prompt);

        var input = new StringBuilder();
        _historyIndex = History.Count;

        while (true)
        {
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    var result = input.ToString();
                    if (!string.IsNullOrEmpty(result) && (History.Count == 0 || History[^1] != result))
                        History.Add(result);
                    return result;

                case ConsoleKey.Tab:
                    var current = input.ToString();
                    var suggestion = CommandAutocomplete.GetCompletion(
                        current, ConsoleState.CurrentState, ConsoleState.IsLoggedIn);

                    if (suggestion != null && suggestion != current)
                    {
                        RedrawLine(prompt + suggestion);
                        input.Clear();
                        input.Append(suggestion);
                    }
                    else if (suggestion == null && current.Length > 0)
                    {
                        // 有多个匹配但没公共前缀，显示可用选项
                        var parts = current.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var isPartial = !current.EndsWith(" ");
                        var prefix = isPartial ? parts[0] : "";

                        var candidates = ConsoleState.CurrentState switch
                        {
                            "create" => new[] { "set", "options", "save", "back", "help" },
                            _ => new[] { "list", "login", "create", "delete", "broadcast", "status", "help", "exit" }
                        };

                        var matches = candidates
                            .Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        if (matches.Length > 1)
                        {
                            Console.WriteLine();
                            Console.WriteLine(string.Join("  ", matches));
                            RedrawLine(prompt + input);
                        }
                    }
                    continue;

                case ConsoleKey.UpArrow:
                    if (_historyIndex > 0)
                    {
                        _historyIndex--;
                        input.Clear();
                        input.Append(History[_historyIndex]);
                        RedrawLine(prompt + input);
                    }
                    continue;

                case ConsoleKey.DownArrow:
                    if (_historyIndex < History.Count - 1)
                    {
                        _historyIndex++;
                        input.Clear();
                        input.Append(History[_historyIndex]);
                        RedrawLine(prompt + input);
                    }
                    else if (_historyIndex == History.Count - 1)
                    {
                        _historyIndex = History.Count;
                        input.Clear();
                        RedrawLine(prompt);
                    }
                    continue;

                case ConsoleKey.Backspace:
                    if (input.Length > 0)
                    {
                        input.Remove(input.Length - 1, 1);
                        RedrawLine(prompt + input);
                    }
                    continue;

                case ConsoleKey.LeftArrow:
                    // 简单忽略方向键导航
                    continue;
                case ConsoleKey.RightArrow:
                    continue;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        input.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                    continue;
            }
        }
    }

    private static void RedrawLine(string text)
    {
        var left = Console.CursorLeft;
        var top = Console.CursorTop;

        // 回到行首，清空整行
        Console.SetCursorPosition(0, top);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, top);

        // 重绘
        if (text.Length > Console.WindowWidth)
            text = text.Substring(0, Console.WindowWidth);
        Console.Write(text);
    }
}

/// <summary>
/// 控制台全局状态
/// </summary>
internal static class ConsoleState
{
    public static string CurrentState { get; set; } = "normal";
    public static bool IsLoggedIn { get; set; }
}
