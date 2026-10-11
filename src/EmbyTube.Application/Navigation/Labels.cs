using System.Globalization;
using EmbyTube.Core.Contracts;

namespace EmbyTube.Application.Navigation;

/// <summary>User-visible folder and message texts. Single place to localize later.</summary>
public static class Labels
{
    public const string Popular = "Popular";
    public const string Newest = "Newest";
    public const string Featured = "Featured";
    public const string AllVideos = "All videos";
    public const string Categories = "Categories";
    public const string SearchPrefix = "Search: ";
    public const string NoProviders = "No sources enabled. An administrator can enable sources in the EmbyTube plugin settings.";
    public const string ProviderDisabled = "This source is disabled.";
    public const string UnknownFolder = "This folder no longer exists. Go back and refresh.";
    public const string PluginDisabled = "EmbyTube is disabled. An administrator must enable it in the plugin settings.";

    public static string NextPage(int pageNumber) =>
        string.Format(CultureInfo.InvariantCulture, "Next page ({0}) ›", pageNumber);

    public static string ForError(ProviderErrorKind kind) => kind switch
    {
        ProviderErrorKind.RateLimited => "The source is rate limiting requests. Try again later.",
        ProviderErrorKind.AccessDenied => "The source denied access.",
        ProviderErrorKind.Unavailable => "The source is temporarily unavailable.",
        ProviderErrorKind.NetworkError => "Could not reach the source (network error or timeout).",
        ProviderErrorKind.ParseChanged => "The source has changed and is no longer understood. An update is needed.",
        ProviderErrorKind.Unsupported => "Not supported by this source.",
        ProviderErrorKind.NotFound => "Not found at the source.",
        ProviderErrorKind.Blocked => "Blocked by EmbyTube security policy.",
        ProviderErrorKind.MediaExpired => "The media link expired. Try again.",
        _ => "Unexpected error from the source. See the server log.",
    };
}
