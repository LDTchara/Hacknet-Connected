using HNC.Chat;
using Xunit;

namespace HNC.Sync.Tests;

public class ChatTests
{
    private sealed class FakeWorld : IChatWorld, ICompletionSource
    {
        public readonly Dictionary<string, (NodeRef node, bool visible)> Nodes = new();
        public List<string> Users = new() { "alice", "Bob", "Channel" };

        public FakeWorld()
        {
            var bank = new NodeRef { Ip = "10.0.0.5", Name = "Bank", Id = "bankNode" };
            bank.Accounts.Add(new AccountRef { User = "admin", Password = "adm1n", Known = true, IsAdmin = true });
            bank.Accounts.Add(new AccountRef { User = "guest", Password = "g", Known = true });
            bank.Accounts.Add(new AccountRef { User = "hidden", Password = "secret", Known = false });
            Nodes[bank.Ip] = (bank, true);

            var bare = new NodeRef { Ip = "10.0.0.6", Name = "Bare", Id = "bareNode" };
            bare.Accounts.Add(new AccountRef { User = "root", Password = "x", Known = false });
            Nodes[bare.Ip] = (bare, true);

            var secret = new NodeRef { Ip = "10.0.0.9", Name = "Secret", Id = "secretNode" };
            Nodes[secret.Ip] = (secret, false);
        }

        public NodeRef? FindNode(string ip, out bool visible)
        {
            if (Nodes.TryGetValue(ip, out var n)) { visible = n.visible; return n.node; }
            visible = false;
            return null;
        }

        public IEnumerable<string> UserNames() => Users;
        public IEnumerable<KeyValuePair<string, string>> VisibleNodes() =>
            Nodes.Values.Where(v => v.visible).Select(v => new KeyValuePair<string, string>(v.node.Ip, v.node.Name));
    }

    // ── 解析 ──

    [Fact]
    public void Plain_text_and_mentions_stay_in_the_text()
    {
        var p = MessageParser.Parse("hello @alice and @everyone", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("hello @alice and @everyone", p.Text);
        Assert.Empty(p.Attachments);
    }

    [Fact]
    public void Ip_mention_adds_a_link_attachment_using_the_stable_node_id()
    {
        var p = MessageParser.Parse("look @10.0.0.5 now", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("look @10.0.0.5 now", p.Text);
        var a = Assert.Single(p.Attachments);
        Assert.Equal("!ATTACHMENT:link#%#Bank#%#bankNode", a);
    }

    [Fact]
    public void Dash_p_attaches_known_accounts_admin_first_and_skips_unknown_ones()
    {
        var p = MessageParser.Parse("creds @10.0.0.5 -p", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("creds @10.0.0.5", p.Text);            // -p 不出现在正文里
        Assert.Equal(3, p.Attachments.Count);                  // link + admin + guest
        Assert.Equal("!ATTACHMENT:account#%#Bank#%#bankNode#%#admin#%#adm1n", p.Attachments[1]);
        Assert.Equal("!ATTACHMENT:account#%#Bank#%#bankNode#%#guest#%#g", p.Attachments[2]);
        Assert.DoesNotContain(p.Attachments, a => a.Contains("secret"));
    }

    [Fact]
    public void Dash_p_with_no_known_account_is_an_error()
    {
        var p = MessageParser.Parse("@10.0.0.6 -p", new FakeWorld());
        Assert.NotNull(p.Error);
    }

    [Fact]
    public void Manual_credentials_are_checked_and_never_appear_in_the_text()
    {
        var p = MessageParser.Parse("@10.0.0.5 -p hidden secret", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("@10.0.0.5", p.Text);
        Assert.DoesNotContain("secret", p.Text);
        Assert.Equal("!ATTACHMENT:account#%#Bank#%#bankNode#%#hidden#%#secret", p.Attachments[1]);
    }

    [Fact]
    public void Wrong_manual_password_blocks_the_whole_send()
    {
        var p = MessageParser.Parse("hi @10.0.0.5 -p admin wrong", new FakeWorld());
        Assert.NotNull(p.Error);
        Assert.Contains("密码", p.Error);
        Assert.Empty(p.Attachments);
    }

    [Fact]
    public void Unknown_manual_account_blocks_the_whole_send()
    {
        var p = MessageParser.Parse("@10.0.0.5 -p nobody pw", new FakeWorld());
        Assert.NotNull(p.Error);
        Assert.Contains("nobody", p.Error);
    }

    [Fact]
    public void Dash_p_followed_by_a_single_word_is_auto_mode_and_the_word_stays_text()
    {
        var p = MessageParser.Parse("@10.0.0.5 -p please", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("@10.0.0.5 please", p.Text);
        Assert.Equal(3, p.Attachments.Count);
    }

    [Fact]
    public void Unknown_or_hidden_nodes_are_rejected()
    {
        Assert.NotNull(MessageParser.Parse("@9.9.9.9", new FakeWorld()).Error);
        var hidden = MessageParser.Parse("@10.0.0.9", new FakeWorld());
        Assert.NotNull(hidden.Error);
        Assert.Contains("发现", hidden.Error);
    }

    [Fact]
    public void Empty_and_overlong_messages_are_rejected()
    {
        Assert.NotNull(MessageParser.Parse("   ", new FakeWorld()).Error);
        Assert.NotNull(MessageParser.Parse(new string('a', 301), new FakeWorld()).Error);
    }

    [Fact]
    public void Attachment_fields_are_never_empty_or_contain_the_separator()
    {
        var n = new NodeRef { Ip = "1.2.3.4", Name = "", Id = "id#%#x" };
        var sep = new[] { "#%#" };
        // 名字为空 → 退回 id；id 里的分隔符被去掉；字段数必须刚好是 3（link / 名字 / id）
        Assert.Equal("!ATTACHMENT:link#%#idx#%#idx", Attachments.Link(n));
        Assert.Equal(3, Attachments.Link(n).Split(sep, StringSplitOptions.None).Length);
        // 账户名/密码为空 → 填 "?"，字段数必须刚好是 5
        Assert.Equal(5, Attachments.Account(n, "", "").Split(sep, StringSplitOptions.None).Length);
    }

    // ── 多行（Shift+Enter 换行） ──

    [Fact]
    public void Multi_line_text_keeps_its_line_breaks_and_mentions_work_on_every_line()
    {
        var p = MessageParser.Parse("hello\nsee @10.0.0.5 and @alice", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("hello\nsee @10.0.0.5 and @alice", p.Text);
        Assert.Single(p.Attachments);
    }

    [Fact]
    public void Leading_and_trailing_blank_lines_are_trimmed_but_inner_ones_stay()
    {
        var p = MessageParser.Parse("\n\nfirst\n\nsecond\n\n", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("first\n\nsecond", p.Text);
    }

    [Fact]
    public void Crlf_is_treated_as_a_line_break()
    {
        var p = MessageParser.Parse("a\r\nb", new FakeWorld());
        Assert.Equal("a\nb", p.Text);
    }

    [Fact]
    public void Dash_p_credentials_never_swallow_words_from_the_next_line()
    {
        // 第一行的 -p 后面没有词了 → 自动模式；第二行的 "admin adm1n" 只是普通文字
        var p = MessageParser.Parse("@10.0.0.5 -p\nadmin adm1n", new FakeWorld());
        Assert.Null(p.Error);
        Assert.Equal("@10.0.0.5\nadmin adm1n", p.Text);
        Assert.Equal(3, p.Attachments.Count); // link + 自动附带的 admin + guest
    }

    [Fact]
    public void An_error_on_any_line_blocks_the_whole_message()
    {
        var p = MessageParser.Parse("fine line\n@9.9.9.9", new FakeWorld());
        Assert.NotNull(p.Error);
        Assert.Empty(p.Attachments.Where(a => a.Contains("secret")));
    }

    [Fact]
    public void A_message_that_is_only_blank_lines_is_empty()
    {
        Assert.NotNull(MessageParser.Parse("\n \n", new FakeWorld()).Error);
    }

    [Fact]
    public void Newline_is_a_token_separator_for_completion()
    {
        var w = new FakeWorld();
        // "hi\n@al" —— 光标在末尾，@al 是新的一行里的词
        var c = Completer.Suggest("hi\n@al", 6, w, 10);
        Assert.Contains(c, x => x.Replacement == "@alice");

        // 后面已经是换行，不再补空格
        var r = Completer.Apply("hi @al\nnext", 6, new Candidate { Display = "@alice", Replacement = "@alice" }, true, out int cur);
        Assert.Equal("hi @alice\nnext", r);
        Assert.Equal(9, cur);
    }

    // ── 附件格式必须和原版 AttachmentRenderer 读到的一致 ──

    /// <summary>原版 AttachmentRenderer.RenderAttachment 的拆分方式：去掉前缀，再按 #%# 拆分（丢弃空项）。</summary>
    private static string[] VanillaSplit(string attachmentLine)
    {
        Assert.StartsWith("!ATTACHMENT:", attachmentLine);
        return attachmentLine.Substring("!ATTACHMENT:".Length).Split(new[] { "#%#" }, StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void Link_attachment_has_the_fields_vanilla_reads_at_those_indexes()
    {
        var p = MessageParser.Parse("@10.0.0.5", new FakeWorld());
        var a = VanillaSplit(Assert.Single(p.Attachments));
        Assert.Equal(3, a.Length);
        Assert.Equal("link", a[0]);       // 原版：array[0].Equals("link")
        Assert.Equal("Bank", a[1]);       // 显示的名字
        Assert.Equal("bankNode", a[2]);   // Programs.getComputer(os, array[2])：节点 id
    }

    [Fact]
    public void Account_attachment_has_the_fields_vanilla_reads_at_those_indexes()
    {
        var p = MessageParser.Parse("@10.0.0.5 -p admin adm1n", new FakeWorld());
        var a = VanillaSplit(p.Attachments[1]);
        Assert.Equal(5, a.Length);
        Assert.Equal("account", a[0]);    // 原版：array[0].Equals("account")
        Assert.Equal("Bank", a[1]);
        Assert.Equal("bankNode", a[2]);   // getComputer(os, array[2])
        Assert.Equal("admin", a[3]);      // value.name.Equals(array[3])
        Assert.Equal("adm1n", a[4]);      // 显示 Pass=array[4]
    }

    [Fact]
    public void Attachment_fields_survive_vanilla_empty_entry_removal_even_for_odd_nodes()
    {
        // 原版拆分会丢弃空项：任何字段为空都会让后面的下标错位 / 越界，所以我们从不留空
        var odd = new NodeRef { Ip = "1.1.1.1", Name = "", Id = "" };
        Assert.Equal(3, VanillaSplit(Attachments.Link(odd)).Length);
        Assert.Equal(5, VanillaSplit(Attachments.Account(odd, "", "")).Length);
    }

    // ── 补全 ──

    [Fact]
    public void At_sign_lists_everyone_users_then_visible_nodes_but_not_hidden_ones()
    {
        var c = Completer.Suggest("@", 1, new FakeWorld(), 20).Select(x => x.Replacement).ToList();
        Assert.Equal("@everyone", c[0]);
        Assert.Contains("@alice", c);
        Assert.Contains("@10.0.0.5", c);
        Assert.DoesNotContain("@10.0.0.9", c);
    }

    [Fact]
    public void Prefix_filters_case_insensitively_and_matches_node_names()
    {
        var w = new FakeWorld();
        Assert.Equal(new[] { "@Bob" }, Completer.Suggest("@b", 2, w, 20).Where(x => x.Replacement == "@Bob").Select(x => x.Replacement));
        Assert.Contains(Completer.Suggest("@ban", 4, w, 20), x => x.Replacement == "@10.0.0.5");   // 按节点名
        Assert.Contains(Completer.Suggest("@10.0.0.", 8, w, 20), x => x.Replacement == "@10.0.0.6"); // 按 IP 前缀
    }

    [Fact]
    public void Dash_after_an_ip_mention_suggests_p()
    {
        var c = Completer.Suggest("hi @10.0.0.5 -", 14, new FakeWorld(), 5);
        Assert.Equal("-p", Assert.Single(c).Replacement);
        Assert.Empty(Completer.Suggest("hi -", 4, new FakeWorld(), 5)); // 前面不是 @IP
    }

    [Fact]
    public void Complete_token_is_not_suggested_again()
    {
        Assert.Empty(Completer.Suggest("@everyone", 9, new FakeWorld(), 5));
    }

    [Fact]
    public void Apply_replaces_only_the_token_at_the_cursor()
    {
        var cand = new Candidate { Display = "@alice", Replacement = "@alice" };
        // "hey @al there"，光标在 "@al" 之后
        var r = Completer.Apply("hey @al there", 7, cand, true, out int cur);
        Assert.Equal("hey @alice there", r);   // 后面已经是空格，不重复补
        Assert.Equal(10, cur);

        var r2 = Completer.Apply("@al", 3, cand, true, out int cur2);
        Assert.Equal("@alice ", r2);
        Assert.Equal(7, cur2);
    }
}
