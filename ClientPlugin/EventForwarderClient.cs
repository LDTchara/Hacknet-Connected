using System;
using Hacknet;
using Pathfinder.Event;
using ServerClientEventsMod.Events;

namespace ClientPlugin;

/// <summary>
/// 客户端事件转发器 — 将本地检测到的游戏操作事件通过 LiteNetLib 发送给服务端。
/// 
/// 订阅 ServerClientEventsMod 的 Pathfinder 事件，格式化为 EVENT:TYPE:data，
/// 通过 ClientNetworkManager.Send() 发往服务端。
/// </summary>
internal static class EventForwarderClient
{
    private static bool _subscribed;

    public static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;

        // ── 文件系统事件 ──
        EventManager<PlayerFileSystemEvent>.AddHandler(e =>
        {
            // 不转发日志文件变化（全是 /log/ 开头的文件）
            if (e.Path != null && e.Path.Contains("/log/"))
                return;

            if (e.Path != null && e.Path.Contains("/log/"))
                return;

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

            SendOrLog(msg);

        });

        // ── 登录/登出事件 ──
        EventManager<PlayerLoginCompEvent>.AddHandler(e =>
            SendOrLog($"EVENT:LOGIN:{e.Computer?.idName}:{e.Username}:{e.IsAdmin}"));

        // ── 连接/断开事件 ──
        EventManager<PlayerConnectToCompEvent>.AddHandler(e =>
            SendOrLog($"EVENT:CONNECT:{e.Computer?.idName}:{e.Computer?.ip}"));

        EventManager<PlayerDisconnectFromCompEvent>.AddHandler(e =>
            SendOrLog($"EVENT:DISCONNECT:{e.IPFrom}"));

        // ── 端口事件 ──
        EventManager<PlayerPortEvent>.AddHandler(e =>
            SendOrLog($"EVENT:PORT:{e.Computer?.idName}:{e.DisplayPort}:{e.CodePort}:{e.IsOpen}"));

        // ── 防火墙事件 ──
        EventManager<PlayerFirewallEvent>.AddHandler(e =>
            SendOrLog($"EVENT:FIREWALL:{e.Computer?.idName}:{e.State}"));

        // ── 节点可见性变化 ──
        EventManager<PlayerNodeVisibilityEvent>.AddHandler(e =>
            SendOrLog($"EVENT:NODE_VIS:{e.Computer?.idName}:{e.Visible}"));

        // ── 节点 IP 变化 ──
        EventManager<PlayerNodeIPChangedEvent>.AddHandler(e =>
            SendOrLog($"EVENT:NODE_IP:{e.Computer?.idName}:{e.OldIP}:{e.NewIP}"));

        // ── Action 触发 ──
        EventManager<PlayerActionTriggeredEvent>.AddHandler(e =>
            SendOrLog($"EVENT:ACTION:{e.ActionType}:{e.XmlContent}"));

        // ── Flag 变化 ──
        EventManager<PlayerFlagEvent>.AddHandler(e =>
            SendOrLog($"EVENT:FLAG:{e.Flag}:{e.Action}"));

        // ── 任务事件 ──
        EventManager<PlayerMissionEvent>.AddHandler(e =>
            SendOrLog($"EVENT:MISSION:{e.Action}:{e.MissionID}:{e.MissionFile}"));

        // ── 代理事件 ──
        EventManager<PlayerProxyEvent>.AddHandler(e =>
            SendOrLog($"EVENT:PROXY:{e.Computer?.idName}:{e.State}:{e.OverloadTicksRemaining}:{e.StartingOverloadTicks}"));

        Console.WriteLine("[EventForwarderClient] Subscribed to local events.");
    }

    /// <summary>发送消息到服务端（连接正常时）或打日志</summary>
    private static void SendOrLog(string msg)
    {
        if (ClientNetworkManager.IsConnected)
        {
            ClientNetworkManager.Send(msg);
            // 全量日志会刷屏，只每 50 条打印一条
            _sendCount++;
            if (_sendCount % 50 == 0)
                Console.WriteLine($"[EventForwarderClient] Sent {_sendCount} events so far...");
        }
        else
        {
            // 未连接时只打日志，不发送
            Console.WriteLine("[EventForwarderClient] (offline) " + msg);
        }
    }
    private static int _sendCount = 0;
}