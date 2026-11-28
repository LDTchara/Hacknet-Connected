using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;

namespace HNC.NodeSync;

/// <summary>节点哪些项全局共享</summary>
internal sealed class SharedFlags
{
    public bool Ports;
    public bool Proxy;
    public bool Firewall;

    public bool Any => Ports || Proxy || Firewall;
}

/// <summary>
/// 共享节点配置：扫描扩展 Nodes 目录里所有 &lt;Computer&gt;，找带 &lt;HNCShared .../&gt; 的。
///
///   &lt;Computer id="bankMain" …&gt;
///     &lt;HNCShared /&gt;                                         端口、代理、防火墙全部共享
///     &lt;HNCShared ports="true" proxy="false" firewall="true" /&gt;   只共享部分（没写的属性 = true）
///   &lt;/Computer&gt;
///
/// 为什么扫描文件而不是在加载节点时读：读档时电脑是从存档恢复的，不会再读节点 XML 里的这个标签。
/// 扫描文件对新游戏和旧存档结果一样；作者改了节点文件，旧存档也生效。
/// 限制：只认 Nodes 目录下文件里的节点；用动作动态添加的节点不认。
/// Pathfinder 遇到不认识的 &lt;Computer&gt; 子元素会直接忽略，所以这个标签不需要注册。
/// </summary>
internal static class SharedNodeConfig
{
    private static Dictionary<string, SharedFlags>? _cache;
    private static string _cacheFolder = "";

    /// <summary>节点 id（Computer 的 id）→ 共享项</summary>
    public static Dictionary<string, SharedFlags> Get()
    {
        string folder = ExtensionLoader.ActiveExtensionInfo?.GetFullFolderPath() ?? "";
        if (_cache != null && folder == _cacheFolder) return _cache;

        _cacheFolder = folder;
        _cache = new Dictionary<string, SharedFlags>(StringComparer.Ordinal);
        string nodes = Path.Combine(folder, "Nodes");
        if (folder.Length == 0 || !Directory.Exists(nodes)) return _cache;

        foreach (var file in Directory.GetFiles(nodes, "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                var root = XDocument.Load(file).Root;
                if (root == null || !root.Name.LocalName.Equals("Computer", StringComparison.OrdinalIgnoreCase)) continue;
                var tag = root.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HNCShared", StringComparison.OrdinalIgnoreCase));
                string? id = (string?)root.Attribute("id");
                if (tag == null || string.IsNullOrEmpty(id)) continue;

                var flags = new SharedFlags
                {
                    Ports = Flag(tag, "ports"),
                    Proxy = Flag(tag, "proxy"),
                    Firewall = Flag(tag, "firewall")
                };
                if (flags.Any) _cache[id!] = flags;
            }
            catch
            {
                // 节点文件不是合法 XML（Hacknet 本身也宽松）：跳过，不影响加载
            }
        }
        Console.WriteLine($"[NodeSync] 共享节点 {_cache.Count} 个" +
                          (_cache.Count > 0 ? ": " + string.Join(", ", _cache.Keys) : ""));
        return _cache;
    }

    public static void Invalidate() => _cache = null;

    private static bool Flag(XElement tag, string name)
    {
        var v = (string?)tag.Attribute(name);
        if (v == null) return true;
        v = v.Trim();
        return !(v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "0" || v.Equals("no", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>共享节点的状态：编码 / 解码 / 应用，以及几个游戏对象的小工具</summary>
internal static class NodeState
{
    /// <summary>Firewall.analysisPasses 是私有字段</summary>
    private static readonly AccessTools.FieldRef<Firewall, int> PassesRef =
        AccessTools.FieldRefAccess<Firewall, int>("analysisPasses");

    public static int GetPasses(Firewall fw) => PassesRef(fw);
    public static void SetPasses(Firewall fw, int value) => PassesRef(fw) = value;

    public static Computer? Find(OS os, string id, string? ip = null)
    {
        var nodes = os.netMap?.nodes;
        if (nodes == null) return null;
        return nodes.FirstOrDefault(c => c.idName == id)
            ?? (string.IsNullOrEmpty(ip) ? null : nodes.FirstOrDefault(c => c.ip == ip));
    }

    public static string PortsString(Computer c)
    {
        var sb = new StringBuilder();
        if (c.portsOpen != null)
            foreach (var b in c.portsOpen) sb.Append(b != 0 ? '1' : '0');
        return sb.ToString();
    }

    /// <summary>NODE_STATE:{b64 id}:{端口}:{分析次数}:{已破解}:{代理剩余}:{代理开启}</summary>
    public static string Encode(string id, Computer c)
    {
        var fw = c.firewall;
        return NodeProtocol.State + string.Join(":",
            NodeProtocol.ToB64(id),
            PortsString(c),
            fw != null ? GetPasses(fw).ToString(CultureInfo.InvariantCulture) : "-1",
            fw?.solved == true ? "1" : "0",
            c.proxyOverloadTicks.ToString("R", CultureInfo.InvariantCulture),
            c.proxyActive ? "1" : "0");
    }

    public sealed class Decoded
    {
        public string Id = "";
        public string Ports = "";
        public int Passes = -1;
        public bool Solved;
        public float Ticks;
        public bool ProxyActive;
    }

    public static Decoded? Decode(string msg)
    {
        if (!msg.StartsWith(NodeProtocol.State, StringComparison.Ordinal)) return null;
        var p = msg.Substring(NodeProtocol.State.Length).Split(':');
        if (p.Length < 6) return null;
        var d = new Decoded { Id = NodeProtocol.FromB64(p[0]), Ports = p[1] };
        int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.Passes);
        d.Solved = p[3] == "1";
        float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out d.Ticks);
        d.ProxyActive = p[5] == "1";
        return d;
    }

    /// <summary>把权威状态写到本机电脑上（只写共享的项）</summary>
    public static void Apply(Computer c, SharedFlags flags, Decoded s)
    {
        if (flags.Ports && c.portsOpen != null)
        {
            for (int i = 0; i < c.portsOpen.Count && i < s.Ports.Length; i++)
                c.portsOpen[i] = (byte)(s.Ports[i] == '1' ? 1 : 0);
        }
        if (flags.Firewall && c.firewall != null && s.Passes >= 0)
        {
            SetPasses(c.firewall, s.Passes);
            c.firewall.solved = s.Solved;
        }
        if (flags.Proxy && c.hasProxy)
        {
            c.proxyOverloadTicks = s.Ticks;
            c.proxyActive = s.ProxyActive;
        }
    }
}

/// <summary>
/// 分享破解进度（share 命令）的两端共用逻辑：组装"我在这个节点上已破解了什么"，以及把收到的分享应用到本机。
/// 共享节点上本来就共享的项不参与分享。服务端主机和客户端都用这一份。
///
/// 载荷：{b64 节点id}:{b64 IP}:{打开的端口号;分隔}:{代理已打掉 0/1}:{防火墙已破 0/1}
/// </summary>
internal static class NodeShare
{
    public const string Usage = "用法: share <IP|节点id> [玩家名|P编号|all] [ports|proxy|firewall|all]";

    /// <summary>按 IP 或节点 id 找本机已发现的节点</summary>
    public static Computer? FindDiscovered(OS os, string token)
    {
        var c = NodeState.Find(os, token, token);
        if (c == null) return null;
        int idx = os.netMap.nodes.IndexOf(c);
        return os.netMap.visibleNodes.Contains(idx) ? c : null;
    }

    /// <summary>组装分享载荷；没有可分享的返回 null，reason 说明原因</summary>
    public static string? BuildPayload(Computer c, string what, out string reason)
    {
        what = (what ?? "all").ToLowerInvariant();
        bool wantPorts = what is "all" or "ports" or "port";
        bool wantProxy = what is "all" or "proxy";
        bool wantFw = what is "all" or "firewall" or "fw";

        SharedNodeConfig.Get().TryGetValue(c.idName, out var shared);

        var ports = new List<int>();
        if (wantPorts && shared?.Ports != true && c.portsOpen != null && c.ports != null)
            for (int i = 0; i < c.portsOpen.Count && i < c.ports.Count; i++)
                if (c.portsOpen[i] != 0) ports.Add(c.ports[i]);
        bool proxyDown = wantProxy && shared?.Proxy != true && c.hasProxy && !c.proxyActive;
        bool fwSolved = wantFw && shared?.Firewall != true && c.firewall?.solved == true;

        if (ports.Count == 0 && !proxyDown && !fwSolved)
        {
            reason = shared != null && shared.Any
                ? "[分享] 这是共享节点，相应的破解进度本来就是所有人共享的；你在其它项上也还没有可分享的进展。"
                : "[分享] 你在这个节点上还没有可分享的进展（没开端口、代理没打掉、防火墙没破）。";
            return null;
        }
        reason = "";
        return string.Join(":",
            NodeProtocol.ToB64(c.idName), NodeProtocol.ToB64(c.ip),
            string.Join(";", ports), proxyDown ? "1" : "0", fwSolved ? "1" : "0");
    }

    /// <summary>
    /// 应用收到的分享（NODE_SHARED 去掉前缀后的部分：{发送者id}:{b64 名}:{载荷}），
    /// 让节点出现在地图上，并在游戏终端里提示。返回是否应用了任何东西。
    /// </summary>
    public static bool Receive(OS os, string sharedPayload)
    {
        var p = sharedPayload.Split(':');
        if (p.Length < 7) return false;

        string from = NodeProtocol.FromB64(p[1]);
        var c = NodeState.Find(os, NodeProtocol.FromB64(p[2]), NodeProtocol.FromB64(p[3]));
        if (c == null) return false;
        SharedNodeConfig.Get().TryGetValue(c.idName, out var shared);

        var got = new List<string>();
        var codes = p[4].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out int v) ? v : -1).Where(v => v >= 0).ToList();
        if (shared?.Ports != true && codes.Count > 0 && c.ports != null && c.portsOpen != null)
        {
            foreach (int code in codes)
            {
                int idx = c.ports.IndexOf(code);
                if (idx >= 0 && idx < c.portsOpen.Count) c.portsOpen[idx] = 1;
            }
            got.Add("端口 " + string.Join(",", codes.Select(c.GetDisplayPortNumberFromCodePort)));
        }
        if (shared?.Proxy != true && p[5] == "1" && c.hasProxy)
        {
            c.proxyOverloadTicks = 0f;
            c.proxyActive = false;
            got.Add("代理");
        }
        if (shared?.Firewall != true && p[6] == "1" && c.firewall != null)
        {
            c.firewall.solved = true;
            got.Add("防火墙");
        }
        if (got.Count == 0) return false;

        os.netMap.discoverNode(c);
        os.write($"[分享] {from} 分享了 {c.name} ({c.ip}) 的破解进度：{string.Join("、", got)}");
        return true;
    }
}
