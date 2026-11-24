using HNC.Sync;
using Xunit;

namespace HNC.Sync.Tests;

public class SerializerTests
{
    private static T RoundTrip<T>(SerializerRegistry reg, T value)
    {
        var bytes = reg.ToBytes(value, typeof(T));
        return (T)reg.Read(new SyncReader(bytes), typeof(T))!;
    }

    [Fact]
    public void Primitives_roundtrip()
    {
        var reg = new SerializerRegistry();
        Assert.Equal(42, RoundTrip(reg, 42));
        Assert.Equal(long.MaxValue, RoundTrip(reg, long.MaxValue));
        Assert.Equal(1.5f, RoundTrip(reg, 1.5f));
        Assert.Equal(2.25, RoundTrip(reg, 2.25));
        Assert.True(RoundTrip(reg, true));
        Assert.Equal((short)-7, RoundTrip(reg, (short)-7));
        Assert.Equal((byte)200, RoundTrip(reg, (byte)200));
    }

    [Fact]
    public void String_null_and_empty_are_distinct()
    {
        var reg = new SerializerRegistry();
        Assert.Null(reg.Read(new SyncReader(reg.ToBytes(null, typeof(string))), typeof(string)));
        Assert.Equal("", RoundTrip(reg, ""));
        Assert.Equal("中文:with:colons", RoundTrip(reg, "中文:with:colons"));
    }

    [Fact]
    public void Enum_roundtrip()
    {
        Assert.Equal(Mood.Angry, RoundTrip(new SerializerRegistry(), Mood.Angry));
    }

    [Fact]
    public void Bytes_roundtrip_including_null()
    {
        var reg = new SerializerRegistry();
        Assert.Equal(new byte[] { 1, 2, 3 }, RoundTrip(reg, new byte[] { 1, 2, 3 }));
        Assert.Null(reg.Read(new SyncReader(reg.ToBytes(null, typeof(byte[]))), typeof(byte[])));
    }

    [Fact]
    public void List_and_dictionary_roundtrip()
    {
        var reg = new SerializerRegistry();
        Assert.Equal(new List<int> { 1, 2, 3 }, RoundTrip(reg, new List<int> { 1, 2, 3 }));
        var d = RoundTrip(reg, new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        Assert.Equal(2, d.Count);
        Assert.Equal(2, d["b"]);
    }

    [Fact]
    public void Nested_collections_roundtrip()
    {
        var reg = new SerializerRegistry();
        var v = new List<List<string>> { new() { "x" }, new() { "y", "z" } };
        var r = RoundTrip(reg, v);
        Assert.Equal("z", r[1][1]);
    }

    [Fact]
    public void Null_list_stays_null()
    {
        var reg = new SerializerRegistry();
        var bytes = reg.ToBytes(null, typeof(List<int>));
        Assert.Null(reg.Read(new SyncReader(bytes), typeof(List<int>)));
    }

    [Fact]
    public void Unsupported_types_are_rejected()
    {
        var reg = new SerializerRegistry();
        Assert.False(reg.CanSerialize(typeof(object)));
        Assert.False(reg.CanSerialize(typeof(List<object>)));
        Assert.False(reg.CanSerialize(typeof(IList<int>))); // 接口类型无法确定构造什么
        Assert.Throws<NotSupportedException>(() => reg.ToBytes(new object(), typeof(object)));
    }

    private sealed class PlainClassSerializer : ISyncValueSerializer
    {
        public bool CanHandle(Type t) { return t == typeof(PlainClass); }
        public void Write(ISyncWriter w, object value, Type t) { w.WriteInt(((PlainClass)value).X); }
        public object Read(ISyncReader r, Type t) { return new PlainClass { X = r.ReadInt() }; }
    }

    [Fact]
    public void Custom_serializer_extends_registry_and_nests_in_list()
    {
        var reg = new SerializerRegistry();
        Assert.False(reg.CanSerialize(typeof(PlainClass)));
        reg.Add(new PlainClassSerializer());
        Assert.True(reg.CanSerialize(typeof(PlainClass)));
        Assert.True(reg.CanSerialize(typeof(List<PlainClass>)));
        var r = RoundTrip(reg, new List<PlainClass> { new() { X = 9 } });
        Assert.Equal(9, r[0].X);
    }

    private sealed class DoubleIntSerializer : ISyncValueSerializer
    {
        public bool CanHandle(Type t) { return t == typeof(int); }
        public void Write(ISyncWriter w, object value, Type t) { w.WriteInt((int)value * 2); }
        public object Read(ISyncReader r, Type t) { return r.ReadInt(); }
    }

    [Fact]
    public void Custom_serializer_overrides_builtin()
    {
        var reg = new SerializerRegistry();
        reg.Add(new DoubleIntSerializer());
        Assert.Equal(20, RoundTrip(reg, 10));
    }
}
