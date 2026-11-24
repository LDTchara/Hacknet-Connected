using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace HNC.RemovableTags;

/// <summary>标签字符串的解析与匹配。刻意不依赖游戏，单元测试直接编译这个文件。</summary>
internal static class TagList
{
    /// <summary>XML 里标记"可被移除"的属性名。</summary>
    public const string AttributeName = "RemoveableTag";

    /// <summary>"a, b ,,c" → [a, b, c]。</summary>
    public static string[] Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
        return s!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(x => x.Trim())
                 .Where(x => x.Length > 0)
                 .ToArray();
    }

    /// <summary>元素上的标签（可以是逗号分隔的多个）和要移除的标签有交集（忽略大小写）。</summary>
    public static bool Intersects(string? elementTags, string[] removeTags)
    {
        if (removeTags.Length == 0) return false;
        foreach (string t in Parse(elementTags))
            foreach (string r in removeTags)
                if (string.Equals(t, r, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// 从一段 XML 文本的根元素上读一个属性。延时队列里的动作是加密后的 XML 文本，
    /// 不需要把它反序列化成对象，读根元素属性就够判断有没有标签了。读不出来返回 null。
    /// </summary>
    public static string? ExtractRootAttribute(string xml, string attribute)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = true };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                    return reader.GetAttribute(attribute);
            }
        }
        catch (XmlException)
        {
            // 格式不对的当作没有标签
        }
        return null;
    }
}
