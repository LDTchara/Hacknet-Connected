using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace HacknetServerTerminal;

internal class Program
{
    private static NamedPipeServerStream? _pipe;
    private static StreamReader? _pipeReader;
    private static StreamWriter? _pipeWriter;
    private static CancellationTokenSource? _pipeCts;

    /// <summary>
    /// 信号量：SendToMod 占用 Pipe 时，PipeListenLoop 暂停读取。
    /// 避免两个线程同时 ReadLine 冲突。
    /// </summary>
    private static readonly ManualResetEventSlim _pipeBusy = new(false);

    /// <summary>Pipe 是否已连接且可用（start 命令后为 true）</summary>
    public static bool IsModConnected => _pipe?.IsConnected == true && _pipeWriter != null;

    /// <summary>返回 Pipe 状态字符串用于调试</summary>
    public static string DebugPipeStatus()
    {
        if (_pipe == null) return "PIPE=NULL (未创建，请重启终端)";
        if (!_pipe.IsConnected) return "PIPE=已断开 (游戏进程可能已关闭，重启游戏+终端)";
        if (_pipeWriter == null) return "PIPE=已连接但未完成握手 (需要按Y确认)";
        return "PIPE=已连接+已握手 OK";
    }

    /// <summary>Mod 回复缓存（供 CommandHandler 同步等待）</summary>
    private static volatile string? _pendingModReply;

    /// <summary>发送消息并同步等待 Mod 回复（阻塞当前线程，最多等 5 秒）</summary>
    public static string? SendToModAndWait(string msg)
    {
        _pendingModReply = null;
        _ = SendToMod(msg, false);
        for (int i = 0; i < 50; i++)
        {
            if (_pendingModReply != null) break;
            Thread.Sleep(100);
        }
        string? reply = _pendingModReply;
        _pendingModReply = null;
        return reply;
    }

    /// <summary>终端日志文件路径（桌面 TerminLog_yyyy-MM-dd.txt）</summary>
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        $"TerminalLog_{DateTime.Now:yyyy-MM-dd}.txt");

    /// <summary>写入终端日志到桌面文件</summary>
    public static void LogToDesktop(string tag, string msg)
    {
        try
        {
            File.AppendAllText(_logPath,
                $"{DateTime.Now:HH:mm:ss.fff} [{tag}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>Pipe 写操作信号量（防并发写导致管道缓冲区满）</summary>
    private static readonly SemaphoreSlim _pipeWriteSemaphore = new(1, 1);

    /// <summary>因背压被推迟的事件队列（超时丢弃→入队，等管道空闲后重试）</summary>
    private static readonly ConcurrentQueue<string> _deferredEventQueue = new();

    /// <summary>推迟事件总计数（用于控制台显示）</summary>
    internal static int DeferredEventCount => _deferredEventQueue.Count;

    /// <summary>一次自动重试最多发多少条（防止再次引发背压）</summary>
    private const int MaxAutoRetryPerWrite = 50;

    /// <summary>
    /// 向模组发送消息（发后即忘，不阻塞 PipeListenLoop）。
    /// 用于高频事件转发——不设 _pipeBusy、不等回复。
    /// 信号量串行化写入；超时时入队等重试，不丢弃。
    /// 写入成功后自动消耗部分推迟队列。
    /// </summary>
    public static async Task SendToModFireForget(string msg)
    {
        if (_pipeWriter == null) return;

        // 背压保护：信号量等太久就入推迟队列
        bool acquired = await _pipeWriteSemaphore.WaitAsync(500);
        if (!acquired)
        {
            _deferredEventQueue.Enqueue(msg);
            return;
        }

        try
        {
            // 发送本次消息
            await _pipeWriter.WriteLineAsync(msg);
            await _pipeWriter.FlushAsync();

            // 管道空闲了，自动消耗一部分推迟队列
            int retried = 0;
            while (retried < MaxAutoRetryPerWrite && _deferredEventQueue.TryDequeue(out var deferred))
            {
                await _pipeWriter.WriteLineAsync(deferred);
                await _pipeWriter.FlushAsync();
                retried++;
            }
            if (retried > 0)
                Console.WriteLine($"[Pipe] 自动重试 {retried} 条推迟事件，剩余 {_deferredEventQueue.Count}");
        }
        catch (Exception ex)
        {
            // 写入失败 → 放回队列重试
            _deferredEventQueue.Enqueue(msg);
            Console.WriteLine($"[Pipe] SendToModFireForget error: {ex.Message}，事件已入重试队列");
        }
        finally
        {
            _pipeWriteSemaphore.Release();
        }
    }

    /// <summary>
    /// 强制重试所有推迟事件（供 retryevents 指令调用）。
    /// 返回实际重试的条数。
    /// </summary>
    public static int RetryDeferredEvents()
    {
        if (_pipeWriter == null) return 0;

        int count = _deferredEventQueue.Count;
        if (count == 0)
        {
            Console.WriteLine("[Pipe] 推迟队列为空，无需重试");
            return 0;
        }

        // 获取信号量，防止与 SendToModFireForget 并发写 Pipe
        if (!_pipeWriteSemaphore.Wait(2000))
        {
            Console.WriteLine("[Pipe] 获取写信号量超时(2s)，请稍后再试");
            return 0;
        }

        int retried = 0;
        try
        {
            while (_deferredEventQueue.TryDequeue(out var msg))
            {
                try
                {
                    _pipeWriter.WriteLine(msg);
                    _pipeWriter.Flush();
                    retried++;
                }
                catch (Exception ex)
                {
                    // 重试失败再放回去
                    _deferredEventQueue.Enqueue(msg);
                    Console.WriteLine($"[Pipe] 重试失败: {ex.Message}，剩余 {_deferredEventQueue.Count}");
                    break;
                }
            }
        }
        finally
        {
            _pipeWriteSemaphore.Release();
        }

        Console.WriteLine($"[Pipe] 手动重试 {retried}/{count} 条推迟事件，剩余 {_deferredEventQueue.Count}");
        return retried;
    }

    /// <summary>
    /// 向模组发送消息（发后即忘）。
    /// 回复通过 PipeListenLoop 接收和处理，不再从这里读取 Pipe，
    /// 避免和 PipeListenLoop 竞争同一个 _pipeReader。
    /// </summary>
    public static async Task<string> SendToMod(string msg, bool expectReply = true)
    {
        if (_pipeWriter == null || _pipeReader == null)
            return "ERROR:NotConnected";

        _pipeBusy.Set();

        try
        {
            await _pipeWriter.WriteLineAsync(msg);
            await _pipeWriter.FlushAsync();

            // 不再从 _pipeReader 读 — PipeListenLoop 会处理所有回复。
            // 短暂等待让 PipeListenLoop 有机会读到 ACK。
            await Task.Delay(50);
            return "OK";
        }
        catch (Exception ex)
        {
            return $"ERROR:{ex.Message}";
        }
        finally
        {
            _pipeBusy.Reset();
        }
    }

    static async Task Main(string[] args)
    {
        Console.Title = "Hacknet Server Console";
        Console.WriteLine("=== Hacknet Server Console ===");
        Console.WriteLine();

        _pipe = new NamedPipeServerStream(
            "HacknetServerPipe", PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);

        Console.Write("Waiting for game mod...");

        try
        {
            await _pipe.WaitForConnectionAsync();
            Console.WriteLine();
            Console.WriteLine("Game mod connected");

            _pipeReader = new StreamReader(_pipe);
            _pipeWriter = new StreamWriter(_pipe) { AutoFlush = true };

            var hello = await _pipeReader.ReadLineAsync();
            if (hello != "HELLO")
            {
                Console.WriteLine("Protocol error: " + hello);
                return;
            }

            Console.Write("Press Y to confirm -> ");
            var confirm = Console.ReadLine()?.Trim().ToUpper();
            if (confirm != "Y")
            {
                Console.WriteLine("Cancelled");
                return;
            }

            await _pipeWriter.WriteLineAsync("OK");
            await _pipeWriter.FlushAsync();
            Console.WriteLine("Bound! Entering command mode.");

            // 启动 Pipe 后台读线程
            _pipeCts = new CancellationTokenSource();
            LogToDesktop("INIT", "Pipe connected, entering command mode");

            _ = Task.Run(() => PipeListenLoop(_pipeCts.Token));

            NetworkManager.Start(9050);
        }
        catch (Exception ex)
        {
            Console.WriteLine("\nConnection failed: " + ex.Message);
            Console.WriteLine("Start console first, then click [Start Server] in game.");
            return;
        }

        await CommandHandler.RunLoop();

        _pipeCts?.Cancel();
        NetworkManager.Stop();
    }

    /// <summary>
    /// 后台线程：持续读取模组推送的事件消息。
    /// 当 SendToMod 占用 Pipe 时(_pipeBusy)，暂停读取。
    /// </summary>
    private static async Task PipeListenLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _pipe != null && _pipe.IsConnected)
            {
                // 如果 SendToMod 正在使用 Pipe，等待它完成
                if (_pipeBusy.IsSet)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                var msg = await _pipeReader!.ReadLineAsync();
                if (msg == null) break;

                // ── 存档就绪：读盘 → 分块 → 有序广播（带进度条） ──
                if (msg.StartsWith("SAVE_FILE_READY:"))
                {
                    // 路径含盘符(E:)，用 Split(':') 会切碎。改用 IndexOf + LastIndexOf
                    int firstColon = msg.IndexOf(':');
                    int lastColon = msg.LastIndexOf(':');
                    if (firstColon > 0 && lastColon > firstColon)
                    {
                        string filePath = msg.Substring(firstColon + 1, lastColon - firstColon - 1);
                        Console.WriteLine($"[Pipe] SAVE_FILE_READY: {filePath}");

                        try
                        {
                            if (File.Exists(filePath))
                            {
                                byte[] data = File.ReadAllBytes(filePath);
                                string b64 = Convert.ToBase64String(data);
                                int chunkSize = 60000;
                                int total = (b64.Length + chunkSize - 1) / chunkSize;

                                // 存档到 Storage/{serverName}/
                                string serverName = CommandHandler.CurrentServerName;
                                if (!string.IsNullOrEmpty(serverName))
                                {
                                    string storageDir = Path.Combine("Storage", serverName);
                                    Directory.CreateDirectory(storageDir);
                                    string saveName = Path.GetFileName(filePath).Replace(".bin", ".xml");
                                    string storagePath = Path.Combine(storageDir, saveName);
                                    File.WriteAllBytes(storagePath, data);
                                    Console.WriteLine($"[Pipe] Archived to {storagePath}");
                                }

                                Console.WriteLine($"[Pipe] File read: {data.Length} bytes, {total} chunks");

                                // 先发文件信息（总块数）
                                NetworkManager.Broadcast($"S_FILE_INFO:{total}");

                                // 逐块发送（ReliableOrdered 保证顺序到达）
                                for (int i = 0; i < total; i++)
                                {
                                    int start = i * chunkSize;
                                    int len = Math.Min(chunkSize, b64.Length - start);
                                    var chunk = b64.Substring(start, len);
                                    NetworkManager.Broadcast($"S_FILE:{i}:{total}:{chunk}");

                                    // 终端进度条
                                    int pct = (i + 1) * 100 / total;
                                    int n = (i + 1) * 20 / total;
                                    Console.Write($"\r[Pipe] Transfer: [{new string('=', n).PadRight(20)}] {pct}% ({i + 1}/{total})");
                                }
                                Console.WriteLine();

                                NetworkManager.Broadcast($"S_FILE_END:{total}");
                                Console.WriteLine($"[Pipe] Save file transfer complete ({total} chunks)");
                                LogToDesktop("SAVE_FILE", $"File={filePath} size={data.Length} {total} chunks");

                                try { File.Delete(filePath); } catch { }
                            }
                            else
                            {
                                Console.WriteLine($"[Pipe] SAVE_FILE_READY file not found: {filePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Pipe] SAVE_FILE_READY error: {ex.Message}");
                        }
                    }
                    continue;
                }

                // Mod 回复（供应程序同步等待）
                if (msg.StartsWith("SAVE_CONFLICT:") || msg.StartsWith("DELETE_SAVE_CONFLICT:") ||
                    msg.StartsWith("ACCOUNT_SAVE_EXISTS:") || msg.StartsWith("ACCOUNT_SAVE_CREATED:") ||
                    msg.StartsWith("PLAYERS_COUNT:"))
                {
                    _pendingModReply = msg;
                    continue;
                }

                // ── 服务端游戏发给客户端的邮件 ──
                if (msg.StartsWith("MAIL_OUT:"))
                {
                    try { NetworkManager.HandleServerMail(msg); }
                    catch (Exception mailEx) { Console.WriteLine($"[Pipe] 邮件转发失败（非致命）: {mailEx.Message}"); }
                    continue;
                }

                // ── HNC.Sync：按头部路由给客户端（载荷可能很大，不打印） ──
                if (msg.StartsWith("SYNC:"))
                {
                    try { NetworkManager.RouteSync(msg); }
                    catch (Exception syncEx)
                    {
                        Console.WriteLine($"[Pipe] SYNC 路由失败（非致命）: {syncEx.Message}");
                    }
                    continue;
                }

                Console.WriteLine("[Pipe] " + msg);

                // 广播 EVENT: / WORLD_SEED: / SAVE_LIST: 消息给客户端
                // 独立 try-catch：Broadcast 内部可能因日志文件竞争抛 IOException，不能杀死循环
                if (msg.StartsWith("EVENT:") || msg.StartsWith("WORLD_SEED:") || msg.StartsWith("SAVE_LIST:"))
                {
                    // EVENT:FILE 消息在 refresh fs 时可能成百上千条瞬间涌入，
                    // 加 2ms 延迟让 LiteNetLib 缓冲区有足够时间排空，防止 overflow
                    if (msg.StartsWith("EVENT:FILE:"))
                        await Task.Delay(2);
                    try { NetworkManager.Broadcast(msg); }
                    catch (Exception broadcastEx)
                    {
                        Console.WriteLine($"[Pipe] Broadcast error (非致命): {broadcastEx.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine("[Pipe] error: " + ex.Message);
        }

        Console.WriteLine("[Pipe] disconnected");
    }
}
