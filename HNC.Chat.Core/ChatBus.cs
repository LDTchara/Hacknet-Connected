using System;
using System.Collections.Generic;
using System.Linq;

namespace HNC.Chat
{
    /// <summary>消息从哪里来。</summary>
    public enum ChatSource
    {
        /// <summary>IRC / DHS 底部的输入框（或 sendto 命令）。</summary>
        InputBox = 0,
        /// <summary>提交 DHS 任务时附带的"附加内容"。</summary>
        MissionResponse = 1,
    }

    /// <summary>玩家发送的一条消息。</summary>
    public sealed class ChatMessage
    {
        /// <summary>全局递增序号，可用来判断"有没有新消息"。</summary>
        public long Sequence;
        /// <summary>频道（IRC / DHS）所在电脑的 idName。</summary>
        public string ComputerId;
        /// <summary>发送者的游戏账户名（os.defaultUser.name）。</summary>
        public string Player;
        /// <summary>正文。输入框里 @IP -p 账户 密码 这类凭据不在其中。</summary>
        public string Text;
        public ChatSource Source;
        /// <summary>随消息发送的附件行（节点 / 账户），原样的 !ATTACHMENT:… 文本。</summary>
        public List<string> Attachments = new List<string>();
        public DateTime Time;
    }

    /// <summary>匹配方式。</summary>
    public enum ChatMatchMode
    {
        /// <summary>片段：消息包含这段文字。</summary>
        Fragment,
        /// <summary>完全：消息与这段文字完全相同（忽略首尾空格）。</summary>
        Exact,
        /// <summary>不包含：消息不包含这段文字。</summary>
        NotContains,
    }

    public static class ChatMatcher
    {
        /// <summary>解析 XML 里的 Match 属性。空 = 片段；认英文名和中文名，忽略大小写。</summary>
        public static bool TryParseMode(string s, out ChatMatchMode mode)
        {
            mode = ChatMatchMode.Fragment;
            if (string.IsNullOrWhiteSpace(s)) return true;
            switch (s.Trim().ToLowerInvariant())
            {
                case "fragment":
                case "contains":
                case "contain":
                case "片段":
                    mode = ChatMatchMode.Fragment;
                    return true;
                case "exact":
                case "equals":
                case "equal":
                case "完全":
                    mode = ChatMatchMode.Exact;
                    return true;
                case "notcontains":
                case "notcontain":
                case "exclude":
                case "不包含":
                    mode = ChatMatchMode.NotContains;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>一条消息是否满足匹配条件。</summary>
        public static bool Matches(string text, string pattern, ChatMatchMode mode, bool ignoreCase)
        {
            text = text ?? "";
            pattern = pattern ?? "";
            var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            switch (mode)
            {
                case ChatMatchMode.Exact:
                    return string.Equals(text.Trim(), pattern.Trim(), cmp);
                case ChatMatchMode.NotContains:
                    return text.IndexOf(pattern, cmp) < 0;
                default:
                    return text.IndexOf(pattern, cmp) >= 0;
            }
        }
    }

    /// <summary>禁言状态的存储。游戏里用存档标记（随存档保存），测试里用内存。</summary>
    public interface IChatMuteStore
    {
        bool IsMuted(string computerId, string player);
        void SetMuted(string computerId, string player, bool muted);
    }

    public sealed class MemoryMuteStore : IChatMuteStore
    {
        private readonly HashSet<string> _set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string Key(string c, string p) { return (c ?? "") + "\n" + (p ?? ""); }

        public bool IsMuted(string computerId, string player) { lock (_set) return _set.Contains(Key(computerId, player)); }

        public void SetMuted(string computerId, string player, bool muted)
        {
            lock (_set)
            {
                if (muted) _set.Add(Key(computerId, player));
                else _set.Remove(Key(computerId, player));
            }
        }
    }

    /// <summary>
    /// 聊天中间层：其他代码通过它按 computerId（电脑的 idName）读取玩家每次从输入框发出的消息，
    /// 也可以订阅 <see cref="MessageSent"/>，以及查询 / 设置禁言。
    ///
    /// 范围：只记录**本机玩家**发出的消息（每个客户端各自一份）；历史在内存里，换存档 / 重启后清空。
    /// 需要跨存档的判断（条件 / 目标）会再结合 IRC 日志（见 Shared/IrcChat/ChatHistory）。
    /// 线程安全；订阅者抛出的异常会被吞掉，不影响发送。
    /// </summary>
    public static class ChatBus
    {
        public const int MaxHistory = 2000;

        private static readonly object Gate = new object();
        private static readonly List<ChatMessage> History = new List<ChatMessage>();
        private static long _sequence;
        private static IChatMuteStore _muteStore = new MemoryMuteStore();

        /// <summary>每发出一条消息触发一次（在发送者的线程里同步调用）。</summary>
        public static event Action<ChatMessage> MessageSent;

        /// <summary>禁言存储。游戏层在启动时换成存档标记版本。</summary>
        public static IChatMuteStore MuteStore
        {
            get { lock (Gate) return _muteStore; }
            set { lock (Gate) _muteStore = value ?? new MemoryMuteStore(); }
        }

        /// <summary>发布一条消息（由发送入口调用）。</summary>
        public static ChatMessage Publish(string computerId, string player, string text, ChatSource source,
            IEnumerable<string> attachments = null)
        {
            ChatMessage m;
            lock (Gate)
            {
                m = new ChatMessage
                {
                    Sequence = ++_sequence,
                    ComputerId = computerId ?? "",
                    Player = player ?? "",
                    Text = text ?? "",
                    Source = source,
                    Time = DateTime.Now,
                };
                if (attachments != null) m.Attachments.AddRange(attachments);
                History.Add(m);
                if (History.Count > MaxHistory) History.RemoveRange(0, History.Count - MaxHistory);
            }

            var handler = MessageSent;
            if (handler != null)
            {
                foreach (Action<ChatMessage> d in handler.GetInvocationList())
                {
                    try { d(m); }
                    catch { /* 订阅者的错误不能影响发送 */ }
                }
            }
            return m;
        }

        /// <summary>某台电脑上（可选：某个玩家）发出的全部消息，按发送顺序。</summary>
        public static IReadOnlyList<ChatMessage> GetMessages(string computerId, string player = null)
        {
            lock (Gate)
            {
                return History
                    .Where(m => string.Equals(m.ComputerId, computerId, StringComparison.OrdinalIgnoreCase)
                             && (player == null || string.Equals(m.Player, player, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        /// <summary>最近一条；没有返回 null。</summary>
        public static ChatMessage Last(string computerId, string player = null)
        {
            var list = GetMessages(computerId, player);
            return list.Count == 0 ? null : list[list.Count - 1];
        }

        /// <summary>是否有一条消息满足匹配条件（只看内存历史）。</summary>
        public static bool HasSent(string computerId, string player, string pattern, ChatMatchMode mode, bool ignoreCase)
        {
            return GetMessages(computerId, player).Any(m => ChatMatcher.Matches(m.Text, pattern, mode, ignoreCase));
        }

        /// <summary>换存档 / 新世界时清空内存历史。</summary>
        public static void Clear()
        {
            lock (Gate) History.Clear();
        }

        // ── 禁言 ──

        /// <summary>玩家名写 "*" 表示该频道所有人。</summary>
        public const string Everyone = "*";

        public static bool IsMuted(string computerId, string player)
        {
            var store = MuteStore;
            return store.IsMuted(computerId, player) || store.IsMuted(computerId, Everyone);
        }

        public static void SetMuted(string computerId, string player, bool muted)
        {
            MuteStore.SetMuted(computerId, player, muted);
        }
    }
}
