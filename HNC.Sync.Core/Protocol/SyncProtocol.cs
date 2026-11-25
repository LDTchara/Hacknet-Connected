using System;
using System.Globalization;
using System.Text;

namespace HNC.Sync
{
    public enum SyncOp { Hello, Full, Delta, Propose, Req, Msg }

    public sealed class SyncMessage
    {
        public SyncOp Op;
        public SyncScope Scope;
        /// <summary>目标玩家；null 表示所有人。</summary>
        public string PlayerId;
        /// <summary>目标 key；Msg 时为通道名。</summary>
        public string Key;
        public int Version;
        public byte[] Payload;
    }

    /// <summary>
    /// 线格式（一行文本，字段用 ':' 分隔，Key 与 Payload 一律 base64，所以内容里的冒号无害）：
    ///   SYNC:{OP}:{scope 0/1/2}:{playerId|*}:{b64 key}:{version}:{b64 payload}
    /// 头部（OP/scope/playerId）足够终端做路由，无需解析 payload。
    /// </summary>
    public static class SyncProtocol
    {
        public const string Prefix = "SYNC:";
        public const string ProtocolVersion = "1";

        public static bool IsSyncMessage(string raw)
        {
            return raw != null && raw.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public static string Encode(SyncMessage m)
        {
            return Prefix + m.Op.ToString().ToUpperInvariant()
                 + ":" + ((int)m.Scope).ToString(CultureInfo.InvariantCulture)
                 + ":" + (string.IsNullOrEmpty(m.PlayerId) ? "*" : m.PlayerId)
                 + ":" + B64(Encoding.UTF8.GetBytes(m.Key ?? ""))
                 + ":" + m.Version.ToString(CultureInfo.InvariantCulture)
                 + ":" + B64(m.Payload ?? new byte[0]);
        }

        public static bool TryDecode(string raw, out SyncMessage msg)
        {
            msg = null;
            if (!IsSyncMessage(raw)) return false;
            var p = raw.Substring(Prefix.Length).Split(':');
            if (p.Length != 6) return false;
            try
            {
                SyncOp op;
                if (!Enum.TryParse(p[0], true, out op)) return false;
                int scope = int.Parse(p[1], CultureInfo.InvariantCulture);
                msg = new SyncMessage
                {
                    Op = op,
                    Scope = (SyncScope)scope,
                    PlayerId = p[2] == "*" ? null : p[2],
                    Key = Encoding.UTF8.GetString(Convert.FromBase64String(p[3])),
                    Version = int.Parse(p[4], CultureInfo.InvariantCulture),
                    Payload = Convert.FromBase64String(p[5]),
                };
                return true;
            }
            catch { msg = null; return false; }
        }

        /// <summary>终端路由专用：只读头部，不解 payload。</summary>
        public static bool TryReadRouting(string raw, out SyncOp op, out SyncScope scope, out string playerId)
        {
            op = SyncOp.Msg; scope = SyncScope.World; playerId = null;
            if (!IsSyncMessage(raw)) return false;
            var p = raw.Substring(Prefix.Length).Split(new[] { ':' }, 4);
            if (p.Length < 3) return false;
            int s;
            if (!Enum.TryParse(p[0], true, out op)) return false;
            if (!int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out s)) return false;
            scope = (SyncScope)s;
            playerId = p[2] == "*" ? null : p[2];
            return true;
        }

        private static string B64(byte[] b) { return Convert.ToBase64String(b); }
    }
}
