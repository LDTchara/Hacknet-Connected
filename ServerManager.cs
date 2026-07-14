using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hacknet;
using Hacknet.PlatformAPI.Storage;
using System.Reflection;
using Hacknet.Extensions;

namespace ServerPlugin;

/// <summary>
/// 后台消息队列——线程安全，供 Pipe 线程入队、游戏主线程出队
/// </summary>
internal static class MessageQueue
{
    private static readonly Queue<string> _queue = new();
    private static readonly object _lock = new();

    public static void Enqueue(string msg)
    {
        lock (_lock) _queue.Enqueue(msg);
    }

    public static string? Dequeue()
    {
        lock (_lock)
        {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }
    }
}

/// <summary>
/// 服务端模式管理器——持久 Pipe 连接 + 后台消息监听
/// </summary>
internal static class ServerManager
{
    public static bool IsActive { get; private set; }
    public static bool IsConnected { get; private set; }
    public static bool IsServerMode { get; set; }

    /// <summary>Pipe 连接就绪时触发，供事件转发器注册</summary>
    public static event Action? OnPipeConnected;

    /// <summary>向控制台 Pipe 发送消息（游戏主线程或后台线程调用）</summary>
    public static void SendToConsole(string msg)
    {
        if (_pipeWriter == null) return;
        lock (_pipeLock)
        {
            try { _pipeWriter.WriteLine(msg); }
            catch { }
        }
    }

    public static void Reset()
    {
        IsActive = false;
        IsConnected = false;
        _cts?.Cancel();
        Cleanup();
        Console.WriteLine("[ServerPlugin] State reset.");
    }

    private static NamedPipeClientStream? _pipe;
    private static StreamReader? _pipeReader;
    private static StreamWriter? _pipeWriter;
    private static CancellationTokenSource? _cts;

    public static void Activate()
    {
        IsActive = true;
        Console.WriteLine("[ServerPlugin] Server mode activated.");
    }

    public static async Task ConnectToConsole()
    {
        try
        {
            _pipe = new NamedPipeClientStream(
                ".", "HacknetServerPipe",
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            Console.WriteLine("[ServerPlugin] Connecting to console...");
            await _pipe.ConnectAsync(timeout: 10000);

            _pipeReader = new StreamReader(_pipe, Encoding.UTF8);
            _pipeWriter = new StreamWriter(_pipe, Encoding.UTF8) { AutoFlush = true };

            // 握手
            await _pipeWriter.WriteLineAsync("HELLO");
            var confirm = await _pipeReader.ReadLineAsync();

            if (confirm != "OK")
            {
                Console.WriteLine($"[ServerPlugin] Handshake failed: {confirm}");
                Cleanup();
                return;
            }

            IsConnected = true;
            Console.WriteLine("[ServerPlugin] Console confirmed. Ready.");

            // 通知所有订阅者 Pipe 已就绪
            OnPipeConnected?.Invoke();

            // 启动后台消息监听
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[ServerPlugin] Console not found.");
            Cleanup();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerPlugin] Pipe error: {ex.Message}");
            Cleanup();
        }
    }

    private static readonly object _pipeLock = new();
    private static string? _lastDedupKey;
    private static int _lastDedupCount;
    private static DateTime _lastDedupTime = DateTime.MinValue;

    /// <summary>向控制台发送事件消息（线程安全），同类型事件 2 秒内只报一次</summary>
    public static void SendEvent(string msg)
    {
        if (_pipeWriter == null) return;

        // WORLD_SEED 不参与去重——必须每次都发送
        // FILE 事件不参与去重——文件增删改必须每条都到
        bool isWorldSeed = msg.Contains("WORLD_SEED");
        bool isFileEvent = msg.StartsWith("EVENT:FILE:");
        // 取 EVENT:TYPE 作为去重 key，不是笼统的 EVENT
        var key = msg.Split(':').Length >= 3 ? msg.Split(':')[0] + ":" + msg.Split(':')[1] : msg.Split(':')[0];
        var now = DateTime.Now;

        if (!isWorldSeed && !isFileEvent && key == _lastDedupKey && (now - _lastDedupTime).TotalSeconds < 2)
        {
            _lastDedupCount++;
            return;
        }

        // 如果之前有折叠，发送计数
        if (_lastDedupCount > 0)
        {
            var foldMsg = $"{_lastDedupKey}:__FOLDED__:{_lastDedupCount}";
            try
            {
                lock (_pipeLock)
                {
                    _pipeWriter.WriteLine(foldMsg);
                    _pipeWriter.Flush();
                }
            }
            catch { }
            _lastDedupCount = 0;
        }

        _lastDedupKey = key;
        _lastDedupTime = now;

        try
        {
            // 不再写 mod_pipe_log.txt — 成百上千次磁盘 I/O 会严重阻塞游戏线程
            lock (_pipeLock)
            {
                _pipeWriter.WriteLine(msg);
                _pipeWriter.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerPlugin] Send error: {ex.Message}");
        }
    }

    private static void LogPipeIn(string msg)
    {
        // 日志写盘已禁用 — File.AppendAllText 在批量消息时严重阻塞
    }

    /// <summary>
    /// 后台线程：持续监听控制台消息，入队到 MessageQueue
    /// </summary>
    private static async Task ListenLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _pipe!.IsConnected)
            {
                var msg = await _pipeReader!.ReadLineAsync();
                if (msg == null) break; // 管道关闭

                Console.WriteLine($"[ServerPlugin] ← Console: {msg}");
                LogPipeIn(msg);
                MessageQueue.Enqueue(msg);
                lock (_pipeLock) { _pipeWriter!.WriteLine("OK"); }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerPlugin] Listen error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("[ServerPlugin] Pipe disconnected.");
            IsConnected = false;
            Cleanup();
        }
    }

    /// <summary>
    /// 游戏主线程调用——处理消息队列
    /// </summary>
    public static void ProcessPendingMessages()
    {
        while (true)
        {
            var msg = MessageQueue.Dequeue();
            if (msg == null) break;

            Console.WriteLine($"[ServerPlugin] Process msg: {msg}");

            // 解析消息
            var parts = msg.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0];
            Console.WriteLine($"[ServerPlugin]   cmd='{cmd}' parts={parts.Length}");

            switch (cmd)
            {
                case "CREATE_ACCOUNT":
                    HandleCreateAccount(parts);
                    break;

                case "DELETE_ACCOUNT":
                    HandleDeleteAccount(parts);
                    break;

                case "LAUNCH_SERVER":
                    HandleLaunchServer(parts);
                    break;
 
                case "SET_WORLD_SEED":
                    HandleSetWorldSeed(parts);
                    break;

                case "GET_WORLD_SEED":
                    HandleGetWorldSeed();
                    break;

                case "GET_SAVE_FILE":
                    HandleGetSaveFile();
                    break;

                case "GET_SAVE_LIST":
                    HandleGetSaveList(parts);
                    break;

                case "SYNC_FILES":
                    // 后台线程，避免阻塞游戏主线程
                    string filterComp = parts.Length >= 2 ? parts[1] : "";
                    ThreadPool.QueueUserWorkItem(_ => HandleSyncFiles(filterComp));
                    break;

                case "EVENT":
                    HandleClientEvent(parts, msg);
                    break;

                default:
                    Console.WriteLine($"[ServerPlugin] Unknown msg: {msg}");
                    break;
            }
        }
    }

    /// <summary>
    /// 创建扩展账户
    ///   CREATE_ACCOUNT:ServerName:AccountName:AccountPassword
    /// </summary>
    private static void HandleCreateAccount(string[] parts)
    {
        if (parts.Length < 4)
        {
            Console.WriteLine("[ServerPlugin] CREATE_ACCOUNT: insufficient params");
            return;
        }

        var serverName = parts[1];
        var accountName = parts[2];
        var accountPassword = parts[3];

        var os = OS.currentInstance;

        // 检查账户是否已存在
        var exists = SaveFileManager.Accounts.Any(a =>
            a.Username.Equals(accountName, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            os?.write($" ");
            os?.write($">>> Account [{accountName}] already exists.");
            Console.WriteLine($"[ServerPlugin] Account already exists: {accountName}");
            return;
        }

        // 创建账户
        var ok = SaveFileManager.AddUser(accountName, accountPassword);
        if (ok)
        {
            os?.write($" ");
            os?.write($">>> Account [{accountName}] created for server [{serverName}].");
            os?.write($">>> Password: {accountPassword}");
            Console.WriteLine($"[ServerPlugin] Account created: {accountName}@{serverName}");
        }
        else
        {
            os?.write($" ");
            os?.write($">>> Failed to create account [{accountName}].");
            Console.WriteLine($"[ServerPlugin] Account creation failed: {accountName}");
        }
    }

    /// <summary>
    /// 启动服务器——创建账户 + 进入游戏
    ///   LAUNCH_SERVER:ServerName:AccountName:AccountPassword:Port
    /// </summary>
    private static void HandleLaunchServer(string[] parts)
    {
        Console.WriteLine($"[HandleLaunch] parts.Length={parts.Length}");
        if (parts.Length < 5) { Console.WriteLine("[HandleLaunch] Too few parts"); return; }

        var serverName = parts[1];
        var accountName = parts[2];
        var accountPass = parts[3];
        var port = parts[4];

        Console.WriteLine($"[HandleLaunch] server={serverName} account={accountName} port={port}");

        var sman = Game1.getSingleton()?.sman;
        Console.WriteLine($"[HandleLaunch] ScreenManager={sman != null}");

        if (sman == null)
        {
            Console.WriteLine("[HandleLaunch] ScreenManager not ready, requeuing...");
            MessageQueue.Enqueue(string.Join(":", parts));
            return;
        }

        IsServerMode = true;

        // 确保事件转发器已订阅（首次启动时订阅）
        EventForwarder.Subscribe();

        Console.WriteLine($"[HandleLaunch] Calling CreateNewAccountForExtensionAndStart...");
        CreateAccountPatch.IsServerLaunch = true;
        MainMenu.CreateNewAccountForExtensionAndStart(
            accountName, accountPass, sman, null);
        Console.WriteLine($"[HandleLaunch] CreateNewAccountForExtensionAndStart returned.");
    }

    /// <summary>
    /// 从控制台接收固定种子设置
    ///   SET_WORLD_SEED:AccountName:Seed
    /// </summary>
    private static void HandleSetWorldSeed(string[] parts)
    {
        if (parts.Length < 3) { Console.WriteLine("[HandleSetWorldSeed] Too few parts"); return; }
        string accountName = parts[1];
        int seed = int.Parse(parts[2]);
        WorldSeedSync.PersistSeed(accountName, seed);
        Console.WriteLine($"[HandleSetWorldSeed] Persisted seed {seed} for {accountName}");
    }

    /// <summary>
    /// 响应 Terminal 的种子查询请求
    ///   GET_WORLD_SEED
    /// 回复：WORLD_SEED:{seed}:{childCounter}:{randomState}:{ipCounter}
    ///   seed          = 主种子
    ///   childCounter  = _childSeedCounter，对齐 new Random() 子种子
    ///   randomState   = Utils.random 内部 58 int 序列化
    ///   ipCounter     = GlobalIpCounter，对齐 NetworkMap.generateRandomIP()
    /// </summary>
    private static void HandleGetWorldSeed()
    {
        if (WorldSeedSync.IsInitialized)
        {
            string state = Utils.random.SaveState();
            int childCounter = WorldSeedSync.ChildSeedCounter;
            int ipCounter = 0;

            // 通过反射读取 SeedSyncGlobalPlugin.GlobalIpCounter（无编译依赖）
            try
            {
                var seedSyncType = Type.GetType("SeedSync.Global.SeedSyncGlobalPlugin");
                if (seedSyncType != null)
                {
                    var field = seedSyncType.GetField("GlobalIpCounter",
                        BindingFlags.Public | BindingFlags.Static);
                    if (field != null)
                        ipCounter = (int)field.GetValue(null);
                }
            }
            catch { /* SeedSyncGlobal 未加载时 ipCounter=0 是安全的 */ }

            // 用 SendEvent 而非 SendToConsole——SendEvent 有 _pipeLock 保护，
            // 避免与其他事件写入冲突（"stream is currently in use" 错误）
            ServerManager.SendEvent($"WORLD_SEED:{WorldSeedSync.MasterSeed}:{childCounter}:{state}:{ipCounter}");
            Console.WriteLine($"[ServerPlugin] GET_WORLD_SEED -> {WorldSeedSync.MasterSeed} (childCtr={childCounter}, state={state.Length} chars, ipCtr={ipCounter})");
        }
        else
        {
            SendToConsole("ERROR:NotInitialized");
            Console.WriteLine("[ServerPlugin] GET_WORLD_SEED called but seed not initialized");
        }
    }

    /// <summary>
    /// 扫描节点的完整文件结构，通过 SendEvent 同步给客户端。
    /// </summary>
    /// <param name="filterComp">可选，指定计算机 idName，为空时扫描所有节点</param>
    private static void HandleSyncFiles(string filterComp = "")
    {
        var os = OS.currentInstance;
        if (os?.netMap?.nodes == null)
        {
            Console.WriteLine("[ServerPlugin] SYNC_FILES: OS not ready, skipping.");
            return;
        }

        // 发送同步开始信号（客户端收到后进入静默模式 + 构建服务端清单）
        if (!string.IsNullOrEmpty(filterComp))
            SendEvent($"EVENT:FILE:SyncStart:{filterComp}");
        else
            SendEvent("EVENT:FILE:SyncStart:all");

        if (!string.IsNullOrEmpty(filterComp))
        {
            Console.WriteLine($"[ServerPlugin] SYNC_FILES: Scanning node '{filterComp}'...");
            var comp = os.netMap.nodes.Find(c => c.idName == filterComp);
            if (comp == null)
            {
                Console.WriteLine($"[ServerPlugin] SYNC_FILES: Node '{filterComp}' not found!");
                SendEvent($"EVENT:FILE:SyncEnd:{filterComp}");
                return;
            }
            if (comp.files?.root == null)
            {
                Console.WriteLine($"[ServerPlugin] SYNC_FILES: Node '{filterComp}' has no files.");
                SendEvent($"EVENT:FILE:SyncEnd:{filterComp}");
                return;
            }
            var count = ScanAndSendFolder(comp, comp.files.root, "");
            SendEvent($"EVENT:FILE:SyncEnd:{filterComp}");
            Console.WriteLine($"[ServerPlugin] SYNC_FILES complete for '{filterComp}': {count} items synced.");
        }
        else
        {
            Console.WriteLine("[ServerPlugin] SYNC_FILES: Scanning all nodes...");
            int folderCount = 0;
            foreach (var comp in os.netMap.nodes)
            {
                if (comp?.files?.root == null) continue;
                folderCount += ScanAndSendFolder(comp, comp.files.root, "");
            }
            SendEvent("EVENT:FILE:SyncEnd:all");
            Console.WriteLine($"[ServerPlugin] SYNC_FILES complete: {folderCount} folders synced.");
        }
    }

    /// <summary>递归扫描文件夹并发送 FILE 事件</summary>
    private static int ScanAndSendFolder(Computer comp, Folder folder, string parentPath)
    {
        int count = 0;
        string path = string.IsNullOrEmpty(parentPath) ? folder.name : parentPath + "/" + folder.name;

        // 发送子文件夹
        foreach (var sub in folder.folders)
        {
            string subPath = path + "/" + sub.name;
            string msg = $"EVENT:FILE:FolderAdded:{comp.idName}:{subPath}:{comp.ip}";
            SendEvent(msg);
            count++;
            count += ScanAndSendFolder(comp, sub, path);
        }

        // 发送文件（仅路径，不包含内容——完整文件状态由存档传输提供）
        foreach (var file in folder.files)
        {
            if (file == null || string.IsNullOrEmpty(file.name)) continue;
            string filePath = path + "/" + file.name;
            string msg = $"EVENT:FILE:FileAdded:{comp.idName}:{filePath}:{comp.ip}";
            SendEvent(msg);
            count++;
            // 每 50 个文件让出 Pipe 缓冲区，防止溢出
            if (count % 50 == 0)
                Thread.Sleep(1);
        }
        return count;
    }

    /// <summary>
    /// 列出指定用户的全部存档文件
    ///   GET_SAVE_LIST:{username}[:{extName}]
    /// 回复：SAVE_LIST:{count}:{size1}:{name1}:{size2}:{name2}:...
    /// </summary>
    private static void HandleGetSaveList(string[] parts)
    {
        string username = parts.Length >= 2 ? parts[1] : "";
        if (string.IsNullOrEmpty(username))
        {
            SendEvent("SAVE_LIST:0");
            return;
        }

        try
        {
            // 尝试通过 SaveFileManager 的 StorageMethod 定位存档路径
            string? saveDir = null;
            var storage0 = SaveFileManager.StorageMethods?[0];
            if (storage0 != null)
            {
                // 反射读 FullFolderPath
                var ffpProp = storage0.GetType().GetProperty("FullFolderPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ffpProp != null)
                {
                    string fullFolder = ffpProp.GetValue(storage0)?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(fullFolder) && Directory.Exists(fullFolder))
                        saveDir = fullFolder;
                }
            }

            // 如果 SaveFileManager 路径无效，尝试 Pathfinder HHBS 路径
            if (saveDir == null)
            {
                string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string extName = parts.Length >= 3 ? parts[2]
                    : (ExtensionLoader.ActiveExtensionInfo?.GetFoldersafeName() ?? "I");
                string pfDir = personal + "/My Games/HacknetPathfinder/Accounts/" + extName + "/HHBS/";
                if (Directory.Exists(pfDir))
                    saveDir = pfDir;
                else
                {
                    // 再试不带 HHBS 的路径
                    pfDir = personal + "/My Games/HacknetPathfinder/Accounts/" + extName + "/";
                    if (Directory.Exists(pfDir))
                        saveDir = pfDir;
                }
            }

            if (saveDir == null || !Directory.Exists(saveDir))
            {
                Console.WriteLine("[HandleGetSaveList] Cannot find save directory");
                SendEvent("SAVE_LIST:0");
                return;
            }

            // 扫描 save_{username}*.xml 文件
            string pattern = $"save_{username}*.xml";
            var files = Directory.GetFiles(saveDir, pattern)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            if (files.Count == 0)
            {
                Console.WriteLine($"[HandleGetSaveList] No saves found for {username} in {saveDir}");
                SendEvent("SAVE_LIST:0");
                return;
            }

            // 构建 SAVE_LIST:{count}:{size1}:{name1}:{size2}:{name2}:...
            var parts2 = new List<string> { files.Count.ToString() };
            foreach (var f in files)
            {
                parts2.Add(f.Length.ToString());
                parts2.Add(f.Name);
            }
            string result = "SAVE_LIST:" + string.Join(":", parts2);
            SendEvent(result);
            Console.WriteLine($"[HandleGetSaveList] Found {files.Count} saves for {username} in {saveDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HandleGetSaveList] Error: {ex.Message}");
            SendEvent("SAVE_LIST:0");
        }
    }

    /// <summary>
    /// 响应 Terminal 的存档请求。
    /// 存档写入桌面临时目录（Mod 与 Terminal 同机共享文件系统），
    /// Pipe 只发信号 SAVE_FILE_READY:{path}:{size}，不传数据本身。
    /// Terminal 收到信号后从磁盘读取，通过 LiteNetLib 发给客户端。
    /// </summary>
    private static void HandleGetSaveFile()
    {
        var os = OS.currentInstance;
        if (os == null || string.IsNullOrEmpty(os.SaveGameUserName))
        {
            Console.WriteLine("[HandleGetSaveFile] OS not ready or no SaveGameUserName");
            return;
        }

        try
        {
            // 先触发游戏保存，确保存档最新
            os.saveGame();

            // 读取存档
            using var stream = SaveFileManager.GetSaveReadStream(os.SaveGameUserName);
            if (stream == null)
            {
                Console.WriteLine("[HandleGetSaveFile] GetSaveReadStream returned null");
                return;
            }
            var memStream = new MemoryStream();
            stream.CopyTo(memStream);
            var data = memStream.ToArray();

            // 写入桌面临时目录（Mod 与 Terminal 共享文件系统）
            string tempDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "HacknetTransfer");
            Directory.CreateDirectory(tempDir);
            string tempPath = Path.Combine(tempDir, $"save_{os.SaveUserAccountName ?? "unknown"}.bin");
            File.WriteAllBytes(tempPath, data);

            // Pipe 只发信号（路径+大小），不传数据
            long size = new FileInfo(tempPath).Length;
            SendEvent($"SAVE_FILE_READY:{tempPath}:{size}");
            Console.WriteLine($"[HandleGetSaveFile] Written to {tempPath} ({size} bytes), signal sent via Pipe");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HandleGetSaveFile] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理客户端通过 Terminal 转发来的事件。
    /// </summary>
    private static void HandleClientEvent(string[] parts, string rawMsg)
    {
        if (parts.Length < 2) return;
        string eventType = parts[1]; // FILE, FLAG, PORT 等
        Console.WriteLine($"[HandleClientEvent] type={eventType}");

        // 只在游戏活跃时应用事件
        var os = OS.currentInstance;
        if (os == null) return;

        if (eventType == "FILE" && parts.Length >= 5)
        {
            string action = parts[2];
            string compID = parts[3];
            string filePath = parts[4];
            string sourceIP = parts.Length > 5 ? parts[5] : os.thisComputer.ip;

            var comp = os.netMap?.nodes?.Find(c =>
                c.idName.Equals(compID, StringComparison.OrdinalIgnoreCase));
            if (comp == null)
            {
                Console.WriteLine($"[HandleClientEvent] Computer not found: {compID}");
                return;
            }

            // 直接调用 EventReceiver 的静态方法来应用文件操作
            // 注意：客户端没引用 EventReceiver，我们反射调用或直接复制逻辑
            ApplyFileAction(comp, action, filePath, sourceIP, parts);
        }
        else
        {
            Console.WriteLine($"[HandleClientEvent] Unhandled event type: {eventType}");
        }
    }

    private static void ApplyFileAction(Computer comp, string action, string filePath, string sourceIP, string[] parts)
    {
        // 临时抑制事件转发，防止 FileSystemDetector 再次触发相同事件形成死循环
        EventForwarder.Suppress = true;
        try
        {
            var pathParts = filePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;

            switch (action)
            {
                case "FileAdded":
                {
                    string content = parts.Length > 6 ? parts[6] : "";
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        var sub = current.folders.FirstOrDefault(f => f.name == pathParts[i]);
                        if (sub == null)
                        {
                            sub = new Folder(pathParts[i]);
                            current.folders.Add(sub);
                        }
                        current = sub;
                    }
                    string fName = pathParts.Last();
                    if (!current.files.Any(f => f.name == fName))
                        current.files.Add(new FileEntry(content, fName));
                    Console.WriteLine($"[HandleClientEvent] FileAdded: {filePath}");
                    break;
                }

                case "FileRemoved":
                {
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        current = current.folders.FirstOrDefault(f => f.name == pathParts[i]);
                        if (current == null) return;
                    }
                    current.files.RemoveAll(f => f.name == pathParts.Last());
                    Console.WriteLine($"[HandleClientEvent] FileRemoved: {filePath}");
                    break;
                }

                case "FileContentChanged":
                {
                    if (parts.Length < 7) return;
                    string newContent = parts[6];
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        current = current.folders.FirstOrDefault(f => f.name == pathParts[i]);
                        if (current == null) return;
                    }
                    var file = current.files.FirstOrDefault(f => f.name == pathParts.Last());
                    if (file != null) file.data = newContent;
                    Console.WriteLine($"[HandleClientEvent] FileContentChanged: {filePath}");
                    break;
                }

                case "FileMoved":
                {
                    if (parts.Length < 8) return;
                    string fromName = parts[6];
                    string toName = parts[7];
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        current = current.folders.FirstOrDefault(f => f.name == pathParts[i]);
                        if (current == null) return;
                    }
                    var oldFile = current.files.FirstOrDefault(f => f.name == fromName);
                    if (oldFile != null)
                    {
                        string data = oldFile.data;
                        current.files.Remove(oldFile);
                        current.files.Add(new FileEntry(data ?? "", toName));
                    }
                    Console.WriteLine($"[HandleClientEvent] FileMoved: {fromName} -> {toName}");
                    break;
                }

                case "FolderAdded":
                {
                    for (int i = 0; i < pathParts.Length; i++)
                    {
                        var sub = current.folders.FirstOrDefault(f => f.name == pathParts[i]);
                        if (sub == null)
                        {
                            sub = new Folder(pathParts[i]);
                            current.folders.Add(sub);
                        }
                        current = sub;
                    }
                    Console.WriteLine($"[HandleClientEvent] FolderAdded: {filePath}");
                    break;
                }

                case "FolderRemoved":
                {
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        current = current.folders.FirstOrDefault(f => f.name == pathParts[i]);
                        if (current == null) return;
                    }
                    current.folders.RemoveAll(f => f.name == pathParts.Last());
                    Console.WriteLine($"[HandleClientEvent] FolderRemoved: {filePath}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HandleClientEvent] ApplyFileAction error: {ex.Message}");
        }
        finally
        {
            EventForwarder.Suppress = false;
        }
    }

    private static void HandleDeleteAccount(string[] parts)
    {
        if (parts.Length < 2) return;

        var accountName = parts[1];
        var os = OS.currentInstance;

        SaveFileManager.DeleteUser(accountName);
        os?.write($" ");
        os?.write($">>> Account [{accountName}] deleted.");
        Console.WriteLine($"[ServerPlugin] Account deleted: {accountName}");
    }

    private static void Cleanup()
    {
        try { _pipeReader?.Dispose(); } catch { }
        try { _pipeWriter?.Dispose(); } catch { }
        try { _pipe?.Dispose(); } catch { }
        _pipeReader = null;
        _pipeWriter = null;
        _pipe = null;
    }
}