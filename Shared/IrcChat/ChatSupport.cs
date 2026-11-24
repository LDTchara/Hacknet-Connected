using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hacknet;
using Hacknet.Daemons.Helpers;
using HarmonyLib;
using HNC.Chat;

namespace HNC.IrcChat;

/// <summary>找出某个 IRCSystem 属于哪台电脑，以及某台电脑上有哪些 IRC / DHS 频道。</summary>
internal static class IrcLocator
{
    public static Computer? FindOwner(OS os, IRCSystem sys)
    {
        var nodes = os.netMap?.nodes;
        if (nodes == null) return null;

        foreach (var c in nodes.ToArray())
        {
            if (c?.daemons == null) continue;
            foreach (var d in c.daemons.ToArray())
            {
                if (d is IRCDaemon irc && ReferenceEquals(irc.System, sys)) return c;
                if (d is DLCHubServer dhs && ReferenceEquals(dhs.IRCSystem, sys)) return c;
            }
        }
        return null;
    }

    public static List<IRCSystem> SystemsOn(Computer comp)
    {
        var list = new List<IRCSystem>();
        if (comp?.daemons == null) return list;
        foreach (var d in comp.daemons.ToArray())
        {
            if (d is IRCDaemon irc && irc.System != null) list.Add(irc.System);
            else if (d is DLCHubServer dhs && dhs.IRCSystem != null) list.Add(dhs.IRCSystem);
        }
        return list;
    }
}

/// <summary>
/// 禁言状态存在存档的进度标记（os.Flags）里：随存档保存和读取，任务脚本也能用 HasFlags 看到。
/// 标记名：HNCChatMute:{computerId}:{玩家名 或 *}
/// </summary>
internal sealed class FlagMuteStore : IChatMuteStore
{
    private static string Key(string computerId, string player) =>
        ("HNCChatMute:" + computerId + ":" + player).Replace(',', '_'); // 存档里标记以逗号分隔

    public bool IsMuted(string computerId, string player)
    {
        var flags = OS.currentInstance?.Flags;
        return flags != null && flags.HasFlag(Key(computerId, player));
    }

    public void SetMuted(string computerId, string player, bool muted)
    {
        var flags = OS.currentInstance?.Flags;
        if (flags == null) return;
        string key = Key(computerId, player);
        if (muted)
        {
            if (!flags.HasFlag(key)) flags.AddFlag(key);
        }
        else
        {
            flags.RemoveFlag(key);
        }
    }
}

/// <summary>"这个玩家是不是我"。XML 里的 Player 属性可以写游戏账户名，也可以写角色（p1 / p2…）。</summary>
internal static class ChatIdentity
{
    /// <summary>各插件在 PostLoad 里设置：返回当前客户端 / 主机的角色名。</summary>
    public static Func<string?> RoleProvider = () => null;

    public static string LocalName(OS os) => os.defaultUser.name ?? "";

    /// <summary>空、*、everyone、all 都表示"所有人"。</summary>
    public static bool IsWildcard(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return true;
        string s = spec!.Trim();
        return s == "*" || s.Equals("everyone", StringComparison.OrdinalIgnoreCase) || s.Equals("all", StringComparison.OrdinalIgnoreCase);
    }

    public static bool Matches(OS os, string? spec)
    {
        if (IsWildcard(spec)) return true;
        string s = spec!.Trim();
        if (s.Equals(LocalName(os), StringComparison.OrdinalIgnoreCase)) return true;
        string? role = RoleProvider();
        return !string.IsNullOrEmpty(role) && s.Equals(role, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 一个玩家在某台电脑上"说过的话"：内存里的 ChatBus 历史（含 DHS 任务附加内容）
/// 加上 IRC 日志里署名为该玩家的消息（日志在存档里，所以换存档后输入框发过的话仍然查得到）。
/// DHS 任务附加内容在日志里署名是 Channel、不是玩家，所以只在本次会话的内存历史里。
/// </summary>
internal static class ChatHistory
{
    private static readonly MethodInfo? GetLogs = AccessTools.Method(typeof(IRCSystem), "GetLogsFromFile");

    public static List<string> SentTexts(Computer comp, string player)
    {
        var texts = new List<string>();
        foreach (var m in ChatBus.GetMessages(comp.idName, player))
            texts.Add(m.Text);

        foreach (var sys in IrcLocator.SystemsOn(comp))
            texts.AddRange(ReadLog(sys, player));
        return texts;
    }

    private static IEnumerable<string> ReadLog(IRCSystem sys, string player)
    {
        var result = new List<string>();
        if (GetLogs == null) return result;
        try
        {
            if (GetLogs.Invoke(sys, null) is not IEnumerable entries) return result;
            foreach (object? entry in entries)
            {
                if (entry == null) continue;
                var type = entry.GetType();
                string? author = type.GetField("Author")?.GetValue(entry) as string;
                string? message = type.GetField("Message")?.GetValue(entry) as string;
                if (author == null || message == null) continue;
                if (!author.Equals(player, StringComparison.OrdinalIgnoreCase)) continue;
                if (message.StartsWith(Attachments.Prefix, StringComparison.Ordinal) ||
                    message.StartsWith("!ANNOUNCEMENT!", StringComparison.Ordinal)) continue;
                result.Add(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IrcChat] 读取 IRC 日志失败: {ex.Message}");
        }
        return result;
    }
}

/// <summary>
/// DHS 提交任务时，vanilla 会把"附加内容"以 Channel 的名义写进聊天记录，然后清空列表。
/// 这里在它清空之前取一份，任务确实完成后当作该玩家发出的消息发布到 ChatBus（Source = MissionResponse）。
/// </summary>
[HarmonyPatch(typeof(DLCHubServer), "PlayerAttemptCompleteMission")]
internal static class DhsMissionResponsePatch
{
    private static readonly FieldInfo? ResponsesField = AccessTools.Field(typeof(DLCHubServer), "MissionTextResponses");

    [HarmonyPrefix]
    private static void Prefix(DLCHubServer __instance, out List<string>? __state)
    {
        __state = null;
        try
        {
            if (ResponsesField?.GetValue(__instance) is List<string> list && list.Count > 0)
                __state = new List<string>(list);
        }
        catch { /* 取不到就算了，不影响原逻辑 */ }
    }

    [HarmonyPostfix]
    private static void Postfix(DLCHubServer __instance, bool __result, List<string>? __state)
    {
        if (!__result || __state == null) return;
        var os = OS.currentInstance;
        if (os == null || __instance.comp == null) return;

        string player = ChatIdentity.LocalName(os);
        foreach (string text in __state)
        {
            if (!string.IsNullOrWhiteSpace(text))
                ChatBus.Publish(__instance.comp.idName, player, text, ChatSource.MissionResponse);
        }
    }
}
