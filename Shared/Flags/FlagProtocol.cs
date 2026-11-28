using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HNC.Flags;

// flag 同步协议：终端（保存全局 flag 集合）、ServerPlugin、ClientPlugin 共用。纯 C#。
//
// 规则（FlagSync 判断）：
//   1. 剧情执行中（动作 Trigger、任务函数）增删的 flag → 全局：同步给所有人（双向，经终端）。
//   2. 带 Scope（players / group）的动作或任务函数增删的 flag → 只给那个玩家，不同步。
//   3. 引擎自己加的 flag（BootFailure、TutorialComplete、startVer:… 等，不在剧情执行中）→ 只在本机，不同步。
//
// 玩家（客户端 / 主机） → 终端：FLAG_SET:{1 增 | 0 删}:{b64 flag}
// 终端 → 其他所有人：       FLAG_SET:{1|0}:{b64 flag}       （全局集合有变化才转发）
// 终端 → 新加入的玩家 / 准备完毕的主机：
//                           FLAG_ALL:{b64 flag},{b64 flag},…   当前所有全局 flag（对方只补加缺的）

internal static class FlagProtocol
{
    public const string Prefix = "FLAG_";
    public const string Set = "FLAG_SET:";
    public const string All = "FLAG_ALL:";

    public static string ToB64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));

    public static string FromB64(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch { return ""; }
    }

    public static string EncodeSet(bool add, string flag) => $"{Set}{(add ? 1 : 0)}:{ToB64(flag)}";

    public static bool TryDecodeSet(string msg, out bool add, out string flag)
    {
        add = false;
        flag = "";
        if (!msg.StartsWith(Set, StringComparison.Ordinal)) return false;
        var p = msg.Substring(Set.Length).Split(new[] { ':' }, 2);
        if (p.Length < 2) return false;
        add = p[0] == "1";
        flag = FromB64(p[1]);
        return flag.Length > 0;
    }

    public static string EncodeAll(IEnumerable<string> flags) => All + string.Join(",", flags.Select(ToB64));

    public static List<string> DecodeAll(string msg) =>
        msg.Substring(All.Length).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
           .Select(FromB64).Where(f => f.Length > 0).ToList();
}
