using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Hacknet;
using Pathfinder.Command;

namespace ClientPlugin;

/// <summary>
/// 玩家之间的邮件。
///
/// 流程（不经过 EVENT: 广播，所以内容只会到收件人那里）：
///   发件人  sendmail 命令 → MAIL_SEND:{收件人id}:{b64 主题}:{b64 正文}
///   终端    校验长度/收件人在线，盖上真实发件人（来自连接身份，客户端无法伪造）
///           → 收件人收到 MAIL:{发件人id}:{b64 发件人名}:{b64 主题}:{b64 正文}
///           → 发件人收到 MAIL_SENT:{收件人id}  或  MAIL_FAIL:{收件人id}:{原因}
///   收件人  把邮件写进自己的 jmail 收件箱（netMap.mailServer，账户名 = os.defaultUser.name）
///
/// 邮件存在收件人自己存档里的 jmail 里，所以随存档保存。收件人不在线时发送会失败（没有离线投递）。
/// </summary>
internal static class PlayerMail
{
    public const int MaxSubjectChars = 80;
    public const int MaxBodyChars = 2000;
    private const int PendingTimeoutMs = 120_000;

    /// <summary>在线玩家（来自服务端 PLAYER_LIST），id → 名字。</summary>
    public static readonly Dictionary<int, string> OnlinePlayers = new();

    private sealed class Pending
    {
        public string From = "";
        public string Subject = "";
        public string Body = "";
        public long ReceivedAt;
    }

    private static readonly List<Pending> PendingMails = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    // ── 在线列表 ──

    /// <summary>PLAYER_LIST:{id}:{name},{id}:{name}…</summary>
    public static void UpdateOnline(string list)
    {
        OnlinePlayers.Clear();
        foreach (var item in list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int c = item.IndexOf(':');
            if (c <= 0) continue;
            if (int.TryParse(item.Substring(0, c), out int id))
                OnlinePlayers[id] = item.Substring(c + 1);
        }
    }

    // ── 发送 ──

    /// <summary>返回 null 表示已发出；否则是要显示给玩家的错误。</summary>
    public static string? Send(int toId, string subject, string body)
    {
        if (!ClientNetworkManager.IsConnected) return "未连接到服务器";
        if (toId == ClientNetworkManager.PlayerId) return "不能给自己发邮件";
        if (string.IsNullOrWhiteSpace(subject)) return "主题不能为空";
        if (string.IsNullOrWhiteSpace(body)) return "正文不能为空";
        if (subject.Length > MaxSubjectChars) return $"主题太长（最多 {MaxSubjectChars} 字符）";
        if (body.Length > MaxBodyChars) return $"正文太长（最多 {MaxBodyChars} 字符）";

        ClientNetworkManager.Send($"MAIL_SEND:{toId}:{B64(subject)}:{B64(body)}");
        return null;
    }

    // ── 接收（EventReceiver 调用，主线程） ──

    /// <summary>MAIL:{fromId}:{b64 fromName}:{b64 subject}:{b64 body}</summary>
    public static void OnReceived(string msg)
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
            Update(); // 能马上投递就马上投递
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerMail] 邮件解码失败: {ex.Message}");
        }
    }

    /// <summary>MAIL_SENT:{toId}</summary>
    public static void OnSent(string msg)
    {
        int toId = ParseId(msg, "MAIL_SENT:");
        Say($"[Mail] 已发送给 {NameOf(toId)}");
    }

    /// <summary>MAIL_FAIL:{toId}:{reason}</summary>
    public static void OnFailed(string msg)
    {
        var p = msg.Split(new[] { ':' }, 3);
        int.TryParse(p.Length > 1 ? p[1] : "", out int toId);
        string reason = p.Length > 2 ? p[2] : "";
        string text = reason switch
        {
            "offline" => "对方不在线",
            "self" => "不能给自己发邮件",
            "toolong" => "邮件太长",
            _ => "服务器拒绝了这封邮件",
        };
        Say($"[Mail] 发送给 {NameOf(toId)} 失败：{text}");
    }

    // ── 投递 ──

    /// <summary>每帧调用：把待投递邮件写进 jmail。世界还没加载好、或账户还不存在时留在队列里。</summary>
    public static void Update()
    {
        if (PendingMails.Count == 0) return;

        long now = Clock.ElapsedMilliseconds;
        for (int i = PendingMails.Count - 1; i >= 0; i--)
        {
            var p = PendingMails[i];
            if (TryDeliver(p))
            {
                PendingMails.RemoveAt(i);
            }
            else if (now - p.ReceivedAt > PendingTimeoutMs)
            {
                Console.WriteLine($"[PlayerMail] 邮件投递超时，已丢弃: {p.Subject} (from {p.From})");
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
        // 账户还没建好（刚进游戏）就再等等；addMail 对无账户会抛 NRE
        if (mail.accounts?.folders == null ||
            !mail.accounts.folders.Any(f => f != null && f.name.Equals(user)))
            return false;

        try
        {
            mail.addMail(MailServer.generateEmail(p.Subject, p.Body, p.From), user);
            os.write($"[Mail] 收到来自 {p.From} 的邮件：{p.Subject}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerMail] 投递失败: {ex.Message}");
            return false;
        }
    }

    // ── 工具 ──

    private static string NameOf(int id) =>
        OnlinePlayers.TryGetValue(id, out var n) ? n : $"玩家#{id}";

    private static int ParseId(string msg, string prefix)
    {
        int.TryParse(msg.Substring(prefix.Length), out int id);
        return id;
    }

    private static void Say(string text)
    {
        Console.WriteLine(text);
        OS.currentInstance?.write(text);
    }

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    private static string Un64(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));
}

/// <summary>
/// sendmail 命令。
///   sendmail list                       列出在线玩家
///   sendmail &lt;玩家名或编号&gt; &lt;主题&gt; &lt;正文...&gt;
/// 主题是一个"词"（命令行按空格拆分），用下划线代替空格，投递时会显示成空格；正文是剩下的所有内容。
/// </summary>
public static class SendMailCommand
{
    private const string CommandName = "sendmail";

    public static void Register()
    {
        CommandManager.RegisterCommand(CommandName, Handle);
    }

    private static void Handle(OS os, string[] args)
    {
        // args[0] 是命令名本身（与 selectplayer 一致）
        if (args.Length >= 2 && string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
        {
            ListPlayers(os);
            return;
        }

        if (args.Length < 4)
        {
            os.write("用法:");
            os.write($"  {CommandName} list                          — 列出在线玩家");
            os.write($"  {CommandName} <玩家名|编号> <主题> <正文...>  — 发邮件（主题里的 _ 显示为空格）");
            return;
        }

        string target = args[1];
        string subject = args[2].Replace('_', ' ');
        string body = string.Join(" ", args.Skip(3));

        if (!TryResolve(target, out int toId, out string? error))
        {
            os.write($"[!] {error}");
            os.write($"    输入 {CommandName} list 查看在线玩家");
            return;
        }

        string? sendError = PlayerMail.Send(toId, subject, body);
        if (sendError != null)
            os.write($"[!] 发送失败：{sendError}");
        else
            os.write("[Mail] 已提交，等待服务器确认…");
    }

    private static void ListPlayers(OS os)
    {
        if (!ClientNetworkManager.IsConnected)
        {
            os.write("[!] 未连接到服务器");
            return;
        }
        os.write("═══ 在线玩家 ═══");
        if (PlayerMail.OnlinePlayers.Count == 0)
            os.write("  （暂无数据）");
        foreach (var kv in PlayerMail.OnlinePlayers.OrderBy(k => k.Key))
        {
            string me = kv.Key == ClientNetworkManager.PlayerId ? "  ← 你" : "";
            os.write($"  [{kv.Key}] {kv.Value}{me}");
        }
    }

    /// <summary>按编号或名字（忽略大小写）找在线玩家；名字有歧义时要求用编号。</summary>
    private static bool TryResolve(string target, out int id, out string? error)
    {
        id = 0;
        error = null;

        if (int.TryParse(target, out int byId))
        {
            if (PlayerMail.OnlinePlayers.ContainsKey(byId)) { id = byId; return true; }
            error = $"没有编号为 {byId} 的在线玩家";
            return false;
        }

        var matches = PlayerMail.OnlinePlayers
            .Where(kv => string.Equals(kv.Value, target, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1) { id = matches[0].Key; return true; }
        error = matches.Count == 0 ? $"没有名为 {target} 的在线玩家" : $"有多个玩家叫 {target}，请用编号";
        return false;
    }
}
