using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Localization;
using Hacknet.PlatformAPI.Storage;
using ServerClientEventsMod.Events;

namespace ClientPlugin;

/// <summary>
/// 接收服务端/主机发来的事件消息，同步到本地游戏状态
/// 
/// 消息格式：EVENT:TYPE:param1:param2:...
/// 
/// 同步范围：
/// - 节点文件结构（FileAdded/FileRemoved/FileContentChanged/FolderAdded/FolderRemoved）
/// - 防火墙状态（FirewallState）
/// - 代理状态（ProxyState）
/// - 端口破解状态（PortState）
/// - 管理员权限（AdminState）
/// - 节点可见性（NodeVis）
/// - 节点 IP 变化（NodeIP）
/// </summary>
internal static class EventReceiver
{
    /// <summary>文件同步中，抑制详细日志避免刷屏</summary>
    private static bool _isSyncing = false;

    /// <summary>SAVE_REQUIRED 冷却（防 exit 时重复上传）</summary>
    private static DateTime _lastSaveUpload = DateTime.MinValue;

    public static void HandleMessage(string msg)
    {
        try
        {
            Console.WriteLine($"[EventRecv] {msg}");

            // 非 EVENT 消息（WELCOME / PLAYER_LIST / WORLD_SEED 等）
            if (!msg.StartsWith("EVENT:"))
            {
                HandleSystemMessage(msg);
                return;
            }

            // 解析 EVENT:TYPE:...
            var content = msg.Substring("EVENT:".Length);
            var colonIdx = content.IndexOf(':');
            string eventType = colonIdx >= 0 ? content.Substring(0, colonIdx) : content;
            string paramStr = colonIdx >= 0 ? content.Substring(colonIdx + 1) : "";

            switch (eventType)
            {
                case "FILE": HandleFileEvent(paramStr); break;
                //case "NODE_VIS": HandleNodeVisibility(paramStr); break;
                //case "NODE_IP": HandleNodeIPChange(paramStr); break;
                case "FIREWALL": HandleFirewallEvent(paramStr); break;
                case "PROXY": HandleProxyEvent(paramStr); break;
                case "PORT": HandlePortEvent(paramStr); break;
                case "ACTION": HandleActionEvent(paramStr); break;
                case "FLAG": HandleFlagEvent(paramStr); break;
                case "WORLD_SEED":
                    // 通过 EVENT: 路径到达的世界种子消息，重建原始格式交给系统处理器
                    HandleSystemMessage($"WORLD_SEED:{paramStr}");
                    break;
                case "SAVE_REQUIRED":
                    HandleSystemMessage("SAVE_REQUIRED");
                    break;
                case "PLAYER_SELECT":
                    // 其他客户端的角色选择广播
                    var sparams = paramStr.Split(':');
                    SelectPlayerCommand.OnPlayerSelectEvent(new[] { "", "", sparams.Length > 0 ? sparams[0] : "",
                        sparams.Length > 1 ? sparams[1] : "", sparams.Length > 2 ? sparams[2] : "" });
                    break;
                case "MISSION_READY":
                    // 私有支线任务满员，所有角色已分配
                    SelectPlayerCommand.HandleMissionReady(paramStr);
                    break;
                default:
                    Console.WriteLine($"[EventRecv] Unknown event type: {eventType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] Error handling message: {ex.Message}");
        }
    }

    // ── 系统消息 ──

    private static void HandleSystemMessage(string msg)
    {
        if (msg.StartsWith("WELCOME:"))
        {
            var idStr = msg.Substring("WELCOME:".Length);
            if (int.TryParse(idStr, out int id))
            {
                ClientNetworkManager.PlayerId = id;
                Console.WriteLine($"[EventRecv] WELCOME: PlayerId={id}");
            }
        }
        else if (msg.StartsWith("PLAYER_LIST:"))
        {
            var listStr = msg.Substring("PLAYER_LIST:".Length);
            Console.WriteLine($"[EventRecv] Online players: {listStr}");
        }

        // ── 玩家数据同步（来自服务端 HELLO 握手） ──
        // PLAYER_DATA:{id}:{name}
        else if (msg.StartsWith("PLAYER_DATA:"))
        {
            var rest = msg.Substring("PLAYER_DATA:".Length);
            var parts = rest.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int pId))
            {
                ClientNetworkManager.PlayerId = pId;
                string pName = parts[1];
                Console.WriteLine($"[EventRecv] PLAYER_DATA: Id={pId} Name={pName}");
                PlayerDataManager.CreateOrUpdate(pId, pName);
            }
        }

        // ── 服务端是否有该玩家存档（HELLO 握手时确定） ──
        else if (msg == "HAS_SAVE")
        {
            ClientPluginMain.HasServerSave = true;
            Console.WriteLine("[EventRecv] HAS_SAVE: 服务端有该玩家存档，等待传输...");
        }
        else if (msg == "NO_SAVE")
        {
            ClientPluginMain.HasServerSave = false;
            Console.WriteLine("[EventRecv] NO_SAVE: 新玩家，跳过存档传输");
        }

        // ── 服务端存档列表（供客户端对比） ──
        // SAVE_LIST:{count}:{size1}:{name1}:{size2}:{name2}:...
        else if (msg.StartsWith("SAVE_LIST:"))
        {
            var data = msg.Substring("SAVE_LIST:".Length);
            var parts = data.Split(':');
            if (parts.Length >= 1 && int.TryParse(parts[0], out int count) && count > 0)
            {
                Console.WriteLine($"[EventRecv] Server has {count} saves:");
                for (int i = 0; i < count && (2 + i * 2) < parts.Length; i++)
                {
                    string size = parts[1 + i * 2];
                    string name = parts[2 + i * 2];
                    Console.WriteLine($"[EventRecv]   {name} ({size} bytes)");
                }
                // 存档文件传输会随后到达，替换本地存档
                Console.WriteLine("[EventRecv] Server save file will replace local copy upon arrival.");
            }
            else
            {
                Console.WriteLine("[EventRecv] No server saves found for this account.");
            }
        }

        // ── 存档分块传输 ──
        // S_FILE_INFO:{total}
        // S_FILE:{seq}:{total}:{base64part}
        // S_FILE_END:{total}
        else if (msg == "S_FILE_END")
        {
            // 新版（不带 total 后缀）的结束信号
            if (ClientPluginMain.PendingSaveFileB64 != null)
            {
                Console.WriteLine($"[EventRecv] Save file assembly complete ({ClientPluginMain.PendingSaveFileB64.Length} base64 chars)");
                var data = Convert.FromBase64String(ClientPluginMain.PendingSaveFileB64);
                ClientPluginMain.PendingSaveFileB64 = null;
                // 存到 GameLauncher 用的字段（沿用旧字段名保持兼容）
                ClientPluginMain.PendingSaveFile = data;
                ClientPluginMain.IsSaveFileReady = true;
                Console.WriteLine($"[EventRecv] Save file ready: {data.Length} bytes");
            }
        }
        else if (msg.StartsWith("S_FILE_INFO:"))
        {
            var totalStr = msg.Substring("S_FILE_INFO:".Length);
            if (int.TryParse(totalStr, out int total))
            {
                ClientPluginMain.PendingSaveChunks = new Dictionary<int, string>();
                ClientPluginMain.PendingSaveTotal = total;
                ClientPluginMain.PendingSaveFileB64 = null;
                ClientPluginMain.IsSaveFileReady = false;
                Console.WriteLine($"[EventRecv] Save file transfer started: {total} chunks");
            }
        }
        else if (msg.StartsWith("S_FILE:"))
        {
            var rest = msg.Substring("S_FILE:".Length);
            var parts = rest.Split(new char[] { ':' }, 3);
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out int seq) &&
                int.TryParse(parts[1], out int total))
            {
                ClientPluginMain.PendingSaveTotal = total;
                if (ClientPluginMain.PendingSaveChunks == null)
                    ClientPluginMain.PendingSaveChunks = new Dictionary<int, string>();
                ClientPluginMain.PendingSaveChunks[seq] = parts[2];

                // 如果全部收齐，立即组装
                if (ClientPluginMain.PendingSaveChunks.Count == total)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < total; i++)
                    {
                        if (ClientPluginMain.PendingSaveChunks.TryGetValue(i, out var chunk))
                            sb.Append(chunk);
                    }
                    ClientPluginMain.PendingSaveFileB64 = sb.ToString();
                    var data = Convert.FromBase64String(ClientPluginMain.PendingSaveFileB64);
                    ClientPluginMain.PendingSaveChunks = null;
                    ClientPluginMain.PendingSaveFile = data;
                    ClientPluginMain.IsSaveFileReady = true;
                    Console.WriteLine($"[EventRecv] Save file received: {data.Length} bytes (all {total} chunks assembled)");
                }
                else if (seq % 25 == 0)
                {
                    Console.WriteLine($"[EventRecv] Save file chunk: {ClientPluginMain.PendingSaveChunks.Count}/{total}");
                }
            }
        }
        else if (msg.StartsWith("S_FILE_END:"))
        {
            var totalStr = msg.Substring("S_FILE_END:".Length);
            if (int.TryParse(totalStr, out int total) && ClientPluginMain.PendingSaveChunks != null)
            {
                // 可能有些块没到（不会发生，ReliableOrdered 保证全部到达）
                // 但以防万一，只组装已收到的
                if (ClientPluginMain.PendingSaveChunks.Count >= total)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < total; i++)
                    {
                        if (ClientPluginMain.PendingSaveChunks.TryGetValue(i, out var chunk))
                            sb.Append(chunk);
                    }
                    ClientPluginMain.PendingSaveFileB64 = sb.ToString();
                    var data = Convert.FromBase64String(ClientPluginMain.PendingSaveFileB64);
                    ClientPluginMain.PendingSaveChunks = null;
                    ClientPluginMain.PendingSaveFile = data;
                    ClientPluginMain.IsSaveFileReady = true;
                    Console.WriteLine($"[EventRecv] Save file assembled ({sb.Length} base64 chars -> {data.Length} bytes)");
                }
                else
                {
                    Console.WriteLine($"[EventRecv] WARNING: S_FILE_END but only {ClientPluginMain.PendingSaveChunks.Count}/{total} chunks received");
                    // 写标记文件：告知 GameLauncher 上次存档传输不完整
                    MarkSaveIncomplete();
                }
            }
        }

        // ── 接收服务端存档文件（兼容旧格式） ──
        else if (msg.StartsWith("SAVE_FILE_DATA:"))
        {
            var b64 = msg.Substring("SAVE_FILE_DATA:".Length);
            try
            {
                var data = Convert.FromBase64String(b64);
                ClientPluginMain.PendingSaveFile = data;
                ClientPluginMain.IsSaveFileReady = true;
                Console.WriteLine($"[EventRecv] Received server save file (legacy): {data.Length} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EventRecv] Failed to decode save file: {ex.Message}");
            }
        }

        // ── 服务端请求上传存档 ──
        else if (msg == "SAVE_REQUIRED")
        {
            if ((DateTime.Now - _lastSaveUpload).TotalSeconds < 10)
            {
                Console.WriteLine("[EventRecv] SAVE_REQUIRED 忽略（10秒冷却）");
                return;
            }
            _lastSaveUpload = DateTime.Now;
            Console.WriteLine("[EventRecv] Server requested save upload!");

            // 委托给 EventForwarderClient.TriggerSaveUpload()
            // 它负责 os.saveGame() + 读取 + 分块 + 发送。
            // 不在此处另做上传——否则 os.saveGame() 触发 OnSaveGame
            // 回调会再起一次上传，导致服务端收到两份存档。
            EventForwarderClient.TriggerSaveUpload();
        }

        // ── 世界种子（含随机状态+子种子计数器） ──
        // 格式：WORLD_SEED:{seed}[:{counter}[:{randomState}]]
        // randomState 是服务端 Utils.random 的完整内部状态序列化
        // counter 是 _childSeedCounter，保证 new Random() 子种子分配一致
        else if (msg.StartsWith("WORLD_SEED:"))
        {
            var rest = msg.Substring("WORLD_SEED:".Length);
            var parts = rest.Split(':');

            if (parts.Length >= 1 && int.TryParse(parts[0], out int seed))
            {
                ClientPluginMain.WorldSeed = seed;
                Console.WriteLine($"[EventRecv] World seed received: {seed}");

                // 如果服务端还传了子种子计数器，保存它
                if (parts.Length >= 2 && int.TryParse(parts[1], out int counter))
                {
                    ClientPluginMain.ChildSeedCounter = counter;
                    Console.WriteLine($"[EventRecv] Child seed counter received: {counter}");
                }

                // 如果服务端还传了随机状态，保存它用于精确恢复
                if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                {
                    ClientPluginMain.RandomState = parts[2];
                    Console.WriteLine($"[EventRecv] Random state received ({parts[2].Length} chars)");
                }

                // 如果服务端还传了 IP 计数器，保存它
                if (parts.Length >= 4 && int.TryParse(parts[3], out int ipCtr))
                {
                    ClientPluginMain.IpCounter = ipCtr;
                    Console.WriteLine($"[EventRecv] IP counter received: {ipCtr}");
                }

                // 保存到当前选中的服务端配置（含随机状态+计数器）
                var server = ClientPluginMain.SelectedServer;
                if (server != null)
                {
                    server.WorldSeed = seed;
                    if (ClientPluginMain.ChildSeedCounter > 0)
                        server.ChildSeedCounter = ClientPluginMain.ChildSeedCounter;
                    if (!string.IsNullOrEmpty(ClientPluginMain.RandomState))
                        server.RandomState = ClientPluginMain.RandomState;
                    ClientPlugin.Listing.ClientProfileStorage.Save();
                    Console.WriteLine($"[EventRecv] Seed/state saved to server profile: {server.HostName}");
                }
            }
        }
    }

    // ── 文件事件 ──

    private static void HandleFileEvent(string paramStr)
    {
        EventForwarderClient.Suppress = true;
        try
        {
        // 格式：action:compID:path:sourceIP[:content]
        var parts = SplitParam(paramStr, 7);
        if (parts.Length < 4) return;

        string action = parts[0];

        // ⚠ 特殊 action 必须先判断再解码：SyncStart/SyncEnd/FOLDER_SYNC 的
        // parts[2] 不是 base64（seq 数字 / "all"），若先解码会抛
        // "Invalid length for a Base-64 char array"，这些消息永远处理不了。
        if (action == "SyncStart")
        {
            _isSyncing = true;
            Console.WriteLine("[EventRecv] File sync started (quiet mode)");
            return;
        }

        if (action == "SyncEnd")
        {
            _isSyncing = false;
            // 简化：不做"删除多余文件"清理（避免误删本地动态文件如 /log/ 连接日志）。
            // 同步 = 服务端逐文件发 FileAdded → 客户端幂等添加/覆盖；
            // 删除由游戏内 FileRemoved 事件驱动，不做镜像清空。
            Console.WriteLine("[EventRecv] File sync ended.");
            return;
        }

        // ── 普通 FILE 事件：到这里才解码 base64 ──
        string compID = parts[1];
        string filePath = parts.Length > 2 ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])) : "";
        string sourceIP = parts[3];
        string raw = parts.Length > 4 ? parts[4] : null;
        string content = raw != null ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(raw)) : null;
        string fromName = parts.Length > 5 ? parts[5] : null;
        string toName = parts.Length > 6 ? parts[6] : null;

        var comp = FindComputer(compID);
        if (comp == null)
        {
            if (!_isSyncing)
                Console.WriteLine($"[EventRecv] Computer not found: {compID}");
            return;
        }

        switch (action)
        {
            case "FileAdded":
                AddFileToComputer(comp, filePath, content);
                break;

            case "FileRemoved":
                RemoveFileFromComputer(comp, filePath);
                break;

            case "FileContentChanged":
                UpdateFileContent(comp, filePath, content);
                break;

            case "FolderAdded":
                AddFolderToComputer(comp, filePath);
                break;

            case "FolderRemoved":
                RemoveFolderFromComputer(comp, filePath);
                break;

            case "FileMoved":
                if (!string.IsNullOrEmpty(fromName) && !string.IsNullOrEmpty(toName))
                {
                    MoveFileOnComputer(comp, filePath, fromName, toName);
                }
                else
                    Console.WriteLine("[EventRecv] FileMoved: missing fromName/toName");
                break;
        }
        }
        finally
        {
            EventForwarderClient.Suppress = false;
        }
    }

    // ── 节点可见性 ──

    private static void HandleNodeVisibility(string paramStr)
    {
        // 格式：compID:visible
        var parts = SplitParam(paramStr, 2);
        if (parts.Length < 2) return;

        string compID = parts[0];
        bool visible = parts[1] == "True" || parts[1] == "true";

        var comp = FindComputer(compID);
        if (comp == null) return;

        var os = OS.currentInstance;
        if (os?.netMap?.nodes == null) return;

        int idx = os.netMap.nodes.IndexOf(comp);
        if (idx < 0) return;

        if (visible && !os.netMap.visibleNodes.Contains(idx))
        {
            os.netMap.visibleNodes.Add(idx);
            Console.WriteLine($"[EventRecv] ShowNode: {compID}");
        }
        else if (!visible && os.netMap.visibleNodes.Contains(idx))
        {
            os.netMap.visibleNodes.Remove(idx);
            Console.WriteLine($"[EventRecv] HideNode: {compID}");
        }
    }

    // ── 节点 IP 变化 ──

    private static void HandleNodeIPChange(string paramStr)
    {
        var parts = SplitParam(paramStr, 3);
        if (parts.Length < 3) return;

        string compID = parts[0];
        string oldIP = parts[1];
        string newIP = parts[2];

        var comp = FindComputer(compID);
        if (comp == null) return;

        comp.ip = newIP;
        //Console.WriteLine($"[EventRecv] IP changed: {compID} {oldIP} → {newIP}");
    }

    // ── 防火墙状态 ──

    private static void HandleFirewallEvent(string paramStr)
    {
        // 格式：compID:isOpen
        var parts = SplitParam(paramStr, 2);
        if (parts.Length < 2) return;

        string compID = parts[0];
        bool isOpen = parts[1] == "True" || parts[1] == "true";

        var comp = FindComputer(compID);
        if (comp?.firewall == null) return;

        comp.firewall.solved = isOpen;
        if (isOpen) comp.firewallAnalysisInProgress = false;
        Console.WriteLine($"[EventRecv] Firewall: {compID} solved={isOpen}");
    }

    // ── 代理状态 ──

    private static void HandleProxyEvent(string paramStr)
    {
        // 格式：compID:isActive
        var parts = SplitParam(paramStr, 2);
        if (parts.Length < 2) return;

        string compID = parts[0];
        bool isActive = parts[1] == "True" || parts[1] == "true";

        var comp = FindComputer(compID);
        if (!comp.hasProxy) return;

        if (isActive && !comp.proxyActive)
        {
            comp.proxyActive = true;
            comp.proxyOverloadTicks = 0f;
        }
        else if (!isActive)
        {
            comp.proxyActive = false;
        }
        Console.WriteLine($"[EventRecv] Proxy: {compID} active={isActive}");
    }

    // ── 端口状态 ──

    private static void HandlePortEvent(string paramStr)
    {
        // 格式：compID:displayPort:codePort:isOpen
        var parts = SplitParam(paramStr, 4);
        if (parts.Length < 4) return;

        string compID = parts[0];
        int codePort = int.TryParse(parts[2], out var cp) ? cp : 0;
        bool isOpen = parts[3] == "True" || parts[3] == "true";

        var comp = FindComputer(compID);
        if (comp == null) return;

        if (isOpen && !comp.portsOpen.Contains((byte)codePort))
        {
            comp.portsOpen.Add((byte)codePort);
        }
        else if (!isOpen && comp.portsOpen.Contains((byte)codePort))
        {
            comp.portsOpen.Remove((byte)codePort);
        }
        Console.WriteLine($"[EventRecv] Port: {compID} port={codePort} open={isOpen}");
    }

    // ── Action 触发 ──

    private static void HandleActionEvent(string paramStr)
    {
        // 格式：actionType:xmlContent
        var colonIdx = paramStr.IndexOf(':');
        if (colonIdx < 0) return;

        string actionType = paramStr.Substring(0, colonIdx);
        string xmlContent = paramStr.Substring(colonIdx + 1);

        try
        {
            using var sr = new StringReader(xmlContent);
            using var rdr = XmlReader.Create(sr);
            rdr.MoveToContent();

            // 反序列化——ScopePatches.ActionDeserializePostfix 会自动读取
            // players/group 并注册到 ScopeManager
            var action = SerializableAction.Deserialize(rdr);
            if (action == null) return;

            // scope 检查：私有 action 只对匹配身份的客户端生效
            var scope = ScopeManager.GetScope(action);
            if (scope != null && scope.IsPrivate)
            {
                string myGloble = ClientPluginMain.SelectPlayerConfig ?? "";
                bool allowed = string.IsNullOrEmpty(scope.Group)
                    ? string.Equals(scope.Players, myGloble, StringComparison.OrdinalIgnoreCase)
                    : ScopePatches.MyPrivateIdentities.TryGetValue(scope.Group, out var myName)
                      && string.Equals(scope.Players, myName, StringComparison.OrdinalIgnoreCase);

                if (!allowed)
                {
                    Console.WriteLine($"[EventRecv] Action {actionType} skipped (scope not matched)");
                    return;
                }
            }

            var os = OS.currentInstance;
            if (os == null) return;

            // 执行——置 SuppressBroadcast 防回环（否则 Trigger 会被拦截器再次广播）
            PlayerActionTriggeredEvent.SuppressBroadcast = true;
            try { action.Trigger(os); }
            finally { PlayerActionTriggeredEvent.SuppressBroadcast = false; }

            Console.WriteLine($"[EventRecv] Action executed: {actionType}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] Action execute failed: {ex.Message}");
        }
    }

    // ── Flag 变化 ──

    private static void HandleFlagEvent(string paramStr)
    {
        // 格式：flag:action (add/remove)
        var parts = SplitParam(paramStr, 2);
        if (parts.Length < 2) return;

        string flag = parts[0];
        string action = parts[1];

        var os = OS.currentInstance;
        if (os == null) return;

        if (action == "add" && !OS.currentInstance.Flags.Flags.Contains(flag))
            os.Flags.AddFlag(flag);
        else if (action == "remove")
            os.Flags.RemoveFlag(flag);

        Console.WriteLine($"[EventRecv] Flag: {flag} {action}");
    }

    // ── 文件操作工具方法 ──

    /// <summary>递归收集某台计算机的所有文件路径</summary>
    private static void CollectFiles(Computer comp, string prefix, HashSet<string> results)
    {
        if (comp?.files?.root == null) return;
        CollectFilesFromFolder(comp.files.root, prefix, results);
    }

    private static void CollectFilesFromFolder(Folder folder, string prefix, HashSet<string> results)
    {
        string path = string.IsNullOrEmpty(prefix) ? folder.name : prefix + "/" + folder.name;
        foreach (var file in folder.files)
        {
            if (file != null && !string.IsNullOrEmpty(file.name))
                results.Add(path + "/" + file.name);
        }
        foreach (var sub in folder.folders)
        {
            if (sub != null)
                CollectFilesFromFolder(sub, path, results);
        }
    }

    private static void AddFileToComputer(Computer comp, string filePath, string? content)
    {
        try
        {
            var parts = filePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;

            // 遍历到父目录
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var sub = current.folders.FirstOrDefault(f => f.name == parts[i]);
                if (sub == null)
                {
                    sub = new Folder(parts[i]);
                    current.folders.Add(sub);
                }
                current = sub;
            }

            // 添加/更新文件
            string fileName = parts.Last();
            var existing = current.files.FirstOrDefault(f => f.name == fileName);
            if (existing != null)
            {
                if (content != null) existing.data = content;
            }
            else
            {
                current.files.Add(new FileEntry(content ?? "", fileName));
            }

            if (!_isSyncing)
                Console.WriteLine($"[EventRecv] File added: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] AddFile error: {ex.Message}");
        }
    }

    private static void RemoveFileFromComputer(Computer comp, string filePath)
    {
        try
        {
            var parts = filePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = current.folders.FirstOrDefault(f => f.name == parts[i]);
                if (current == null) return;
            }

            string fileName = parts.Last();
            current.files.RemoveAll(f => f.name == fileName);
            if (!_isSyncing)
                Console.WriteLine($"[EventRecv] File removed: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] RemoveFile error: {ex.Message}");
        }
    }

    private static void UpdateFileContent(Computer comp, string filePath, string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        try
        {
            var parts = filePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = current.folders.FirstOrDefault(f => f.name == parts[i]);
                if (current == null) return;
            }

            string fileName = parts.Last();
            var file = current.files.FirstOrDefault(f => f.name == fileName);
            if (file != null)
            {
                file.data = content;
                if (!_isSyncing)
                    Console.WriteLine($"[EventRecv] File updated: {filePath} ({content.Length} chars)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] UpdateFile error: {ex.Message}");
        }
    }

    private static void AddFolderToComputer(Computer comp, string folderPath)
    {
        try
        {
            var parts = folderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;

            for (int i = 0; i < parts.Length; i++)
            {
                var sub = current.folders.FirstOrDefault(f => f.name == parts[i]);
                if (sub == null)
                {
                    sub = new Folder(parts[i]);
                    current.folders.Add(sub);
                }
                current = sub;
            }
            if (!_isSyncing)
                Console.WriteLine($"[EventRecv] Folder added: {folderPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] AddFolder error: {ex.Message}");
        }
    }

    private static void RemoveFolderFromComputer(Computer comp, string folderPath)
    {
        try
        {
            var parts = folderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = current.folders.FirstOrDefault(f => f.name == parts[i]);
                if (current == null) return;
            }

            string folderName = parts.Last();
            current.folders.RemoveAll(f => f.name == folderName);
            if (!_isSyncing)
                Console.WriteLine($"[EventRecv] Folder removed: {folderPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] RemoveFolder error: {ex.Message}");
        }
    }

    private static void MoveFileOnComputer(Computer comp, string oldPath, string fromName, string toName)
    {
        try
        {
            var parts = oldPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = current.folders.FirstOrDefault(f => f.name == parts[i]);
                if (current == null) return;
            }
            var file = current.files.FirstOrDefault(f => f.name == fromName);
            if (file == null)
            {
                Console.WriteLine($"[EventRecv] FileMoved: source file '{fromName}' not found");
                return;
            }
            string data = file.data;
            current.files.Remove(file);
            current.files.Add(new FileEntry(data ?? "", toName));
            Console.WriteLine($"[EventRecv] File moved: {oldPath} -> {toName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] MoveFile error: {ex.Message}");
        }
    }

    // ── 工具方法 ──

    /// <summary>按 Computer.idName 查找节点</summary>
    private static Computer? FindComputer(string idName)
    {
        var os = OS.currentInstance;
        if (os?.netMap?.nodes == null) return null;
        return os.netMap.nodes.Find(c =>
            c.idName.Equals(idName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 分割参数，支持值中有冒号的情况（如文件内容）。
    /// 只分割前 expected-1 个冒号，剩下的全给最后一个字段。
    /// </summary>
    private static string[] SplitParam(string input, int expected)
    {
        if (expected <= 1)
            return new[] { input };

        var result = new string[expected];
        string remaining = input;

        for (int i = 0; i < expected - 1; i++)
        {
            var idx = remaining.IndexOf(':');
            if (idx < 0)
            {
                result[i] = remaining;
                for (int j = i + 1; j < expected; j++)
                    result[j] = "";
                return result;
            }
            result[i] = remaining.Substring(0, idx);
            remaining = remaining.Substring(idx + 1);
        }
        result[expected - 1] = remaining;
        return result;
    }

    // ─── 存档不完整标记 ───

    /// <summary>写入标记文件：上次服务端存档传输不完整</summary>
    private static void MarkSaveIncomplete()
    {
        try
        {
            string? dir = GetStorageDir();
            if (dir == null) return;
            Directory.CreateDirectory(dir);
            string marker = Path.Combine(dir, "INCOMPLETE");
            File.WriteAllText(marker, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine($"[EventRecv] Incomplete marker written: {marker}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] Marker write error: {ex.Message}");
        }
    }

    /// <summary>检查并清除标记文件</summary>
    public static bool CheckAndClearIncompleteMarker()
    {
        try
        {
            string? dir = GetStorageDir();
            if (dir == null) return false;
            string marker = Path.Combine(dir, "INCOMPLETE");
            if (File.Exists(marker))
            {
                File.Delete(marker);
                Console.WriteLine($"[EventRecv] Incomplete marker found and cleared: {marker}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] Marker check error: {ex.Message}");
            return false;
        }
    }

    private static string? GetStorageDir()
    {
        var server = ClientPluginMain.SelectedServer;
        var account = ClientPluginMain.SelectedAccount;
        if (server == null || account == null) return null;
        string storageDir = Path.Combine(
            ClientPluginMain.EnvPath, "OnlineSaves", "Client", server.HostName);
        return storageDir;
    }

    /// <summary>当服务端存档不完整时，将本地存档上传到服务端</summary>
    public static void UploadLocalSaveToServer()
    {
        try
        {
            byte[]? data = null;
            string username = "unknown";

            var os = OS.currentInstance;
            if (os != null && !string.IsNullOrEmpty(os.SaveGameUserName))
            {
                // 遍历所有 StorageMethod 找存档（修复 [0] 指向不确定的问题）
                os.saveGame();
                using var stream = SaveFileHelper.GetReadStream(os.SaveGameUserName);
                if (stream != null)
                {
                    var mem = new System.IO.MemoryStream();
                    stream.CopyTo(mem);
                    data = mem.ToArray();
                    username = os.SaveUserAccountName ?? os.SaveGameUserName.Replace("save_", "").Replace(".xml", "");
                }
            }

            if (data == null)
            {
                // 游戏未运行：从文件读取（HHBS 路径）
                var server = ClientPluginMain.SelectedServer;
                var account = ClientPluginMain.SelectedAccount;
                if (account != null && !string.IsNullOrEmpty(account.SaveFilePath) && File.Exists(account.SaveFilePath))
                {
                    data = File.ReadAllBytes(account.SaveFilePath);
                    username = account.GameUsername ?? "unknown";
                }
            }

            if (data == null || data.Length == 0)
            {
                Console.WriteLine("[EventRecv] No local save data to upload");
                return;
            }

            var b64 = Convert.ToBase64String(data);
            int chunkSize = 60000;
            int total = (b64.Length + chunkSize - 1) / chunkSize;
            int upSeq = EventForwarderClient.GetNextUploadSeq();
            for (int i = 0; i < total; i++)
            {
                int start = i * chunkSize;
                int len = Math.Min(chunkSize, b64.Length - start);
                ClientNetworkManager.Send($"SAVE_DATA_CHUNK:{i}:{total}:{upSeq}:{username}:{b64.Substring(start, len)}");
            }
            ClientNetworkManager.Send($"SAVE_DATA_DONE:{total}:{upSeq}");
            Console.WriteLine($"[EventRecv] Local save uploaded to server: {data.Length} bytes ({total} chunks)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventRecv] Local save upload failed: {ex.Message}");
        }
    }
}