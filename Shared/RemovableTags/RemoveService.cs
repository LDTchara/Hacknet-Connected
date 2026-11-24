using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Hacknet;
using HarmonyLib;

namespace HNC.RemovableTags;

/// <summary>
/// 把带指定标签的条件 / 动作从"挂载状态"移除。覆盖三处：
///   1. os.ConditionalActions：挂载中等待条件的"条件 + 动作组"
///   2. DelayableActionSystem（IRC / DHS 的 runtime 文件夹）：带 Delay 的待执行动作，序列化成加密文件
///   3. FastDelayableActionSystem（FastActionHost）：同样的待执行动作，但存的是内存对象
///
/// 对 2、3 **不删除条目，而是原地换成什么都不做的空动作**：
/// vanilla 的 Update 是"先触发动作，再 RemoveAt(i)"，如果移除动作在被触发时直接删别的条目，
/// 下标会错位，循环最后删掉的是另一个无辜的动作。原地替换不改变下标和计时，空动作到点后自然消失。
/// </summary>
internal static class RemoveService
{
    private const string DelayPass = "dasencrypt"; // DelayableActionSystem.EncryptionPass（私有）

    private static readonly FieldInfo? IsUpdatingField = AccessTools.Field(typeof(RunnableConditionalActions), "IsUpdating");
    private static readonly FieldInfo? FolderField = AccessTools.Field(typeof(DelayableActionSystem), "folder");
    private static readonly FieldInfo? FastActionsField = AccessTools.Field(typeof(FastDelayableActionSystem), "Actions");

    private static readonly List<string[]> Pending = new();

    /// <returns>被移除 / 被替换的条目数。</returns>
    public static int Remove(OS os, string[] tags)
    {
        if (tags.Length == 0) return 0;

        int n = RemoveDelayed(os, tags);

        var cond = os.ConditionalActions;
        if (IsUpdating(cond))
        {
            // 此刻 RunnableConditionalActions.Update 正在遍历这个列表（本动作多半就是它触发的）：
            // 现在删会让下标错位、漏检一项，所以等 Update 结束再删
            lock (Pending) Pending.Add(tags);
        }
        else
        {
            n += RemoveMounted(cond, tags);
        }
        return n;
    }

    /// <summary>RunnableConditionalActions.Update 结束后调用：处理延后的移除请求。</summary>
    public static void FlushPending(RunnableConditionalActions cond)
    {
        string[][] batch;
        lock (Pending)
        {
            if (Pending.Count == 0) return;
            batch = Pending.ToArray();
            Pending.Clear();
        }
        int n = 0;
        foreach (var tags in batch) n += RemoveMounted(cond, tags);
        if (n > 0) Console.WriteLine($"[RemovableTags] 延后移除了 {n} 个挂载中的条件 / 动作");
    }

    private static bool IsUpdating(RunnableConditionalActions cond) =>
        IsUpdatingField?.GetValue(cond) is bool b && b;

    private static int RemoveMounted(RunnableConditionalActions cond, string[] tags)
    {
        int n = cond.Actions.RemoveAll(set => set != null && TagStore.Matches(set.Condition, tags));
        foreach (var set in cond.Actions)
        {
            if (set?.Actions != null)
                n += set.Actions.RemoveAll(a => TagStore.Matches(a, tags));
        }
        return n;
    }

    private static int RemoveDelayed(OS os, string[] tags)
    {
        var nodes = os.netMap?.nodes;
        if (nodes == null) return 0;

        int n = 0;
        foreach (var comp in nodes.ToArray())
        {
            if (comp?.daemons == null) continue;
            foreach (var daemon in comp.daemons.ToArray())
            {
                DelayableActionSystem? sys = daemon switch
                {
                    IRCDaemon irc => irc.DelayedActions,
                    DLCHubServer dhs => dhs.DelayedActions,
                    FastActionHost fast => fast.DelayedActions,
                    _ => null,
                };
                if (sys == null) continue;

                n += sys is FastDelayableActionSystem f ? ReplaceInMemory(f, tags) : ReplaceInFiles(sys, tags);
            }
        }
        return n;
    }

    /// <summary>读队列文件第一行的剩余秒数；读不出来或为负数按 0。</summary>
    internal static float ParseDelay(string head) =>
        float.TryParse(head, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && seconds > 0f ? seconds : 0f;

    /// <summary>与 vanilla 的 DelayableActionSystem 写入的格式一致。</summary>
    internal static string FormatDelay(float seconds) => seconds.ToString("0.0000000000", CultureInfo.InvariantCulture);

    private static int ReplaceInFiles(DelayableActionSystem sys, string[] tags)
    {
        if (FolderField?.GetValue(sys) is not Folder folder) return 0;

        int n = 0;
        for (int i = 0; i < folder.files.Count; i++)
        {
            try
            {
                var file = folder.files[i];
                // 格式：第一行是剩余秒数，后面是加密的动作 XML
                string data = file.data;
                int newline = data.IndexOf('\n');
                if (newline < 0) continue;

                string xml = FileEncrypter.DecryptString(data.Substring(newline + 1), DelayPass)[2];
                if (!TagList.Intersects(TagList.ExtractRootAttribute(xml, TagList.AttributeName), tags)) continue;

                // 空动作继承原条目剩余的延迟；原条目没有可用的延迟（读不出来 / 已经 ≤ 0）就是 0（下一帧就触发，什么都不做）。
                // 文件名不变。空动作的 XML 不带 Delay / DelayHost：到点后直接执行，不会再被丢回队列里等第二次。
                file.data = FormatDelay(ParseDelay(data.Substring(0, newline))) + "\n" +
                            FileEncrypter.EncryptString("<" + NoOpAction.TagName + " />", "das", "UNKNOWN", DelayPass);
                n++;
            }
            catch
            {
                // 解不开的条目留着不动（和 vanilla 的 Update 一样容忍坏条目）
            }
        }
        return n;
    }

    private static int ReplaceInMemory(FastDelayableActionSystem sys, string[] tags)
    {
        if (FastActionsField?.GetValue(sys) is not List<KeyValuePair<float, SerializableAction>> list) return 0;

        int n = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (!TagStore.Matches(list[i].Value, tags)) continue;
            // 同样继承剩余延迟（key），没有可用的就是 0
            list[i] = new KeyValuePair<float, SerializableAction>(Math.Max(0f, list[i].Key), new NoOpAction());
            n++;
        }
        return n;
    }
}

[HarmonyPatch(typeof(RunnableConditionalActions), nameof(RunnableConditionalActions.Update))]
internal static class FlushPendingRemovalsPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunnableConditionalActions __instance) => RemoveService.FlushPending(__instance);
}
