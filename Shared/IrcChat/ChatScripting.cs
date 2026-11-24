using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hacknet;
using HNC.Chat;
using Pathfinder.Action;
using Pathfinder.Mission;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace HNC.IrcChat;

/// <summary>
/// 供 Action / Condition / Goal 共用的判断。
/// 只能看到**本机玩家**说过的话（每个客户端各自一份），所以 Player 不是本机玩家时一律为 false。
/// </summary>
internal static class ChatChecks
{
    private static readonly HashSet<string> Warned = new();

    public static void WarnOnce(string who, string message)
    {
        if (Warned.Add(who + message)) Console.WriteLine($"[IrcChat] {who}: {message}");
    }

    public static Computer? FindComputer(OS os, string who, string? spec)
    {
        var comp = string.IsNullOrWhiteSpace(spec) ? null : Programs.getComputer(os, spec);
        if (comp == null) WarnOnce(who, $"找不到电脑 \"{spec}\"");
        return comp;
    }

    public static bool HasSent(OS os, string who, string? computer, string? player, string pattern,
        ChatMatchMode mode, bool ignoreCase, bool negate)
    {
        var comp = FindComputer(os, who, computer);
        if (comp == null) return false;
        if (!ChatIdentity.Matches(os, player)) return false; // 看不到别人说的话

        bool any = false;
        foreach (string text in ChatHistory.SentTexts(comp, ChatIdentity.LocalName(os)))
        {
            if (ChatMatcher.Matches(text, pattern, mode, ignoreCase)) { any = true; break; }
        }
        return negate ? !any : any;
    }

    public static bool ParseBool(string? s, bool fallback) =>
        bool.TryParse(s, out bool b) ? b : fallback;

    /// <summary>Condition.Check / Goal.isComplete 每帧都会被调用，读一遍 IRC 日志不便宜，所以最多每 0.5 秒算一次。</summary>
    public sealed class Throttle
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private long _lastMs = long.MinValue / 2;
        private bool _last;

        public bool Run(Func<bool> compute)
        {
            long now = Clock.ElapsedMilliseconds;
            if (now - _lastMs < 500) return _last;
            _lastMs = now;
            return _last = compute();
        }
    }
}

/// <summary>
/// 禁止 / 解除禁止某个玩家在某个 DHS / IRC 里通过输入框（和 sendto）发消息。
///   &lt;BanChatInput Computer="dhsNode" Player="p2" /&gt;
///   &lt;UnbanChatInput Computer="dhsNode" Player="p2" /&gt;
/// Computer：频道所在电脑（idName / IP / 名字）。Player：游戏账户名或角色（p1…）；* 或不写 = 所有人。
/// 状态存在存档里。被禁止的玩家点发送会看到提示，消息不会发出。
/// 注意：对"所有人"的禁止不会被"解除某个玩家"覆盖，要用同样的写法（Player="*"）解除。
/// </summary>
public abstract class ChatMuteAction : DelayablePathfinderAction
{
    [XMLStorage] public string Computer = "";
    [XMLStorage] public string Player = "";

    protected abstract bool Muted { get; }
    protected abstract string TagName { get; }

    public override void LoadFromXml(ElementInfo info)
    {
        base.LoadFromXml(info);
        if (string.IsNullOrWhiteSpace(Computer))
            throw new FormatException($"{TagName}: 缺少 Computer 属性（频道所在的电脑）");
    }

    public override void Trigger(OS os)
    {
        var comp = ChatChecks.FindComputer(os, TagName, Computer);
        if (comp == null) return;
        if (!ChatIdentity.Matches(os, Player)) return; // 不是我，别的客户端会处理

        string key = ChatIdentity.IsWildcard(Player) ? ChatBus.Everyone : ChatIdentity.LocalName(os);
        ChatBus.SetMuted(comp.idName, key, Muted);
    }
}

public sealed class BanChatInputAction : ChatMuteAction
{
    protected override bool Muted => true;
    protected override string TagName => "BanChatInput";
}

public sealed class UnbanChatInputAction : ChatMuteAction
{
    protected override bool Muted => false;
    protected override string TagName => "UnbanChatInput";
}

/// <summary>
/// 条件：玩家在指定 DHS / IRC 里发送过满足条件的消息。
///   &lt;ChatSent Computer="dhsNode" Player="p2" Message="xxx" Match="Fragment" /&gt;
/// Match：Fragment（片段，默认）/ Exact（完全）/ NotContains（不包含），也认中文"片段/完全/不包含"。
/// 对玩家发过的每条消息逐条判断，只要有**一条**满足就为真；NotContains 就是"有一条不含 xxx 的消息"。
/// IgnoreCase（默认 true）；Negate="true" 把整个结果取反（例如 Fragment + Negate = 从没说过含 xxx 的话）。
/// </summary>
public sealed class ChatSentCondition : PathfinderCondition
{
    [XMLStorage] public string Computer = "";
    [XMLStorage] public string Player = "";
    [XMLStorage] public string Message = "";
    [XMLStorage] public string Match = "";
    [XMLStorage] public string IgnoreCase = "";
    [XMLStorage] public string Negate = "";

    private ChatMatchMode _mode;
    private bool _ignoreCase = true;
    private bool _negate;
    private readonly ChatChecks.Throttle _throttle = new();

    public override void LoadFromXml(ElementInfo info)
    {
        base.LoadFromXml(info);
        if (string.IsNullOrWhiteSpace(Computer)) throw new FormatException("ChatSent: 缺少 Computer 属性");
        if (Message == null) throw new FormatException("ChatSent: 缺少 Message 属性");
        if (!ChatMatcher.TryParseMode(Match, out _mode))
            throw new FormatException($"ChatSent: 未知的 Match \"{Match}\"（可用 Fragment / Exact / NotContains）");
        _ignoreCase = ChatChecks.ParseBool(IgnoreCase, true);
        _negate = ChatChecks.ParseBool(Negate, false);
    }

    public override bool Check(object os_obj)
    {
        var os = (OS)os_obj;
        return _throttle.Run(() => ChatChecks.HasSent(os, "ChatSent", Computer, Player, Message, _mode, _ignoreCase, _negate));
    }
}

/// <summary>
/// 目标：玩家在指定 DHS / IRC 里发送过满足条件的消息。属性和 ChatSent 条件相同。
///   &lt;goal type="chatsent" Computer="dhsNode" Message="xxx" Match="Exact" /&gt;
/// </summary>
public sealed class ChatSentGoal : PathfinderGoal
{
    [XMLStorage] public string Computer = "";
    [XMLStorage] public string Player = "";
    [XMLStorage] public string Message = "";
    [XMLStorage] public string Match = "";
    [XMLStorage] public string IgnoreCase = "";
    [XMLStorage] public string Negate = "";

    private ChatMatchMode _mode;
    private bool _ignoreCase = true;
    private bool _negate;
    private readonly ChatChecks.Throttle _throttle = new();

    public override void LoadFromXML(ElementInfo info)
    {
        base.LoadFromXML(info);
        if (string.IsNullOrWhiteSpace(Computer)) throw new FormatException("chatsent: 缺少 Computer 属性");
        if (Message == null) throw new FormatException("chatsent: 缺少 Message 属性");
        if (!ChatMatcher.TryParseMode(Match, out _mode))
            throw new FormatException($"chatsent: 未知的 Match \"{Match}\"（可用 Fragment / Exact / NotContains）");
        _ignoreCase = ChatChecks.ParseBool(IgnoreCase, true);
        _negate = ChatChecks.ParseBool(Negate, false);
    }

    public override bool isComplete(List<string>? additionalDetails = null)
    {
        var os = OS.currentInstance;
        if (os == null) return false;
        return _throttle.Run(() => ChatChecks.HasSent(os, "chatsent", Computer, Player, Message, _mode, _ignoreCase, _negate));
    }
}
