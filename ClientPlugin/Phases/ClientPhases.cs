using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hacknet;
using Hacknet.Extensions;
using HNC.Lobby;
using HNC.Pause;
using HNC.Phases;

namespace ClientPlugin;

/// <summary>
/// 客户端这一侧的阶段预设：
/// - 记录服务端报告的已开始阶段（HNC_PHASES）；
/// - 中途加入（游戏已开始、服务端没有本玩家存档）时，在大厅列出本角色可用的预设，由玩家自己挑：
///   已经过去的阶段不能选；正在进行的阶段选了立即进入；还没开始的阶段选了就等它开始后自动进入；
/// - 开新游戏时（GameLauncher）把预设的起始任务 / 起始动作临时写进 ExtensionInfo，进入游戏后再加预设的 flag。
/// 都在游戏主线程调用。
/// </summary>
internal static class ClientPhases
{
    private static readonly HashSet<string> _started = new(StringComparer.OrdinalIgnoreCase);

    private static List<PhaseDef>? _defs;
    private static string _defsFolder = "";

    /// <summary>玩家选中的阶段（等它开始，或已开始则立即进入）</summary>
    public static PhaseDef? Chosen { get; private set; }

    /// <summary>即将用于开新游戏的预设（选中阶段开始后加载）</summary>
    private static PhaseSet? _pending;

    /// <summary>进入游戏后要加的 flag</summary>
    private static readonly List<string> _flagsToApply = new();

    // 被预设改动前的 ExtensionInfo 原值（下次开局前还原）
    private static ExtensionInfo? _patchedInfo;
    private static string? _origMission;
    private static string? _origActions;

    // ═══════════════════════════════════════════════ 阶段进度

    public static bool TryHandle(string msg)
    {
        if (!msg.StartsWith(PauseProtocol.Phases, StringComparison.Ordinal)) return false;
        _started.Clear();
        foreach (var b64 in msg.Substring(PauseProtocol.Phases.Length).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            _started.Add(LobbyProtocol.FromB64(b64));
        return true;
    }

    public static bool IsStarted(PhaseDef p) => _started.Contains(p.Id);

    /// <summary>扩展的阶段定义（按扩展目录缓存）</summary>
    public static List<PhaseDef> Defs
    {
        get
        {
            string folder = ExtensionLoader.ActiveExtensionInfo?.GetFullFolderPath() ?? "";
            if (_defs == null || folder != _defsFolder)
            {
                _defsFolder = folder;
                _defs = folder.Length > 0 ? PhaseConfig.Load(folder) : new List<PhaseDef>();
            }
            return _defs;
        }
    }

    /// <summary>
    /// 本玩家的角色：优先用已加载的 SelectPlayerConfig，否则读扩展根目录 Config.tmp 第一行
    /// （和 PlayerConfigLoader 的规则一致；它要到开局时才运行）。
    /// </summary>
    public static string Role => _role ??= ReadRole();

    /// <summary>缓存的角色（界面每帧都要用，不能每帧读文件）；进大厅时清空</summary>
    private static string? _role;

    private static string ReadRole()
    {
        if (!string.IsNullOrWhiteSpace(ClientPluginMain.SelectPlayerConfig))
            return ClientPluginMain.SelectPlayerConfig!.Trim();
        try
        {
            string folder = ExtensionLoader.ActiveExtensionInfo?.GetFullFolderPath() ?? "";
            string path = Path.Combine(folder, "Config.tmp");
            if (!File.Exists(path)) return "";
            return File.ReadAllText(path)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim().TrimEnd(';') ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>进入大厅时调用：清空选择、角色缓存和阶段进度（终端会在加入时补发阶段进度）</summary>
    public static void ResetForLobby()
    {
        _role = null;
        _defs = null;
        Chosen = null;
        _started.Clear();
    }

    /// <summary>本角色有预设的阶段（按定义顺序）</summary>
    public static List<PhaseDef> OptionsForRole()
    {
        string role = Role;
        return Defs.Where(p => p.SetFor(role) != null).ToList();
    }

    /// <summary>当前正在进行的阶段：定义顺序里最后一个已开始的</summary>
    public static PhaseDef? Current => Defs.LastOrDefault(IsStarted);

    /// <summary>该阶段能不能选：正在进行，或还没开始（已经过去的不能选）</summary>
    public static bool CanChoose(PhaseDef p)
    {
        if (!IsStarted(p)) return true;
        return Current == p;
    }

    /// <summary>
    /// 大厅里要不要显示预设选择：游戏已开始、服务端没有本玩家的存档（这个世界里的新玩家）、本角色有预设。
    /// 有存档的老玩家照常读自己的存档，不走预设。
    /// </summary>
    public static bool ShouldOffer(LobbyState? state) =>
        state?.Phase == LobbyPhase.InGame &&
        ClientPluginMain.ServerReportedNoSave &&
        OptionsForRole().Count > 0;

    public static void Choose(PhaseDef p)
    {
        Chosen = p;
        Console.WriteLine($"[Phases] 选择阶段 {p.Id}（角色 {Role}）" + (IsStarted(p) ? "，已开始，立即进入" : "，等待它开始"));
    }

    public static void ClearChoice() => Chosen = null;

    /// <summary>选中的阶段已经开始，可以进入了</summary>
    public static bool ChosenReady => Chosen != null && IsStarted(Chosen);

    /// <summary>大厅进入游戏前调用：加载选中阶段本角色的预设，交给 GameLauncher</summary>
    public static void PrepareForStart()
    {
        _pending = null;
        var p = Chosen;
        Chosen = null;
        if (p == null) return;

        string? path = p.SetFor(Role);
        string folder = ExtensionLoader.ActiveExtensionInfo?.GetFullFolderPath() ?? "";
        if (path == null || folder.Length == 0) return;

        _pending = PhaseSet.Load(folder, path);
        if (_pending != null)
            Console.WriteLine($"[Phases] 将以预设 \"{_pending.Title}\"（阶段 {p.Id}）开始新游戏");
    }

    // ═══════════════════════════════════════════════ GameLauncher 调用

    /// <summary>开局前：还原上一次预设对 ExtensionInfo 的改动</summary>
    public static void RestoreExtensionInfo()
    {
        if (_patchedInfo == null) return;
        _patchedInfo.StartingMissionPath = _origMission;
        _patchedInfo.StartingActionsPath = _origActions;
        _patchedInfo = null;
    }

    /// <summary>
    /// 新游戏（没有存档）时应用预设：起始任务 / 起始动作写进 ExtensionInfo（保留到下次开局前还原——
    /// 扩展有开场动画时起始任务是在动画结束后才读的），进入游戏后加 flag。
    /// 有存档时调用 DiscardPending。
    /// </summary>
    public static void ApplyPendingToNewGame(ExtensionInfo info)
    {
        var set = _pending;
        _pending = null;
        if (set == null) return;

        _patchedInfo = info;
        _origMission = info.StartingMissionPath;
        _origActions = info.StartingActionsPath;

        if (set.StartingMission != null)
            info.StartingMissionPath = set.StartingMission.Length > 0 ? set.StartingMission : null;
        if (set.StartingActions != null)
            info.StartingActionsPath = set.StartingActions.Length > 0 ? set.StartingActions : null;

        _flagsToApply.Clear();
        _flagsToApply.AddRange(set.Flags);
        Console.WriteLine($"[Phases] 已应用预设: mission={info.StartingMissionPath ?? "(none)"} actions={info.StartingActionsPath ?? "(none)"} flags={set.Flags.Count}");
    }

    public static void DiscardPending()
    {
        if (_pending != null)
            Console.WriteLine("[Phases] 本地已有存档，忽略预设");
        _pending = null;
    }

    /// <summary>每帧：进入游戏后加预设的 flag</summary>
    public static void Update()
    {
        if (_flagsToApply.Count == 0 || !HncPause.GameRunning()) return;
        var flags = OS.currentInstance?.Flags;
        if (flags == null) return;
        foreach (var f in _flagsToApply)
            if (!flags.HasFlag(f)) flags.AddFlag(f);
        Console.WriteLine($"[Phases] 已添加预设 flag: {string.Join(", ", _flagsToApply)}");
        _flagsToApply.Clear();
    }
}
