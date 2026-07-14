using Hacknet;
using HarmonyLib;

namespace ServerClientEventsMod.Events;

/// <summary>
/// 全局替换字典 —— 记录 #KEY# → 实际值 的映射。
/// 供服务端或初始化代码在游戏启动时预填充。
/// 也可通过 Set 供 Pathfinder 自定义替换注册。
/// </summary>
public static class GlobalReplaceDict
{
    private static readonly Dictionary<string, string> _dict = new();

    public static string Get(string key) => _dict.TryGetValue(key, out var val) ? val : null;
    public static bool Has(string key) => _dict.ContainsKey(key);
    public static void Set(string key, string value) { _dict[key] = value; }
    public static void SetRange(IEnumerable<KeyValuePair<string, string>> entries)
    {
        foreach (var kv in entries)
            if (!_dict.ContainsKey(kv.Key))
                _dict[kv.Key] = kv.Value;
    }
    public static void Clear() => _dict.Clear();
}
