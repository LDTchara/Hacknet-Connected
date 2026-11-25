using System;
using System.Collections;
using System.Collections.Generic;

namespace HNC.Sync
{
    /// <summary>
    /// 值序列化注册表。内置：bool/整数/浮点/string/byte[]/enum/List&lt;T&gt;/Dictionary&lt;K,V&gt;。
    /// 自定义序列化器优先（后注册的优先），因此 mod 可以覆盖或扩展任意类型。
    /// </summary>
    public sealed class SerializerRegistry
    {
        private readonly List<ISyncValueSerializer> _custom = new List<ISyncValueSerializer>();

        public void Add(ISyncValueSerializer s)
        {
            if (s != null) _custom.Insert(0, s);
        }

        public bool CanSerialize(Type t)
        {
            if (FindCustom(t) != null) return true;
            if (IsLeaf(t)) return true;
            if (TryList(t, out var e)) return CanSerialize(e);
            if (TryDict(t, out var k, out var v)) return CanSerialize(k) && CanSerialize(v);
            return false;
        }

        public void Write(ISyncWriter w, object value, Type t)
        {
            var c = FindCustom(t);
            if (c != null) { c.Write(w, value, t); return; }

            if (t == typeof(bool)) w.WriteBool((bool)value);
            else if (t == typeof(int)) w.WriteInt((int)value);
            else if (t == typeof(short)) w.WriteInt((short)value);
            else if (t == typeof(byte)) w.WriteInt((byte)value);
            else if (t == typeof(long)) w.WriteLong((long)value);
            else if (t == typeof(float)) w.WriteFloat((float)value);
            else if (t == typeof(double)) w.WriteDouble((double)value);
            else if (t == typeof(string)) w.WriteString((string)value);
            else if (t == typeof(byte[])) w.WriteBytes((byte[])value);
            else if (t.IsEnum) w.WriteLong(Convert.ToInt64(value));
            else if (TryList(t, out var et))
            {
                var list = (IList)value;
                if (list == null) { w.WriteInt(-1); return; }
                w.WriteInt(list.Count);
                foreach (var item in list) Write(w, item, et);
            }
            else if (TryDict(t, out var kt, out var vt))
            {
                var dict = (IDictionary)value;
                if (dict == null) { w.WriteInt(-1); return; }
                w.WriteInt(dict.Count);
                foreach (DictionaryEntry de in dict) { Write(w, de.Key, kt); Write(w, de.Value, vt); }
            }
            else throw new NotSupportedException("无序列化器: " + t.FullName);
        }

        public object Read(ISyncReader r, Type t)
        {
            var c = FindCustom(t);
            if (c != null) return c.Read(r, t);

            if (t == typeof(bool)) return r.ReadBool();
            if (t == typeof(int)) return r.ReadInt();
            if (t == typeof(short)) return (short)r.ReadInt();
            if (t == typeof(byte)) return (byte)r.ReadInt();
            if (t == typeof(long)) return r.ReadLong();
            if (t == typeof(float)) return r.ReadFloat();
            if (t == typeof(double)) return r.ReadDouble();
            if (t == typeof(string)) return r.ReadString();
            if (t == typeof(byte[])) return r.ReadBytes();
            if (t.IsEnum) return Enum.ToObject(t, r.ReadLong());
            if (TryList(t, out var et))
            {
                int n = r.ReadInt();
                if (n < 0) return null;
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(et));
                for (int i = 0; i < n; i++) list.Add(Read(r, et));
                return list;
            }
            if (TryDict(t, out var kt, out var vt))
            {
                int n = r.ReadInt();
                if (n < 0) return null;
                var dict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(kt, vt));
                for (int i = 0; i < n; i++) { var k = Read(r, kt); dict[k] = Read(r, vt); }
                return dict;
            }
            throw new NotSupportedException("无序列化器: " + t.FullName);
        }

        public byte[] ToBytes(object value, Type t)
        {
            var w = new SyncWriter();
            Write(w, value, t);
            return w.ToArray();
        }

        private ISyncValueSerializer FindCustom(Type t)
        {
            foreach (var s in _custom) if (s.CanHandle(t)) return s;
            return null;
        }

        private static bool IsLeaf(Type t)
        {
            return t == typeof(bool) || t == typeof(int) || t == typeof(short) || t == typeof(byte)
                || t == typeof(long) || t == typeof(float) || t == typeof(double)
                || t == typeof(string) || t == typeof(byte[]) || t.IsEnum;
        }

        // 只接受恰好是 List<T> / Dictionary<K,V> 的声明类型（接口类型无法确定要构造什么）
        private static bool TryList(Type t, out Type elem)
        {
            elem = null;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            { elem = t.GetGenericArguments()[0]; return true; }
            return false;
        }

        private static bool TryDict(Type t, out Type k, out Type v)
        {
            k = v = null;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            { var a = t.GetGenericArguments(); k = a[0]; v = a[1]; return true; }
            return false;
        }
    }
}
