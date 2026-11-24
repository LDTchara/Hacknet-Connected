using HNC.Chat;
using Xunit;

namespace HNC.Sync.Tests;

public class ChatEditorTests
{
    private static ChatEditorModel Model(string text, int cursor)
    {
        var m = new ChatEditorModel();
        m.Set(text, cursor);
        return m;
    }

    [Fact]
    public void Insert_goes_at_the_cursor_and_advances_it()
    {
        var m = Model("helo", 3);
        Assert.True(m.Insert("l"));
        Assert.Equal("hello", m.Text);
        Assert.Equal(4, m.Cursor);
    }

    [Fact]
    public void Control_characters_are_dropped_and_crlf_becomes_one_newline()
    {
        var m = new ChatEditorModel();
        m.Insert("a\u0001b\u007fc");
        Assert.Equal("abc", m.Text);

        m.Clear();
        m.Insert("x\r\ny\rz");
        Assert.Equal("x\ny\nz", m.Text);
    }

    [Fact]
    public void Chinese_text_from_an_input_method_is_inserted()
    {
        var m = new ChatEditorModel();
        Assert.True(m.Insert("你好 @张三"));
        Assert.Equal("你好 @张三", m.Text);
    }

    [Fact]
    public void Newline_count_is_capped()
    {
        var m = new ChatEditorModel();
        for (int i = 0; i < 20; i++) m.InsertNewline();
        Assert.Equal(ChatEditorModel.MaxLines, m.LineCount);
    }

    [Fact]
    public void Length_is_capped()
    {
        var m = new ChatEditorModel { MaxLength = 5 };
        m.Insert("abcdefgh");
        Assert.Equal("abcde", m.Text);
        Assert.False(m.Insert("x"));
    }

    [Fact]
    public void Backspace_at_line_start_joins_with_the_previous_line()
    {
        var m = Model("ab\ncd", 3); // 光标在第二行行首
        m.Backspace();
        Assert.Equal("abcd", m.Text);
        Assert.Equal(2, m.Cursor);
    }

    [Fact]
    public void Delete_removes_the_character_after_the_cursor_and_is_safe_at_the_end()
    {
        var m = Model("abc", 1);
        m.Delete();
        Assert.Equal("ac", m.Text);
        m.Set("ac", 2);
        m.Delete(); // 在末尾什么都不发生
        Assert.Equal("ac", m.Text);
    }

    [Fact]
    public void Up_and_down_keep_the_column_and_clamp_to_short_lines()
    {
        var m = Model("hello\nhi\nworld", 4); // 第一行第 4 列
        m.Down();
        Assert.Equal("hello\nhi".Length, m.Cursor);          // 第二行只有 2 个字符：夹到行尾
        m.Down();
        m.GetCursorPosition(out int line, out int col);
        Assert.Equal(2, line);
        Assert.Equal(2, col);
        m.Up();
        m.GetCursorPosition(out line, out col);
        Assert.Equal(1, line);
    }

    [Fact]
    public void Up_on_the_first_line_goes_to_the_start_and_down_on_the_last_goes_to_the_end()
    {
        var m = Model("ab\ncd", 1);
        m.Up();
        Assert.Equal(0, m.Cursor);
        m.Set("ab\ncd", 4);
        m.Down();
        Assert.Equal(5, m.Cursor);
    }

    [Fact]
    public void Home_and_end_stay_within_the_current_line()
    {
        var m = Model("one\ntwo\nthree", 6); // 在 "two" 的中间
        m.Home();
        Assert.Equal(4, m.Cursor);
        m.End();
        Assert.Equal(7, m.Cursor);
    }

    [Fact]
    public void Left_and_right_stop_at_the_ends()
    {
        var m = Model("ab", 0);
        m.Left();
        Assert.Equal(0, m.Cursor);
        m.Set("ab", 2);
        m.Right();
        Assert.Equal(2, m.Cursor);
    }

    [Fact]
    public void Set_and_move_to_clamp_out_of_range_values()
    {
        var m = Model("abc", 99);
        Assert.Equal(3, m.Cursor);
        m.MoveTo(50, 50);
        Assert.Equal(3, m.Cursor);
        m.MoveTo(-1, -1);
        Assert.Equal(0, m.Cursor);
    }

    [Fact]
    public void Cursor_position_reports_line_and_column()
    {
        var m = Model("ab\ncde", 5);
        m.GetCursorPosition(out int line, out int col);
        Assert.Equal(1, line);
        Assert.Equal(2, col);
    }

    // ── @ 提及的高亮判断 ──

    [Theory]
    [InlineData("hi @Bob how are you", "Bob", true)]
    [InlineData("hi @bob", "Bob", true)]                 // 忽略大小写
    [InlineData("@Bob", "Bob", true)]
    [InlineData("hi @Bobby", "Bob", false)]               // 不是同一个人
    [InlineData("hi @Bob_2", "Bob", false)]
    [InlineData("mail a@Bob", "Bob", false)]               // 邮箱不算
    [InlineData("hi Bob", "Bob", false)]                   // 没有 @
    [InlineData("hello @everyone", "Bob", true)]
    [InlineData("hello @Everyone!", "Bob", true)]
    [InlineData("hi @张三你好", "张三", true)]               // 中文名后面直接接文字
    [InlineData("", "Bob", false)]
    public void Mention_detection(string line, string player, bool expected)
    {
        Assert.Equal(expected, Mentions.IsMentioned(line, player));
    }

    [Fact]
    public void An_empty_player_name_only_matches_everyone()
    {
        Assert.False(Mentions.IsMentioned("hi @", ""));
        Assert.True(Mentions.IsMentioned("@everyone", ""));
    }
}
