using System;
using Hacknet;
using Pathfinder.Event;
using ServerClientEventsMod.Events;

namespace ServerPlugin;

/// <summary>
/// 事件转发器——将 ServerClientEventsMod 捕获的游戏事件通过 Pipe 发给控制台
/// </summary>
internal static class EventForwarder
{
    private static bool _subscribed;

    /// <summary>临时抑制事件转发，防止 ApplyFileAction 触发自反事件形成死循环</summary>
    public static bool Suppress { get; set; }

    public static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;

        // ── 文件系统事件 ──
        EventManager<PlayerFileSystemEvent>.AddHandler(e =>
        {
            if (Suppress) return;
            string contentPart = "";
            if (e.Content != null && (e.Action == FileSystemAction.FileAdded || e.Action == FileSystemAction.FileContentChanged))
            {
                contentPart = $":{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(e.Content ?? ""))}";
            }

            string movePart = "";
            if (e.Action == FileSystemAction.FileMoved && e.FromName != null && e.ToName != null)
                movePart = $":{e.FromName}:{e.ToName}";

            var encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(e.Path ?? ""));
            var msg = $"EVENT:FILE:{e.Action}:{e.Computer?.idName}:{encodedPath}:{e.SourceIP}{movePart}{contentPart}";
            ServerManager.SendEvent(msg);
        });

        // ── 登录/登出事件 ──
        EventManager<PlayerLoginCompEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:LOGIN:{e.Computer?.idName}:{e.Username}:{e.IsAdmin}"));

        // ── 连接/断开事件 ──
        EventManager<PlayerConnectToCompEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:CONNECT:{e.Computer?.idName}:{e.Computer?.ip}"));

        EventManager<PlayerDisconnectFromCompEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:DISCONNECT:{e.IPFrom}"));

        // ── 端口 / 防火墙 / 代理：不再作为 EVENT 转发 ──
        // 默认每人自己破解；带 <HNCShared/> 的共享节点由 NodeSync（NODE_RPT / NODE_STATE）同步。
        // （以前的 EVENT:PORT / FIREWALL / PROXY 收发格式对不上，实际一直没生效。）

        // ── 节点可见性变化（已禁用——每个节点在两 IP 间按帧来回跳，无意义） ──
        // EventManager<PlayerNodeVisibilityEvent>.AddHandler(e =>
        //     ServerManager.SendEvent(
        //         $"EVENT:NODE_VIS:{e.Computer?.idName}:{e.Visible}"));

        // ── 节点 IP 变化（已禁用——同上，两 IP 间往返 bounce-back 已由 NetMapDetector 去重） ──
        // EventManager<PlayerNodeIPChangedEvent>.AddHandler(e =>
        //     ServerManager.SendEvent(
        //         $"EVENT:NODE_IP:{e.Computer?.idName}:{e.OldIP}:{e.NewIP}"));

        // ── 动作 / flag：不再作为 EVENT 转发 ──
        // 每个玩家本机都在跑同一套剧情，再把动作复制给别人会让公开动作执行多次（邮件、IRC 消息重复）。
        // 动作的效果由各自的同步负责：文件 EVENT:FILE、Daemon HNC.Sync、flag FlagSync（FLAG_SET）。

        // ── 任务事件 ──
        EventManager<PlayerMissionEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:MISSION:{e.Action}:{e.MissionID}:{e.MissionFile}"));

        // ── 邮件事件 ──
        EventManager<PlayerMailEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:MAIL:{e.Sender}:{e.Subject}:{e.Body}:{string.Join("|", e.Attachments ?? new List<string>())}"));

        // ── 函数事件 ──
        EventManager<PlayerFunctionEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:FUNCTION:{e.FunctionName}:{e.Value}"));

        // ── 命令事件 ──
        EventManager<PlayerCommandEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:COMMAND:{e.Command}:{string.Join(" ", e.Args ?? Array.Empty<string>())}"));

        // ── 游戏保存事件 → 通知客户端上传存档（30 秒冷却） ──
        ServerClientEventsMod.Events.PlayerEventAPI.OnSaveGame += OnGameSave;

        Console.WriteLine("[EventForwarder] Subscribed to all events.");
    }

    private static DateTime _lastSaveNotify = DateTime.MinValue;
    private static void OnGameSave(Hacknet.OS os)
    {
        if (!ServerManager.IsConnected) return;
        if (DateTime.Now - _lastSaveNotify < TimeSpan.FromSeconds(30))
        {
            Console.WriteLine("[EventForwarder] Save notify skipped (cooldown)");
            return;
        }
        _lastSaveNotify = DateTime.Now;
        ServerManager.SendEvent("EVENT:SAVE_REQUIRED");
        Console.WriteLine("[EventForwarder] Save detected -> SAVE_REQUIRED broadcast");
    }
}
