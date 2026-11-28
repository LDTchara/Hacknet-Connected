using System;
using System.Text;

namespace HNC.NodeSync;

// 节点（端口 / 代理 / 防火墙）同步协议：终端、ServerPlugin、ClientPlugin 共用。纯 C#。
//
// 默认：节点的端口、代理、防火墙状态只在各自本机有效（每人自己破解）。
// 节点 XML 里加 <HNCShared ports="true" proxy="true" firewall="true"/> 后，对应项全局共享，服务端游戏是权威：
//
// 客户端 → 终端 → 服务端游戏（终端原样转发）
//   NODE_RPT:{b64 节点id}:P:{code 端口号}      本机打开了一个端口
//   NODE_RPT:{b64 节点id}:F:{次数}             本机跑了几次防火墙分析（analyze）
//   NODE_RPT:{b64 节点id}:S:1                  本机破解了防火墙（solve）
//   NODE_RPT:{b64 节点id}:X:{量}               本机的 Shell 对代理造成的过载量（这段时间累计，各人叠加）
//   NODE_RPT:{b64 节点id 或空}:C:1             本机现在连着哪台共享节点（空 = 没连共享节点）
//   （终端转发时在末尾追加 :{发送者 playerId}）
// 终端 → 服务端游戏
//   NODE_GONE:{playerId}                       该客户端断线，清除它的连接状态
// 共享节点只要还有玩家连着（主机或任何客户端），管理员就不重置；最后一个人离开时服务端执行一次重置。
// 服务端游戏 → 终端 → 所有已加入的客户端
//   NODE_STATE:{b64 节点id}:{端口 0/1 串}:{分析次数}:{已破解 0/1}:{代理剩余}:{代理开启 0/1}
//                                             共享节点的权威状态（有变化才发；有玩家加入时全部重发）
// 分享（不改变节点是否共享，只把自己已破解的状态给别人）
//   客户端 → 终端：NODE_SHARE:{b64 目标}:{b64 节点id}:{b64 IP}:{打开的端口号;分隔}:{代理已打掉 0/1}:{防火墙已破 0/1}
//                  目标 = all / 玩家名 / P编号
//   终端 → 目标：  NODE_SHARED:{发送者id}:{b64 发送者名}:{b64 节点id}:{b64 IP}:{端口}:{代理}:{防火墙}
//   终端 → 发送者：NODE_MSG:{b64 文字}

internal static class NodeProtocol
{
    public const string Prefix = "NODE_";
    public const string Report = "NODE_RPT:";
    public const string State = "NODE_STATE:";
    public const string Share = "NODE_SHARE:";
    public const string Shared = "NODE_SHARED:";
    public const string Msg = "NODE_MSG:";
    public const string Gone = "NODE_GONE:";

    public static string ToB64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));

    public static string FromB64(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch { return ""; }
    }
}
