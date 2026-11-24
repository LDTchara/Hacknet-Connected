using System;
using Hacknet;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace HNC.RemovableTags;

/// <summary>什么都不做的动作。被移除的延时动作会被原地换成它。</summary>
public sealed class NoOpAction : DelayablePathfinderAction
{
    public const string TagName = "HNCNoOp";

    public override void Trigger(OS os)
    {
    }
}

/// <summary>
/// &lt;RemoveConditionsOrActions RemoveTag="f1" /&gt;
/// 把带 RemoveableTag="f1" 的条件 / 动作从挂载状态移除（可用逗号写多个标签）。
/// 局限：移除的是"别的"条目。正在执行的这一组动作已经被 vanilla 从挂载列表里摘下来了，
/// 所以不能用它移除同一组里的兄弟动作。
/// </summary>
public sealed class RemoveConditionsOrActionsAction : DelayablePathfinderAction
{
    [XMLStorage] public string RemoveTag = "";

    public override void LoadFromXml(ElementInfo info)
    {
        base.LoadFromXml(info);
        if (TagList.Parse(RemoveTag).Length == 0)
            throw new FormatException("RemoveConditionsOrActions: 缺少 RemoveTag 属性");
    }

    public override void Trigger(OS os)
    {
        int n = RemoveService.Remove(os, TagList.Parse(RemoveTag));
        Console.WriteLine($"[RemovableTags] RemoveTag=\"{RemoveTag}\"：移除 / 替换了 {n} 个条目");
    }
}

internal static class RemovableTagsFeature
{
    public static void Register()
    {
        TryRegister(NoOpAction.TagName, () => ActionManager.RegisterAction<NoOpAction>(NoOpAction.TagName));
        TryRegister("RemoveConditionsOrActions",
            () => ActionManager.RegisterAction<RemoveConditionsOrActionsAction>("RemoveConditionsOrActions"));
    }

    private static void TryRegister(string name, Action register)
    {
        try { register(); }
        catch (Exception ex) { Console.WriteLine($"[RemovableTags] 注册 {name} 失败: {ex.Message}"); }
    }
}
