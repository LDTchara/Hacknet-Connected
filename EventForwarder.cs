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
                string c = e.Content.Length > 8000 ? e.Content.Substring(0, 8000) + "...[TRUNCATED]" : e.Content;
                contentPart = $":{c}";
            }

            string movePart = "";
            if (e.Action == FileSystemAction.FileMoved && e.FromName != null && e.ToName != null)
                movePart = $":{e.FromName}:{e.ToName}";

            var msg = $"EVENT:FILE:{e.Action}:{e.Computer?.idName}:{e.Path}:{e.SourceIP}{movePart}{contentPart}";
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

        // ── 端口事件 ──
        EventManager<PlayerPortEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:PORT:{e.Computer?.idName}:{e.DisplayPort}:{e.CodePort}:{e.IsOpen}"));

        // ── 防火墙事件 ──
        EventManager<PlayerFirewallEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:FIREWALL:{e.Computer?.idName}:{e.State}"));

        // ── 节点可见性变化 ──
        EventManager<PlayerNodeVisibilityEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:NODE_VIS:{e.Computer?.idName}:{e.Visible}"));

        // ── 节点 IP 变化 ──
        EventManager<PlayerNodeIPChangedEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:NODE_IP:{e.Computer?.idName}:{e.OldIP}:{e.NewIP}"));

        // ── Action 触发 ──
        EventManager<PlayerActionTriggeredEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:ACTION:{e.ActionType}:{e.XmlContent}"));

        // ── Flag 变化 ──
        EventManager<PlayerFlagEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:FLAG:{e.Flag}:{e.Action}"));

        // ── 任务事件 ──
        EventManager<PlayerMissionEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:MISSION:{e.Action}:{e.MissionID}:{e.MissionFile}"));

        // ── 代理事件 ──
        EventManager<PlayerProxyEvent>.AddHandler(e =>
            ServerManager.SendEvent(
                $"EVENT:PROXY:{e.Computer?.idName}:{e.State}:{e.OverloadTicksRemaining}:{e.StartingOverloadTicks}"));

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

        Console.WriteLine("[EventForwarder] Subscribed to all events.");
    }
}
