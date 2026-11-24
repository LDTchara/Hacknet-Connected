using System;

namespace HNC.Sync
{
    public interface ISyncWriter
    {
        void WriteBool(bool v);
        void WriteInt(int v);
        void WriteLong(long v);
        void WriteFloat(float v);
        void WriteDouble(double v);
        /// <summary>null 与空串可区分。</summary>
        void WriteString(string v);
        void WriteBytes(byte[] v);
    }

    public interface ISyncReader
    {
        bool ReadBool();
        int ReadInt();
        long ReadLong();
        float ReadFloat();
        double ReadDouble();
        string ReadString();
        byte[] ReadBytes();
    }

    /// <summary>
    /// 值序列化器。非泛型以便注册表按 <see cref="Type"/> 查找；
    /// 复合类型（List/Dictionary）由 Core 通过递归查找元素序列化器构造。
    /// </summary>
    public interface ISyncValueSerializer
    {
        bool CanHandle(Type type);
        void Write(ISyncWriter w, object value, Type type);
        object Read(ISyncReader r, Type type);
    }
}
