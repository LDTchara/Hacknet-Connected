using System;
using System.Linq;
using System.Threading.Tasks;

namespace HacknetServerTerminal;

/// <summary>
/// 命令处理器 — 支持 seed 查看/设置
/// </summary>
internal static class CommandHandler
{
    private static ServerProfile _currentProfile = new();

    public static async Task RunLoop()
    {
        while (true)
        {
            var prefix = ConsoleState.CurrentState switch
            {
                "create" => $"({_currentProfile.ServerName}:create) ",
                _ => ""
            };

            var input = ConsoleHelper.ReadLine($"{prefix}> ");
            if (input == null) break;

            var trimmed = input.Trim();
            if (trimmed == "") continue;

            await Execute(trimmed);
        }
    }

    private static async Task Execute(string cmd)
    {
        try
        {
            var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();

            switch (ConsoleState.CurrentState)
            {
                case "normal":
                    await HandleNormal(command, parts);
                    break;
                case "create":
                    HandleCreate(command, parts);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
    }

    // ─── Normal ───

    private static async Task HandleNormal(string cmd, string[] parts)
    {
        if (ConsoleState.IsLoggedIn)
        {
            switch (cmd)
            {
                case "seed":
                    HandleSeedCommand(parts);
                    return;
                case "logout":
                    ConsoleState.IsLoggedIn = false;
                    Console.WriteLine($"已登出 [{_currentProfile.ServerName}]");
                    return;
                case "options":
                    ShowOptions(_currentProfile);
                    return;
                case "start":
                    await StartServer();
                    return;
                case "broadcast":
                    if (parts.Length < 2) { Console.WriteLine("用法: broadcast <消息>"); return; }
                    NetworkManager.Broadcast(string.Join(" ", parts, 1, parts.Length - 1));
                    Console.WriteLine("[广播] 已发送");
                    return;
                case "refresh":
                    if (!Program.IsModConnected)
                    {
                        string dbg = Program.DebugPipeStatus();
                        Console.WriteLine("[文件同步] Pipe 未连接，无法同步文件");
                        Console.WriteLine("[文件同步] " + dbg);
                        Console.WriteLine("[文件同步] 请依次操作：exit → 关游戏 → 重启终端 → 重启游戏 → 确认(Y) → login → start → refresh");
                        return;
                    }
                    if (parts.Length >= 2 && parts[1] == "fs")
                    {
                        if (parts.Length >= 3 && parts[2] != "playerid")
                        {
                            // refresh fs <compId>
                            string compId = parts[2];
                            Console.WriteLine($"[文件同步] 请求节点 {compId} 文件同步...");
                            await Program.SendToMod($"SYNC_FILES:{compId}", false);
                        }
                        else
                        {
                            // refresh fs [playerid id]
                            Console.WriteLine("[文件同步] 请求全量文件同步...");
                            await Program.SendToMod("SYNC_FILES", false);
                        }
                    }
                    else
                    {
                        Console.WriteLine("用法:");
                        Console.WriteLine("  refresh fs            - 全量文件系统同步");
                        Console.WriteLine("  refresh fs <computer> - 指定节点文件同步");
                        Console.WriteLine("  refresh fs playerid <id> - 指定玩家文件同步");
                    }
                    return;
                case "status":
                    ShowNetworkStatus();
                    return;
                case "help":
                    Console.WriteLine("  options       - 查看当前服务端配置");
                    Console.WriteLine("  start         - 启动服务器（创建账户）");
                    Console.WriteLine("  broadcast <m> - 广播消息给所有客户端");
                    Console.WriteLine("  refresh fs    - 全量文件系统同步");
                    Console.WriteLine("  status        - 显示服务端状态");
                    Console.WriteLine("  logout        - 登出");
                    Console.WriteLine("  seed [值]     - 查看或设置世界种子（0=自动生成）");
                    Console.WriteLine("  exit          - 退出");
                    return;
                case "exit":
                    Console.WriteLine("请求客户端上传存档...");
                    NetworkManager.Broadcast("SAVE_REQUIRED");
                    System.Threading.Thread.Sleep(2000);
                    Console.WriteLine("关闭服务端...");
                    Environment.Exit(0);
                    return;
                default:
                    Console.WriteLine($"未知: {cmd}  输入 help 查看");
                    break;
            }
            return;
        }

        // 未登录状态下的命令
        switch (cmd)
        {
            case "seed":
                HandleSeedCommand(parts);
                return;
            case "list":
                List();
                return;
            case "refresh":
                Console.WriteLine("[文件同步] 请先使用 login + start 启动服务器");
                return;
            case "broadcast":
                if (parts.Length < 2) { Console.WriteLine("用法: broadcast <消息>"); return; }
                NetworkManager.Broadcast(string.Join(" ", parts, 1, parts.Length - 1));
                Console.WriteLine("[广播] 已发送");
                return;
            case "status":
                ShowNetworkStatus();
                return;
            case "login":
                if (parts.Length < 2) { Console.WriteLine("用法: login <服务器名称>"); return; }
                await StartLogin(parts[1]);
                return;
            case "create":
                if (parts.Length < 2) { Console.WriteLine("用法: create <服务器名称>"); return; }
                StartCreate(parts[1]);
                return;
            case "delete":
                if (parts.Length < 2) { Console.WriteLine("用法: delete <服务器名称>"); return; }
                await DeleteServer(parts[1]);
                return;
            case "help":
                Console.WriteLine("  list              - 列出所有服务端");
                Console.WriteLine("  login <名称>      - 登录（需密码）");
                Console.WriteLine("  create <名称>     - 创建新服务端");
                Console.WriteLine("  delete <名称>     - 删除服务端");
                Console.WriteLine("  broadcast <消息>  - 广播消息给所有客户端");
                Console.WriteLine("  status            - 显示服务端状态");
                Console.WriteLine("  seed <名称> [值]  - 查看或设置指定服务端的种子");
                Console.WriteLine("  exit              - 退出");
                return;
            case "exit":
                Console.WriteLine("关闭服务端...");
                Environment.Exit(0);
                break;
            default:
                Console.WriteLine($"未知: {cmd}  输入 help 查看");
                break;
        }
    }

    // ─── Seed 命令 ───

    /// <summary>
    /// 处理 seed 命令（已登录和未登录状态共用）
    /// </summary>
    private static void HandleSeedCommand(string[] parts)
    {
        if (ConsoleState.IsLoggedIn)
        {
            // 已登录：seed 或 seed <值>
            if (parts.Length >= 2 && int.TryParse(parts[1], out int newSeed))
            {
                if (newSeed < 0) { Console.WriteLine("种子必须 >= 0（0=自动生成）"); return; }
                _currentProfile.WorldSeed = newSeed;
                ServerStorage.Save(_currentProfile);
                Console.WriteLine($"世界种子已设置为 {newSeed}" + (newSeed == 0 ? "（自动生成）" : ""));
            }
            else
            {
                Console.WriteLine($"当前种子: {_currentProfile.WorldSeed}" +
                    (_currentProfile.WorldSeed == 0 ? "（自动生成）" : ""));
                Console.WriteLine("使用 seed <数值> 设置固定种子，seed 0 恢复自动生成。");
            }
        }
        else
        {
            // 未登录：seed <名称> 或 seed <名称> <值>
            if (parts.Length < 2) { Console.WriteLine("用法: seed <服务器名称> [值]"); return; }

            var profile = ServerStorage.Find(parts[1]);
            if (profile == null) { Console.WriteLine($"找不到 [{parts[1]}]"); return; }

            if (parts.Length >= 3 && int.TryParse(parts[2], out int newSeed))
            {
                if (newSeed < 0) { Console.WriteLine("种子必须 >= 0（0=自动生成）"); return; }
                profile.WorldSeed = newSeed;
                ServerStorage.Save(profile);
                Console.WriteLine($"[{parts[1]}] 种子已设置为 {newSeed}" + (newSeed == 0 ? "（自动生成）" : ""));
            }
            else
            {
                Console.WriteLine($"[{parts[1]}] 种子: {profile.WorldSeed}" +
                    (profile.WorldSeed == 0 ? "（自动生成）" : ""));
                Console.WriteLine("使用 seed <名称> <数值> 设置固定种子。");
            }
        }
    }

    // ─── Login ───

    private static async Task DeleteServer(string name)
    {
        var profile = ServerStorage.Find(name);
        if (profile == null)
        {
            Console.WriteLine($"找不到 [{name}]。");
            return;
        }

        Console.Write($"确认删除 [{name}]？输入 Y 继续: ");
        var confirm = (Console.ReadLine() ?? "").Trim().ToUpper();
        if (confirm != "Y")
        {
            Console.WriteLine("已取消。");
            return;
        }

        if (ConsoleState.IsLoggedIn && _currentProfile.ServerName.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            ConsoleState.IsLoggedIn = false;
            Console.WriteLine("已登出。");
        }

        var accountName = profile.HostAccountName;
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            var reply = await Program.SendToMod($"DELETE_ACCOUNT:{accountName}");
            Console.WriteLine($"模组: {reply}");
        }

        if (ServerStorage.Delete(name))
            Console.WriteLine($"已删除服务端 [{name}]。");
        else
            Console.WriteLine($"删除失败 [{name}]。");
    }

    private static async Task StartServer()
    {
        var p = _currentProfile;

        if (!p.IsComplete)
        {
            Console.WriteLine("配置不完整，请使用 options 查看。");
            return;
        }

        if (string.IsNullOrWhiteSpace(p.Port))
        {
            Console.WriteLine("端口未设置。");
            return;
        }

        if (!IsPortAvailable(int.Parse(p.Port)))
        {
            Console.WriteLine($"端口 {p.Port} 已被占用！");
            return;
        }

        Console.WriteLine($"正在启动服务器 [{p.ServerName}]...");

        // 如果有固定种子，先发送 SET_WORLD_SEED 命令
        if (p.WorldSeed > 0)
        {
            Console.WriteLine($"使用固定种子: {p.WorldSeed}");
            await Program.SendToMod($"SET_WORLD_SEED:{p.HostAccountName}:{p.WorldSeed}");
        }
        else
        {
            Console.WriteLine("种子: 自动生成（使用 seed <值> 设置固定种子）");
        }

        var msg = $"LAUNCH_SERVER:{p.ServerName}:{p.HostAccountName}:{p.HostAccountPassword}:{p.Port}";
        var reply = await Program.SendToMod(msg);
        Console.WriteLine($"模组: {reply}");

        // start 后保持登录态，用户可继续使用 refresh/broadcast 等命令
        // 不需要登出 — 游戏端账户由模组管理，终端登录态独立
        Console.WriteLine($"服务器 [{p.ServerName}] 已启动。");
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            var listener = new System.Net.Sockets.TcpListener(
                System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task StartLogin(string name)
    {
        var profile = ServerStorage.Find(name);
        if (profile == null)
        {
            Console.WriteLine($"找不到 [{name}]。使用 list 查看。");
            return;
        }

        _currentProfile = profile.Clone();
        Console.Write("密码: ");
        var password = Console.ReadLine() ?? "";
        Console.WriteLine();

        if (ServerStorage.ValidatePassword(profile, password))
        {
            ConsoleState.IsLoggedIn = true;
            _currentProfile = profile.Clone();
            Console.WriteLine($"登录成功 [{profile.ServerName}]");
            Console.WriteLine($"  Port: {profile.Port}  Account: {profile.HostAccountName}" +
                (profile.WorldSeed > 0 ? $"  Seed: {profile.WorldSeed} ✱" : "  Seed: 自动生成"));
            Console.WriteLine("  使用 start 启动服务器。");
        }
        else
        {
            Console.WriteLine("密码错误");
        }
    }

    // ─── Create ───

    private static void StartCreate(string name)
    {
        if (ServerStorage.Find(name) != null)
        {
            Console.WriteLine($"[{name}] 已存在，使用 login {name}");
            return;
        }

        _currentProfile = new ServerProfile { ServerName = name };
        ConsoleState.CurrentState = "create";
        Console.WriteLine($"创建 [{name}] — 使用 set 配置参数，save 保存");
    }

    private static void HandleCreate(string cmd, string[] parts)
    {
        switch (cmd)
        {
            case "back":
            case "exit":
                ConsoleState.CurrentState = "normal";
                Console.WriteLine("已返回");
                break;

            case "seed":
                if (parts.Length < 2 || !int.TryParse(parts[1], out int newSeed) || newSeed < 0)
                {
                    Console.WriteLine("用法: seed <数值>（0=自动生成）");
                    return;
                }
                _currentProfile.WorldSeed = newSeed;
                Console.WriteLine($"WorldSeed = {newSeed}" + (newSeed == 0 ? "（自动生成）" : ""));
                break;

            case "options":
                Console.WriteLine("┌─ 当前配置 ─────────────────────");
                Console.WriteLine($"│ ServerName         = {_currentProfile.ServerName}");
                Console.WriteLine($"│ HostAccountName    = {_currentProfile.HostAccountName}");
                Console.WriteLine($"│ HostAccountPassword = {_currentProfile.HostAccountPassword}");
                Console.WriteLine($"│ Port               = {_currentProfile.Port}");
                Console.WriteLine($"│ WorldSeed          = {_currentProfile.WorldSeed}" +
                    (_currentProfile.WorldSeed == 0 ? "（自动生成）" : ""));
                Console.WriteLine("└────────────────────────────────");
                break;

            case "set":
                if (parts.Length < 3) { Console.WriteLine("用法: set <参数> <值>"); break; }
                var val = string.Join(" ", parts.Skip(2));
                switch (parts[1].ToLower())
                {
                    case "port":
                        _currentProfile.Port = val;
                        Console.WriteLine($"Port = {val}");
                        break;
                    case "servername":
                        _currentProfile.ServerName = val;
                        Console.WriteLine($"ServerName = {val}");
                        break;
                    case "hostaccountname":
                        _currentProfile.HostAccountName = val;
                        Console.WriteLine($"HostAccountName = {val}");
                        break;
                    case "hostaccountpassword":
                        _currentProfile.HostAccountPassword = val;
                        Console.WriteLine($"HostAccountPassword = {val}");
                        break;
                    case "seed":
                    case "worldseed":
                        if (int.TryParse(val, out int s) && s >= 0)
                        {
                            _currentProfile.WorldSeed = s;
                            Console.WriteLine($"WorldSeed = {val}" + (s == 0 ? "（自动生成）" : ""));
                        }
                        else
                        {
                            Console.WriteLine("种子必须 >= 0 的整数（0=自动生成）");
                        }
                        break;
                    default:
                        Console.WriteLine($"未知参数: {parts[1]}");
                        break;
                }
                break;

            case "save":
                if (!_currentProfile.IsComplete)
                {
                    Console.WriteLine("配置不完整，请检查选项 (options)");
                    break;
                }
                ServerStorage.Save(_currentProfile);
                Console.WriteLine($"已保存 [{_currentProfile.ServerName}]");
                Console.WriteLine($"  种子: {_currentProfile.WorldSeed}" +
                    (_currentProfile.WorldSeed == 0 ? "（自动生成）" : ""));
                ConsoleState.CurrentState = "normal";
                break;

            default:
                Console.WriteLine($"未知: {cmd}  help 查看");
                break;
        }
    }

    private static void ShowOptions(ServerProfile p)
    {
        Console.WriteLine("┌─ 当前配置 ─────────────────────");
        Console.WriteLine($"│ ServerName         = {p.ServerName}");
        Console.WriteLine($"│ HostAccountName    = {p.HostAccountName}");
        Console.WriteLine($"│ HostAccountPassword = {p.HostAccountPassword}");
        Console.WriteLine($"│ Port               = {p.Port}");
        Console.WriteLine($"│ WorldSeed          = {p.WorldSeed}" +
            (p.WorldSeed == 0 ? "（自动生成）" : ""));
        Console.WriteLine("└────────────────────────────────");
    }

    private static void ShowNetworkStatus()
    {
        Console.WriteLine($"网络端口: 9050");
        Console.WriteLine($"已连接客户端: {NetworkManager.PeerCount}");
        if (NetworkManager.PeerCount > 0)
        {
            Console.WriteLine("客户端列表:");
            foreach (var c in NetworkManager.GetAllClients())
                Console.WriteLine($"  P{c.PlayerId} {c.PlayerName}  Peer={c.PeerId}  Ping={c.Ping}ms");
        }
    }

    private static void List()
    {
        var all = ServerStorage.LoadAll();
        if (all.Count == 0)
        {
            Console.WriteLine("（无服务端） 使用 create <名称> 创建");
            return;
        }

        Console.WriteLine($"共 {all.Count} 个:");
        foreach (var p in all)
            Console.WriteLine($"  [{p.ServerName}]  Port={p.Port}  Account={p.HostAccountName}" +
                (p.WorldSeed > 0 ? $"  Seed={p.WorldSeed}" : ""));
    }
}
