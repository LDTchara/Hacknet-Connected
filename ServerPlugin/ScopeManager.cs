using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ServerPlugin;

/// <summary>
/// 执行范围存储管理器。
///
/// 结构：
///   Global  ← players="PUBLIC" 的对象
///   Private ← 玩家标识符 → 该玩家的对象列表
///   _meta   ← ConditionalWeakTable: 对象 → ScopeInfo（自动 GC 回收）
///
/// 当玩家注册到服务端后，用其玩家标识符查 Private 字典即可得到所有私有对象。
/// </summary>
public static class ScopeManager
{
    /// <summary>全局对象（players="PUBLIC"）</summary>
    public static readonly List<object> Global = new();

    /// <summary>
    /// 私有对象按玩家索引。
    /// Key = 玩家标识符（如 "1", "p1"），
    /// Value = 该玩家的 Action/Condition/Function/Mission 列表。
    /// </summary>
    public static readonly Dictionary<string, List<object>> Private =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>对象→ScopeInfo 元数据（弱引用，GC 自动清理）</summary>
    private static readonly ConditionalWeakTable<object, ScopeInfo> _meta =
        new();

    /// <summary>
    /// 注册一个对象及其执行范围。
    /// </summary>
    /// <param name="item">Action/Condition/Goal/Mission 实例</param>
    /// <param name="scope">范围信息</param>
    public static void Register(object item, ScopeInfo scope)
    {
        if (item == null) return;

        // 存元数据（net472: 无 AddOrUpdate，用 Remove+Add）
        _meta.Remove(item);
        _meta.Add(item, scope);

        // 按类型加入索引
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

    /// <summary>获取对象的 ScopeInfo（无 scope 时返回 null）</summary>
    public static ScopeInfo GetScope(object item)
    {
        return _meta.TryGetValue(item, out var scope) ? scope : null;
    }

    /// <summary>移除对象及其 scope（比如 action 已被触发消耗后）</summary>
    public static void Remove(object item)
    {
        _meta.Remove(item);
        Global.Remove(item);
        foreach (var kv in Private)
            kv.Value.Remove(item);
    }

    /// <summary>清空所有注册</summary>
    public static void Clear()
    {
        Global.Clear();
        Private.Clear();
        // net472 的 ConditionalWeakTable 没有 Clear()，靠 GC 自动回收
    }

    /// <summary>获取指定玩家的所有对象（全局+私有）</summary>
    public static IEnumerable<object> GetForPlayer(string playerId)
    {
        // 全局的都适用
        foreach (var item in Global)
            yield return item;

        // 私有的按玩家匹配
        if (Private.TryGetValue(playerId, out var privateList))
        {
            foreach (var item in privateList)
                yield return item;
        }
    }
}
