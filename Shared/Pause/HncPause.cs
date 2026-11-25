using System;
using System.Collections.Generic;
using System.Linq;
using Hacknet;
using Hacknet.Input;
using HarmonyLib;
using HNC.Lobby;
using Microsoft.Xna.Framework;

namespace HNC.Pause;

/// <summary>
/// 暂停闸门（取自 Freezer）：暂停时跳过 Game1.Update、Game1.Draw 和 SDL 文字输入。
///
/// 和 Freezer 的不同：
/// - 按"原因"暂停：global（终端下发的全员暂停）、net（本机与服务器断线）等，任何一个原因还在就保持暂停；
/// - 只在游戏已加载（OS.isLoaded）时生效，大厅、菜单不会被冻住；
/// - 暂停时在 Draw 后置补丁里画提示（原版 Draw 被跳过，否则屏幕是黑的或残影）；
/// - 没有玩家快捷键：联机中由终端统一控制（服务端主机的 F8 / F9 见 ServerPause）。
///
/// Harmony 的规则：前置补丁跳过原方法后，后置补丁照样执行——
/// HNC 的网络轮询、管道消息、HNC.Sync 都挂在 Game1.Update 的后置补丁上，所以暂停期间网络照常工作。
///
/// 已知限制（同 Freezer）：ActionDelayer 按真实时间计时，暂停期间到期的延时动作会在恢复后一起执行；
/// 独立线程、已在播放的音乐不会停。
/// </summary>
internal static class HncPause
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, string> _reasons = new();
    private static DateTime _pausedSince = DateTime.MinValue;

    /// <summary>提示框最下面一行的操作说明（服务端主机用来显示 F8 / F9）</summary>
    public static string HostHint = "";

    public const string ReasonGlobal = "global";
    public const string ReasonNet = "net";

    /// <summary>有暂停原因（不论游戏是否在运行）</summary>
    public static bool HasReasons
    {
        get { lock (_lock) return _reasons.Count > 0; }
    }

    /// <summary>闸门是否关闭：有暂停原因，且游戏已加载</summary>
    public static bool IsPaused => HasReasons && GameRunning();

    public static bool GameRunning()
    {
        var os = OS.currentInstance;
        return os != null && os.isLoaded && !os.HasExitedAndEnded;
    }

    public static void Set(string reason, string text)
    {
        lock (_lock)
        {
            if (_reasons.Count == 0) _pausedSince = DateTime.UtcNow;
            bool isNew = !_reasons.ContainsKey(reason);
            _reasons[reason] = text ?? "";
            if (isNew) Console.WriteLine($"[Pause] 暂停（{reason}）: {text}");
        }
    }

    public static void Clear(string reason)
    {
        lock (_lock)
        {
            if (_reasons.Remove(reason))
                Console.WriteLine($"[Pause] 解除（{reason}）" + (_reasons.Count == 0 ? "，游戏继续" : ""));
        }
    }

    public static void ClearAll()
    {
        lock (_lock) _reasons.Clear();
    }

    private static List<string> Texts()
    {
        lock (_lock) return _reasons.Values.Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
    }

    private static TimeSpan Elapsed()
    {
        lock (_lock) return _reasons.Count == 0 ? TimeSpan.Zero : DateTime.UtcNow - _pausedSince;
    }

    // ═══════════════════════════════════════════════ 补丁

    [HarmonyPatch(typeof(Game1), "Update", new[] { typeof(GameTime) })]
    private static class UpdateGate
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix() => !IsPaused;
    }

    [HarmonyPatch(typeof(Game1), "Draw", new[] { typeof(GameTime) })]
    private static class DrawGate
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix() => !IsPaused;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Game1 __instance)
        {
            if (!IsPaused) return;
            try
            {
                DrawOverlay(__instance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Pause] 提示绘制失败: {ex.Message}");
            }
        }
    }

    // SDL 文字事件不经过 Update/Draw：暂停期间丢弃，恢复后不会冒出来
    [HarmonyPatch(typeof(TextInputHook), "OnTextInput", new[] { typeof(char) })]
    private static class TextInputGate
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix() => !IsPaused;
    }

    private static void DrawOverlay(Game1 game)
    {
        var gd = game.GraphicsDevice;
        var sb = GuiData.spriteBatch;
        if (gd == null || sb == null) return;

        var vp = gd.Viewport.Bounds;
        gd.Clear(Color.Black);
        sb.Begin();

        int cx = vp.Width / 2;
        float y = vp.Height * 0.32f;
        LobbyUi.Centered(GuiData.font, "GAME PAUSED", cx, y, Color.OrangeRed);
        y += 56;

        foreach (var text in Texts())
        {
            LobbyUi.Centered(GuiData.smallfont, text, cx, y, Color.White);
            y += 28;
        }

        var t = Elapsed();
        LobbyUi.Centered(GuiData.smallfont, $"Paused for {(int)t.TotalMinutes:00}:{t.Seconds:00}", cx, y + 10, Color.Gray);

        if (!string.IsNullOrEmpty(HostHint))
            LobbyUi.Centered(GuiData.smallfont, HostHint, cx, vp.Height - 80, Color.LightGreen);

        sb.End();
    }
}
