using System;
using System.Diagnostics;
using Hacknet;
using Hacknet.Daemons.Helpers;
using HNC.Chat;

namespace HNC.IrcChat;

/// <summary>把玩家输入的消息发到一个 IRC / DHS 频道。</summary>
internal static class IrcSender
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static IRCSystem? _lastSystem;
    private static long _lastSeenMs;

    /// <summary>输入框每帧调用，记下玩家正在看的频道，sendto 命令用。</summary>
    public static void NoteViewing(IRCSystem sys)
    {
        _lastSystem = sys;
        _lastSeenMs = Clock.ElapsedMilliseconds;
    }

    /// <summary>
    /// 玩家当前的频道：优先是最近 3 秒内画出来的那个（正在看的）；
    /// 否则是当前连接的电脑上的 IRC / DHS。找不到返回 null。
    /// </summary>
    public static IRCSystem? FindCurrent(OS os)
    {
        if (_lastSystem != null && Clock.ElapsedMilliseconds - _lastSeenMs < 3000)
            return _lastSystem;

        var comp = os.connectedComp;
        if (comp == null) return null;
        if (comp.getDaemon(typeof(IRCDaemon)) is IRCDaemon irc && irc.System != null) return irc.System;
        if (comp.getDaemon(typeof(DLCHubServer)) is DLCHubServer dhs && dhs.IRCSystem != null) return dhs.IRCSystem;
        return null;
    }

    /// <summary>
    /// 解析并发送。失败时用 os.write 提示原因并返回 false，此时什么都没有发出。
    /// 发送者是 os.defaultUser；日志会被同步层带给其他玩家（IRC 和 DHS 都已适配）。
    /// </summary>
    public static bool TrySend(IRCSystem sys, OS os, string input)
    {
        string author = os.defaultUser.name;
        if (string.IsNullOrEmpty(author)) author = "Player";

        // 被禁止在这个频道发言：整条不发送（对应 BanChatInput 动作）
        string computerId = IrcLocator.FindOwner(os, sys)?.idName ?? "";
        if (computerId.Length > 0 && ChatBus.IsMuted(computerId, author))
        {
            os.write("[!] 你被禁止在此频道通过输入框发言");
            return false;
        }

        var plan = MessageParser.Parse(input, new GameWorld(os, Array.Empty<string>()));
        if (plan.Error != null)
        {
            os.write("[!] " + plan.Error);
            return false;
        }

        // 自己发的消息不需要再弹一次"新消息"提示
        var handler = sys.LogAdded;
        sys.LogAdded = null;
        try
        {
            // IRC 日志用 "\n#" 分隔条目：多行消息里某一行以 # 开头会把一条消息拆成两条，所以给它前面垫个空格
            if (!string.IsNullOrEmpty(plan.Text))
                sys.AddLog(author, plan.Text.Replace("\n#", "\n #"));
            foreach (var attachment in plan.Attachments)
                sys.AddLog(author, attachment);
        }
        finally
        {
            sys.LogAdded = handler;
        }

        // 通知中间层：其他代码（条件 / 目标 / 别的 mod）靠它拿到玩家发出的每一条消息
        ChatBus.Publish(computerId, author, plan.Text, ChatSource.InputBox, plan.Attachments);
        return true;
    }
}
