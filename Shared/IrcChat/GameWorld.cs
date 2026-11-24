using System.Collections.Generic;
using Hacknet;
using HNC.Chat;

namespace HNC.IrcChat;

/// <summary>把游戏里的节点和 IRC 用户名提供给 HNC.Chat 的解析器与补全。</summary>
internal sealed class GameWorld : IChatWorld, ICompletionSource
{
    private readonly OS _os;
    private readonly IEnumerable<string> _users;

    public GameWorld(OS os, IEnumerable<string> users)
    {
        _os = os;
        _users = users;
    }

    public NodeRef? FindNode(string ip, out bool visible)
    {
        visible = false;
        var nodes = _os.netMap?.nodes;
        if (nodes == null) return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            var c = nodes[i];
            if (c == null || c.ip != ip) continue;
            visible = _os.netMap.visibleNodes.Contains(i);
            return ToRef(c);
        }
        return null;
    }

    public IEnumerable<string> UserNames() => _users;

    public IEnumerable<KeyValuePair<string, string>> VisibleNodes()
    {
        var map = _os.netMap;
        if (map?.nodes == null || map.visibleNodes == null) yield break;

        foreach (int idx in map.visibleNodes.ToArray())
        {
            if (idx < 0 || idx >= map.nodes.Count) continue;
            var c = map.nodes[idx];
            if (c != null) yield return new KeyValuePair<string, string>(c.ip, c.name);
        }
    }

    private static NodeRef ToRef(Computer c)
    {
        var n = new NodeRef { Ip = c.ip, Name = c.name, Id = c.idName };
        if (c.users != null)
        {
            foreach (var u in c.users)
            {
                n.Accounts.Add(new AccountRef
                {
                    User = u.name,
                    Password = u.pass,
                    Known = u.known,
                    IsAdmin = u.type == 0, // 原版：type 0 才是 admin
                });
            }
        }
        return n;
    }
}
