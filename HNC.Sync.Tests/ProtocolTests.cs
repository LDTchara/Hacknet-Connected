using HNC.Sync;
using Xunit;

namespace HNC.Sync.Tests;

public class ProtocolTests
{
    [Fact]
    public void Encode_decode_roundtrip_with_colons_in_key()
    {
        var m = new SyncMessage
        {
            Op = SyncOp.Delta, Scope = SyncScope.World, PlayerId = null,
            Key = "daemon:node:Name:field", Version = 7, Payload = new byte[] { 1, 2, 3, 255 },
        };
        var raw = SyncProtocol.Encode(m);
        Assert.StartsWith("SYNC:DELTA:0:*:", raw);

        Assert.True(SyncProtocol.TryDecode(raw, out var d));
        Assert.Equal(SyncOp.Delta, d.Op);
        Assert.Equal("daemon:node:Name:field", d.Key);
        Assert.Equal(7, d.Version);
        Assert.Null(d.PlayerId);
        Assert.Equal(new byte[] { 1, 2, 3, 255 }, d.Payload);
    }

    [Fact]
    public void Player_target_survives_roundtrip()
    {
        var raw = SyncProtocol.Encode(new SyncMessage { Op = SyncOp.Full, Scope = SyncScope.Player, PlayerId = "p2", Key = "k" });
        Assert.True(SyncProtocol.TryDecode(raw, out var d));
        Assert.Equal("p2", d.PlayerId);
        Assert.Equal(SyncScope.Player, d.Scope);
        Assert.Empty(d.Payload);
    }

    [Fact]
    public void Routing_header_can_be_read_without_payload()
    {
        var raw = SyncProtocol.Encode(new SyncMessage
        {
            Op = SyncOp.Msg, Scope = SyncScope.Player, PlayerId = "p3", Key = "chan", Payload = new byte[1000],
        });
        Assert.True(SyncProtocol.TryReadRouting(raw, out var op, out var scope, out var player));
        Assert.Equal(SyncOp.Msg, op);
        Assert.Equal(SyncScope.Player, scope);
        Assert.Equal("p3", player);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EVENT:FILE:x")]
    [InlineData("SYNC:")]
    [InlineData("SYNC:DELTA:0:*:a")]
    [InlineData("SYNC:BOGUS:0:*:a:1:b")]
    [InlineData("SYNC:DELTA:x:*:a:1:b")]
    [InlineData("SYNC:DELTA:0:*:!!!notbase64:1:b")]
    public void Garbage_is_rejected_not_thrown(string? raw)
    {
        Assert.False(SyncProtocol.TryDecode(raw!, out var m));
        Assert.Null(m);
    }

    [Fact]
    public void IsSyncMessage_does_not_match_event_messages()
    {
        Assert.True(SyncProtocol.IsSyncMessage("SYNC:HELLO:0:*:a:0:b"));
        Assert.False(SyncProtocol.IsSyncMessage("EVENT:PORT:a:b"));
        Assert.False(SyncProtocol.IsSyncMessage(null!));
    }
}
