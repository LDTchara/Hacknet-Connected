using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HNC.Chat
{
    /// <summary>节点上的一个账户。</summary>
    public sealed class AccountRef
    {
        public string User;
        public string Password;
        /// <summary>玩家已经知道（记录）了这个账户。</summary>
        public bool Known;
        public bool IsAdmin;
    }

    /// <summary>一个节点的快照。</summary>
    public sealed class NodeRef
    {
        public string Ip;
        public string Name;
        /// <summary>节点 idName，跨玩家稳定（IP 不一定）。附件里用它定位节点。</summary>
        public string Id;
        public List<AccountRef> Accounts = new List<AccountRef>();
    }

    /// <summary>解析器看到的"世界"。游戏里由 Shared/IrcChat/GameWorld 实现，测试里用假的。</summary>
    public interface IChatWorld
    {
        /// <summary>按 IP 找节点；不存在返回 null。visible 表示玩家是否已经发现了它。</summary>
        NodeRef FindNode(string ip, out bool visible);
    }

    /// <summary>解析结果：要么是 Error（整条消息不发送），要么是正文 + 附件。</summary>
    public sealed class SendPlan
    {
        public string Text = "";
        public List<string> Attachments = new List<string>();
        public string Error;
    }

    /// <summary>IRC 日志里的附件行格式（与原版 AttachmentRenderer 一致，字段用 #%# 分隔）。</summary>
    public static class Attachments
    {
        public const string Prefix = "!ATTACHMENT:";
        private const string Sep = "#%#";

        public static string Link(NodeRef n)
        {
            return Prefix + "link" + Sep + Clean(n.Name, n.Id) + Sep + Clean(n.Id, n.Ip);
        }

        public static string Account(NodeRef n, string user, string pass)
        {
            return Prefix + "account" + Sep + Clean(n.Name, n.Id) + Sep + Clean(n.Id, n.Ip)
                 + Sep + Clean(user, "?") + Sep + Clean(pass, "?");
        }

        // 原版用 RemoveEmptyEntries 拆分，空字段会让后面的下标错位甚至越界，所以不能留空
        private static string Clean(string s, string fallback)
        {
            string c = Strip(s);
            if (c.Length > 0) return c;
            c = Strip(fallback);
            return c.Length > 0 ? c : "?";
        }

        private static string Strip(string s)
        {
            return (s ?? "").Replace(Sep, "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    /// <summary>
    /// 把玩家输入变成"正文 + 附件"。
    ///   @everyone / @用户名      原样留在正文里（提及）
    ///   @1.2.3.4                 正文保留 @IP，并附上该节点（link 附件）
    ///   @1.2.3.4 -p              再附上该节点上你已记录的账户和密码
    ///   @1.2.3.4 -p 账户 密码     手动指定；账户或密码不对 → 整条不发送
    /// -p 后面如果还有至少两个词，就把它们当作账户和密码；否则 -p 单独使用，后面的词仍是正文。
    /// </summary>
    public static class MessageParser
    {
        public const int MaxLength = 300;
        public const int MaxAutoAccounts = 5;
        internal static readonly Regex IpMention = new Regex(@"^@(\d{1,3}(?:\.\d{1,3}){3})$", RegexOptions.Compiled);

        public static SendPlan Parse(string input, IChatWorld world)
        {
            if (string.IsNullOrWhiteSpace(input)) return Fail("消息不能为空");
            input = input.Replace("\r\n", "\n").Replace('\r', '\n');
            if (input.Length > MaxLength) return Fail("消息太长（最多 " + MaxLength + " 字符）");

            var plan = new SendPlan();
            var lines = new List<string>();
            // 一行一行解析：@IP -p 账户 密码 只在同一行内生效，不会把下一行的文字吃掉
            foreach (string rawLine in input.Split('\n'))
            {
                string text;
                string error = ParseLine(rawLine, world, plan, out text);
                if (error != null) return Fail(error);
                lines.Add(text);
            }

            // 去掉开头和结尾的空行（中间的空行保留）
            while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);

            plan.Text = string.Join("\n", lines);
            if (plan.Text.Length == 0 && plan.Attachments.Count == 0) return Fail("消息不能为空");
            return plan;
        }

        /// <summary>解析一行。返回错误文字（整条消息作废）或 null；附件追加到 plan。</summary>
        private static string ParseLine(string line, IChatWorld world, SendPlan plan, out string text)
        {
            text = "";
            var tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var words = new List<string>();

            for (int i = 0; i < tokens.Length; i++)
            {
                var m = IpMention.Match(tokens[i]);
                if (!m.Success)
                {
                    words.Add(tokens[i]);
                    continue;
                }

                string ip = m.Groups[1].Value;
                bool visible;
                NodeRef node = world.FindNode(ip, out visible);
                if (node == null) return "找不到节点 " + ip;
                if (!visible) return "你还没有发现节点 " + ip + "，不能发送它";

                words.Add(tokens[i]);
                plan.Attachments.Add(Attachments.Link(node));

                if (i + 1 < tokens.Length && tokens[i + 1] == "-p")
                {
                    if (i + 3 < tokens.Length)
                    {
                        // 手动：-p 账户 密码
                        string user = tokens[i + 2];
                        string pass = tokens[i + 3];
                        AccountRef acc = node.Accounts.FirstOrDefault(a => a.User == user);
                        if (acc == null) return "节点 " + ip + " 上没有账户 " + user;
                        if (acc.Password != pass) return "账户 " + user + " 的密码不正确";
                        plan.Attachments.Add(Attachments.Account(node, user, pass));
                        i += 3;
                    }
                    else
                    {
                        // 自动：用你已记录的账户
                        var known = node.Accounts
                            .Where(a => a.Known && !string.IsNullOrEmpty(a.User) && !string.IsNullOrEmpty(a.Password))
                            .OrderByDescending(a => a.IsAdmin)
                            .Take(MaxAutoAccounts)
                            .ToList();
                        if (known.Count == 0)
                            return "节点 " + ip + " 上没有你已记录的账户（可用 @" + ip + " -p 账户 密码 手动指定）";
                        foreach (var a in known)
                            plan.Attachments.Add(Attachments.Account(node, a.User, a.Password));
                        i += 1;
                    }
                }
            }

            text = string.Join(" ", words);
            return null;
        }

        private static SendPlan Fail(string error)
        {
            return new SendPlan { Error = error };
        }
    }

    /// <summary>一个补全候选。</summary>
    public sealed class Candidate
    {
        /// <summary>候选列表里显示的文字。</summary>
        public string Display;
        /// <summary>替换光标所在词的文字。</summary>
        public string Replacement;
    }

    /// <summary>补全需要的数据。</summary>
    public interface ICompletionSource
    {
        /// <summary>IRC / DHS 里的用户名。</summary>
        IEnumerable<string> UserNames();
        /// <summary>玩家已发现的节点：(ip, 名字)。</summary>
        IEnumerable<KeyValuePair<string, string>> VisibleNodes();
    }

    /// <summary>@ 提及的 Tab 补全与候选列表。</summary>
    public static class Completer
    {
        /// <summary>取光标所在的词（从上一个空格之后到光标）。</summary>
        public static bool TryGetToken(string text, int cursor, out int start, out string token)
        {
            start = 0;
            token = "";
            if (text == null) return false;
            if (cursor < 0 || cursor > text.Length) cursor = text.Length;
            int space = cursor == 0 ? -1 : text.LastIndexOfAny(new[] { ' ', '\n' }, cursor - 1);
            start = space + 1;
            token = text.Substring(start, cursor - start);
            return true;
        }

        public static List<Candidate> Suggest(string text, int cursor, ICompletionSource src, int max)
        {
            var list = new List<Candidate>();
            int start;
            string token;
            if (src == null || !TryGetToken(text, cursor, out start, out token)) return list;

            if (token.StartsWith("@", StringComparison.Ordinal))
            {
                string p = token.Substring(1);

                if ("everyone".StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    Add(list, "@everyone", "@everyone", token);

                foreach (var u in (src.UserNames() ?? new string[0])
                             .Where(x => !string.IsNullOrEmpty(x) && x.IndexOf(' ') < 0)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(u, "everyone", StringComparison.OrdinalIgnoreCase)) continue;
                    if (u.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        Add(list, "@" + u, "@" + u, token);
                }

                foreach (var n in src.VisibleNodes() ?? new KeyValuePair<string, string>[0])
                {
                    string ip = n.Key;
                    string name = n.Value;
                    bool match = ip.StartsWith(p, StringComparison.Ordinal)
                              || (p.Length > 0 && name != null && name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                    if (match)
                        Add(list, "@" + ip + (string.IsNullOrEmpty(name) ? "" : "   " + name), "@" + ip, token);
                }
            }
            else if (token.StartsWith("-", StringComparison.Ordinal))
            {
                // 紧跟在 @IP 后面的 -p
                string before = text.Substring(0, start).TrimEnd();
                string prev = before.Length == 0 ? "" : before.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
                if (MessageParser.IpMention.IsMatch(prev) && "-p".StartsWith(token, StringComparison.Ordinal))
                    Add(list, "-p   附上账户和密码", "-p", token);
            }

            return list.Take(Math.Max(1, max)).ToList();
        }

        private static void Add(List<Candidate> list, string display, string replacement, string token)
        {
            if (replacement == token) return; // 已经是完整的了，没必要再列出来
            list.Add(new Candidate { Display = display, Replacement = replacement });
        }

        /// <summary>用候选替换光标所在的词；addSpace=true 时在后面补一个空格（后面已经是空格就不重复）。</summary>
        public static string Apply(string text, int cursor, Candidate c, bool addSpace, out int newCursor)
        {
            int start;
            string token;
            TryGetToken(text, cursor, out start, out token);
            if (cursor < 0 || cursor > text.Length) cursor = text.Length;

            string after = text.Substring(cursor);
            bool sepFollows = after.StartsWith(" ", StringComparison.Ordinal) || after.StartsWith("\n", StringComparison.Ordinal);
            string repl = c.Replacement + (addSpace && !sepFollows ? " " : "");
            newCursor = start + repl.Length;
            return text.Substring(0, start) + repl + after;
        }
    }
}
