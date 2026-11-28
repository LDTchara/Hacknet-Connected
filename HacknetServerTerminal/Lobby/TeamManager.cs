using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HNC.Teams;
using LiteNetLib;

namespace HacknetServerTerminal;

/// <summary>
/// 组队任务（终端持有权威状态）。
///
/// 先到先得：第一个在任务板点 Accept 的玩家锁定该任务并成为队长（队伍处于"组队中"）；
/// 其他人用 team join 加入，每人 team confirm，队长 team submit 后队伍"已开始"，终端让每个成员本机接受任务。
/// 提交前队长可以 team disband（或队长 team leave）解散，任务随即解锁。已开始的队伍一直锁定该任务。
///
/// 已开始的队伍按服务器保存到 Storage/Teams/{服务器名}.txt：玩家重连、终端重开后补发分配（支线身份、占位符）；
/// 成员还没在本机接受任务（例如提交时掉线）的，在他下次加入时补发 TEAM_START。
/// 线程：都在 LiteNetLib 轮询线程调用（客户端消息 / 加入），Open 在命令行线程——统一用 _lock。
/// </summary>
internal static class TeamManager
{
    private sealed class Member
    {
        public int PlayerId;
        public string Account = "";     // 终端里的账户名（大厅名单上的）
        public string GameName = "";    // 游戏内名字（#PLAYERNAME#，用于支线占位符）
        public int Slot;                // 位置下标（0 起）
        public bool Confirmed;
        public bool Delivered;          // 已在本机接受任务（仅已开始的队伍）
    }

    private sealed class Team
    {
        public string Name = "";
        public string Key = "";
        public string Group = "";
        public string Title = "";
        public int Slots;
        public int LeaderId;
        public bool Started;
        /// <summary>任务来源：{界面类型}|{电脑id}（listing / hub / dhs），成员接任务时走同一个界面的原生接受</summary>
        public string Source = "";
        public readonly List<Member> Members = new();

        public bool IsFull => Members.Count >= Slots;
        public Member? Find(int pid) => Members.FirstOrDefault(m => m.PlayerId == pid);
        public IEnumerable<TeamAssignment> Assignments =>
            Members.Select(m => new TeamAssignment { Slot = m.Slot, PlayerId = m.PlayerId, Name = m.GameName });
    }

    /// <summary>发命令的玩家：客户端（LiteNetLib）或服务端主机（0 号，管道）</summary>
    private sealed class Actor
    {
        public int Pid;
        public string Name = "";
        public Action<string> Reply = _ => { };
    }

    private const int HostId = 0;

    private static readonly object _lock = new();
    private static readonly List<Team> _teams = new();
    private static string _serverName = "";
    private static int _autoNameCounter = 1;

    // ═══════════════════════════════════════════════ 生命周期

    /// <summary>start 开放大厅时：丢掉组队中的队伍，读回该服务器已开始的队伍</summary>
    public static void Open(string serverName)
    {
        lock (_lock)
        {
            _serverName = serverName;
            _teams.Clear();
            _autoNameCounter = 1;
            _lastState = "";
            Load();
        }
    }

    /// <summary>玩家加入大厅（确定身份）后：补发已开始队伍的分配；他是成员但还没接任务的，补发 TEAM_START</summary>
    public static void OnPlayerJoined(NetPeer peer, int playerId)
    {
        lock (_lock)
        {
            foreach (var t in _teams.Where(t => t.Started))
            {
                var me = t.Find(playerId);
                string prefix = me != null && !me.Delivered ? TeamProtocol.Start : TeamProtocol.Apply;
                NetworkManager.SendTo(peer, TeamProtocol.EncodeStart(prefix, t.Key, t.Group, t.Assignments, t.Source));
            }
            NetworkManager.SendTo(peer, CurrentState());
        }
    }

    // ═══════════════════════════════════════════════ 客户端消息（LiteNetLib 线程）

    public static void OnClientMessage(NetPeer peer, string msg)
    {
        var client = NetworkManager.GetClient(peer.Id);
        if (client == null || !client.Joined) return;
        var actor = new Actor
        {
            Pid = client.PlayerId,
            Name = client.PlayerName,
            Reply = text => NetworkManager.SendTo(peer, TeamProtocol.Msg + TeamProtocol.ToB64(text))
        };
        Dispatch(actor, msg);
    }

    /// <summary>Pipe 读线程：服务端主机（0 号玩家）发来的 TEAM_*</summary>
    public static void OnModMessage(string msg) => Dispatch(HostActor(), msg);

    /// <summary>服务端游戏进入游戏并准备完毕：给主机补发已开始队伍的分配（他是成员但还没接任务的补发 TEAM_START）</summary>
    public static void OnHostReady()
    {
        lock (_lock)
        {
            foreach (var t in _teams.Where(t => t.Started))
            {
                var me = t.Find(HostId);
                string prefix = me != null && !me.Delivered ? TeamProtocol.Start : TeamProtocol.Apply;
                ToHost(TeamProtocol.EncodeStart(prefix, t.Key, t.Group, t.Assignments, t.Source));
            }
            ToHost(CurrentState());
        }
    }

    private static Actor HostActor() => new()
    {
        Pid = HostId,
        Name = HostName,
        Reply = text => ToHost(TeamProtocol.Msg + TeamProtocol.ToB64(text))
    };

    private static string HostName
    {
        get
        {
            string n = LobbyManager.HostName;
            return string.IsNullOrWhiteSpace(n) ? "Server" : n;
        }
    }

    private static void ToHost(string msg)
    {
        if (Program.IsModConnected) _ = Program.SendToModFireForget(msg);
    }

    private static void Dispatch(Actor a, string msg)
    {
        lock (_lock)
        {
            if (msg.StartsWith(TeamProtocol.Lock, StringComparison.Ordinal))
                HandleLock(a, msg.Substring(TeamProtocol.Lock.Length));
            else if (msg.StartsWith(TeamProtocol.Cmd, StringComparison.Ordinal))
                HandleCommand(a, msg.Substring(TeamProtocol.Cmd.Length));
            else if (msg.StartsWith(TeamProtocol.Ack, StringComparison.Ordinal))
                HandleAck(a, TeamProtocol.FromB64(msg.Substring(TeamProtocol.Ack.Length)));

            // 队伍有任何变化：把结构化状态发给所有人（界面用）
            BroadcastStateIfChanged();
        }
    }

    // ═══════════════════════════════════════════════ 结构化状态（TEAM_STATE，给界面用）

    private static string _lastState = "";

    private static string CurrentState() => TeamProtocol.EncodeState(_teams.Select(t => new TeamView
    {
        Name = t.Name, Key = t.Key, Group = t.Group, Title = t.Title, Slots = t.Slots,
        LeaderId = t.LeaderId, Started = t.Started, Source = t.Source,
        Members = t.Members.Select(m => new TeamMemberView
        {
            PlayerId = m.PlayerId, Slot = m.Slot, Confirmed = m.Confirmed, Name = m.Account
        }).ToList()
    }));

    /// <summary>状态有变化就发给所有已加入的客户端和主机（必须在 _lock 内调用）</summary>
    private static void BroadcastStateIfChanged()
    {
        string state = CurrentState();
        if (state == _lastState) return;
        _lastState = state;
        foreach (var cl in NetworkManager.GetAllClients())
            if (cl.Joined && cl.Peer != null) NetworkManager.SendTo(cl.Peer, state);
        ToHost(state);
    }

    /// <summary>TEAM_LOCK：玩家在任务板点了 Accept</summary>
    private static void HandleLock(Actor a, string payload)
    {
        var p = payload.Split(':');
        if (p.Length < 5 || !int.TryParse(p[2], out int slots) || slots <= 0) return;
        string key = TeamProtocol.FromB64(p[0]).ToLowerInvariant();
        string group = TeamProtocol.FromB64(p[1]);
        string title = TeamProtocol.FromB64(p[3]);
        string gameName = TeamProtocol.FromB64(p[4]);
        string source = p.Length > 5 ? TeamProtocol.FromB64(p[5]) : "";

        var owner = _teams.FirstOrDefault(t => t.Key == key);
        if (owner != null)
        {
            if (owner.Find(a.Pid) != null)
                a.Reply(owner.Started ? "你的队伍已经开始了这个任务。" : $"你已在锁定此任务的队伍 [{owner.Name}] 里。输入 team 查看。");
            else if (owner.Started)
                a.Reply($"任务 \"{title}\" 已被队伍 [{owner.Name}] 接取。");
            else
                a.Reply($"任务 \"{title}\" 已被队伍 [{owner.Name}] 锁定（队长 {LeaderName(owner)}，{owner.Members.Count}/{owner.Slots} 人）。\n" +
                            $"输入 team join {owner.Name} 加入该队伍。");
            return;
        }

        var mine = FormingTeamOf(a.Pid);
        if (mine != null)
        {
            a.Reply($"你已在组队中的队伍 [{mine.Name}] 里（任务 \"{mine.Title}\"）。先 team leave 才能锁定别的任务。");
            return;
        }

        var team = new Team
        {
            Name = NextAutoName(),
            Key = key,
            Group = group,
            Title = title,
            Slots = slots,
            LeaderId = a.Pid,
            Source = source
        };
        team.Members.Add(new Member { PlayerId = a.Pid, Account = a.Name, GameName = gameName, Slot = 0 });
        _teams.Add(team);

        Console.WriteLine($"[组队] {a.Name}(P{a.Pid}) 锁定任务 \"{title}\"（{key}），建队 [{team.Name}]，需要 {slots} 人");
        a.Reply(
            $"任务 \"{title}\" 已为你锁定，你是队伍 [{team.Name}] 的队长。需要 {slots} 人。\n" +
            "  team name <名字>   给队伍命名（可选）\n" +
            $"  其他玩家输入 team join {team.Name} 加入\n" +
            "  所有人 team confirm 确认后，队长 team submit 提交，任务开始。\n" +
            "  team disband 解散队伍并解除锁定。");
        if (slots == 1)
            a.Reply("这个任务只需要 1 人：直接 team confirm 再 team submit 即可。");
        Notify(team, $"[{team.Name}] 由 {a.Name} 创建。", exceptId: a.Pid, everyone: true);
    }

    /// <summary>TEAM_CMD：team 子命令</summary>
    private static void HandleCommand(Actor a, string payload)
    {
        var p = payload.Split(new[] { ':' }, 2);
        string line = TeamProtocol.FromB64(p[0]).Trim();
        string gameName = p.Length > 1 ? TeamProtocol.FromB64(p[1]) : a.Name;
        var args = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

        switch (sub)
        {
            case "status": Status(a); break;
            case "list": List(a); break;
            case "name": Rename(a, args); break;
            case "join": Join(a, args, gameName); break;
            case "leave": Leave(a); break;
            case "slot": MoveSlot(a, args); break;
            case "kick": KickMember(a, args); break;
            case "confirm": SetConfirm(a, true); break;
            case "unconfirm": SetConfirm(a, false); break;
            case "submit": Submit(a); break;
            case "disband": Disband(a); break;
            default:
                a.Reply(
                    "team 命令：\n" +
                    "  team                 查看我的队伍\n" +
                    "  team list            列出所有队伍\n" +
                    "  team join <队伍>      加入组队中的队伍\n" +
                    "  team leave           离开队伍（队长离开 = 解散）\n" +
                    "  team slot <n>        换到第 n 个位置\n" +
                    "  team confirm         确认（team unconfirm 取消）\n" +
                    "  队长：team name <名字> / team kick <玩家> / team submit / team disband\n" +
                    "  在任务板点 Accept 组队任务 = 锁定任务并建队（先到先得）");
                break;
        }
    }

    private static void HandleAck(Actor a, string key)
    {
        var t = _teams.FirstOrDefault(x => x.Started && x.Key == key.ToLowerInvariant());
        var m = t?.Find(a.Pid);
        if (m == null || m.Delivered) return;
        m.Delivered = true;
        Save();
        Console.WriteLine($"[组队] {a.Name}(P{a.Pid}) 已接受任务 \"{t!.Title}\"");
    }

    // ═══════════════════════════════════════════════ 子命令

    private static void Status(Actor a)
    {
        var t = FormingTeamOf(a.Pid) ?? _teams.LastOrDefault(x => x.Started && x.Find(a.Pid) != null);
        if (t == null)
        {
            a.Reply("你不在任何队伍里。在任务板点 Accept 组队任务即可锁定并建队；team list 查看现有队伍。");
            return;
        }
        a.Reply(Describe(t));
    }

    private static void List(Actor a)
    {
        if (_teams.Count == 0)
        {
            a.Reply("现在没有队伍。");
            return;
        }
        var sb = new StringBuilder("所有队伍：");
        foreach (var t in _teams)
            sb.Append($"\n  [{t.Name}] \"{t.Title}\"  {t.Members.Count}/{t.Slots} 人  " +
                      (t.Started ? "已开始" : $"组队中（队长 {LeaderName(t)}）"));
        a.Reply(sb.ToString());
    }

    private static void Rename(Actor a, string[] args)
    {
        var t = LeaderTeam(a);
        if (t == null) return;
        if (args.Length < 2)
        {
            a.Reply("用法: team name <名字>（不能有空格）");
            return;
        }
        string name = args[1];
        if (name.Length > 24 || name.Any(ch => char.IsWhiteSpace(ch) || ch == ':' || ch == ','))
        {
            a.Reply("队伍名最多 24 个字符，不能有空格、冒号或逗号。");
            return;
        }
        if (_teams.Any(x => x != t && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            a.Reply($"已经有叫 [{name}] 的队伍了。");
            return;
        }
        string old = t.Name;
        t.Name = name;
        Notify(t, $"队伍 [{old}] 改名为 [{name}]。");
    }

    private static void Join(Actor a, string[] args, string gameName)
    {
        if (args.Length < 2)
        {
            a.Reply("用法: team join <队伍名>（team list 查看）");
            return;
        }
        var t = _teams.FirstOrDefault(x => x.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase));
        if (t == null) { a.Reply($"没有叫 [{args[1]}] 的队伍。"); return; }
        if (t.Started) { a.Reply($"队伍 [{t.Name}] 已经开始任务了，不能再加入。"); return; }
        if (t.Find(a.Pid) != null) { a.Reply($"你已经在 [{t.Name}] 里了。"); return; }
        var mine = FormingTeamOf(a.Pid);
        if (mine != null) { a.Reply($"你已在队伍 [{mine.Name}] 里，先 team leave。"); return; }
        if (t.IsFull) { a.Reply($"队伍 [{t.Name}] 已满（{t.Slots} 人）。"); return; }

        int slot = Enumerable.Range(0, t.Slots).First(s => t.Members.All(m => m.Slot != s));
        t.Members.Add(new Member { PlayerId = a.Pid, Account = a.Name, GameName = gameName, Slot = slot });
        ResetConfirms(t);
        Notify(t, $"{a.Name} 加入了 [{t.Name}]（位置 {slot + 1}，{t.Members.Count}/{t.Slots} 人）。所有人需要重新 team confirm。");
    }

    private static void Leave(Actor a)
    {
        var t = FormingTeamOf(a.Pid);
        if (t == null)
        {
            a.Reply("你不在组队中的队伍里（已开始的队伍不能离开）。");
            return;
        }
        if (t.LeaderId == a.Pid)
        {
            DisbandTeam(t, $"队长 {a.Name} 离开");
            return;
        }
        t.Members.RemoveAll(m => m.PlayerId == a.Pid);
        ResetConfirms(t);
        a.Reply($"你离开了 [{t.Name}]。");
        Notify(t, $"{a.Name} 离开了 [{t.Name}]（{t.Members.Count}/{t.Slots} 人）。所有人需要重新 team confirm。");
    }

    private static void MoveSlot(Actor a, string[] args)
    {
        var t = FormingTeamOf(a.Pid);
        if (t == null) { a.Reply("你不在组队中的队伍里。"); return; }
        if (args.Length < 2 || !int.TryParse(args[1], out int n) || n < 1 || n > t.Slots)
        {
            a.Reply($"用法: team slot <1~{t.Slots}>");
            return;
        }
        if (t.Members.Any(m => m.Slot == n - 1 && m.PlayerId != a.Pid))
        {
            a.Reply($"位置 {n} 已被占用。");
            return;
        }
        t.Find(a.Pid)!.Slot = n - 1;
        ResetConfirms(t);
        Notify(t, $"{a.Name} 换到了位置 {n}。所有人需要重新 team confirm。");
    }

    private static void KickMember(Actor a, string[] args)
    {
        var t = LeaderTeam(a);
        if (t == null) return;
        if (args.Length < 2) { a.Reply("用法: team kick <玩家名|P编号>"); return; }
        var m = FindMember(t, args[1]);
        if (m == null) { a.Reply($"队伍里没有 {args[1]}。"); return; }
        if (m.PlayerId == a.Pid) { a.Reply("不能移出自己（要解散用 team disband）。"); return; }
        t.Members.Remove(m);
        ResetConfirms(t);
        SendToPlayer(m.PlayerId, $"你被队长移出了 [{t.Name}]。");
        Notify(t, $"{m.Account} 被移出 [{t.Name}]（{t.Members.Count}/{t.Slots} 人）。所有人需要重新 team confirm。");
    }

    private static void SetConfirm(Actor a, bool value)
    {
        var t = FormingTeamOf(a.Pid);
        if (t == null) { a.Reply("你不在组队中的队伍里。"); return; }
        var m = t.Find(a.Pid)!;
        if (m.Confirmed == value) { a.Reply(value ? "你已经确认过了。" : "你还没有确认。"); return; }
        m.Confirmed = value;
        int n = t.Members.Count(x => x.Confirmed);
        Notify(t, $"{a.Name} {(value ? "已确认" : "取消确认")}（{n}/{t.Slots}）。" +
                  (n == t.Slots && t.IsFull ? $" 全员已确认，队长可以 team submit。" : ""));
    }

    private static void Submit(Actor a)
    {
        var t = LeaderTeam(a);
        if (t == null) return;
        if (!t.IsFull) { a.Reply($"人数不足：{t.Members.Count}/{t.Slots}。"); return; }
        var waiting = t.Members.Where(m => !m.Confirmed).Select(m => m.Account).ToList();
        if (waiting.Count > 0) { a.Reply($"还有人没确认：{string.Join(", ", waiting)}。"); return; }

        t.Started = true;
        Save();
        Console.WriteLine($"[组队] 队伍 [{t.Name}] 提交，开始任务 \"{t.Title}\"：" +
                          string.Join(", ", t.Members.OrderBy(m => m.Slot).Select(m => $"{m.Slot + 1}={m.Account}")));

        // 成员：接任务 + 设置身份；其他人：只设置占位符
        string start = TeamProtocol.EncodeStart(TeamProtocol.Start, t.Key, t.Group, t.Assignments, t.Source);
        string apply = TeamProtocol.EncodeStart(TeamProtocol.Apply, t.Key, t.Group, t.Assignments, t.Source);
        foreach (var cl in NetworkManager.GetAllClients())
        {
            if (!cl.Joined || cl.Peer == null) continue;
            NetworkManager.SendTo(cl.Peer, t.Find(cl.PlayerId) != null ? start : apply);
        }
        ToHost(t.Find(HostId) != null ? start : apply);
        Notify(t, $"队伍 [{t.Name}] 已提交，任务 \"{t.Title}\" 开始！");
    }

    private static void Disband(Actor a)
    {
        var t = LeaderTeam(a);
        if (t == null) return;
        DisbandTeam(t, $"队长 {a.Name} 解散");
    }

    // ═══════════════════════════════════════════════ 内部（都在 _lock 内）

    private static void DisbandTeam(Team t, string why)
    {
        Notify(t, $"队伍 [{t.Name}] 已解散（{why}），任务 \"{t.Title}\" 解除锁定，可以重新在任务板接取。");
        _teams.Remove(t);
        Console.WriteLine($"[组队] 队伍 [{t.Name}] 解散（{why}），任务解锁: {t.Key}");
    }

    private static Team? FormingTeamOf(int pid) => _teams.FirstOrDefault(t => !t.Started && t.Find(pid) != null);

    /// <summary>调用者是组队中队伍的队长时返回该队伍，否则回复错误并返回 null</summary>
    private static Team? LeaderTeam(Actor a)
    {
        var t = FormingTeamOf(a.Pid);
        if (t == null) { a.Reply("你不在组队中的队伍里。"); return null; }
        if (t.LeaderId != a.Pid) { a.Reply($"只有队长 {LeaderName(t)} 能这样做。"); return null; }
        return t;
    }

    private static Member? FindMember(Team t, string who)
    {
        string w = who.TrimStart('P', 'p');
        if (int.TryParse(w, out int pid)) return t.Find(pid);
        return t.Members.FirstOrDefault(m => m.Account.Equals(who, StringComparison.OrdinalIgnoreCase)
                                          || m.GameName.Equals(who, StringComparison.OrdinalIgnoreCase));
    }

    private static string LeaderName(Team t) => t.Find(t.LeaderId)?.Account ?? $"P{t.LeaderId}";

    private static void ResetConfirms(Team t)
    {
        foreach (var m in t.Members) m.Confirmed = false;
    }

    private static string NextAutoName()
    {
        string name;
        do name = $"team{_autoNameCounter++}";
        while (_teams.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        return name;
    }

    private static string Describe(Team t)
    {
        var sb = new StringBuilder();
        sb.Append($"队伍 [{t.Name}]  任务 \"{t.Title}\"  {(t.Started ? "已开始" : "组队中")}  {t.Members.Count}/{t.Slots} 人");
        for (int s = 0; s < t.Slots; s++)
        {
            var m = t.Members.FirstOrDefault(x => x.Slot == s);
            sb.Append($"\n  位置 {s + 1}: ");
            if (m == null) sb.Append("（空）");
            else sb.Append($"{m.Account}{(m.PlayerId == t.LeaderId ? " [队长]" : "")}" +
                           (t.Started ? "" : m.Confirmed ? "  已确认" : "  未确认"));
        }
        return sb.ToString();
    }

    private static void SendToPlayer(int pid, string text)
    {
        string msg = TeamProtocol.Msg + TeamProtocol.ToB64(text);
        if (pid == HostId) ToHost(msg);
        else NetworkManager.SendToPlayer(pid, msg);
    }

    /// <summary>通知队伍成员（everyone = 通知所有已加入的玩家）</summary>
    private static void Notify(Team t, string text, int exceptId = int.MinValue, bool everyone = false)
    {
        string msg = TeamProtocol.Msg + TeamProtocol.ToB64("[组队] " + text);
        foreach (var cl in NetworkManager.GetAllClients())
        {
            if (!cl.Joined || cl.Peer == null || cl.PlayerId == exceptId) continue;
            if (everyone || t.Find(cl.PlayerId) != null) NetworkManager.SendTo(cl.Peer, msg);
        }
        if (exceptId != HostId && (everyone || t.Find(HostId) != null)) ToHost(msg);
        Console.WriteLine("[组队] " + text);
    }

    // ═══════════════════════════════════════════════ 持久化（只存已开始的队伍）

    private static string FilePath => Path.Combine("Storage", "Teams", $"{_serverName}.txt");

    private static void Save()
    {
        if (string.IsNullOrEmpty(_serverName)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var lines = _teams.Where(t => t.Started).Select(t => string.Join("|",
                TeamProtocol.ToB64(t.Name), TeamProtocol.ToB64(t.Key), TeamProtocol.ToB64(t.Group),
                TeamProtocol.ToB64(t.Title), t.Slots, t.LeaderId,
                string.Join(";", t.Members.Select(m =>
                    $"{m.PlayerId},{m.Slot},{(m.Delivered ? 1 : 0)},{TeamProtocol.ToB64(m.Account)},{TeamProtocol.ToB64(m.GameName)}")),
                TeamProtocol.ToB64(t.Source)));
            File.WriteAllLines(FilePath, lines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[组队] 保存失败: {ex.Message}");
        }
    }

    private static void Load()
    {
        if (string.IsNullOrEmpty(_serverName) || !File.Exists(FilePath)) return;
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var f = line.Split('|');
                if (f.Length < 7) continue;
                var t = new Team
                {
                    Name = TeamProtocol.FromB64(f[0]),
                    Key = TeamProtocol.FromB64(f[1]),
                    Group = TeamProtocol.FromB64(f[2]),
                    Title = TeamProtocol.FromB64(f[3]),
                    Slots = int.TryParse(f[4], out int s) ? s : 0,
                    LeaderId = int.TryParse(f[5], out int l) ? l : 0,
                    Started = true,
                    Source = f.Length > 7 ? TeamProtocol.FromB64(f[7]) : ""
                };
                foreach (var mm in f[6].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var g = mm.Split(',');
                    if (g.Length < 5 || !int.TryParse(g[0], out int pid) || !int.TryParse(g[1], out int slot)) continue;
                    t.Members.Add(new Member
                    {
                        PlayerId = pid,
                        Slot = slot,
                        Delivered = g[2] == "1",
                        Account = TeamProtocol.FromB64(g[3]),
                        GameName = TeamProtocol.FromB64(g[4]),
                        Confirmed = true
                    });
                }
                _teams.Add(t);
            }
            Console.WriteLine($"[组队] 读回 {_teams.Count} 个已开始的队伍");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[组队] 读取失败: {ex.Message}");
        }
    }

    /// <summary>终端 team 命令：打印所有队伍</summary>
    public static void PrintStatus()
    {
        lock (_lock)
        {
            if (_teams.Count == 0) { Console.WriteLine("没有队伍。"); return; }
            foreach (var t in _teams) Console.WriteLine(Describe(t));
        }
    }
}
