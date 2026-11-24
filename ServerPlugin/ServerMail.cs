using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Hacknet;
using Pathfinder.Command;

namespace ServerPlugin;

/// <summary>
/// 服务端游戏（主机玩家）的邮件收发。客户端那一半见 ClientPlugin/PlayerMail.cs，协议说明见 README.PlayerMail.md。
///
/// 服务端不是终端里的一个 LiteNetLib 连接，所以走 Pipe：
///   客户端 → 服务端：终端收到 MAIL_SEND 且收件人 id 为 0 → Pipe: MAIL_TO_SERVER:{发件人id}:{b64 名}:{b64 主题}:{b64 正文}
///   服务端 → 客户端：sendmail 命令 → Pipe: MAIL_OUT:{收件人id}:{b64 主题}:{b64 正文}
///                    终端校验在线后转给客户端，并回 MAIL_RESULT:{收件人id}:{ok|offline}
///   在线列表：终端每次玩家变化时推 PLAYERS:{id}:{name},{id}:{name}…
/// 服务端在客户端眼里是 0 号玩家，名字 "Server"。
/// </summary>
internal static class ServerMail
{
    private const int PendingTimeoutMs = 120_000;
    public const int MaxSubjectChars = 80;
    public const int MaxBodyChars = 2000;

    private static readonly Dictionary<int, string> OnlinePlayers = new();

    private sealed class Pending
    {
        public string From = "";
        public string Subject = "";
        public string Body = "";
        public long ReceivedAt;
    }

    private static readonly List<Pending> PendingMails = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static void RegisterCommand() => CommandManager.RegisterCommand("sendmail", HandleCommand);

    // ── Pipe 消息（主线程，ProcessPendingMessages 里先于 ':' 拆分调用） ──

    public static bool TryHandlePipeMessage(string msg)
    {
        if (msg.StartsWith("PLAYERS:", StringComparison.Ordinal))
        {
            UpdateOnline(msg.Substring("PLAYERS:".Length));
            return true;
        }
        if (msg.StartsWith("MAIL_TO_SERVER:", StringComparison.Ordinal))
        {
            OnMail(msg);
            return true;
        }
        if (msg.StartsWith("MAIL_RESULT:", StringComparison.Ordinal))
        {
            OnResult(msg);
            return true;
        }
        return false;
    }

    private static void UpdateOnline(string list)
    {
        OnlinePlayers.Clear();
        foreach (var item in list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int c = item.IndexOf(':');
            if (c > 0 && int.TryParse(item.Substring(0, c), out int id))
                OnlinePlayers[id] = item.Substring(c + 1);
        }
    }

    /// <summary>MAIL_TO_SERVER:{fromId}:{b64 fromName}:{b64 subject}:{b64 body}</summary>
    private static void OnMail(string msg)
    {
        var p = msg.Split(new[] { ':' }, 5);
        if (p.Length != 5) return;
        try
        {
            PendingMails.Add(new Pending
            {
                From = Un64(p[2]),
                Subject = Un64(p[3]),
                Body = Un64(p[4]),
                ReceivedAt = Clock.ElapsedMilliseconds,
            });
            Update();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMail] 邮件解码失败: {ex.Message}");
        }
    }

    /// <summary>MAIL_RESULT:{toId}:{ok|offline}</summary>
    private static void OnResult(string msg)
    {
        var p = msg.Split(new[] { ':' }, 3);
        int.TryParse(p.Length > 1 ? p[1] : "", out int toId);
        bool ok = p.Length > 2 && p[2] == "ok";
        Say(ok ? $"[Mail] 已发送给 {NameOf(toId)}" : $"[Mail] 发送给 {NameOf(toId)} 失败：对方不在线");
    }

    // ── 投递到服务端自己的 jmail ──

    /// <summary>每帧调用。</summary>
    public static void Update()
    {
        if (PendingMails.Count == 0) return;
        long now = Clock.ElapsedMilliseconds;
        for (int i = PendingMails.Count - 1; i >= 0; i--)
        {
            var p = PendingMails[i];
            if (TryDeliver(p)) PendingMails.RemoveAt(i);
            else if (now - p.ReceivedAt > PendingTimeoutMs)
            {
                Console.WriteLine($"[ServerMail] 邮件投递超时，已丢弃: {p.Subject} (from {p.From})");
                PendingMails.RemoveAt(i);
            }
        }
    }

    private static bool TryDeliver(Pending p)
    {
        var os = OS.currentInstance;
        if (os?.netMap?.mailServer == null) return false;

        var mail = os.netMap.mailServer.getDaemon(typeof(MailServer)) as MailServer;
        if (mail == null) return false;

        string user = os.defaultUser.name;
        if (string.IsNullOrEmpty(user)) return false;
        if (mail.accounts?.folders == null || !mail.accounts.folders.Any(f => f != null && f.name.Equals(user)))
            return false;

        try
        {
            mail.addMail(MailServer.generateEmail(p.Subject, p.Body, p.From), user);
            os.write($"[Mail] 收到来自 {p.From} 的邮件：{p.Subject}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMail] 投递失败: {ex.Message}");
            return false;
        }
    }

    // ── sendmail 命令（服务端玩家用） ──

    private static void HandleCommand(OS os, string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
        {
            os.write("═══ 在线玩家 ═══");
            if (OnlinePlayers.Count == 0) os.write("  （暂无）");
            foreach (var kv in OnlinePlayers.OrderBy(k => k.Key))
                os.write($"  [{kv.Key}] {kv.Value}");
            return;
        }

        if (args.Length < 4)
        {
            os.write("用法:");
            os.write("  sendmail list                          — 列出在线玩家");
            os.write("  sendmail <玩家名|编号> <主题> <正文...>  — 发邮件（主题里的 _ 显示为空格）");
            return;
        }

        string subject = args[2].Replace('_', ' ');
        string body = string.Join(" ", args.Skip(3));

        int toId;
        if (!int.TryParse(args[1], out toId))
        {
            var matches = OnlinePlayers.Where(kv => string.Equals(kv.Value, args[1], StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
            {
                os.write(matches.Count == 0 ? $"[!] 没有名为 {args[1]} 的在线玩家" : $"[!] 有多个玩家叫 {args[1]}，请用编号");
                return;
            }
            toId = matches[0].Key;
        }
        else if (!OnlinePlayers.ContainsKey(toId))
        {
            os.write($"[!] 没有编号为 {toId} 的在线玩家");
            return;
        }

        if (subject.Length > MaxSubjectChars) { os.write($"[!] 主题太长（最多 {MaxSubjectChars} 字符）"); return; }
        if (body.Length > MaxBodyChars) { os.write($"[!] 正文太长（最多 {MaxBodyChars} 字符）"); return; }
        if (!ServerManager.IsConnected) { os.write("[!] 未连接到控制台"); return; }

        // 必须用 SendToConsole：SendEvent 会把 2 秒内同类消息折叠掉
        ServerManager.SendToConsole($"MAIL_OUT:{toId}:{B64(subject)}:{B64(body)}");
        os.write("[Mail] 已提交，等待控制台确认…");
    }

    // ── 工具 ──

    private static string NameOf(int id) => OnlinePlayers.TryGetValue(id, out var n) ? n : $"玩家#{id}";

    private static void Say(string text)
    {
        Console.WriteLine(text);
        OS.currentInstance?.write(text);
    }

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    private static string Un64(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));
}
