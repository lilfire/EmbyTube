using EmbyTube.Core.Ids;

namespace EmbyTube.Core.Tests;

public class ItemIdsTests
{
    [Fact]
    public void Folder_roundtrips_all_fields()
    {
        var id = new FolderId(FolderKind.Search, "xv")
        {
            CategoryId = "cat|1:x",
            Sort = "newest",
            Query = "a|b:c %20 æøå",
            PageToken = "tok/=+&?",
            PageNumber = 4,
        };

        var encoded = ItemIds.Encode(id);

        Assert.True(ItemIds.TryDecodeFolder(encoded, out var decoded));
        Assert.Equal(id, decoded);
        Assert.Equal(8, encoded.Split('|').Length); // separators in values are escaped
    }

    [Fact]
    public void Folder_encoding_is_stable()
    {
        var a = ItemIds.Encode(new FolderId(FolderKind.Browse, "mock") { Sort = "popular" });
        var b = ItemIds.Encode(new FolderId(FolderKind.Browse, "mock") { Sort = "popular" });
        Assert.Equal(a, b);
        Assert.Equal("f1|b|mock||popular|||1", a);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("f1|b|mock")]
    [InlineData("f2|b|mock||||| 1")]
    [InlineData("f1|z|mock||||| 1")]
    [InlineData("f1|b|||||| 1")]
    [InlineData("f1|b|mock|||||0")]
    [InlineData("f1|b|mock|||||-1")]
    [InlineData("f1|b|mock|||||x")]
    public void Malformed_folder_ids_are_rejected(string? value)
    {
        Assert.False(ItemIds.TryDecodeFolder(value, out _));
    }

    [Fact]
    public void Video_roundtrips_and_escapes_separator()
    {
        var id = new VideoId("xv", "abc:123/x");
        var encoded = ItemIds.Encode(id);
        Assert.Equal(1, encoded.Count(c => c == ':'));
        Assert.True(ItemIds.TryDecodeVideo(encoded, out var decoded));
        Assert.Equal(id, decoded);
    }

    [Theory]
    [InlineData("mock")]
    [InlineData(":abc")]
    [InlineData("mock:")]
    [InlineData("a:b:c")]
    [InlineData("f1|b|mock|||||1")]
    public void Malformed_video_ids_are_rejected(string value)
    {
        Assert.False(ItemIds.TryDecodeVideo(value, out _));
    }

    [Fact]
    public void Describe_never_contains_query_or_token()
    {
        var encoded = ItemIds.Encode(new FolderId(FolderKind.Search, "mock") { Query = "secret words", PageToken = "tok123", PageNumber = 2 });
        var described = ItemIds.Describe(encoded);
        Assert.DoesNotContain("secret", described);
        Assert.DoesNotContain("tok123", described);
        Assert.Equal("Search/mock/p2", described);
    }
}
