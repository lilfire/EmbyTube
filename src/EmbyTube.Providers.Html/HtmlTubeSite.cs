using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;

namespace EmbyTube.Providers.Html;

/// <summary>How a site exposes its stream URLs on/behind the video page (all plain, no decoding).</summary>
public enum StreamStrategy
{
    /// <summary><c>&lt;source src="…" [label|title|size]="720p"&gt;</c> on the video page.</summary>
    SourceTags,

    /// <summary>DrTuber: <c>/player_config_json/?vid=…</c> → <c>files.{lq,hq,4k}</c> MP4 URLs.</summary>
    DrTuberJson,

    /// <summary>TnAflix: page's <c>"embedUrl"</c> → <c>/ajax/video-player/…</c> → <c>&lt;source … size="720"&gt;</c>.</summary>
    TnaflixAjax,

    /// <summary>
    /// KVS player: <c>flashvars.video_url / video_alt_url[N]</c> with <c>…_text</c> labels.
    /// Only plain URLs are used; obfuscated <c>function/0/…</c> values are ignored.
    /// </summary>
    KvsFlashvars,

    /// <summary>The video page links a sub-page (<see cref="HtmlTubeSite.SubPage"/>) that contains plain MP4 URLs.</summary>
    SubPage,
}

/// <summary>
/// Declarative description of a classic HTML tube site. Every regex runs on site HTML with a
/// timeout; ids handed to Emby are site-relative paths, validated by
/// <see cref="VideoPath"/>/<see cref="CategoryPath"/>/<see cref="ListingPath"/> before any request.
/// </summary>
public sealed record HtmlTubeSite
{
    private const RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private static readonly TimeSpan T = TimeSpan.FromSeconds(2);

    public string Id { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string BaseUrl { get; init; } = null!;
    public IReadOnlyCollection<string> AllowedHosts { get; init; } = null!;

    /// <summary>Sort key (<see cref="TubeSorts"/>) → listing path. The first entry is the default.</summary>
    public IReadOnlyDictionary<string, string> Sorts { get; init; } = null!;

    /// <summary>Marks the start of one video tile.</summary>
    public Regex ItemStart { get; init; } = null!;

    /// <summary>Group 1 = video page path (or absolute URL on the site).</summary>
    public Regex ItemLink { get; init; } = null!;

    public Regex ItemTitle { get; init; } = null!;
    public Regex ItemThumb { get; init; } = null!;
    public Regex? ItemDuration { get; init; }

    /// <summary>Group 1 = next page href.</summary>
    public Regex NextPage { get; init; } = null!;

    /// <summary>Valid video page paths (without leading slash), e.g. <c>^video/\d+/[a-z0-9-]+$</c>.</summary>
    public Regex VideoPath { get; init; } = null!;

    /// <summary>Valid listing paths for page tokens (with leading slash).</summary>
    public Regex ListingPath { get; init; } = null!;

    public string? CategoriesPage { get; init; }

    /// <summary>Group 1 = category path, group 2 = name.</summary>
    public Regex? CategoryLink { get; init; }

    /// <summary>Valid category paths (without leading slash).</summary>
    public Regex? CategoryPath { get; init; }

    /// <summary>Search path with <c>{q}</c>; null = no search (e.g. disallowed by robots.txt).</summary>
    public string? SearchTemplate { get; init; }

    /// <summary>Separator used for spaces in the query.</summary>
    public string SearchSpace { get; init; } = "+";

    public StreamStrategy Streams { get; init; } = StreamStrategy.SourceTags;

    /// <summary>Optional: tiles matching this are skipped (e.g. premium tiles).</summary>
    public Regex? SkipItem { get; init; }

    /// <summary>For <see cref="StreamStrategy.SubPage"/>: group 1 = sub-page path on the video page.</summary>
    public Regex? SubPage { get; init; }

    /// <summary>
    /// For sites whose listings have no "next" link: page path template with <c>{n}</c>
    /// (e.g. <c>/porn-videos/{n}/</c>). Applied to the default listing and its numbered pages only.
    /// </summary>
    public string? PageTemplate { get; init; }

    /// <summary>Optional: only stream candidates whose URL matches are kept (e.g. to drop ad clips).</summary>
    public Regex? StreamFilter { get; init; }

    /// <summary>
    /// The CDN requires <c>Referer: BaseUrl</c> (and rejects ffmpeg's default User-Agent): variants
    /// then carry Referer + EmbyTube's User-Agent as required headers, which Emby forwards to
    /// ffmpeg and which disables direct play (spec §6.4).
    /// </summary>
    public bool StreamNeedsReferer { get; init; }

    public static Regex R(string pattern, bool singleLine = false) =>
        new(pattern, singleLine ? Opts | RegexOptions.Singleline : Opts, T);
}
