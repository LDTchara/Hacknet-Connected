using System;
using System.Collections.Generic;
using System.Reflection;
using Hacknet;
using Hacknet.Input;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;

namespace HNC.IrcChat;

/// <summary>输入框用的原始输入：字符缓冲区和带按键重复的按键触发。</summary>
internal static class ChatInput
{
    private static readonly FieldInfo? HookField = AccessTools.Field(typeof(GuiData), "TextInputHook");
    private static readonly Dictionary<Keys, float> Held = new();

    private const float RepeatDelay = 0.44f;
    private const float RepeatInterval = 0.04f;

    /// <summary>
    /// 取走本帧输入的文字（包含输入法输入的中文和 Ctrl+V 粘贴的内容）并清空缓冲区。
    /// vanilla 的 GuiData.getFilteredKeys 只保留 ASCII 可见字符，所以这里直接读底层缓冲区。
    /// 注意缓冲区是全局共享的、谁先读谁拿走——所以输入框有焦点时必须屏蔽终端（见 TerminalInputBlocker）。
    /// </summary>
    public static string TakeText()
    {
        if (HookField?.GetValue(null) is TextInputHook hook)
        {
            string text = hook.Buffer;
            hook.clearBuffer();
            return text;
        }
        return new string(GuiData.getFilteredKeys()); // 兜底：只有 ASCII
    }

    /// <summary>按下的那一帧触发一次；按住超过 0.44 秒后每 0.04 秒再触发一次。</summary>
    public static bool Triggered(Keys key, KeyboardState current, KeyboardState last)
    {
        if (!current.IsKeyDown(key))
        {
            Held.Remove(key);
            return false;
        }
        if (!last.IsKeyDown(key))
        {
            Held[key] = RepeatDelay;
            return true;
        }
        if (!Held.TryGetValue(key, out float left))
        {
            Held[key] = RepeatDelay;
            return false;
        }
        left -= GuiData.lastTimeStep;
        if (left <= 0f)
        {
            Held[key] = RepeatInterval;
            return true;
        }
        Held[key] = left;
        return false;
    }

    /// <summary>只在按下的那一帧触发（没有重复）。</summary>
    public static bool Pressed(Keys key, KeyboardState current, KeyboardState last) =>
        current.IsKeyDown(key) && !last.IsKeyDown(key);

    public static bool ShiftDown(KeyboardState s) => s.IsKeyDown(Keys.LeftShift) || s.IsKeyDown(Keys.RightShift);

    public static void ResetRepeat() => Held.Clear();
}
