using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClientPlugin;

/// <summary>
/// 执行范围存储管理器。
/// Global ← players="PUBLIC" 的对象
/// Private ← 玩家标识符 → 该玩家的对象列表
/// _meta ← ConditionalWeakTable: 对象 → ScopeInfo
/// </summary>
public static class ScopeManager
{
    public static readonly List<object> Global = new();
    public static readonly Dictionary<string, List<object>> Private =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConditionalWeakTable<object, ScopeInfo> _meta = new();

    public static void Register(object item, ScopeInfo scope)
    {
        if (item == null) return;
        _meta.Remove(item);
        _meta.Add(item, scope);

        if (scope.IsPublic)
        {
            Global.Add(item);
        }
        else
        {
            string key = scope.Players;
            if (!Private.TryGetValue(key, out var list))
            {
                list = new List<object>();
                Private[key] = list;
            }
            list.Add(item);
        }
    }

    public static ScopeInfo GetScope(object item) =>
        _meta.TryGetValue(item, out var scope) ? scope : null;

    public static void Remove(object item)
    {
        _meta.Remove(item);
        Global.Remove(item);
        foreach (var kv in Private)
            kv.Value.Remove(item);
    }

    public static void Clear()
    {
        Global.Clear();
        Private.Clear();
        // net472 的 ConditionalWeakTable 没有 Clear()，靠 GC 自动回收
    }

    public static IEnumerable<object> GetForPlayer(string playerId)
    {
        foreach (var item in Global)
            yield return item;
        if (Private.TryGetValue(playerId, out var list))
            foreach (var item in list)
                yield return item;
    }
}
