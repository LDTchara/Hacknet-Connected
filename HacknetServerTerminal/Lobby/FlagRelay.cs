using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HNC.Flags;
using LiteNetLib;

namespace HacknetServerTerminal;

/// <summary>
/// 全局 flag（终端保存集合）：
/// - 任何玩家（客户端或服务端主机）报来的 FLAG_SET，集合有变化才转发给其他所有人；
///   每个人本机都在跑同一套剧情，同一个 flag 会被报好几次，集合去重后只转发一次；
/// - 新加入大厅的客户端、准备完毕的主机，收到 FLAG_ALL（当前所有全局 flag）；
/// - 按服务器保存到 Storage/Flags/{服务器名}.txt，终端重开后照样补发给新玩家。
/// </summary>
internal static class FlagRelay
{
    private const int HostId = 0;

    private static readonly object _lock = new();
    private static readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private static string _serverName = "";

    private static string FilePath => Path.Combine("Storage", "Flags", $"{_serverName}.txt");

    /// <summary>start 开放大厅时：读回该服务器的全局 flag</summary>
    public static void Open(string serverName)
    {
        lock (_lock)
        {
            _serverName = serverName;
            _flags.Clear();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        string f = FlagProtocol.FromB64(line.Trim());
                        if (f.Length > 0) _flags.Add(f);
                    }
                if (_flags.Count > 0) Console.WriteLine($"[flag] 读回 {_flags.Count} 个全局 flag");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[flag] 读取失败: {ex.Message}");
            }
        }
    }

    /// <summary>LiteNetLib 线程：客户端报来的 FLAG_SET</summary>
    public static void OnClientMessage(NetPeer peer, string msg)
    {
        var c = NetworkManager.GetClient(peer.Id);
        if (c is { Joined: true }) Handle(c.PlayerId, msg);
    }

    /// <summary>Pipe 读线程：主机报来的 FLAG_SET</summary>
    public static void OnModMessage(string msg) => Handle(HostId, msg);

    /// <summary>加入大厅的客户端：补发所有全局 flag</summary>
    public static void SendAllTo(NetPeer peer)
    {
        string all;
        lock (_lock) all = FlagProtocol.EncodeAll(_flags);
        NetworkManager.SendTo(peer, all);
    }

    /// <summary>服务端游戏准备完毕：补发所有全局 flag</summary>
    public static void SendAllToHost()
    {
        string all;
        lock (_lock) all = FlagProtocol.EncodeAll(_flags);
        if (Program.IsModConnected) _ = Program.SendToModFireForget(all);
    }

    private static void Handle(int fromPid, string msg)
    {
        if (!FlagProtocol.TryDecodeSet(msg, out bool add, out string flag)) return;

        lock (_lock)
        {
            bool changed = add ? _flags.Add(flag) : _flags.Remove(flag);
            if (!changed) return;
            Save();
        }

        string forward = FlagProtocol.EncodeSet(add, flag);
        foreach (var c in NetworkManager.GetAllClients())
            if (c.Joined && c.Peer != null && c.PlayerId != fromPid) NetworkManager.SendTo(c.Peer, forward);
        if (fromPid != HostId && Program.IsModConnected) _ = Program.SendToModFireForget(forward);

        Console.WriteLine($"[flag] {(add ? "+" : "-")}{flag}（来自 {(fromPid == HostId ? "服务端" : $"P{fromPid}")}）");
    }

    private static void Save()
    {
        if (string.IsNullOrEmpty(_serverName)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, _flags.Select(FlagProtocol.ToB64));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[flag] 保存失败: {ex.Message}");
        }
    }
}
