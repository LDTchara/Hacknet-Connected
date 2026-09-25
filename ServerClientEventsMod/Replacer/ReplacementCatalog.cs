using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using BepInEx;
using Hacknet.Extensions;

namespace ExtraEventsPlugin;

/// <summary>
/// Exact, non-recursive runtime replacements. Tokens must use #...# and values
/// may not contain another token. Mappings are immutable for one OS session so
/// eager replacement has deterministic semantics.
/// </summary>
internal static class ReplacementCatalog
{
    internal const string GlobalFileName = "replacements.json";
    internal const string ExtensionFileName = "RuntimeReplacements.json";

    private static readonly Regex TokenRegex = new(
        @"#[^#\r\n]+#",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly object WriteLock = new();
    private static Dictionary<string, string> _snapshot = NewMap();
    private static string _globalPath;

    internal static void Initialize()
    {
        string directory = Path.Combine(Paths.ConfigPath, "ReplaceSystem");
        Directory.CreateDirectory(directory);
        _globalPath = Path.Combine(directory, GlobalFileName);

        if (!File.Exists(_globalPath))
            File.WriteAllText(_globalPath, "{\r\n}\r\n");
    }

    internal static void BeginSession()
    {
        var combined = NewMap();
        MergeFile(combined, _globalPath, required: true);

        ExtensionInfo extension = ExtensionLoader.ActiveExtensionInfo;
        if (extension != null && !string.IsNullOrWhiteSpace(extension.FolderPath))
        {
            string extensionPath = Path.Combine(extension.FolderPath, ExtensionFileName);
            MergeFile(combined, extensionPath, required: false);
        }

        lock (WriteLock)
            Volatile.Write(ref _snapshot, combined);

        ExtraEventsPlugin.LogDebug($"Loaded {combined.Count} runtime text replacement(s).");
    }

    internal static void Clear()
    {
        lock (WriteLock)
            Volatile.Write(ref _snapshot, NewMap());
    }

    internal static string Resolve(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('#') < 0)
            return text;

        Dictionary<string, string> snapshot = Volatile.Read(ref _snapshot);
        return TokenRegex.Replace(text, match =>
            snapshot.TryGetValue(match.Value, out string value) ? value : match.Value);
    }

    internal static void Add(string token, string replacement)
    {
        ValidateEntry(token, replacement, "Replace action");

        lock (WriteLock)
        {
            Dictionary<string, string> current = Volatile.Read(ref _snapshot);
            if (current.TryGetValue(token, out string existing))
            {
                if (string.Equals(existing, replacement, StringComparison.Ordinal))
                    return;

                throw ExtraEventsPlugin.CatalogError(
                    $"Token '{token}' is already mapped to '{existing}'. " +
                    "Mappings cannot be reassigned after eager replacement.");
            }

            var next = new Dictionary<string, string>(current, StringComparer.Ordinal)
            {
                [token] = replacement
            };
            Volatile.Write(ref _snapshot, next);
        }
    }

    private static void MergeFile(Dictionary<string, string> target, string path, bool required)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            if (required)
                throw ExtraEventsPlugin.CatalogError($"Replacement file was not found: {path}");
            return;
        }

        Dictionary<string, string> loaded;
        try
        {
            string json = File.ReadAllText(path);
            loaded = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json)
                     ?? NewMap();
        }
        catch (Exception ex)
        {
            throw ExtraEventsPlugin.CatalogError($"Could not parse replacement JSON '{path}'.", ex);
        }

        foreach (KeyValuePair<string, string> pair in loaded)
        {
            ValidateEntry(pair.Key, pair.Value, path);
            target[pair.Key] = pair.Value;
        }
    }

    private static void ValidateEntry(string token, string replacement, string source)
    {
        if (string.IsNullOrEmpty(token))
            throw ExtraEventsPlugin.CatalogError($"{source}: replacement token cannot be empty.");

        Match match = TokenRegex.Match(token);
        if (!match.Success || match.Index != 0 || match.Length != token.Length)
        {
            throw ExtraEventsPlugin.CatalogError(
                $"{source}: invalid token '{token}'. Tokens must use the complete #...# format.");
        }

        if (replacement == null)
            throw ExtraEventsPlugin.CatalogError($"{source}: token '{token}' has a null value.");

        Match nested = TokenRegex.Match(replacement);
        if (nested.Success)
        {
            throw ExtraEventsPlugin.CatalogError(
                $"{source}: value for '{token}' contains token '{nested.Value}'. " +
                "Recursive replacements are forbidden.");
        }
    }

    private static Dictionary<string, string> NewMap() => new(StringComparer.Ordinal);
}
