using System.Linq;
using Hacknet;
using HarmonyLib;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

public enum FileSystemAction
{
    FileAdded,
    FileRemoved,
    FolderAdded,
    FolderRemoved,
    FileContentChanged,
    FileMoved       // 仅来自 log（有 FromName/ToName）
}

[EventAutoSubscribe]
public class PlayerFileSystemEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public FileSystemAction Action { get; }
    public string Path { get; }
    public string SourceIP { get; }

    // 仅 FileMoved 时有效
    public string FromName { get; }     // 原名
    public string ToName { get; }       // 新名

    // 文件内容（FileAdded / FileContentChanged 时填充）
    public string Content { get; }

    public PlayerFileSystemEvent(Computer comp, OS os, FileSystemAction action, string path, string sourceIP)
        : this(comp, os, action, path, sourceIP, null, null, null) { }

    public PlayerFileSystemEvent(Computer comp, OS os, FileSystemAction action, string path, string sourceIP, string fromName, string toName)
        : this(comp, os, action, path, sourceIP, fromName, toName, null) { }

    public PlayerFileSystemEvent(Computer comp, OS os, FileSystemAction action, string path, string sourceIP, string fromName, string toName, string content)
    {
        Computer = comp;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        Action = action;
        Path = path;
        SourceIP = sourceIP;
        FromName = fromName;
        ToName = toName;
        Content = content;
    }
}

// ================================================================
// 1. log 拦截 —— 记录最近的操作日志缓存
// ================================================================
internal static class FileLogInterceptor
{
    private struct LogEntry
    {
        public string ComputerID;   // idName
        public string Action;
        public string IPFrom;
        public string Name;
        public string NewName;      // 仅 move
        public float GameTime;
    }

    private static readonly Queue<LogEntry> _recentLogs = new();

    public static string TryGetIP(string compID, string actionPrefix, string fileName)
    {
        foreach (var entry in _recentLogs)
        {
            if (entry.ComputerID == compID && entry.Action == actionPrefix && entry.Name == fileName)
                return entry.IPFrom;
        }
        return null;
    }

    public static string TryGetMoveInfo(string compID, out string fromName, out string toName)
    {
        foreach (var entry in _recentLogs)
        {
            if (entry.ComputerID == compID && entry.Action == "FileMoved")
            {
                fromName = entry.Name;
                toName = entry.NewName;
                return entry.IPFrom;
            }
        }
        fromName = null;
        toName = null;
        return null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Computer), nameof(Computer.log))]
    private static void OnLogPostfix(Computer __instance, string message)
    {
        string[] parts = message.Split(' ');
        if (parts.Length < 2) return;
        string action = parts[1]; // "FileCreated:", "FileDeleted:", "FileMoved:", "FolderCreated:", "FileCopied:"

        if (action != "FileCreated:" && action != "FileDeleted:" && action != "FileMoved:" && action != "FolderCreated:" && action != "FileCopied:")
            return;

        var entry = new LogEntry
        {
            ComputerID = __instance.idName,
            Action = action.TrimEnd(':'),
            IPFrom = ParseIPFrom(parts),
            Name = ParseFileName(parts),
            GameTime = (float)OS.currentElapsedTime
        };

        // 提取 NewName（仅 move）
        if (action == "FileMoved:")
        {
            for (int i = 0; i < parts.Length - 2; i++)
            {
                if (parts[i] == "To:")
                {
                    entry.NewName = parts[i + 1];
                    break;
                }
            }
        }

        _recentLogs.Enqueue(entry);
        while (_recentLogs.Count > 200)
            _recentLogs.Dequeue();
    }

    private static string ParseIPFrom(string[] parts)
    {
        // "FileCreated: by {ipFrom} - file:{name}"
        for (int i = 0; i < parts.Length - 1; i++)
            if (parts[i] == "by") return parts[i + 1];
        return null;
    }

    private static string ParseFileName(string[] parts)
    {
        // "file:{name}" 或 "folder:{name}"
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("file:") || parts[i].StartsWith("folder:"))
                return parts[i].Substring(parts[i].IndexOf(':') + 1);
        }
        return null;
    }
}

// ================================================================
// 2. 帧检测 —— 对比文件树变化，结合 log 缓存补充 ipFrom
// ================================================================
internal static class FileSystemDetector
{
    private static string _lastConnIP;
    private static Dictionary<string, int> _lastConnSnapshot = new();
    private static string _lastLocalIP;
    private static Dictionary<string, int> _lastLocalSnapshot = new();

    [FrameDetect]
    internal static void Detect(OS os)
    {
        DetectComputer(os.connectedComp, os, ref _lastConnIP, ref _lastConnSnapshot);
        DetectComputer(os.thisComputer, os, ref _lastLocalIP, ref _lastLocalSnapshot);
    }

    private static void DetectComputer(Computer comp, OS os, ref string lastIP, ref Dictionary<string, int> lastSnapshot)
    {
        if (comp == null) { lastIP = null; return; }

        if (comp.ip != lastIP)
        {
            lastSnapshot = BuildSnapshot(comp);
            lastIP = comp.ip;
            return;
        }

        var current = BuildSnapshot(comp);
        string compID = comp.idName;

        foreach (var kv in current)
        {
            if (!lastSnapshot.TryGetValue(kv.Key, out int lastLen))
            {
                bool isFolder = kv.Value == -1;
                string fileName = Path.GetFileName(kv.Key);
                string ip = FileLogInterceptor.TryGetIP(compID, isFolder ? "FolderCreated" : "FileCreated", fileName)
                            ?? os.thisComputer.ip;
                // 文件添加时读取内容
                string content = isFolder ? null : GetFileContent(comp, kv.Key);
                Console.WriteLine("[FSEVENT] " + (isFolder ? "FolderAdded" : "FileAdded") + " " + compID + " " + kv.Key);
                EventManager<PlayerFileSystemEvent>.InvokeAll(
                    new PlayerFileSystemEvent(comp, os,
                        isFolder ? FileSystemAction.FolderAdded : FileSystemAction.FileAdded, kv.Key, ip, null, null, content));
            }
            else if (lastLen != kv.Value && kv.Value != -1 && lastLen != -1)
            {
                string fileName = Path.GetFileName(kv.Key);
                string ip = FileLogInterceptor.TryGetIP(compID, "FileCreated", fileName) ?? os.thisComputer.ip;
                // 文件内容变更时读取新内容
                string content = GetFileContent(comp, kv.Key);
                Console.WriteLine("[FSEVENT] FileContentChanged " + compID + " " + kv.Key);
                EventManager<PlayerFileSystemEvent>.InvokeAll(
                    new PlayerFileSystemEvent(comp, os, FileSystemAction.FileContentChanged, kv.Key, ip, null, null, content));
            }
        }

        foreach (var kv in lastSnapshot)
        {
            if (!current.ContainsKey(kv.Key))
            {
                bool isFolder = kv.Value == -1;
                string fileName = Path.GetFileName(kv.Key);
                string ip = FileLogInterceptor.TryGetIP(compID, isFolder ? "FolderCreated" : "FileDeleted", fileName)
                            ?? os.thisComputer.ip;

                // 尝试匹配 move
                if (!isFolder)
                {
                    string fromName, toName;
                    string moveIP = FileLogInterceptor.TryGetMoveInfo(compID, out fromName, out toName);
                    if (moveIP != null && fromName == fileName)
                    {
                        EventManager<PlayerFileSystemEvent>.InvokeAll(
                            new PlayerFileSystemEvent(comp, os, FileSystemAction.FileMoved, kv.Key, moveIP, fromName, toName));
                        lastSnapshot = current;
                        return;
                    }
                }

                Console.WriteLine("[FSEVENT] " + (isFolder ? "FolderRemoved" : "FileRemoved") + " " + compID + " " + kv.Key);
                EventManager<PlayerFileSystemEvent>.InvokeAll(
                    new PlayerFileSystemEvent(comp, os,
                        isFolder ? FileSystemAction.FolderRemoved : FileSystemAction.FileRemoved, kv.Key, ip));
            }
        }

        lastSnapshot = current;
    }

    private static Dictionary<string, int> BuildSnapshot(Computer comp)
    {
        var snap = new Dictionary<string, int>();
        WalkFolder(comp.files.root, "", snap);
        return snap;
    }

    private static void WalkFolder(Folder folder, string prefix, Dictionary<string, int> snap)
    {
        if (folder == null) return;
        foreach (var f in folder.folders)
        {
            snap[$"{prefix}/{f.name}"] = -1;
            WalkFolder(f, $"{prefix}/{f.name}", snap);
        }
        foreach (var f in folder.files)
        {
            snap[$"{prefix}/{f.name}"] = f.data?.Length ?? 0;
        }
    }

    /// <summary>从文件树中读取指定路径的文件内容</summary>
    internal static string GetFileContent(Computer comp, string fullPath)
    {
        if (comp?.files?.root == null) return null;
        var parts = fullPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        Folder current = comp.files.root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            current = current.folders?.FirstOrDefault(f => f.name == parts[i]);
            if (current == null) return null;
        }
        var file = current.files?.FirstOrDefault(f => f.name == parts.Last());
        return file?.data;
    }
}
