using System;
using Hacknet;
using Hacknet.PlatformAPI.Storage;
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

    /// <summary>临时抑制事件转发，防止 EventReceiver 应用文件操作触发自反事件形成死循环</summary>
    public static bool Suppress { get; set; }

    public static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;

        // ── 文件系统事件 ──
        EventManager<PlayerFileSystemEvent>.AddHandler(e =>
        {
            if (Suppress) return;
            // 不转发日志文件变化（全是 /log/ 开头的文件）
            if (e.Path != null && e.Path.Contains("/log/"))
                return;

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

            SendOrLog(msg);

        });

        // ── 登录/登出事件 ──
        EventManager<PlayerLoginCompEvent>.AddHandler(e =>
            SendOrLog($"EVENT:LOGIN:{e.Computer?.idName}:{e.Username}:{e.IsAdmin}"));

        // ── 连接/断开事件（跳过 playerComp——登录时自动连接，不额外转发） ──
        EventManager<PlayerConnectToCompEvent>.AddHandler(e =>
        {
            if (e.Computer?.idName == "playerComp") return;
            SendOrLog($"EVENT:CONNECT:{e.Computer?.idName}:{e.Computer?.ip}");
        });

        EventManager<PlayerDisconnectFromCompEvent>.AddHandler(e =>
            SendOrLog($"EVENT:DISCONNECT:{e.IPFrom}"));

        // ── 端口 / 防火墙 / 代理：不再作为 EVENT 转发 ──
        // 默认每人自己破解；带 <HNCShared/> 的共享节点由 NodeSync（NODE_RPT / NODE_STATE）同步。
        // （以前的 EVENT:PORT / FIREWALL / PROXY 收发格式对不上，实际一直没生效。）

        // ── 节点可见性变化（静默，不同步） ──
        // 已完全移除订阅，Hacknet 每秒数十次 IP 轮转会触发，
        // 不需要转发也不需要本地日志。

        // ── 动作 / flag：不再作为 EVENT 转发 ──
        // 每个玩家本机都在跑同一套剧情，再把动作复制给别人会让公开动作执行多次（邮件、IRC 消息重复）。
        // 动作的效果由各自的同步负责：文件 EVENT:FILE、Daemon HNC.Sync、flag FlagSync（FLAG_SET）。

        // ── 任务事件 ──
        EventManager<PlayerMissionEvent>.AddHandler(e =>
            SendOrLog($"EVENT:MISSION:{e.Action}:{e.MissionID}:{e.MissionFile}"));

        // ── 游戏保存事件 → 自动上传存档（30 秒冷却） ──
        ServerClientEventsMod.Events.PlayerEventAPI.OnSaveGame += os =>
        {
            if (!ClientNetworkManager.IsConnected) return;
            if (DateTime.Now - _lastUploadTime < TimeSpan.FromSeconds(30))
            {
                Console.WriteLine("[EventForwarderClient] Save skipped (cooldown)");
                return;
            }
            _lastUploadTime = DateTime.Now;
            Console.WriteLine("[EventForwarderClient] Local save -> auto upload...");
            TriggerSaveUpload();
        };

        Console.WriteLine("[EventForwarderClient] Subscribed to local events.");
    }

    /// <summary>发送消息到服务端（连接正常时）或打日志</summary>
    private static void SendOrLog(string msg)
    {
        if (ClientNetworkManager.IsConnected)
        {
            Console.WriteLine("[EventForwarderClient] Sending: " + msg.Substring(0, System.Math.Min(80, msg.Length)));
            ClientNetworkManager.Send(msg);
            _sendCount++;
            if (_sendCount % 50 == 0)
                Console.WriteLine($"[EventForwarderClient] Sent {_sendCount} events so far...");
        }
        else
        {
            Console.WriteLine("[EventForwarderClient] (offline) " + msg);
        }
    }
    private static int _sendCount = 0;
    private static DateTime _lastUploadTime = DateTime.MinValue;
    private static int _uploadSequence = 0;
    public static int GetNextUploadSeq() => _uploadSequence++;

    /// <summary>读取当前游戏存档并分块上传到服务端</summary>
    public static void TriggerSaveUpload()
    {
        // 立即设置冷却，防止 os.saveGame() 触发 OnSaveGame 回调再进来一次
        _lastUploadTime = DateTime.Now;

        try
        {
            var os = OS.currentInstance;
            if (os == null || string.IsNullOrEmpty(os.SaveGameUserName)) return;

            os.saveGame();
            using var stream = SaveFileHelper.GetReadStream(os.SaveGameUserName);
            if (stream == null) return;

            var mem = new System.IO.MemoryStream();
            stream.CopyTo(mem);
            var data = mem.ToArray();
            var b64 = Convert.ToBase64String(data);
            string username = os.SaveUserAccountName ?? os.SaveGameUserName.Replace("save_", "").Replace(".xml", "");

            int chunkSize = 60000;
            int total = (b64.Length + chunkSize - 1) / chunkSize;
            int uploadSeq = GetNextUploadSeq();  // 每次上传递增，终端用来区分新旧
            for (int i = 0; i < total; i++)
            {
                int start = i * chunkSize;
                int len = Math.Min(chunkSize, b64.Length - start);
                ClientNetworkManager.Send($"SAVE_DATA_CHUNK:{i}:{total}:{uploadSeq}:{username}:{b64.Substring(start, len)}");
            }
            ClientNetworkManager.Send($"SAVE_DATA_DONE:{total}:{uploadSeq}");
            // 注意：uploadSeq 是 int，溢出后回绕。正常情况下不会溢出（2^31 次上传）。
            Console.WriteLine($"[EventForwarderClient] Save uploaded: {data.Length} bytes ({total} chunks)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventForwarderClient] Upload error: {ex.Message}");
        }
    }
}