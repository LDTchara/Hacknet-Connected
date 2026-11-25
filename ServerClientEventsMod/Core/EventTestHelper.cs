using System.IO;
using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

/// <summary>
/// 事件日志辅助 —— 订阅所有事件，输出到 <c>event_debug_log.txt</c>。
/// 用于测试触发是否正常。上线时注释掉或删除此文件。
/// </summary>
internal static class EventTestHelper
{
    private static StreamWriter _log;

    internal static void StartLogging()
    {
        string path = Path.Combine(Path.GetDirectoryName(path: typeof(EventTestHelper).Assembly.Location), "event_debug_log.txt");
        _log = new StreamWriter(path, append: true) { AutoFlush = true };
        _log.WriteLine($"=== Event Test Log Started @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        _log.WriteLine();

        SubscribeAll();
    }

    static void Write(string label, string detail)
    {
        _log.WriteLine($"[{OS.currentElapsedTime:F2}s] {label}: {detail}");
    }

    static void SubscribeAll()
    {
        PlayerEventAPI.OnConnected += (c, o) =>
            Write("CONNECT", $"comp={c.name} ({c.ip})");

        PlayerEventAPI.OnDisconnected += (ip, o) =>
            Write("DISCONNECT", $"ipFrom={ip}");

        PlayerEventAPI.OnLoggedIn += (c, o, u, a) =>
            Write("LOGIN", $"comp={c.name} user={u} admin={a}");

        PlayerEventAPI.OnPortChanged += (c, o, dp, cp, open) =>
            Write("PORT", $"comp={c.name} port={dp}(code={cp}) {(open ? "OPEN" : "CLOSE")}");

        PlayerEventAPI.OnProxyChanged += (c, o, s, remain, start) =>
            Write("PROXY", $"comp={c.name} state={s} remain={remain:F2}/{start:F2}");

        PlayerEventAPI.OnFirewallChanged += (c, o, s, fw) =>
            Write("FIREWALL", $"comp={c.name} state={s} solved={fw?.solved}");

        PlayerEventAPI.OnNodeVisibilityChanged += (c, o, v) =>
            Write("VISIBILITY", $"comp={c.name} visible={v}");

        PlayerEventAPI.OnNodeIPChanged += (c, o, old, nw) =>
            Write("IP_CHANGE", $"comp={c.name} {old} → {nw}");

        PlayerEventAPI.OnFileSystemChanged += (c, o, a, p, ip) =>
            Write("FILE", $"action={a} path={p} sourceIP={ip}");

        PlayerEventAPI.OnActionTriggered += (a, o, t, x) =>
            Write("ACTION", $"type={t} xml={x?.Replace("\r\n", " ")}");

        PlayerEventAPI.On<PlayerMissionEvent>(e =>
            Write("MISSION", $"action={e.Action} file={e.MissionFile} next={e.NextMission} func={e.EndFunctionName}"));

        PlayerEventAPI.On<PlayerFunctionEvent>(e =>
            Write("FUNCTION", $"name={e.FunctionName} value={e.Value}"));

        PlayerEventAPI.On<PlayerFactionEvent>(e =>
            Write("FACTION", $"action={e.Action} name={e.FactionName} value={e.PlayerValue}/{e.NeededValue} rankup={e.IsRankUp}"));

        PlayerEventAPI.On<PlayerFlagEvent>(e =>
            Write("FLAG", $"action={e.Action} flag={e.Flag}"));

        PlayerEventAPI.On<PlayerMailEvent>(e =>
            Write("MAIL", $"to={e.Receiver} from={e.Sender} subj={e.Subject} attaches={e.Attachments.Count}"));

        PlayerEventAPI.On<PlayerModuleEvent>(e =>
            Write("MODULE", $"action={e.Action} name={e.Name} visible={e.Visible}"));

        PlayerEventAPI.On<PlayerCommandEvent>(e =>
            Write("COMMAND", $"cmd={e.Command} args={string.Join(" ", e.Args)}"));
    }
}
