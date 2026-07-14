using System;
using System.IO;
using System.Linq;
using System.Text;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Localization;
using Hacknet.PlatformAPI.Storage;

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

    /// <summary>服务端文件清单（SyncStart 时构建，SyncEnd 时清理多余文件）</summary>
    private static readonly HashSet<string> _serverFileSet = new();

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
                case "NODE_VIS": HandleNodeVisibility(paramStr); break;
                case "NODE_IP": HandleNodeIPChange(paramStr); break;
                case "FIREWALL": HandleFirewallEvent(paramStr); break;
                case "PROXY": HandleProxyEvent(paramStr); break;
                case "PORT": HandlePortEvent(paramStr); break;
                case "ACTION": HandleActionEvent(paramStr); break;
                case "FLAG": HandleFlagEvent(paramStr); break;
                case "WORLD_SEED":
                    // 通过 EVENT: 路径到达的世界种子消息，重建原始格式交给系统处理器
                    HandleSystemMessage($"WORLD_SEED:{paramStr}");
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
            Console.WriteLine("[EventRecv] Server requested save upload!");
            try
            {
                // 1. 先强制保存当前游戏状态
                var os = OS.currentInstance;
                if (os != null)
                {
                    string saveUser = os.SaveGameUserName;
                    if (!string.IsNullOrEmpty(saveUser))
                    {
                        // 触发游戏保存（确保存档文件是最新的）
                        os.saveGame();
                        Console.WriteLine("[EventRecv] OS forced save done");

                        // 通过 SaveFileManager 读取存档文件
                        using var stream = SaveFileManager.GetSaveReadStream(saveUser);
                        if (stream != null)
                        {
                            var memStream = new MemoryStream();
                            stream.CopyTo(memStream);
                            var data = memStream.ToArray();
                            var b64 = Convert.ToBase64String(data);
                            string username = ClientPluginMain.SelectedAccount?.GameUsername ?? saveUser.Replace("save_", "").Replace(".xml", "");

                            // 分块发送，避免单条 LiteNetLib 消息过大
                            int chunkSize = 60000;
                            int total = (b64.Length + chunkSize - 1) / chunkSize;
                            for (int i = 0; i < total; i++)
                            {
                                int start = i * chunkSize;
                                int len = Math.Min(chunkSize, b64.Length - start);
                                var chunk = b64.Substring(start, len);
                                ClientNetworkManager.Send($"SAVE_DATA_CHUNK:{i}:{total}:{username}:{chunk}");
                            }
                            ClientNetworkManager.Send($"SAVE_DATA_DONE:{total}");
                            Console.WriteLine($"[EventRecv] Save uploaded ({data.Length} bytes, {total} chunks)");
                            return;
                        }
                    }
                }

                // 2. 退回到 SelectedAccount 的路径
                var path = ClientPluginMain.SelectedAccount?.SaveFilePath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var bytes = File.ReadAllBytes(path);
                    var b64 = Convert.ToBase64String(bytes);
                    string user2 = ClientPluginMain.SelectedAccount.GameUsername ?? "unknown";
                    int cs2 = 60000;
                    int t2 = (b64.Length + cs2 - 1) / cs2;
                    for (int i = 0; i < t2; i++)
                    {
                        int start = i * cs2;
                        int len = Math.Min(cs2, b64.Length - start);
                        ClientNetworkManager.Send($"SAVE_DATA_CHUNK:{i}:{t2}:{user2}:{b64.Substring(start, len)}");
                    }
                    ClientNetworkManager.Send($"SAVE_DATA_DONE:{t2}");
                    Console.WriteLine($"[EventRecv] Save uploaded ({bytes.Length} bytes, {t2} chunks, from SelectedAccount path)");
                    return;
                }

                Console.WriteLine("[EventRecv] No save file to upload (no file path set)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EventRecv] Save upload failed: {ex.Message}");
            }
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
        // 格式：action:compID:path:sourceIP[:content]
        var parts = SplitParam(paramStr, 7);
        if (parts.Length < 4) return;

        string action = parts[0];
        string compID = parts[1];
        string filePath = parts[2];
        string sourceIP = parts[3];
        string content = parts.Length > 4 ? parts[4] : null;
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
            case "SyncStart":
                // 开始全量文件同步：清空清单，进入静默模式
                _serverFileSet.Clear();
                _isSyncing = true;
                Console.WriteLine("[EventRecv] File sync started (quiet mode)");
                return;

            case "SyncEnd":
                // 同步结束：删除客户端多余的文件
                _isSyncing = false;
                var currentFiles = new HashSet<string>();
                CollectFiles(comp, "", currentFiles);
                foreach (var clientFile in currentFiles)
                {
                    if (!_serverFileSet.Contains(clientFile))
                    {
                        RemoveFileFromComputer(comp, clientFile);
                    }
                }
                Console.WriteLine($"[EventRecv] File sync ended. Removed {currentFiles.Count - _serverFileSet.Count} extra files.");
                _serverFileSet.Clear();
                return;

            case "FileAdded":
                if (_isSyncing)
                {
                    _serverFileSet.Add(filePath);
                }
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
        Console.WriteLine($"[EventRecv] IP changed: {compID} {oldIP} → {newIP}");
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

        // TODO: 从 XML 反序列化 SerializableAction 并在本地执行
        Console.WriteLine($"[EventRecv] Action: {actionType} (length={xmlContent.Length})");
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
        if (content == null) return;
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
}