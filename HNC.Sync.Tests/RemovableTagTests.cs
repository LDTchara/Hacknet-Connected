using HNC.RemovableTags;
using Xunit;

namespace HNC.Sync.Tests;

public class RemovableTagTests
{
    [Fact]
    public void Parse_splits_trims_and_drops_empties()
    {
        Assert.Equal(new[] { "a", "b", "c" }, TagList.Parse(" a, b ,,c "));
        Assert.Empty(TagList.Parse(null));
        Assert.Empty(TagList.Parse("  "));
    }

    [Fact]
    public void Intersects_matches_any_tag_ignoring_case()
    {
        var remove = TagList.Parse("f1,F2");
        Assert.True(TagList.Intersects("f1", remove));
        Assert.True(TagList.Intersects("x, f2", remove));          // 元素上有多个标签
        Assert.False(TagList.Intersects("f3", remove));
        Assert.False(TagList.Intersects(null, remove));            // 没有标签的永远不会被移除
        Assert.False(TagList.Intersects("f1", System.Array.Empty<string>()));
    }

    [Fact]
    public void Root_attribute_is_read_from_a_delayed_action_xml()
    {
        string xml = "<RunFunction FunctionName=\"addFlags:x\" RemoveableTag=\"f1,f2\" FunctionValue=\"0\" />";
        Assert.Equal("f1,f2", TagList.ExtractRootAttribute(xml, TagList.AttributeName));
    }

    [Fact]
    public void Root_attribute_ignores_nested_elements_and_missing_attributes()
    {
        string withChild = "<Outer a=\"1\"><Inner RemoveableTag=\"deep\" /></Outer>";
        Assert.Null(TagList.ExtractRootAttribute(withChild, TagList.AttributeName)); // 只看根元素
        Assert.Null(TagList.ExtractRootAttribute("<Plain />", TagList.AttributeName));
    }

    [Fact]
    public void Malformed_or_empty_xml_means_no_tag_not_a_crash()
    {
        Assert.Null(TagList.ExtractRootAttribute("<Broken RemoveableTag=\"x\"", TagList.AttributeName));
        Assert.Null(TagList.ExtractRootAttribute("", TagList.AttributeName));
        Assert.Null(TagList.ExtractRootAttribute("not xml at all", TagList.AttributeName));
    }

    [Fact]
    public void Entities_in_the_tag_are_decoded()
    {
        Assert.Equal("a&b", TagList.ExtractRootAttribute("<X RemoveableTag=\"a&amp;b\" />", TagList.AttributeName));
    }
}
