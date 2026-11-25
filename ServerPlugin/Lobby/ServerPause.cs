using System;
using Hacknet;
using HNC.Pause;
using Microsoft.Xna.Framework.Input;

namespace ServerPlugin;

/// <summary>
/// 服务端游戏这一侧的全员暂停：按终端下发的 HNC_PAUSE 暂停/恢复本机，
/// 主机在游戏里按 F8 请求全员暂停、F9 强制继续（不再等掉线的人）。
/// 权威状态在终端（PauseManager）。都在游戏主线程调用（GameUpdatePatch 后置补丁，暂停时照常执行）。
/// </summary>
internal static class ServerPause
{
    private const Keys PauseKey = Keys.F8;
    private const Keys ResumeKey = Keys.F9;

    private static bool _pauseWasDown;
    private static bool _resumeWasDown;

    public static bool TryHandlePipeMessage(string msg)
    {
        if (!PauseProtocol.TryDecodeState(msg, out bool paused, out string text)) return false;

        if (paused)
        {
            HncPause.Set(HncPause.ReasonGlobal, text);
            HncPause.HostHint = "F9: continue now (stop waiting)";
        }
        else
        {
            HncPause.Clear(HncPause.ReasonGlobal);
            HncPause.HostHint = "";
        }
        return true;
    }

    /// <summary>每帧：主机快捷键（只在游戏中、窗口有焦点时）</summary>
    public static void Update()
    {
        var kb = Keyboard.GetState();
        bool pauseDown = kb.IsKeyDown(PauseKey);
        bool resumeDown = kb.IsKeyDown(ResumeKey);

        bool focused = Game1.getSingleton()?.IsActive == true;
        if (focused && HncPause.GameRunning() && ServerManager.IsConnected)
        {
            if (pauseDown && !_pauseWasDown && !HncPause.HasReasons)
                ServerManager.SendToConsole(PauseProtocol.Request + "1");
            if (resumeDown && !_resumeWasDown && HncPause.HasReasons)
                ServerManager.SendToConsole(PauseProtocol.Request + "0");
        }

        _pauseWasDown = pauseDown;
        _resumeWasDown = resumeDown;
    }

    /// <summary>退出扩展页时清空</summary>
    public static void Reset()
    {
        HncPause.ClearAll();
        HncPause.HostHint = "";
    }
}
