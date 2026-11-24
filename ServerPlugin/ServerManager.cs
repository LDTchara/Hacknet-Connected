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

    /// <summary>Suppress 状态防死锁上次检查时间</summary>
    private static DateTime? _lastSuppressCheck = null;

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

            // HNC.Sync：真正的启动在主线程的 Update 里做
            ServerSync.RequestStart();

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

                if (msg.StartsWith("SYNC:") || msg.StartsWith("SYNC_FROM:") || msg.StartsWith("SYNC_JOIN:"))
                    Console.WriteLine($"[ServerPlugin] ← Console: {msg.Substring(0, Math.Min(40, msg.Length))}...({msg.Length} 字符)");
                else
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
        // Suppress 防死锁检测（每 30 秒检查一次）
        if (_lastSuppressCheck == null || (DateTime.Now - _lastSuppressCheck.Value).TotalSeconds >= 30)
        {
            _lastSuppressCheck = DateTime.Now;
            if (EventForwarder.Suppress)
            {
                Console.WriteLine("[ServerPlugin] WARNING: Suppress stuck true for >30s! Forcing reset.");
                EventForwarder.Suppress = false;
            }
        }

        while (true)
        {
            var msg = MessageQueue.Dequeue();
            if (msg == null) break;

            // HNC.Sync 消息：先于 ':' 拆分处理，也不整条打印
            if (ServerSync.TryHandlePipeMessage(msg)) continue;
            if (ServerMail.TryHandlePipeMessage(msg)) continue;

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

                case "GET_PLAYERS_COUNT":
                    HandleGetPlayersCount();
                    break;

                case "GET_SAVE_FILE":
                    HandleGetSaveFile();
                    break;

                case "GET_SAVE_LIST":
                    HandleGetSaveList(parts);
                    break;

                case "CHECK_SAVE_CONFLICT":
                    HandleCheckSaveConflict(parts);
                    break;

                case "DELETE_SAVE_CONFLICT":
                    HandleDeleteSaveConflict(parts);
                    break;

                case "CHECK_ACCOUNT_SAVE":
                    HandleCheckAccountSave(parts);
                    break;

                case "CREATE_ACCOUNT_SAVE":
                    HandleCreateAccountSave(parts);
                    break;

                case "SYNC_FILES":
                    // 后台线程，避免阻塞游戏主线程
                    string filterComp = parts.Length >= 2 ? parts[1] : "";
                    ThreadPool.QueueUserWorkItem(_ => HandleSyncFiles(filterComp));
                    break;

                case "EXIT_SERVER":
                    Console.WriteLine("[ServerPlugin] Server exit requested, saving game...");
                    try
                    {
                        var exitOs = OS.currentInstance;
                        if (exitOs != null)
                        {
                            exitOs.saveGame();
                            Console.WriteLine("[ServerPlugin] Game saved before exit");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ServerPlugin] Save before exit error: {ex.Message}");
                    }
                    // 触发存档写入（供终端下载到客户端）
                    HandleGetSaveFile();
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
    /// 从拓展目录读取 Players-Ser.xml 或 Players.xml，返回玩家数量。
    /// 回复格式: PLAYERS_COUNT:X
    /// </summary>
    private static void HandleGetPlayersCount()
    {
        try
        {
            var eif = ExtensionLoader.ActiveExtensionInfo;
            if (eif == null)
            {
                SendToConsole("PLAYERS_COUNT:0");
                Console.WriteLine("[ServerPlugin] GET_PLAYERS_COUNT: ActiveExtensionInfo is null");
                return;
            }

            string extPath = eif.GetFullFolderPath();
            if (string.IsNullOrWhiteSpace(extPath) || !Directory.Exists(extPath))
            {
                SendToConsole("PLAYERS_COUNT:0");
                Console.WriteLine($"[ServerPlugin] GET_PLAYERS_COUNT: Extension path not found: {extPath}");
                return;
            }

            // 搜索 Players-Ser.xml / Players.xml
            string[] candidates =
            {
                Path.Combine(extPath, "Players-Ser.xml"),
                Path.Combine(extPath, "Content", "Players-Ser.xml"),
                Path.Combine(extPath, "Players.xml"),
                Path.Combine(extPath, "Content", "Players.xml")
            };

            string? foundPath = null;
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    foundPath = path;
                    break;
                }
            }

            if (foundPath == null)
            {
                SendToConsole("PLAYERS_COUNT:0");
                Console.WriteLine($"[ServerPlugin] GET_PLAYERS_COUNT: Players file not found in {extPath}");
                return;
            }

            var doc = System.Xml.Linq.XDocument.Load(foundPath);
            var countEl = doc.Root?.Element("PlayersCount");
            int count = 0;
            if (countEl?.Attribute("num") != null &&
                int.TryParse(countEl.Attribute("num").Value, out int parsed) && parsed > 0)
            {
                count = parsed;
            }

            SendToConsole($"PLAYERS_COUNT:{count}");
            Console.WriteLine($"[ServerPlugin] GET_PLAYERS_COUNT -> {count} (from {Path.GetFileName(foundPath)})");
        }
        catch (Exception ex)
        {
            SendToConsole("PLAYERS_COUNT:0");
            Console.WriteLine($"[ServerPlugin] GET_PLAYERS_COUNT error: {ex.Message}");
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
            SendEvent($"EVENT:FILE:SyncStart:{filterComp}");
            ScanAndSendFolder(comp, comp.files.root, "");
            SendEvent($"EVENT:FILE:SyncEnd:{filterComp}");
            Console.WriteLine($"[ServerPlugin] SYNC_FILES complete for '{filterComp}'.");
        }
        else
        {
            Console.WriteLine("[ServerPlugin] SYNC_FILES: Scanning all nodes...");
            int nodeCount = 0;
            foreach (var comp in os.netMap.nodes)
            {
                if (comp?.files?.root == null) continue;
                ScanAndSendFolder(comp, comp.files.root, "");
                nodeCount++;
            }
            SendEvent("EVENT:FILE:SyncEnd:all");
            Console.WriteLine($"[ServerPlugin] SYNC_FILES complete: {nodeCount} nodes synced.");
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
            string encodedSubPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(subPath ?? ""));
                string msg = $"EVENT:FILE:FolderAdded:{comp.idName}:{encodedSubPath}:{comp.ip}";
            SendEvent(msg);
            count++;
            count += ScanAndSendFolder(comp, sub, path);
        }

        // 发送文件（含内容——远程测试发现不同机 Utils.random 状态不同，binary 内容不一致）
        foreach (var file in folder.files)
        {
            if (file == null || string.IsNullOrEmpty(file.name)) continue;
            string filePath = path + "/" + file.name;
            string content = file.data ?? "";
            if (content.Length > 8000)
                content = content.Substring(0, 8000) + "...[TRUNCATED]";
            string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content ?? ""));
                string encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(filePath ?? ""));
                string msg = $"EVENT:FILE:FileAdded:{comp.idName}:{encodedPath}:{comp.ip}:{encoded}";
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
            // 遍历所有 StorageMethod 找有效存档目录（修复 [0] 指向不确定的问题）
            string? saveDir = null;
            var methods = SaveFileManager.StorageMethods;
            if (methods != null)
            {
                foreach (var storage in methods)
                {
                    if (storage == null) continue;
                    try
                    {
                        var ffpProp = storage.GetType().GetProperty("FullFolderPath",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (ffpProp != null)
                        {
                            string fullFolder = ffpProp.GetValue(storage)?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(fullFolder) && Directory.Exists(fullFolder))
                            {
                                saveDir = fullFolder;
                                Console.WriteLine($"[HandleGetSaveList] Using storage {storage.GetType().Name}: {saveDir}");
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 尝试用 Pathfinder 的 HHBS API（正确处理了 IL 补丁的路径修改）
            if (saveDir == null)
            {
                try
                {
                    string hhbsPath = HostileHackerBreakinSequence.GetBaseDirectory();
                    if (Directory.Exists(hhbsPath))
                        saveDir = hhbsPath;
                    else
                        Console.WriteLine($"[HandleGetSaveList] HHBS API path not found: {hhbsPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HandleGetSaveList] HHBS API error: {ex.Message}");
                }
            }

            // 终极 fallback：手动拼常见的 HacknetPathfinder 路径
            if (saveDir == null)
            {
                string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string extName = ExtensionLoader.ActiveExtensionInfo?.GetFoldersafeName() ?? "I";
                foreach (var candidate in new[] {
                    personal + "/My Games/HacknetPathfinder/Accounts/" + extName + "/HHBS/",
                    personal + "/My Games/HacknetPathfinder/Accounts/" + extName + "/",
                    personal + "/My Games/Hacknet/Accounts/" + extName + "/",
                })
                {
                    if (Directory.Exists(candidate))
                    { saveDir = candidate; break; }
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
    /// 检查指定用户的存档是否已存在（用于创建服务器前的冲突检测）
    ///   CHECK_SAVE_CONFLICT:{username}
    /// 回复：SAVE_CONFLICT:no 或 SAVE_CONFLICT:yes:{size}:{path}
    /// </summary>
    private static void HandleCheckSaveConflict(string[] parts)
    {
        string username = parts.Length >= 2 ? parts[1] : "";
        if (string.IsNullOrEmpty(username))
        {
            SendEvent("SAVE_CONFLICT:no");
            return;
        }

        try
        {
            string hhbsPath = HostileHackerBreakinSequence.GetBaseDirectory();
            string saveFile = Path.Combine(hhbsPath, $"save_{username}.xml");

            if (File.Exists(saveFile))
            {
                var fi = new FileInfo(saveFile);
                SendEvent($"SAVE_CONFLICT:yes:{fi.Length}:{fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"[CheckSaveConflict] Conflict found: {saveFile}");
            }
            else
            {
                SendEvent("SAVE_CONFLICT:no");
                Console.WriteLine($"[CheckSaveConflict] No conflict for {username}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CheckSaveConflict] Error: {ex.Message}");
            SendEvent("SAVE_CONFLICT:no");
        }
    }

    /// <summary>
    /// 删除冲突的存档文件并从 HHBS 账户列表中移除
    ///   DELETE_SAVE_CONFLICT:{username}
    /// </summary>
    private static void HandleDeleteSaveConflict(string[] parts)
    {
        string username = parts.Length >= 2 ? parts[1] : "";
        if (string.IsNullOrEmpty(username))
        {
            SendEvent("DELETE_SAVE_CONFLICT:fail:no username");
            return;
        }

        try
        {
            string hhbsPath = HostileHackerBreakinSequence.GetBaseDirectory();
            string saveFile = Path.Combine(hhbsPath, $"save_{username}.xml");

            // 1. 删除存档文件
            if (File.Exists(saveFile))
            {
                File.Delete(saveFile);
                Console.WriteLine($"[DeleteSaveConflict] Deleted: {saveFile}");
            }

            // 2. 从所有 StorageMethod 的 manifest 移除对应账户（冗余操作）
            try
            {
                var methods = SaveFileManager.StorageMethods;
                if (methods != null)
                {
                    foreach (var storage in methods)
                    {
                        if (storage == null) continue;
                        var manifest = storage.GetSaveManifest();
                        var removed = manifest.Accounts.RemoveAll(a =>
                            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                            a.FileUsername?.Equals($"save_{username}.xml", StringComparison.OrdinalIgnoreCase) == true);
                        if (removed > 0)
                        {
                            manifest.Save(storage);
                            Console.WriteLine($"[DeleteSaveConflict] Removed {removed} account(s) from {storage.GetType().Name} manifest");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteSaveConflict] Manifest removal error: {ex.Message}");
            }

            SendEvent("DELETE_SAVE_CONFLICT:ok");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeleteSaveConflict] Error: {ex.Message}");
            SendEvent($"DELETE_SAVE_CONFLICT:fail:{ex.Message}");
        }
    }

    /// <summary>
    /// 检查指定用户的存档是否已存在（供 creataccount 指令使用）
    ///   CHECK_ACCOUNT_SAVE:{username}
    /// 回复：ACCOUNT_SAVE_EXISTS:yes 或 ACCOUNT_SAVE_EXISTS:no
    /// </summary>
    private static void HandleCheckAccountSave(string[] parts)
    {
        string username = parts.Length >= 2 ? parts[1] : "";
        if (string.IsNullOrEmpty(username))
        {
            SendEvent("ACCOUNT_SAVE_EXISTS:no");
            return;
        }

        try
        {
            string filePath = SaveFileManager.GetFilePathForLogin(username, "");
            bool exists = !string.IsNullOrEmpty(filePath) && SaveFileHelper.SaveExists(filePath);
            SendEvent(exists ? "ACCOUNT_SAVE_EXISTS:yes" : "ACCOUNT_SAVE_EXISTS:no");
            Console.WriteLine($"[CheckAccountSave] username={username} exists={exists}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CheckAccountSave] Error: {ex.Message}");
            SendEvent("ACCOUNT_SAVE_EXISTS:no");
        }
    }

    /// <summary>
    /// 为指定用户创建存档（供 creataccount 指令使用）
    ///   CREATE_ACCOUNT_SAVE:{username}:{password}
    /// 回复：ACCOUNT_SAVE_CREATED:ok 或 ACCOUNT_SAVE_CREATED:fail:{reason}
    /// </summary>
    private static void HandleCreateAccountSave(string[] parts)
    {
        string username = parts.Length >= 2 ? parts[1] : "";
        string password = parts.Length >= 3 ? parts[2] : "";

        if (string.IsNullOrEmpty(username))
        {
            SendEvent("ACCOUNT_SAVE_CREATED:fail:no username");
            return;
        }

        try
        {
            // 先检查账户是否已注册
            bool accountExists = SaveFileManager.Accounts.Any(a =>
                a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (!accountExists)
            {
                // 注册账户
                SaveFileManager.AddUser(username, password);
                SaveFileHelper.SaveManifestToAll();
                Console.WriteLine($"[CreateAccountSave] Account registered: {username}");
            }

            // 再次检查存档文件是否已存在
            string filePath = SaveFileManager.GetFilePathForLogin(username, password);
            bool saveAlreadyExists = !string.IsNullOrEmpty(filePath) && SaveFileHelper.SaveExists(filePath);

            if (saveAlreadyExists)
            {
                SendEvent($"ACCOUNT_SAVE_CREATED:fail:save already exists for {username}");
                Console.WriteLine($"[CreateAccountSave] Save already exists for {username}");
                return;
            }

            SendEvent("ACCOUNT_SAVE_CREATED:ok");
            Console.WriteLine($"[CreateAccountSave] Account ready: {username}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateAccountSave] Error: {ex.Message}");
            SendEvent($"ACCOUNT_SAVE_CREATED:fail:{ex.Message}");
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

            // 遍历所有 StorageMethod 找存档（修复 [0] 指向不确定的问题）
            using var stream = SaveFileHelper.GetReadStream(os.SaveGameUserName);
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
            string filePath = parts.Length > 4 ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[4])) : "";
            string sourceIP = parts.Length > 5 ? parts[5] : os.thisComputer.ip;

            var comp = os.netMap?.nodes?.Find(c =>
                c.idName.Equals(compID, StringComparison.OrdinalIgnoreCase));
            if (comp == null)
            {
                Console.WriteLine($"[HandleClientEvent] Computer not found: {compID}");
                return;
            }

            // 直接调用 EventReceiver 的静态方法来应用文件操作
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
        // 如果上一次异常导致 Suppress 没被复位，先检测并复位
        if (EventForwarder.Suppress)
        {
            Console.WriteLine("[ServerPlugin] WARNING: Suppress was already true! Auto-reset to prevent lockout.");
        }
        EventForwarder.Suppress = true;
        try
        {
            var pathParts = filePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Folder current = comp.files.root;

            switch (action)
            {
                case "FileAdded":
                {
                    string content = parts.Length > 6 ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[6])) : "";
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
                    string newContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[6]));
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