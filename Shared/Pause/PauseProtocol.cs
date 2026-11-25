using System;

namespace HNC.Pause;

// 全员暂停协议：终端（权威）、ServerPlugin、ClientPlugin 三方共用。纯 C#，不依赖游戏程序集。
//
// 客户端 → 终端：PING:{playerId}:{0|1}     每 2 秒一次心跳；第二段 1 = 已进入游戏且加载完成
// 终端 → 客户端：PONG                       心跳回应（客户端据此判断自己是否掉线）
// 终端 → 所有已加入的客户端 + 服务端游戏：
//                HNC_PAUSE:{0|1}:{b64 说明文字}   全员暂停状态（有变化才发；加入大厅时补发一次）
// 服务端游戏 → 终端：
//                HNC_PAUSE_REQ:1             主机手动暂停（F8）
//                HNC_PAUSE_REQ:0             主机强制继续（F9）：解除所有暂停，不再等掉线的人
// 服务端游戏 → 终端：
//                HNC_PHASES:{b64 阶段id},{...}   已开始的阶段（有变化才发），终端转给大厅成员

internal static class PauseProtocol
{
    public const string Ping = "PING:";
    public const string Pong = "PONG";
    public const string State = "HNC_PAUSE:";
    public const string Request = "HNC_PAUSE_REQ:";
    public const string Phases = "HNC_PHASES:";

    /// <summary>多久没收到心跳（或心跳回应）算掉线</summary>
    public const double TimeoutSeconds = 6.0;

    /// <summary>心跳间隔</summary>
    public const double HeartbeatSeconds = 2.0;

    public static string EncodeState(bool paused, string text) =>
        $"{State}{(paused ? 1 : 0)}:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text ?? ""))}";

    /// <summary>解析 HNC_PAUSE:{0|1}:{b64}；格式不对返回 false</summary>
    public static bool TryDecodeState(string msg, out bool paused, out string text)
    {
        paused = false;
        text = "";
        if (!msg.StartsWith(State, StringComparison.Ordinal)) return false;
        var p = msg.Substring(State.Length).Split(new[] { ':' }, 2);
        if (p.Length < 1) return false;
        paused = p[0] == "1";
        if (p.Length > 1)
        {
            try { text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[1])); }
            catch { text = ""; }
        }
        return true;
    }
}
