using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace HNC.Phases;

/// <summary>
/// 阶段预设：扩展作者把任务流程切成 A → B → C … 多个阶段，每个阶段按角色准备预设，
/// 供中途加入的玩家选择：选一个还没开始（或正在进行）的阶段，等它开始时用对应预设开新游戏进入。
///
/// 阶段定义文件：扩展根目录下的 HNC_Phases.xml（或 HNC/Phases.xml）
///
///   &lt;HNCPhases&gt;
///     &lt;Phase id="B" title="Chapter 2"&gt;              （元素名随意，如 &lt;Ph1&gt;；没写 id 时用元素名）
///       &lt;Description&gt;…&lt;/Description&gt;
///       &lt;Conditions&gt;                               （服务端游戏的 flag；全部条件满足即视为阶段开始，之后不会再变回未开始）
///         &lt;HasFlags&gt;f1,f2,f3&lt;/HasFlags&gt;           任意一个存在即可（可写多个 HasFlags，每个都要满足）
///         &lt;RequireFlags&gt;f2&lt;/RequireFlags&gt;          必须全部存在
///       &lt;/Conditions&gt;                              （没有 Conditions = 开局即开始）
///       &lt;Set p="p1" pth="/Sets/p1_B.xml"/&gt;         按角色的预设文件（路径相对扩展根目录；pth 或 path 都行）
///       &lt;Set p="p2" pth="/Sets/p2_B.xml"/&gt;
///     &lt;/Phase&gt;
///   &lt;/HNCPhases&gt;
///
/// 预设文件：
///
///   &lt;HNCSet title="Chapter 2 - Alice"&gt;
///     &lt;Description&gt;…&lt;/Description&gt;
///     &lt;StartingMission&gt;Missions/ch2_p1.xml&lt;/StartingMission&gt;   可选；NONE = 没有起始任务；不写 = 用扩展默认
///     &lt;StartingActions&gt;Actions/ch2_p1.xml&lt;/StartingActions&gt;   可选；NONE = 没有起始动作；不写 = 用扩展默认
///     &lt;Flags&gt;ch1_done,met_bob&lt;/Flags&gt;                         可选；进入游戏后加到玩家身上
///   &lt;/HNCSet&gt;
///
/// 程序、文件等用起始动作给（例如 AddAsset）。玩家电脑由 HNC 的角色机制（PlayerNodePatch）决定，预设不替换。
/// </summary>
internal sealed class PhaseDef
{
    public string Id = "";
    public string Title = "";
    public string Description = "";

    /// <summary>每组至少有一个 flag 存在（HasFlags，可多组）</summary>
    public readonly List<List<string>> AnyOf = new();

    /// <summary>全部存在（RequireFlags）</summary>
    public readonly List<string> AllOf = new();

    /// <summary>角色 → 预设文件（相对扩展根目录）</summary>
    public readonly Dictionary<string, string> Sets = new(StringComparer.OrdinalIgnoreCase);

    public bool IsMet(Func<string, bool> hasFlag) =>
        AnyOf.All(group => group.Count == 0 || group.Any(hasFlag)) && AllOf.All(hasFlag);

    public string? SetFor(string role) =>
        !string.IsNullOrEmpty(role) && Sets.TryGetValue(role, out var path) ? path : null;
}

internal sealed class PhaseSet
{
    public string Title = "";
    public string Description = "";

    /// <summary>null = 用扩展默认；"" = 没有（NONE）</summary>
    public string? StartingMission;

    /// <summary>null = 用扩展默认；"" = 没有（NONE）</summary>
    public string? StartingActions;

    public readonly List<string> Flags = new();

    public static PhaseSet? Load(string extFolder, string relativePath)
    {
        string full = PhaseConfig.Resolve(extFolder, relativePath);
        if (!File.Exists(full))
        {
            Console.WriteLine($"[Phases] 找不到预设文件: {full}");
            return null;
        }
        try
        {
            var root = XDocument.Load(full).Root;
            if (root == null) return null;
            var set = new PhaseSet
            {
                Title = (string?)root.Attribute("title") ?? Path.GetFileNameWithoutExtension(full),
                Description = PhaseConfig.ChildText(root, "Description") ?? "",
                StartingMission = NoneToEmpty(PhaseConfig.ChildText(root, "StartingMission")),
                StartingActions = NoneToEmpty(PhaseConfig.ChildText(root, "StartingActions"))
            };
            set.Flags.AddRange(PhaseConfig.SplitList(PhaseConfig.ChildText(root, "Flags")));
            return set;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Phases] 预设文件解析失败 {full}: {ex.Message}");
            return null;
        }
    }

    private static string? NoneToEmpty(string? s) =>
        s == null ? null : s.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase) ? "" : s.Trim();
}

internal static class PhaseConfig
{
    private static readonly string[] Candidates = { "HNC_Phases.xml", Path.Combine("HNC", "Phases.xml") };

    /// <summary>读取扩展的阶段定义；没有定义文件返回空列表（功能不启用）</summary>
    public static List<PhaseDef> Load(string extFolder)
    {
        var result = new List<PhaseDef>();
        string? file = Candidates.Select(c => Path.Combine(extFolder, c)).FirstOrDefault(File.Exists);
        if (file == null) return result;

        try
        {
            var root = XDocument.Load(file).Root;
            if (root == null) return result;

            foreach (var el in root.Elements())
            {
                var def = new PhaseDef
                {
                    Id = (string?)el.Attribute("id") ?? el.Name.LocalName,
                    Description = ChildText(el, "Description") ?? ""
                };
                def.Title = (string?)el.Attribute("title") ?? (string?)el.Attribute("name") ?? def.Id;

                var cond = el.Elements().FirstOrDefault(e => Is(e, "Conditions"));
                if (cond != null)
                {
                    foreach (var c in cond.Elements())
                    {
                        if (Is(c, "HasFlags")) def.AnyOf.Add(SplitList(c.Value));
                        else if (Is(c, "RequireFlags")) def.AllOf.AddRange(SplitList(c.Value));
                        else Console.WriteLine($"[Phases] 阶段 {def.Id}: 不认识的条件 <{c.Name.LocalName}>，已忽略");
                    }
                }

                foreach (var s in el.Elements().Where(e => Is(e, "Set")))
                {
                    string role = ((string?)s.Attribute("p") ?? "").Trim();
                    string path = ((string?)s.Attribute("pth") ?? (string?)s.Attribute("path") ?? "").Trim();
                    if (role.Length > 0 && path.Length > 0) def.Sets[role] = path;
                }

                if (result.Any(p => p.Id.Equals(def.Id, StringComparison.OrdinalIgnoreCase)))
                    Console.WriteLine($"[Phases] 阶段 id 重复: {def.Id}（后一个仍会加载，但选择时可能混淆）");
                result.Add(def);
            }
            Console.WriteLine($"[Phases] 已加载 {result.Count} 个阶段: {file}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Phases] 阶段定义解析失败 {file}: {ex.Message}");
        }
        return result;
    }

    /// <summary>预设路径相对扩展根目录（容忍开头的 / 或 \）</summary>
    public static string Resolve(string extFolder, string relativePath) =>
        Path.Combine(extFolder, relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));

    public static List<string> SplitList(string? s) =>
        (s ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    internal static string? ChildText(XElement el, string name) =>
        el.Elements().FirstOrDefault(e => Is(e, name))?.Value;

    private static bool Is(XElement e, string name) =>
        e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase);
}
