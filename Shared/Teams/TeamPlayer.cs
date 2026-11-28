using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using System.Collections;
using System.Reflection;
using HNC.Pause;
using Pathfinder.Command;

namespace HNC.Teams;

/// <summary>一个 Private 组（Players-*.xml 的 &lt;Private&gt;&lt;PlayerReplacementsGroup&gt;）：人数、位置名、各位置的 PTR</summary>
internal sealed class TeamGroupDef
{
    public string Name = "";
    public int Num;
    public readonly List<string> Slots = new();
    public readonly List<(string Slot, string Org, string Curr)> Ptrs = new();
}

/// <summary>
/// 玩家这一侧的组队任务（客户端和服务端主机共用；主机是 0 号玩家，经管道和终端通信）。
/// 权威状态在终端（TeamManager），这里负责：
/// - 拦截任务板上的 Accept：组队任务不直接接，改为向终端申请锁定（先到先得，点的人成为队长）；
/// - team 命令：参数原样交给终端执行，终端回的文字显示在游戏终端里；
/// - 收到 TEAM_START：设置支线身份（Scope）和占位符，在本机接受任务；TEAM_APPLY 只设置身份和占位符。
///
/// "组队任务"：任务 XML 带 group（根元素或 missionStart / missionEnd / nextMission），
/// 且 Players-Cli.xml 的 &lt;Private&gt; 里有同名的 PlayerReplacementsGroup——人数取它的 num，位置取它的 Player 顺序。
/// 只在联机游戏中生效；单机玩扩展时任务照常接受。
/// 宿主插件通过下面几个钩子接入（ClientTeams / ServerTeams 里设置）。都在游戏主线程调用。
/// </summary>
internal static class TeamPlayer
{
    // ═══════════════════════════════════════════════ 宿主钩子

    /// <summary>联机游戏中（拦截只在这时生效）</summary>
    public static Func<bool> IsOnline = () => false;

    /// <summary>发消息给终端（客户端：LiteNetLib；主机：管道）</summary>
    public static Action<string> Send = _ => { };

    /// <summary>本机玩家的 PlayerId（主机为 0）</summary>
    public static Func<int> MyPlayerId = () => -1;

    /// <summary>设置 Scope 支线身份：group → 位置名</summary>
    public static Action<string, string> SetMyIdentity = (_, _) => { };

    /// <summary>名字占位符（#M-P1NAME# 等）→ 成员名字（所有人都设置）</summary>
    public static Action<string, string> SetNamePlaceholder = (_, _) => { };

    /// <summary>自己位置的其余占位符 Curr → Org（如 #M-P1_IP# → #PLAYER_IP#）</summary>
    public static Action<string, string> SetMyPlaceholder = (_, _) => { };

    /// <summary>本机的支线身份：group → 位置名（服务端的 Scope 门控读这里）</summary>
    public static readonly Dictionary<string, string> MyIdentities = new(StringComparer.OrdinalIgnoreCase);

    internal sealed class TeamMissionInfo
    {
        public string Key = "";
        public string Group = "";
        public int Slots;
        public string Title = "";
    }

    /// <summary>正在执行我们自己的接受流程（TEAM_START），拦截补丁放行</summary>
    internal static bool Accepting;

    /// <summary>MissionListingServer.draw 执行期间（它的 Accept 写在 draw 里）</summary>
    internal static bool InListingDraw;

    /// <summary>本帧被拦下的任务（sendEmail 拦下后，紧接着的 Activate 也要拦）</summary>
    internal static ActiveMission? Blocked;

    /// <summary>等游戏加载好后要接受的任务（key, 来源）</summary>
    private static readonly List<(string Key, string Source)> _pendingStarts = new();

    // ═══════════════════════════════════════════════ 界面用的状态

    /// <summary>终端发来的所有队伍（TEAM_STATE）</summary>
    public static List<TeamView> Teams { get; private set; } = new();

    /// <summary>终端最近一条回复（界面底部显示）</summary>
    public static string LastMessage { get; private set; } = "";

    /// <summary>我所在的组队中的队伍（没有则为 null）</summary>
    public static TeamView? MyFormingTeam => Teams.FirstOrDefault(t => !t.Started && t.Find(MyPlayerId()) != null);

    /// <summary>锁定（或已接取）这个任务的队伍</summary>
    public static TeamView? TeamForKey(string key) => Teams.FirstOrDefault(t => t.Key == key);

    /// <summary>
    /// 界面按钮发命令（和在终端里输入 team … 相同，由终端执行）。
    /// 确认前检查本机是否有任务在进行；不能确认时返回提示文字，否则返回 null。
    /// </summary>
    public static string? SendCommand(string line)
    {
        var os = OS.currentInstance;
        if (!IsOnline()) return "Not in an online game.";
        if (line.Trim().StartsWith("confirm", StringComparison.OrdinalIgnoreCase) && os?.currentMission != null)
            return "Finish or abandon your current mission before confirming.";
        Send($"{TeamProtocol.Cmd}{TeamProtocol.ToB64(line)}:{TeamProtocol.ToB64(os?.defaultUser.name ?? "")}");
        return null;
    }

    /// <summary>打开组队面板（team ui）</summary>
    public static void OpenPanel(string? focusKey = null)
    {
        var sman = Game1.getSingleton()?.sman;
        if (sman == null) return;
        foreach (var screen in sman.GetScreens())
            if (screen is TeamScreen ts && !ts.IsExiting) { ts.Focus(focusKey); return; }
        sman.AddScreen(new TeamScreen(focusKey));
    }

    /// <summary>收到过的分配（开局时 PlayerConfigLoader 会清空占位符映射，加载完要重新应用）</summary>
    private static readonly Dictionary<string, List<TeamAssignment>> _applied = new(StringComparer.OrdinalIgnoreCase);
    private static bool _wasRunning;

    private static bool Online => IsOnline();

    // ═══════════════════════════════════════════════ 命令

    public static void RegisterCommand() => CommandManager.RegisterCommand("team", HandleCommand);

    private static void HandleCommand(OS os, string[] args)
    {
        string line = string.Join(" ", args.Skip(1));
        string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";

        if (!IsOnline())
        {
            os.write("[组队] 没有在联机游戏中。");
            return;
        }
        if (sub == "ui")
        {
            OpenPanel();
            return;
        }
        if (sub == "confirm" && os.currentMission != null)
        {
            os.write("[组队] 你当前有任务在进行，先完成或放弃它，再确认组队。");
            return;
        }

        Send($"{TeamProtocol.Cmd}{TeamProtocol.ToB64(line)}:{TeamProtocol.ToB64(os.defaultUser.name ?? "")}");
    }

    // ═══════════════════════════════════════════════ 消息（EventReceiver）

    public static bool TryHandle(string msg)
    {
        if (!msg.StartsWith(TeamProtocol.Prefix, StringComparison.Ordinal)) return false;

        if (msg.StartsWith(TeamProtocol.Msg, StringComparison.Ordinal))
        {
            string text = TeamProtocol.FromB64(msg.Substring(TeamProtocol.Msg.Length));
            LastMessage = text.Split('\n').LastOrDefault(l => l.Trim().Length > 0) ?? "";
            var os = OS.currentInstance;
            foreach (var l in text.Split('\n'))
            {
                if (os != null && HncPause.GameRunning()) os.write(l);
                else Console.WriteLine("[Team] " + l);
            }
        }
        else if (msg.StartsWith(TeamProtocol.State, StringComparison.Ordinal))
        {
            Teams = TeamProtocol.DecodeState(msg);
        }
        else if (TeamProtocol.TryDecodeStart(msg, TeamProtocol.Start, out var key, out var group, out var list, out var source))
        {
            Remember(group, list);
            Apply(group, list);
            if (!_pendingStarts.Any(p => p.Key == key)) _pendingStarts.Add((key, source));
        }
        else if (TeamProtocol.TryDecodeStart(msg, TeamProtocol.Apply, out _, out var group2, out var list2))
        {
            Remember(group2, list2);
            Apply(group2, list2);
        }
        return true;
    }

    // ═══════════════════════════════════════════════ 每帧（GameUpdatePatch）

    public static void Update()
    {
        bool running = HncPause.GameRunning();
        if (running && !_wasRunning)
        {
            // 刚进入游戏：PlayerConfigLoader 在开局时清空过占位符映射，重新应用收到过的分配
            foreach (var kv in _applied) Apply(kv.Key, kv.Value);
        }
        _wasRunning = running;

        if (!running || _pendingStarts.Count == 0) return;
        var keys = _pendingStarts.ToList();
        _pendingStarts.Clear();
        foreach (var k in keys) StartMission(k.Key, k.Source);
    }

    // ═══════════════════════════════════════════════ 组队任务识别 / 锁定

    /// <summary>是组队任务就返回信息，否则 null</summary>
    internal static TeamMissionInfo? GetTeamInfo(ActiveMission? m)
    {
        if (m == null || !Online) return null;
        string group = GroupOf(m);
        if (string.IsNullOrEmpty(group)) return null;

        var def = LoadGroup(group);
        if (def == null || def.Num <= 0) return null;

        string? key = KeyOf(m.reloadGoalsSourceFile);
        if (key == null) return null;

        string title = !string.IsNullOrWhiteSpace(m.postingTitle) ? m.postingTitle
                     : !string.IsNullOrWhiteSpace(m.email.subject) ? m.email.subject
                     : Path.GetFileNameWithoutExtension(key);
        return new TeamMissionInfo { Key = key, Group = group, Slots = def.Num, Title = title };
    }

    /// <summary>向终端申请锁定任务（任务板 Accept 被拦下时）</summary>
    /// <param name="source">任务来源 {listing|hub|dhs}|{电脑id}，成员接任务时走同一个界面</param>
    internal static void RequestLock(TeamMissionInfo info, OS os, string source = "")
    {
        os.write($"[组队] \"{info.Title}\" 是组队任务（需要 {info.Slots} 人），正在向服务器申请锁定...");
        Send(
            $"{TeamProtocol.Lock}{TeamProtocol.ToB64(info.Key)}:{TeamProtocol.ToB64(info.Group)}:{info.Slots}:" +
            $"{TeamProtocol.ToB64(info.Title)}:{TeamProtocol.ToB64(os.defaultUser.name ?? "")}:{TeamProtocol.ToB64(source)}");
    }

    /// <summary>任务所属的 group：任务文件根元素的 group，没有再看 missionStart / missionEnd / nextMission</summary>
    private static string GroupOf(ActiveMission m)
    {
        try
        {
            string path = LocalizedFileLoader.GetLocalizedFilepath(m.reloadGoalsSourceFile);
            if (!File.Exists(path)) return "";
            var root = XDocument.Load(path).Root;
            if (root == null) return "";
            string? g = (string?)root.Attribute("group");
            if (!string.IsNullOrEmpty(g)) return g!;
            foreach (var name in new[] { "missionStart", "missionEnd", "nextMission" })
            {
                g = (string?)root.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Attribute("group");
                if (!string.IsNullOrEmpty(g)) return g!;
            }
        }
        catch { }
        return "";
    }

    /// <summary>读扩展里的 Players-Cli.xml（没有再找 Players-Ser.xml / Players.xml）中的 Private 组</summary>
    internal static TeamGroupDef? LoadGroup(string group)
    {
        string ext = ExtensionLoader.ActiveExtensionInfo?.GetFullFolderPath() ?? "";
        if (ext.Length == 0) return null;
        string? file = new[] { "Players-Cli.xml", "Players-Ser.xml", "Players.xml" }
            .SelectMany(n => new[] { Path.Combine(ext, n), Path.Combine(ext, "Content", n) })
            .FirstOrDefault(File.Exists);
        if (file == null) return null;

        try
        {
            var el = XDocument.Load(file).Root?.Element("PlayerToReplacements")?.Element("Private")?
                .Elements("PlayerReplacementsGroup")
                .FirstOrDefault(g => string.Equals((string?)g.Attribute("name"), group, StringComparison.OrdinalIgnoreCase));
            if (el == null) return null;

            var def = new TeamGroupDef { Name = group };
            foreach (var p in el.Elements("Player"))
            {
                string slot = (string?)p.Attribute("name") ?? "";
                if (slot.Length == 0) continue;
                def.Slots.Add(slot);
                foreach (var ptr in p.Elements("PTR"))
                    def.Ptrs.Add((slot, (string?)ptr.Attribute("Org") ?? "", (string?)ptr.Attribute("Curr") ?? ""));
            }
            def.Num = int.TryParse((string?)el.Attribute("num"), out int n) && n > 0 ? n : def.Slots.Count;
            return def;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>任务 key：任务文件相对扩展根目录的路径（小写、/ 分隔），各端一致</summary>
    internal static string? KeyOf(string? sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile)) return null;
        try
        {
            string full = Path.GetFullPath(sourceFile);
            string ext = ExtensionLoader.ActiveExtensionInfo?.GetFullFolderPath() ?? "";
            string rel = Path.GetFileName(full);
            if (ext.Length > 0)
            {
                string extFull = Path.GetFullPath(ext).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                if (full.StartsWith(extFull, StringComparison.OrdinalIgnoreCase))
                    rel = full.Substring(extFull.Length);
            }
            return rel.Replace('\\', '/').ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    // ═══════════════════════════════════════════════ 身份 / 占位符 / 接任务

    private static void Remember(string group, List<TeamAssignment> list) => _applied[group] = list;

    /// <summary>
    /// 应用一支队伍的位置分配：
    /// - 每个位置的名字占位符（#M-P1NAME# 等，Org=#PLAYERNAME# 的 PTR）→ 该成员的游戏内名字（所有玩家都设置）；
    /// - 自己的位置：Scope 支线身份（group 的第几号），以及其余占位符（如 #M-P1_IP# → #PLAYER_IP#，解析为本机 IP）。
    /// </summary>
    private static void Apply(string group, List<TeamAssignment> list)
    {
        var def = LoadGroup(group);
        if (def == null)
        {
            Console.WriteLine($"[Team] Players-*.xml 里没有 Private 组 {group}，无法设置支线身份");
            return;
        }

        foreach (var a in list)
        {
            if (a.Slot < 0 || a.Slot >= def.Slots.Count) continue;
            string slot = def.Slots[a.Slot];
            bool mine = a.PlayerId == MyPlayerId();

            foreach (var ptr in def.Ptrs.Where(p => p.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrEmpty(ptr.Curr) || string.IsNullOrEmpty(ptr.Org)) continue;
                if (ptr.Org == "#PLAYERNAME#") SetNamePlaceholder(ptr.Curr, a.Name);
                else if (mine) SetMyPlaceholder(ptr.Curr, ptr.Org);
            }

            if (mine)
            {
                MyIdentities[group] = slot;
                SetMyIdentity(group, slot);
                Console.WriteLine($"[Team] 支线身份: {group} → 位置 {slot}");
            }
        }
    }

    /// <summary>TEAM_START：在本机接受任务（和任务板 Accept 相同的步骤），回 TEAM_ACK</summary>
    private static void StartMission(string key, string source)
    {
        var os = OS.currentInstance;
        var eif = ExtensionLoader.ActiveExtensionInfo;
        if (os == null || eif == null) return;

        if (KeyOf(os.currentMission?.reloadGoalsSourceFile) == key)
        {
            Ack(key);
            return;
        }

        string path = eif.FolderPath.TrimEnd('/', '\\') + "/" + key;
        if (!File.Exists(LocalizedFileLoader.GetLocalizedFilepath(path)) && !File.Exists(Path.Combine(eif.GetFullFolderPath(), key)))
        {
            os.write($"[组队] 找不到任务文件 {key}，无法开始组队任务。");
            return;
        }

        if (os.currentMission != null)
            os.write("[组队] 当前任务被组队任务替换。");

        // 先走任务来源界面的原生接受（DHS 的任务必须在 DHS 上"认领"才能在那里完成；任务中心会归档合约）
        try
        {
            Accepting = true;
            if (TeamBoards.NativeAccept(os, source, key))
            {
                os.write($"[组队] 组队任务开始：{os.currentMission?.postingTitle ?? key}");
                Ack(key);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Team] 原生接受失败（改用通用方式）: {ex.Message}");
        }
        finally
        {
            Accepting = false;
        }

        try
        {
            Accepting = true;
            var mission = (ActiveMission)ComputerLoader.readMission(path);
            os.currentMission = mission;
            mission.sendEmail(os);
            mission.ActivateSuppressedStartFunctionIfPresent();
            os.write($"[组队] 组队任务开始：{mission.postingTitle ?? mission.email.subject}");
            Ack(key);
        }
        catch (Exception ex)
        {
            os.write($"[组队] 开始组队任务失败: {ex.Message}");
            Console.WriteLine($"[Team] 开始任务失败 {path}: {ex}");
        }
        finally
        {
            Accepting = false;
        }
    }

    private static void Ack(string key) => Send(TeamProtocol.Ack + TeamProtocol.ToB64(key));
}

/// <summary>
/// 拦截任务界面上的接受（组队任务改为申请锁定）。只拦"从任务界面接受"，剧情自动下发的任务（loadMission）不受影响。
/// - 合约板 MissionListingServer：Accept 写在 draw 里，顺序是 currentMission=任务 → sendEmail → Activate；
///   在 draw 执行期间拦 sendEmail（并撤销 currentMission），再拦紧接着的 Activate。
/// - 任务中心 MissionHubServer：直接拦私有方法 acceptMission（否则它会把任务从列表里删掉）。
/// - DLC 任务中心（DHS）DLCHubServer：直接拦私有方法 PlayerAcceptMission（否则会把合约标成被本人认领）。
/// </summary>
[HarmonyPatch]
internal static class TeamAcceptPatches
{
    /// <summary>正在绘制的合约板（拦截时据此记下任务来源）</summary>
    private static MissionListingServer? _listing;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionListingServer), nameof(MissionListingServer.draw))]
    private static void ListingDrawPrefix(MissionListingServer __instance)
    {
        TeamPlayer.InListingDraw = true;
        TeamPlayer.Blocked = null;
        _listing = __instance;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(MissionListingServer), nameof(MissionListingServer.draw))]
    private static Exception? ListingDrawFinalizer(Exception? __exception)
    {
        TeamPlayer.InListingDraw = false;
        TeamPlayer.Blocked = null;
        _listing = null;
        return __exception;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActiveMission), nameof(ActiveMission.sendEmail))]
    private static bool SendEmailPrefix(ActiveMission __instance, OS os)
    {
        if (TeamPlayer.Accepting || !TeamPlayer.InListingDraw || os?.currentMission != __instance) return true;
        var info = TeamPlayer.GetTeamInfo(__instance);
        if (info == null) return true;

        os.currentMission = null;          // 撤销接受：等组队提交后再接
        TeamPlayer.Blocked = __instance;
        TeamPlayer.RequestLock(info, os, TeamBoards.SourceOf("listing", _listing));
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActiveMission), nameof(ActiveMission.ActivateSuppressedStartFunctionIfPresent))]
    [HarmonyPriority(Priority.First)]
    private static bool ActivatePrefix(ActiveMission __instance) =>
        TeamPlayer.Accepting || TeamPlayer.Blocked != __instance;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionHubServer), "acceptMission")]
    private static bool HubAcceptPrefix(MissionHubServer __instance, ActiveMission mission)
    {
        if (TeamPlayer.Accepting) return true;
        var info = TeamPlayer.GetTeamInfo(mission);
        if (info == null) return true;
        var os = OS.currentInstance;
        if (os == null) return true;
        TeamPlayer.RequestLock(info, os, TeamBoards.SourceOf("hub", __instance));
        return false;
    }
}

/// <summary>DHS（DLCHubServer）的认领拦截：类型在 Hacknet 里是 internal，按名字找</summary>
[HarmonyPatch]
internal static class TeamDhsAcceptPatch
{
    private static bool Prepare() => TeamBoards.DhsType != null;

    private static MethodBase TargetMethod() => AccessTools.Method(TeamBoards.DhsType, "PlayerAcceptMission");

    private static bool Prefix(object __instance, object mission)
    {
        if (TeamPlayer.Accepting) return true;
        var m = TeamBoards.MissionOfClaimable(mission);
        var info = TeamPlayer.GetTeamInfo(m);
        if (info == null) return true;
        var os = OS.currentInstance;
        if (os == null) return true;
        TeamPlayer.RequestLock(info, os, TeamBoards.SourceOf("dhs", __instance as Daemon));
        return false;
    }
}
