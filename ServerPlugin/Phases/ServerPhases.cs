using System;
using System.Collections.Generic;
using System.Linq;
using Hacknet;
using Hacknet.Extensions;
using HNC.Lobby;
using HNC.Pause;
using HNC.Phases;

namespace ServerPlugin;

/// <summary>
/// 阶段判定（服务端是权威）：游戏中每秒按服务端的 flag 检查 HNC_Phases.xml 里各阶段的条件，
/// 满足即视为开始（之后不会变回未开始），有变化就把已开始的阶段报告给终端（HNC_PHASES），
/// 终端再转给大厅里等待的客户端。没有阶段定义文件时什么都不做。
/// </summary>
internal static class ServerPhases
{
    private static List<PhaseDef>? _phases;
    private static readonly HashSet<string> _started = new(StringComparer.OrdinalIgnoreCase);
    private static string _lastSent = "";
    private static DateTime _nextCheck = DateTime.MinValue;

    public static void Update()
    {
        if (!HncPause.GameRunning() || !ServerManager.IsConnected) return;
        var now = DateTime.UtcNow;
        if (now < _nextCheck) return;
        _nextCheck = now.AddSeconds(1);

        if (_phases == null)
        {
            var eif = ExtensionLoader.ActiveExtensionInfo;
            if (eif == null) return;
            _phases = PhaseConfig.Load(eif.GetFullFolderPath());
        }
        if (_phases.Count == 0) return;

        var os = OS.currentInstance;
        if (os?.Flags == null) return;

        foreach (var phase in _phases)
        {
            if (_started.Contains(phase.Id)) continue;
            if (phase.IsMet(f => os.Flags.HasFlag(f)))
            {
                _started.Add(phase.Id);
                Console.WriteLine($"[Phases] 阶段开始: {phase.Id} ({phase.Title})");
            }
        }

        // 按定义顺序发送
        string msg = PauseProtocol.Phases + string.Join(",",
            _phases.Where(p => _started.Contains(p.Id)).Select(p => LobbyProtocol.ToB64(p.Id)));
        if (msg == _lastSent) return;
        _lastSent = msg;
        ServerManager.SendToConsole(msg);
    }

    /// <summary>每次开局 / 退出扩展页时清空（重新按 flag 判定）</summary>
    public static void Reset()
    {
        _phases = null;
        _started.Clear();
        _lastSent = "";
        _nextCheck = DateTime.MinValue;
    }
}
