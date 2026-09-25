using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hacknet;

namespace ReplaceSystem;

/// <summary>
/// Rewrites token-bearing strings in the live Hacknet content graph so normal
/// game logic and save serialization see resolved values. Traversal starts at
/// gameplay data roots and deliberately refuses framework/graphics objects.
/// </summary>
internal static class RuntimeObjectRewriter
{
    private const int MaxDepth = 32;
    private const int MaxVisitedObjects = 100000;

    private static readonly ReferenceComparer Comparer = new();

    internal static void ClearSession()
    {
        // Traversal state is local to Apply. This is an explicit lifecycle hook
        // for session-owned adapters that may be added later.
    }

    internal static void Apply(OS os)
    {
        if (os == null)
            return;

        var visited = new HashSet<object>(Comparer);

        // Do not use OS itself as a traversal root: that would lead into
        // ScreenManager, Game, ContentManager and native graphics state.
        RewriteRoot(os.thisComputer, visited);
        RewriteRoot(os.currentMission, visited);
        RewriteRoot(os.terminal, visited);
        RewriteRoot(os.display, visited);

        if (os.netMap?.nodes != null)
        {
            foreach (Computer computer in os.netMap.nodes)
                RewriteRoot(computer, visited);
        }

        os.displayCache = ReplacementCatalog.Resolve(os.displayCache);

        if (visited.Count >= MaxVisitedObjects)
        {
            ReplaceSystemPlugin.LogWarning(
                $"Runtime text scan stopped at the safety limit of {MaxVisitedObjects} objects.");
        }
    }

    private static void RewriteRoot(object root, HashSet<object> visited)
    {
        if (root == null || visited.Count >= MaxVisitedObjects)
            return;

        RewriteValue(root, visited, 0, out _);
    }

    private static bool RewriteValue(
        object value,
        HashSet<object> visited,
        int depth,
        out object rewritten)
    {
        rewritten = value;
        if (value == null || depth > MaxDepth || visited.Count >= MaxVisitedObjects)
            return false;

        if (value is string text)
        {
            string resolved = ReplacementCatalog.Resolve(text);
            rewritten = resolved;
            return !string.Equals(text, resolved, StringComparison.Ordinal);
        }

        Type type = value.GetType();
        if (IsTerminalType(type))
            return false;

        bool isValueType = type.IsValueType;
        if (!isValueType && !visited.Add(value))
            return false;

        bool changed = false;

        if (value is IDictionary dictionary)
        {
            var keys = new List<object>();
            foreach (object key in dictionary.Keys)
                keys.Add(key);

            foreach (object key in keys)
            {
                object item = dictionary[key];
                if (!RewriteValue(item, visited, depth + 1, out object next))
                    continue;

                try
                {
                    dictionary[key] = next;
                    changed = true;
                }
                catch (NotSupportedException)
                {
                    // A read-only dictionary is safe to leave untouched.
                }
            }

            return changed;
        }

        if (value is IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                object item = list[i];
                if (!RewriteValue(item, visited, depth + 1, out object next))
                    continue;

                try
                {
                    list[i] = next;
                    changed = true;
                }
                catch (NotSupportedException)
                {
                    // A read-only list is safe to leave untouched.
                }
            }

            return changed;
        }

        if (ShouldSkipObject(type))
            return false;

        foreach (FieldInfo field in GetFields(type))
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly || ShouldSkipField(field))
                continue;

            object fieldValue;
            try
            {
                fieldValue = field.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (!RewriteValue(fieldValue, visited, depth + 1, out object nextValue))
                continue;

            try
            {
                // For boxed structs this mutates the box; the parent call then
                // writes that box back into its field/list slot.
                field.SetValue(value, nextValue);
                changed = true;
            }
            catch (Exception ex)
            {
                ReplaceSystemPlugin.LogDebug(
                    $"Could not rewrite {type.FullName}.{field.Name}: {ex.GetType().Name}");
            }
        }

        rewritten = value;
        return changed;
    }

    private static IEnumerable<FieldInfo> GetFields(Type type)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    private static bool IsTerminalType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(TimeSpan) ||
               type == typeof(IntPtr) ||
               type == typeof(UIntPtr);
    }

    private static bool ShouldSkipObject(Type type)
    {
        if (typeof(Delegate).IsAssignableFrom(type) ||
            typeof(Stream).IsAssignableFrom(type) ||
            typeof(MemberInfo).IsAssignableFrom(type) ||
            typeof(Type).IsAssignableFrom(type) ||
            typeof(OS).IsAssignableFrom(type))
        {
            return true;
        }

        string ns = type.Namespace ?? string.Empty;
        return ns.StartsWith("Microsoft.Xna.Framework", StringComparison.Ordinal) ||
               ns.StartsWith("System.Reflection", StringComparison.Ordinal) ||
               ns.StartsWith("System.Threading", StringComparison.Ordinal) ||
               ns.StartsWith("HarmonyLib", StringComparison.Ordinal) ||
               ns.StartsWith("BepInEx", StringComparison.Ordinal);
    }

    private static bool ShouldSkipField(FieldInfo field)
    {
        Type type = field.FieldType;
        return typeof(OS).IsAssignableFrom(type) ||
               typeof(Delegate).IsAssignableFrom(type) ||
               typeof(Stream).IsAssignableFrom(type);
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
