using HNC.Chat;
using Xunit;

namespace HNC.Sync.Tests;

[Collection("ChatBus")] // ChatBus 是全局静态状态，这组测试不能并行
public class ChatBusTests
{
    public ChatBusTests()
    {
        ChatBus.Clear();
        ChatBus.MuteStore = new MemoryMuteStore();
    }

    [Fact]
    public void Publish_then_query_by_computer_and_player()
    {
        ChatBus.Publish("dhs", "alice", "hello", ChatSource.InputBox);
        ChatBus.Publish("dhs", "bob", "hi", ChatSource.InputBox);
        ChatBus.Publish("irc1", "alice", "elsewhere", ChatSource.InputBox);

        Assert.Equal(2, ChatBus.GetMessages("dhs").Count);
        var alice = ChatBus.GetMessages("DHS", "ALICE"); // 忽略大小写
        Assert.Equal("hello", Assert.Single(alice).Text);
        Assert.Equal("elsewhere", ChatBus.Last("irc1")!.Text);
        Assert.Null(ChatBus.Last("nowhere"));
    }

    [Fact]
    public void Messages_have_increasing_sequence_and_keep_attachments_and_source()
    {
        var a = ChatBus.Publish("dhs", "alice", "one", ChatSource.InputBox, new[] { "!ATTACHMENT:link#%#x#%#y" });
        var b = ChatBus.Publish("dhs", "alice", "[extra]", ChatSource.MissionResponse);
        Assert.True(b.Sequence > a.Sequence);
        Assert.Single(a.Attachments);
        Assert.Equal(ChatSource.MissionResponse, b.Source);
    }

    [Fact]
    public void Event_fires_and_a_throwing_subscriber_does_not_break_others_or_the_send()
    {
        var seen = new List<string>();
        Action<ChatMessage> bad = _ => throw new InvalidOperationException("boom");
        Action<ChatMessage> good = m => seen.Add(m.Text);
        ChatBus.MessageSent += bad;
        ChatBus.MessageSent += good;
        try
        {
            ChatBus.Publish("dhs", "alice", "x", ChatSource.InputBox);
            Assert.Equal(new[] { "x" }, seen);
            Assert.Single(ChatBus.GetMessages("dhs"));
        }
        finally
        {
            ChatBus.MessageSent -= bad;
            ChatBus.MessageSent -= good;
        }
    }

    [Fact]
    public void History_is_capped()
    {
        for (int i = 0; i < ChatBus.MaxHistory + 50; i++)
            ChatBus.Publish("dhs", "alice", "m" + i, ChatSource.InputBox);
        var all = ChatBus.GetMessages("dhs");
        Assert.Equal(ChatBus.MaxHistory, all.Count);
        Assert.Equal("m50", all[0].Text); // 最旧的被丢掉
    }

    [Theory]
    [InlineData("fragment", ChatMatchMode.Fragment)]
    [InlineData("Contains", ChatMatchMode.Fragment)]
    [InlineData("片段", ChatMatchMode.Fragment)]
    [InlineData("", ChatMatchMode.Fragment)]
    [InlineData(null, ChatMatchMode.Fragment)]
    [InlineData("EXACT", ChatMatchMode.Exact)]
    [InlineData("完全", ChatMatchMode.Exact)]
    [InlineData("NotContains", ChatMatchMode.NotContains)]
    [InlineData("不包含", ChatMatchMode.NotContains)]
    public void Match_mode_parsing_accepts_english_and_chinese_names(string? s, ChatMatchMode expected)
    {
        Assert.True(ChatMatcher.TryParseMode(s!, out var mode));
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void Unknown_match_mode_is_rejected()
    {
        Assert.False(ChatMatcher.TryParseMode("regex", out _));
    }

    [Fact]
    public void Matching_modes_behave_per_message()
    {
        Assert.True(ChatMatcher.Matches("the password is 123", "password", ChatMatchMode.Fragment, true));
        Assert.False(ChatMatcher.Matches("hello", "password", ChatMatchMode.Fragment, true));

        Assert.True(ChatMatcher.Matches("  Done ", "done", ChatMatchMode.Exact, true));     // 忽略首尾空格和大小写
        Assert.False(ChatMatcher.Matches("done!", "done", ChatMatchMode.Exact, true));
        Assert.False(ChatMatcher.Matches("Done", "done", ChatMatchMode.Exact, false));      // 区分大小写时不同

        Assert.True(ChatMatcher.Matches("hello", "password", ChatMatchMode.NotContains, true));
        Assert.False(ChatMatcher.Matches("my PASSWORD", "password", ChatMatchMode.NotContains, true));
    }

    [Fact]
    public void HasSent_checks_any_message_by_that_player_on_that_computer()
    {
        ChatBus.Publish("dhs", "alice", "I found the key", ChatSource.InputBox);
        ChatBus.Publish("dhs", "bob", "nothing", ChatSource.InputBox);

        Assert.True(ChatBus.HasSent("dhs", "alice", "key", ChatMatchMode.Fragment, true));
        Assert.False(ChatBus.HasSent("dhs", "bob", "key", ChatMatchMode.Fragment, true));
        Assert.False(ChatBus.HasSent("irc1", "alice", "key", ChatMatchMode.Fragment, true));
        // 不包含 = 存在至少一条不含这段文字的消息
        Assert.True(ChatBus.HasSent("dhs", "bob", "key", ChatMatchMode.NotContains, true));
        Assert.False(ChatBus.HasSent("dhs", "alice", "key", ChatMatchMode.NotContains, true));
    }

    [Fact]
    public void Mute_is_per_computer_and_player_and_star_means_everyone()
    {
        ChatBus.SetMuted("dhs", "alice", true);
        Assert.True(ChatBus.IsMuted("dhs", "alice"));
        Assert.False(ChatBus.IsMuted("dhs", "bob"));
        Assert.False(ChatBus.IsMuted("irc1", "alice"));

        ChatBus.SetMuted("irc1", ChatBus.Everyone, true);
        Assert.True(ChatBus.IsMuted("irc1", "anyone"));

        ChatBus.SetMuted("dhs", "alice", false);
        Assert.False(ChatBus.IsMuted("dhs", "alice"));
    }
}
