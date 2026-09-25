using System.Reflection;
using Hacknet;
using HarmonyLib;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerMailEvent : PathfinderEvent
{
    public string Receiver { get; }       // 收件人（userTo）
    public string Sender { get; }         // 发件人
    public string Subject { get; }        // 主题
    public string Body { get; }           // 正文
    public List<string> Attachments { get; } // 附件列表
    public OS Os { get; }
    public string CurrentPlayerIP { get; }

    public PlayerMailEvent(string receiver, string sender, string subject,
        string body, List<string> attachments, OS os)
    {
        Receiver = receiver;
        Sender = sender;
        Subject = subject;
        Body = body;
        Attachments = attachments;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
    }
}

internal static class MailInterceptor
{
    private static readonly string _delim = "@*&^#%@)_!_)*#^@!&*)(#^&\n";
    private static readonly string[] _delims;

    static MailInterceptor()
    {
        _delims = new string[] { _delim };
    }

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("Hacknet.MailServer");
        return AccessTools.Method(type, "addMail");
    }

    [HarmonyPrefix]
    static void OnAddMail(string mail, string userTo)
    {
        var os = OS.currentInstance;
        if (os == null) return;

        string[] parts = mail.Split(_delims, StringSplitOptions.None);
        if (parts.Length < 4) return;

        // parts[0] = type (0=unread), parts[1] = sender, parts[2] = subject, parts[3] = body
        var attachments = new List<string>();
        for (int i = 4; i < parts.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(parts[i]))
                attachments.Add(parts[i]);
        }

        EventManager<PlayerMailEvent>.InvokeAll(
            new PlayerMailEvent(userTo, parts[1], parts[2], parts[3], attachments, os));
    }
}
