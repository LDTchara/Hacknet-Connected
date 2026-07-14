using System;
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
                    var parts = msg.Split(':', 3);
                    if (parts.Length == 3)
                    {
                        string filePath = parts[1];
                        Console.WriteLine($"[Pipe] SAVE_FILE_READY: {filePath}");

                        try
                        {
                            if (File.Exists(filePath))
                            {
                                byte[] data = File.ReadAllBytes(filePath);
                                string b64 = Convert.ToBase64String(data);
                                int chunkSize = 60000;
                                int total = (b64.Length + chunkSize - 1) / chunkSize;

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

                Console.WriteLine("[Pipe] " + msg);

                // 广播 EVENT: / WORLD_SEED: / SAVE_LIST: 消息给客户端
                if (msg.StartsWith("EVENT:") || msg.StartsWith("WORLD_SEED:") || msg.StartsWith("SAVE_LIST:"))
                {
                    NetworkManager.Broadcast(msg);
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
