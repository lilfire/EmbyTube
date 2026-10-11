namespace EmbyTube.Core.Ids;

/// <summary>Kinds of channel folders. The serialized value is part of persistent Emby ids: never rename.</summary>
public enum FolderKind
{
    /// <summary>Provider root: Popular / Newest / Categories.</summary>
    Provider,

    /// <summary>Category list of a provider.</summary>
    Categories,

    /// <summary>A video listing (browse with optional category and sort).</summary>
    Browse,

    /// <summary>A saved search.</summary>
    Search,

    /// <summary>Informational, non-navigable entry (e.g. an error message).</summary>
    Info,
}

/// <summary>A decoded channel folder id.</summary>
public sealed record FolderId(FolderKind Kind, string ProviderId)
{
    public string? CategoryId { get; init; }
    public string? Sort { get; init; }
    public string? Query { get; init; }
    public string? PageToken { get; init; }

    /// <summary>1-based page number, used to cap "Next page" depth.</summary>
    public int PageNumber { get; init; } = 1;
}

/// <summary>A decoded video id: <c>providerId:videoId</c>.</summary>
public sealed record VideoId(string ProviderId, string Id);

/// <summary>
/// Serializes ids handed to Emby. Every component is percent-encoded, so text from
/// remote sites can never inject separators. Ids never contain stream URLs.
/// </summary>
public static class ItemIds
{
    private const string FolderPrefix = "f1";
    private const char Sep = '|';
    private const char VideoSep = ':';

    private static readonly Dictionary<FolderKind, string> KindCodes = new()
    {
        [FolderKind.Provider] = "p",
        [FolderKind.Categories] = "c",
        [FolderKind.Browse] = "b",
        [FolderKind.Search] = "s",
        [FolderKind.Info] = "i",
    };

    private static readonly Dictionary<string, FolderKind> CodeKinds =
        KindCodes.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    public static string Encode(FolderId id)
    {
        if (id.PageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(id), "PageNumber must be >= 1.");

        return string.Join(Sep, new[]
        {
            FolderPrefix,
            KindCodes[id.Kind],
            Esc(id.ProviderId),
            Esc(id.CategoryId),
            Esc(id.Sort),
            Esc(id.Query),
            Esc(id.PageToken),
            id.PageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    public static bool TryDecodeFolder(string? value, out FolderId? id)
    {
        id = null;
        if (string.IsNullOrEmpty(value))
            return false;

        var parts = value.Split(Sep);
        if (parts.Length != 8 || parts[0] != FolderPrefix || !CodeKinds.TryGetValue(parts[1], out var kind))
            return false;

        if (!int.TryParse(parts[7], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var page) || page < 1)
            return false;

        try
        {
            var provider = Unesc(parts[2]);
            if (string.IsNullOrEmpty(provider))
                return false;

            id = new FolderId(kind, provider)
            {
                CategoryId = Unesc(parts[3]),
                Sort = Unesc(parts[4]),
                Query = Unesc(parts[5]),
                PageToken = Unesc(parts[6]),
                PageNumber = page,
            };
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>Log-safe description of a folder id: kind, provider and page, never query text or tokens.</summary>
    public static string Describe(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(root)";
        return TryDecodeFolder(value, out var f) && f is not null
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{f.Kind}/{f.ProviderId}/p{f.PageNumber}{(f.CategoryId is null ? string.Empty : "/cat")}")
            : "(unparsed)";
    }

    public static string Encode(VideoId id) => Esc(id.ProviderId) + VideoSep + Esc(id.Id);

    public static bool TryDecodeVideo(string? value, out VideoId? id)
    {
        id = null;
        if (string.IsNullOrEmpty(value) || value.StartsWith(FolderPrefix + Sep, StringComparison.Ordinal))
            return false;

        var idx = value.IndexOf(VideoSep);
        if (idx <= 0 || idx == value.Length - 1 || value.IndexOf(VideoSep, idx + 1) >= 0)
            return false;

        var provider = Uri.UnescapeDataString(value[..idx]);
        var video = Uri.UnescapeDataString(value[(idx + 1)..]);
        if (provider.Length == 0 || video.Length == 0)
            return false;

        id = new VideoId(provider, video);
        return true;
    }

    // Empty string encodes null; real empty strings are normalized to null.
    private static string Esc(string? s) => string.IsNullOrEmpty(s) ? string.Empty : Uri.EscapeDataString(s);

    private static string? Unesc(string s) => s.Length == 0 ? null : Uri.UnescapeDataString(s);
}
