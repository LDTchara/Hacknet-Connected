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

    /// <summary>当前已登录的服务器名称（供 Program 等外部使用）</summary>
    public static string CurrentServerName => _currentProfile?.ServerName ?? "";

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
                case "creataccount":
                    await CreateAccountSave();
                    return;
                case "retryevents":
                    RetryEvents();
                    return;
                case "players":
                    HandlePlayersCommand(parts);
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
                    Console.WriteLine("  creataccount  - 创建账户存档（存档不存在时）");
                    Console.WriteLine("  retryevents   - 重试因管道背压未送达模组的事件");
                    Console.WriteLine("  players       - 管理已知玩家列表 (list/add/remove/edit)");
                    Console.WriteLine("  broadcast <m> - 广播消息给所有客户端");
                    Console.WriteLine("  refresh fs    - 全量文件系统同步");
                    Console.WriteLine("  status        - 显示服务端状态");
                    Console.WriteLine("  logout        - 登出");
                    Console.WriteLine("  seed [值]     - 查看或设置世界种子（0=自动生成）");
                    Console.WriteLine("  exit          - 退出");
                    return;
                case "exit":
                    Console.WriteLine("请求客户端上传存档（等待 6 秒完成传输）...");
                    NetworkManager.Broadcast("SAVE_REQUIRED");
                    System.Threading.Thread.Sleep(6000);
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

        // 同步删除玩家数据（和服务器 profile 解耦）
        if (!string.IsNullOrWhiteSpace(accountName) && PlayerDataManager.RemovePlayer(accountName, name))
            Console.WriteLine($"已删除玩家 [{accountName}] 的数据。");
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

        // 将主机管理员注册到玩家列表（作为服务端自己的账号）
        if (!string.IsNullOrWhiteSpace(p.HostAccountName))
        {
            var hostPlayer = PlayerDataManager.CreatePlayer(p.HostAccountName, p.ServerName);
            Console.WriteLine($"主机玩家 [{p.HostAccountName}] (Id={hostPlayer.Id}) 已加入玩家列表。");
        }

        var msg = $"LAUNCH_SERVER:{p.ServerName}:{p.HostAccountName}:{p.HostAccountPassword}:{p.Port}";
        var reply = await Program.SendToMod(msg);
        Console.WriteLine($"模组: {reply}");

        // start 后保持登录态，用户可继续使用 refresh/broadcast 等命令
        // 不需要登出 — 游戏端账户由模组管理，终端登录态独立
        Console.WriteLine($"服务器 [{p.ServerName}] 已启动。");
    }

    /// <summary>
    /// 创建账户存档（供 creataccount 指令使用）。
    /// 先检查存档是否已存在，不存在则创建。
    /// </summary>
    private static async Task CreateAccountSave()
    {
        var p = _currentProfile;

        if (string.IsNullOrWhiteSpace(p.HostAccountName))
        {
            Console.WriteLine("HostAccountName 未设置，请先用 options 查看配置。");
            return;
        }

        if (!Program.IsModConnected)
        {
            Console.WriteLine("Mod 未连接，无法创建账户。请先确认游戏已启动并按下 Y。");
            return;
        }

        // 1. 检查存档是否已存在
        Console.WriteLine($"检查 [{p.HostAccountName}] 的存档状态...");
        string? checkReply = Program.SendToModAndWait($"CHECK_ACCOUNT_SAVE:{p.HostAccountName}");

        if (checkReply == "ACCOUNT_SAVE_EXISTS:yes")
        {
            Console.WriteLine("存档已存在，指令不可用。");
            Console.WriteLine("如需重置请使用 delete 指令删除服务端后重新 create。");
            return;
        }
        else if (checkReply == null)
        {
            Console.WriteLine("Mod 未回复，请确认游戏正在运行。");
            return;
        }

        // 2. 存档不存在，创建
        Console.WriteLine("存档不存在，正在创建...");
        string password = p.HostAccountPassword;
        string? createReply = Program.SendToModAndWait($"CREATE_ACCOUNT_SAVE:{p.HostAccountName}:{password}");

        if (createReply == "ACCOUNT_SAVE_CREATED:ok")
        {
            Console.WriteLine($"账户 [{p.HostAccountName}] 存档已创建。");
            Console.WriteLine("现在可以使用 start 指令启动服务器。");
        }
        else if (createReply != null && createReply.StartsWith("ACCOUNT_SAVE_CREATED:fail:"))
        {
            string reason = createReply.Substring("ACCOUNT_SAVE_CREATED:fail:".Length);
            Console.WriteLine($"创建失败: {reason}");
        }
        else
        {
            Console.WriteLine($"创建失败，Mod 回复: {createReply ?? "(无回复)"}");
        }
    }

    /// <summary>
    /// 重试因管道背压未送达模组的事件（供 retryevents 指令使用）。
    /// </summary>
    private static void RetryEvents()
    {
        if (!Program.IsModConnected)
        {
            Console.WriteLine("Mod 未连接，无法重试。");
            return;
        }

        int count = Program.DeferredEventCount;
        if (count == 0)
        {
            Console.WriteLine("没有待重试的事件。");
            return;
        }

        Console.WriteLine($"正在重试 {count} 条推迟事件...");
        int retried = Program.RetryDeferredEvents();
        Console.WriteLine($"重试完成: {retried} 条已发送，{Program.DeferredEventCount} 条剩余。");
    }

    /// <summary>
    /// 管理已知玩家列表（供 players 指令使用）
    ///   players list          — 列出所有
    ///   players add <name>    — 添加
    ///   players remove <name> — 删除
    ///   players edit <a> <b>  — 改名
    /// </summary>
    private static void HandlePlayersCommand(string[] parts)
    {
        if (parts.Length < 2) { Console.WriteLine("用法: players list/add/remove/edit"); return; }
        string serverName = _currentProfile.ServerName;

        switch (parts[1].ToLower())
        {
            case "list":
                var all = PlayerDataManager.GetAllPlayers(serverName);
                if (all.Count == 0)
                {
                    Console.WriteLine("（无已知玩家）");
                    return;
                }
                Console.WriteLine($"共 {all.Count} 个已知玩家:");
                foreach (var p in all)
                {
                    string seedInfo = p.WorldSeed > 0 ? $" Seed={p.WorldSeed}" : "";
                    string loginInfo = p.LastLogin > DateTime.MinValue
                        ? $" 上次={p.LastLogin:yyyy-MM-dd HH:mm}" : "";
                    Console.WriteLine($"  [P{p.Id}] {p.Name}{seedInfo}{loginInfo}");
                }
                break;

            case "add":
                if (parts.Length < 3) { Console.WriteLine("用法: players add <玩家名称>"); return; }
                if (PlayerDataManager.CreatePlayer(parts[2], serverName) != null)
                    Console.WriteLine($"已添加玩家 [{parts[2]}]");
                else
                    Console.WriteLine($"添加失败（可能已存在）");
                break;

            case "remove":
                if (parts.Length < 3) { Console.WriteLine("用法: players remove <玩家名称>"); return; }
                if (PlayerDataManager.RemovePlayer(parts[2], serverName))
                    Console.WriteLine($"已删除玩家 [{parts[2]}]");
                else
                    Console.WriteLine($"找不到 [{parts[2]}]");
                break;

            case "edit":
                if (parts.Length < 4) { Console.WriteLine("用法: players edit <旧名称> <新名称>"); return; }
                if (PlayerDataManager.RenamePlayer(parts[2], parts[3], serverName))
                    Console.WriteLine($"已重命名: {parts[2]} → {parts[3]}");
                else
                    Console.WriteLine($"重命名失败（找不到 [{parts[2]}] 或 [{parts[3]}] 已存在）");
                break;

            default:
                Console.WriteLine("用法: players list/add/remove/edit");
                break;
        }
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

                // 冲突检测：检查 HHBS 是否已存在同名存档
                if (Program.IsModConnected && !string.IsNullOrEmpty(_currentProfile.HostAccountName))
                {
                    string? reply = Program.SendToModAndWait($"CHECK_SAVE_CONFLICT:{_currentProfile.HostAccountName}");
                    if (reply != null && reply.StartsWith("SAVE_CONFLICT:yes:"))
                    {
                        var pcs = reply.Split(':');
                        string size = pcs.Length >= 3 ? pcs[2] : "?";
                        string date = pcs.Length >= 4 ? pcs[3] : "?";
                        Console.Write($"HHBS 已存在存档 save_{_currentProfile.HostAccountName}.xml ({size} bytes, {date})。删除并继续？(y/n): ");
                        var confirm = Console.ReadLine()?.Trim().ToUpper();
                        if (confirm == "Y")
                        {
                            var delReply = Program.SendToModAndWait($"DELETE_SAVE_CONFLICT:{_currentProfile.HostAccountName}");
                            if (delReply == "DELETE_SAVE_CONFLICT:ok")
                                Console.WriteLine("旧存档已删除。");
                            else
                                Console.WriteLine($"删除失败: {delReply}");
                        }
                        else
                        {
                            Console.WriteLine("已取消。请修改 HostAccountName 或手动清理后重试。");
                            break;
                        }
                    }
                    else if (reply == null)
                    {
                        Console.WriteLine("(Mod 未回复，跳过冲突检测)");
                    }
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
