using EmbyTube.Core.Contracts;
using static EmbyTube.Providers.Html.HtmlTubeSite;

namespace EmbyTube.Providers.Html;

/// <summary>
/// Site profiles, written from each site's live markup and robots.txt (2026-10-11).
/// Cumination's modules for these sites were read as functional references only (GPL, no
/// code copied; ADR-003). See docs/sites/html-tubes.md for per-site notes.
/// </summary>
public static class HtmlTubeSites
{
    public static readonly HtmlTubeSite DrTuber = new()
    {
        Id = "drtuber",
        DisplayName = "DrTuber",
        BaseUrl = "https://www.drtuber.com/",
        AllowedHosts = new[] { "drtuber.com", "drtst.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Featured] = "/" },
        ItemStart = R(@"<a href=""/video/\d+/[^""]+""\s+class=""th\b"),
        ItemLink = R(@"<a href=""(/video/\d+/[^""]+)"""),
        ItemTitle = R(@"\balt=""([^""]+)"""),
        ItemThumb = R(@"<img\b[^>]*\bsrc=""(https://[^""]+)"""),
        ItemDuration = R(@"time_thumb"">\s*<em>\s*([\d:]+)\s*</em>"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)"""),
        VideoPath = R(@"^video/\d+/[A-Za-z0-9_\-]+$"),
        ListingPath = R(@"^/(?:\d+|tags/[A-Za-z0-9_\-]+(?:/\d+)?|search/videos/[^/?#]+(?:/\d+)?)?/?$"),
        CategoriesPage = "/categories/",
        CategoryLink = R(@"<li class=""item"">\s*<a href=""(/tags/[A-Za-z0-9_\-]+)"">\s*<span>([^<]+)</span>"),
        CategoryPath = R(@"^tags/[A-Za-z0-9_\-]+$"),
        SearchTemplate = "/search/videos/{q}",
        SearchSpace = "%20",
        Streams = StreamStrategy.DrTuberJson, // /player_config_json/ (robots disallows /player/ and /ajax/, not this)
    };

    public static readonly HtmlTubeSite TnAflix = new()
    {
        Id = "tnaflix",
        DisplayName = "TNAFlix",
        BaseUrl = "https://www.tnaflix.com/",
        AllowedHosts = new[] { "tnaflix.com", "tnastatic.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/new/1" },
        ItemStart = R(@"<div data-vid=""\d+"""),
        ItemLink = R(@"href=""(https://www\.tnaflix\.com/[A-Za-z0-9_\-]+/[A-Za-z0-9_\-]+/video\d+)"""),
        ItemTitle = R(@"\balt=""([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""(https://[^""]+)"""),
        ItemDuration = R(@"video-duration"">\s*([\d:]+)\s*<"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)"""),
        VideoPath = R(@"^[A-Za-z0-9_\-]+/[A-Za-z0-9_\-]+/video\d+$"),
        ListingPath = R(@"^/(?:new/\d+|[A-Za-z0-9_\-]+-porn/\d+|search\.php\?what=[^&#]+(?:&page=\d+)?|[A-Za-z0-9_\-]+(?:/\d+)?(?:\?page=\d+)?)$"),
        CategoriesPage = "/categories",
        CategoryLink = R(@"<a class=""thumb[^""]*"" href=""(https://www\.tnaflix\.com/[A-Za-z0-9_\-]+)"">.*?class=""thumb-title"">([^<]+)<", singleLine: true),
        CategoryPath = R(@"^[A-Za-z0-9_\-]+-porn$"),
        SearchTemplate = "/search.php?what={q}",
        Streams = StreamStrategy.TnaflixAjax,
    };

    public static readonly HtmlTubeSite PornOne = new()
    {
        Id = "pornone",
        DisplayName = "PornOne",
        BaseUrl = "https://pornone.com/",
        AllowedHosts = new[] { "pornone.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/newest/" },
        ItemStart = R(@"<a href=""https://pornone\.com/[^""]+/\d+/(?:#[^""]*)?""\s+class=""popbop"),
        ItemLink = R(@"<a href=""(https://pornone\.com/[A-Za-z0-9_\-]+/[A-Za-z0-9_\-]+/\d+/)"),
        ItemTitle = R(@"\balt=""(?!HD Video|Add to)([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""(https://[^""]+)"""),
        ItemDuration = R(@"durlabel"">(?:\s*<img[^>]*>)?\s*([\d:]+)"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)"""),
        VideoPath = R(@"^[A-Za-z0-9_\-]+/[A-Za-z0-9_\-]+/\d+/$"),
        ListingPath = R(@"^/(?:newest/(?:\d+/)?|[A-Za-z0-9_\-]+/(?:\d+/)?|search\?q=[^&#]+(?:&page=\d+)?)$"),
        CategoriesPage = "/categories/",
        CategoryLink = R(@"<a href=""(/[a-z0-9\-]+/)"" class=""popbop[^""]*"">\s*<img[^>]*\balt=""Video category ([^""]+)"""),
        CategoryPath = R(@"^[a-z0-9\-]+/$"),
        SearchTemplate = "/search?q={q}", // robots.txt disallows only sorted search variants
    };

    public static readonly HtmlTubeSite SuperPorn = new()
    {
        Id = "superporn",
        DisplayName = "SuperPorn",
        BaseUrl = "https://www.superporn.com/",
        AllowedHosts = new[] { "superporn.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/" },
        ItemStart = R(@"<div class=""thumb-video\s*"""),
        ItemLink = R(@"href=""(https://www\.superporn\.com/video/[A-Za-z0-9_\-]+)"""),
        ItemTitle = R(@"\balt=""([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""(https://[^""]+)"""),
        ItemDuration = R(@"(?:duracion|duration)[^>]*>\s*(?:<[^>]+>\s*)*([\d]{1,2}:[\d]{2}(?::[\d]{2})?)"),
        NextPage = R(@"pagination_item--next[^>]*>\s*<a[^>]*\bhref=""([^""]+)""", singleLine: true),
        VideoPath = R(@"^video/[A-Za-z0-9_\-]+$"),
        ListingPath = R(@"^/(?:\?page=\d+)?$"),
        SearchTemplate = null, // robots.txt: Disallow: /*?q=*
    };

    public static readonly HtmlTubeSite PlayVids = PlayVidsFamily("playvids", "PlayVids", "https://www.playvids.com/", "playvids.com", "/?page=1",
        @"href=""(/[A-Za-z0-9_\-]{8,14}/[A-Za-z0-9_\-]+)""", @"^[A-Za-z0-9_\-]{8,14}/[A-Za-z0-9_\-]+$");

    public static readonly HtmlTubeSite PeekVids = PlayVidsFamily("peekvids", "PeekVids", "https://www.peekvids.com/", "peekvids.com", "/Trending-Porn",
        @"href=""(/v/[A-Za-z0-9_\-]+/[A-Za-z0-9_\-]+)""", @"^v/[A-Za-z0-9_\-]+/[A-Za-z0-9_\-]+$");

    public static readonly HtmlTubeSite Xozilla = Kvs("xozilla", "Xozilla", "https://www.xozilla.com/", new[] { "xozilla.com", "ahcdn.com" },
        "/latest-updates/", @"<a href=""(https://www\.xozilla\.com/videos/\d+/[A-Za-z0-9_\-]+/)""\s+class=""item",
        @"<a href=""(https://www\.xozilla\.com/categories/[A-Za-z0-9_\-]+/)"">\s*([^<]+?)\s*<span class=""rating"">", "/search/{q}/", "-");

    public static readonly HtmlTubeSite ThreeMovs = Kvs("3movs", "3Movs", "https://www.3movs.com/", new[] { "3movs.com", "mjedge.net" },
        "/videos/", @"<a class=""wrap_image"" href=""(https://www\.3movs\.com/videos/\d+/[A-Za-z0-9_\-]+/)""",
        @"<a class=""th_cat"" href=""(https://www\.3movs\.com/categories/[A-Za-z0-9_\-]+/)"" title=""([^""]+)""", "/search_videos/?q={q}", "+") with
    {
        ItemStart = R(@"<div class=""item thumb"),
    };

    public static readonly HtmlTubeSite VaginaNl = new()
    {
        Id = "vaginanl",
        DisplayName = "Vagina.nl (Dutch)",
        BaseUrl = "https://vagina.nl/",
        AllowedHosts = new[] { "vagina.nl", "mjedge.net" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/sexfilms/newest" },
        ItemStart = R(@"<a href=""/sexfilms/view/[^""]+"" class=""thumbnail-link"""),
        ItemLink = R(@"<a href=""(/sexfilms/view/[A-Za-z0-9_\-]+)"" class=""thumbnail-link"""),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""([^""]+)"""),
        ItemDuration = R(@">\s*(\d{1,2}:\d{2}(?::\d{2})?)\s*<"),
        NextPage = R(@"<a href=""([^""]+)"" rel=""next"""),
        VideoPath = R(@"^sexfilms/view/[A-Za-z0-9_\-]+$"),
        ListingPath = R(@"^/(?:sexfilms/newest(?:/page:\d+)?|categories/view/[A-Za-z0-9_\-]+(?:/page:\d+)?|sexfilms/search\?q=[^&#]+(?:&page=\d+)?|sexfilms/search(?:/page:\d+)?\?q=[^&#]+)$"),
        CategoriesPage = "/categories",
        CategoryLink = R(@"<a href=""(/categories/view/[A-Za-z0-9_\-]+)"" itemprop=""url"">.*?item-text"">([^<]+)<", singleLine: true),
        CategoryPath = R(@"^categories/view/[A-Za-z0-9_\-]+$"),
        SearchTemplate = "/sexfilms/search?q={q}",
    };

    public static readonly HtmlTubeSite Poldertube = DutchWordPress("poldertube", "Poldertube.nl (Dutch)", "https://www.poldertube.nl/", "poldertube.nl",
        @"<article\b", @"<article\b.*?<a href=""(https://www\.poldertube\.nl/[A-Za-z0-9_\-]+/)""\s+title=");

    public static readonly HtmlTubeSite SextubeNl = DutchWordPress("sextubenl", "Sextube.nl (Dutch)", "https://www.sextube.nl/", "sextube.nl",
        @"data-post-id=""\d+""", @"<a class=""thumb"" href=""(https://www\.sextube\.nl/[A-Za-z0-9_\-]+/?)""");

    public static readonly HtmlTubeSite HelloPorn = PrivatehostItems("helloporn", "Hello Porn", "https://hello.porn/", "hello.porn");

    public static readonly HtmlTubeSite HomoXxx = PrivatehostItems("homoxxx", "Homo XXX (gay)", "https://homo.xxx/", "homo.xxx");

    public static readonly HtmlTubeSite MrSexe = new()
    {
        Id = "mrsexe",
        DisplayName = "MrSexe (French)",
        BaseUrl = "https://www.mrsexe.tv/",
        AllowedHosts = new[] { "mrsexe.tv", "mrsexe.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/" },
        ItemStart = R(@"<li class=""\s*(?:hd)?\s*"">\s*<a class=""thumbnail"""),
        ItemLink = R(@"<a class=""thumbnail"" href=""(/videos/streaming/[A-Za-z0-9_\-]+\.html)"""),
        ItemTitle = R(@"</span>\s*([^<]+?)\s*</figcaption>"),
        ItemThumb = R(@"<img\b[^>]*\bsrc=""([^""]+)"""),
        ItemDuration = R(@"fa-clock-o""></i>\s*([^<]+?)\s*</span>"),
        NextPage = R(@"<a href=""([^""]+)"">\s*suivant"),
        VideoPath = R(@"^videos/streaming/[A-Za-z0-9_\-]+\.html$"),
        ListingPath = R(@"^/(?:page\d+\.html|videos/page\d+\.html|cat/[A-Za-z0-9_\-/]+?(?:page\d+\.html)?|\?search=[^&#]+(?:&page=\d+)?)$"),
        CategoriesPage = "/",
        CategoryLink = R(@"<option value=""(/cat/\d+/[A-Za-z0-9_\-]+/)""\s*>\s*([^<]+?)\s*</option>"),
        CategoryPath = R(@"^cat/\d+/[A-Za-z0-9_\-]+/$"),
        SearchTemplate = null, // "/?search=" answers 200 with an empty body (2026-10-11)
        Streams = StreamStrategy.SubPage,
        SubPage = R(@"src='(/inc/clic\.php\?video=[A-Za-z0-9_]+&(?:amp;)?cat=mrsex[a-z]*)'"),
    };

    public static readonly HtmlTubeSite LuxureTv = new()
    {
        Id = "luxuretv",
        DisplayName = "LuxureTV",
        BaseUrl = "https://en.luxuretv.com/",
        AllowedHosts = new[] { "luxuretv.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/" },
        ItemStart = R(@"<div class=""content"">"),
        ItemLink = R(@"<a href=""(https://en\.luxuretv\.com/videos/[A-Za-z0-9_\-]+\.html)"""),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""([^""]+)"""),
        ItemDuration = R(@"class=""time"">(?:<b>)?\s*([\d:]+)"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)"""),
        VideoPath = R(@"^videos/[A-Za-z0-9_\-]+\.html$"),
        ListingPath = R(@"^/(?:page\d+\.html|channels/[A-Za-z0-9_\-]+/(?:page\d+\.html)?|searchgate/videos/[^/?#]+/(?:page\d+\.html)?)$"),
        CategoriesPage = "/channels/",
        CategoryLink = R(@"<div class=""vtitle""><a href='(https://en\.luxuretv\.com/channels/[A-Za-z0-9_\-]+/)'>([^<]+)</a>"),
        CategoryPath = R(@"^channels/[A-Za-z0-9_\-]+/$"),
        SearchTemplate = "/searchgate/videos/{q}/",
        SearchSpace = "-",
        StreamNeedsReferer = true, // media hosts answer 403 without Referer and to ffmpeg's User-Agent
    };

    public static readonly HtmlTubeSite CumLouder = new()
    {
        Id = "cumlouder",
        DisplayName = "CumLouder",
        BaseUrl = "https://www.cumlouder.com/",
        AllowedHosts = new[] { "cumlouder.com", "imgcm.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/porn/" },
        ItemStart = R(@"<a class=""muestra-escena"" href=""/porn-video/"),
        ItemLink = R(@"href=""(/porn-video/[A-Za-z0-9_\-]+/)"""),
        ItemTitle = R(@"\balt=""([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""([^""]+)"""),
        ItemDuration = R(@"ico-minutos sprite""></span>\s*([\d:]+)"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)"""),
        VideoPath = R(@"^porn-video/[A-Za-z0-9_\-]+/$"),
        ListingPath = R(@"^/(?:porn/(?:\d+/)?|\d+/|porn-videos/[a-z0-9\-]+/(?:\d+/)?|search/(?:\d+/)?\?q=[^&#]+)$"),
        CategoriesPage = "/categories/",
        CategoryLink = R(@"class=""muestra-escena muestra-categoria[^""]*"" href=""(/porn-videos/[a-z0-9\-]+/)[^""]*"">.*?\balt=""([^""]+)""", singleLine: true),
        CategoryPath = R(@"^porn-videos/[a-z0-9\-]+/$"),
        SearchTemplate = null, // /search?q= answers 403 (2026-10-11)
    };

    public static readonly HtmlTubeSite HomeMoviesTube = new()
    {
        Id = "homemoviestube",
        DisplayName = "HomeMoviesTube",
        BaseUrl = "https://www.homemoviestube.com/",
        AllowedHosts = new[] { "homemoviestube.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/most-recent/" },
        ItemStart = R(@"<article class=""media-card video-card"">"),
        ItemLink = R(@"href=""https://www\.homemoviestube\.com(/videos/\d+/[A-Za-z0-9_\-]+\.html)"""),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"<img src=""([^""]+)"""),
        ItemDuration = R(@"(?:duration|time)[^""]*""[^>]*>\s*(?:<[^>]+>\s*)*([\d:]{4,8})"),
        NextPage = R(@"<a class=""page-link"" href=""([^""]+)"" rel=""next"""),
        VideoPath = R(@"^videos/\d+/[A-Za-z0-9_\-]+\.html$"),
        ListingPath = R(@"^/(?:most-recent(?:/|\?page=\d+)|channels/\d+/[a-z0-9\-]+(?:\?page=\d+)?|search/[^/?#]+/(?:\?page=\d+)?)$"),
        CategoriesPage = "/channels/",
        CategoryLink = R(@"<a href=""https://www\.homemoviestube\.com(/channels/\d+/[a-z0-9\-]+)"" class=""channel-card-link"">.*?\balt=""([^""]+)""", singleLine: true),
        CategoryPath = R(@"^channels/\d+/[a-z0-9\-]+$"),
        SearchTemplate = "/search/{q}/",
        SearchSpace = "-",
        StreamFilter = R(@"/media/videos/"), // the page also carries an ad clip (/tantaly_s.mp4)
    };

    public static readonly HtmlTubeSite HypnoTube = new()
    {
        Id = "hypnotube",
        DisplayName = "HypnoTube",
        BaseUrl = "https://hypnotube.com/",
        AllowedHosts = new[] { "hypnotube.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/videos/page1.html", [TubeSorts.Popular] = "/most-viewed/month/" },
        ItemStart = R(@"<div class=""item-col col\s"),
        ItemLink = R(@"href=""https://hypnotube\.com(/video/[A-Za-z0-9_\-]+\.html)"""),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"<img\b[^>]*\bsrc=""([^""]+)"""),
        ItemDuration = R(@"class=""time"">\s*([\d:]+)\s*<"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)"""),
        VideoPath = R(@"^video/[A-Za-z0-9_\-]+\.html$"),
        ListingPath = R(@"^/(?:videos/page\d+\.html|most-viewed/month/(?:page\d+\.html)?|channels/\d+/[a-z0-9\-]+/(?:page\d+\.html)?|search/[^/?#]+/(?:page\d+\.html)?)$"),
        CategoriesPage = "/channels/",
        CategoryLink = R(@"<a href=""https://hypnotube\.com(/channels/\d+/[a-z0-9\-]+/)"" title=""([^""]+)"""),
        CategoryPath = R(@"^channels/\d+/[a-z0-9\-]+/$"),
        SearchTemplate = null, // search pages return no result tiles (2026-10-11)
    };

    public static readonly HtmlTubeSite JustPorn = Kvs("justporn", "JustPorn", "https://www.justporn.com/", new[] { "justporn.com", "mjedge.net" },
        "/porn-videos/", @"<a href=""(https://www\.justporn\.com/video/\d+/[A-Za-z0-9_\-]+/)""\s+title=",
        @"<a[^>]+href=""(https://www\.justporn\.com/categories/[A-Za-z0-9_\-]+/)""[^>]*\btitle=""([^""]+)""", "/search/{q}/", "-") with
    {
        ItemStart = R(@"<div class=""thumb thumb_rel item"),
        VideoPath = R(@"^video/\d+/[A-Za-z0-9_\-]+/$"),
        PageTemplate = "/porn-videos/{n}/", // listings have no "next" link
        ListingPath = R(@"^/(?:porn-videos/(?:\d+/)?|categories/[A-Za-z0-9_\-]+/(?:\d+/)?|search/[^/?#]+/(?:\d+/)?)$"),
    };

    public static readonly HtmlTubeSite NonkTube = DutchWordPress("nonktube", "NonkTube", "https://nonktube.com/", "nonktube.com",
        @"<div class=""video-block\b", @"<a class=""thumb"" href=""(https://nonktube\.com/video/[A-Za-z0-9_\-]+/)""") with
    {
        AllowedHosts = new[] { "nonktube.com", "freeones.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/?sorting=latest" },
        ItemTitle = R(@"class=""title""[^>]*>\s*(?:<a[^>]*>)?\s*([^<]+?)\s*<|\btitle=""(?!Video)([^""]+)"""),
        VideoPath = R(@"^video/[A-Za-z0-9_\-]+/$"),
        ListingPath = R(@"^/(?:page/\d+/)?(?:\?sorting=latest|\?s=[^&#]+(?:&[a-z]+=[^&#]*)?)?$|^/(?:category|categories|tag)/[A-Za-z0-9_\-]+/(?:page/\d+/)?$"),
    };

    public static readonly HtmlTubeSite TabooTube = Kvs("tabootube", "TabooTube", "https://www.tabootube.xxx/", new[] { "tabootube.xxx" },
        "/latest-updates/", @"<a href=""(https://www\.tabootube\.xxx/video/[A-Za-z0-9_\-]+)""\s+title=",
        @"<a[^>]+href=""(https://www\.tabootube\.xxx/categories/[A-Za-z0-9_\-]+/)""[^>]*\btitle=""([^""]+)""", "/search/{q}/", "-") with
    {
        ItemStart = R(@"<div class=""item item-\d+"),
        ItemThumb = R(@"<img class=""thumb[^""]*""[^>]*?\b(?:data-original|data-src|src)=""(https://[^""]+)"""),
        VideoPath = R(@"^video/[A-Za-z0-9_\-]+$"),
    };

    public static readonly HtmlTubeSite XFreeHd = new()
    {
        Id = "xfreehd",
        DisplayName = "XFreeHD",
        BaseUrl = "https://beta.xfreehd.com/",
        AllowedHosts = new[] { "xfreehd.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/videos?o=mr" },
        ItemStart = R(@"<div class=""well well-sm vidthumbmoveobjects"),
        ItemLink = R(@"<a class=""video-link"" href=""https://beta\.xfreehd\.com(/video/\d+/[A-Za-z0-9_\-]+)"""),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""([^""]+)"""),
        ItemDuration = R(@"duration-new"">\s*([^<]+?)\s*<"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)""|<a[^>]+href=""([^""]+)""[^>]*>\s*(?:&raquo;|»|Next)"),
        VideoPath = R(@"^video/\d+/[A-Za-z0-9_\-]+$"),
        ListingPath = R(@"^/(?:videos\?o=mr(?:&page=\d+)?|search\?search_query=[^&#]+(?:&page=\d+)?|videos/[a-z0-9\-]+(?:\?page=\d+)?)$"),
        SearchTemplate = null, // search pages return no result tiles (2026-10-11)
    };

    public static readonly HtmlTubeSite FreeusePorn = new()
    {
        Id = "freeuseporn",
        DisplayName = "Freeuse Porn",
        BaseUrl = "https://www.freeuseporn.com/",
        AllowedHosts = new[] { "freeuseporn.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/videos?o=mr&page=1" },
        ItemStart = R(@"<a href=""/video/\d+/"),
        ItemLink = R(@"<a href=""(/video/\d+/[A-Za-z0-9_\-]+)"""),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"<img src=""(https://[^""]+)"""),
        ItemDuration = R(@">\s*(\d{1,2}:\d{2}(?::\d{2})?)\s*<"),
        NextPage = R(@"class=""page-item active""><a[^>]*>\d+</a></li><li[^>]*><a class=""page-link"" href=""([^""]+)"""),
        VideoPath = R(@"^video/\d+/[A-Za-z0-9_\-]+$"),
        ListingPath = R(@"^/(?:videos\?o=mr&page=\d+|search/videos/[^/?#]+\?o=mr&page=\d+)$"),
        SearchTemplate = "/search/videos/{q}?o=mr&page=1",
        SearchSpace = "-",
    };

    public static readonly HtmlTubeSite Porno1Hu = Kvs("porno1hu", "Porno1.hu (Hungarian)", "https://porno1.hu/", new[] { "porno1.hu" },
        "/friss-porno/", @"<a href=""(https://porno1\.hu/szexvideo/\d+/[A-Za-z0-9_\-]+/)""\s+title=",
        @"<a class=""item"" href=""(https://porno1\.hu/kategoriak/[A-Za-z0-9_\-]+/)"" title=""([^""]+)""", "/kereses/{q}/", "-") with
    {
        ItemStart = R(@"<div class=""item p1ppnd"),
        VideoPath = R(@"^szexvideo/\d+/[A-Za-z0-9_\-]+/$"),
        ListingPath = R(@"^/(?:friss-porno/(?:\d+/)?|kategoriak/[A-Za-z0-9_\-]+/(?:\d+/)?|kereses/[^/?#]+/(?:\d+/)?)$"),
        CategoriesPage = "/kategoriak/",
        CategoryPath = R(@"^kategoriak/[A-Za-z0-9_\-]+/$"),
        Streams = StreamStrategy.SourceTags, // KVS-based, but serves plain <source> tags
    };

    /// <summary>
    /// Active profiles. <see cref="PeekVids"/> is kept for reference but not enabled: the site
    /// answers 429 to EmbyTube's honest User-Agent while serving browsers (bot blocking), and
    /// EmbyTube does not imitate browsers (2026-10-11). <see cref="SextubeNl"/> is not enabled either:
    /// its tokenized stream host answers 403 to every request variant. <see cref="LuxureTv"/> is not
    /// enabled: the site answers 403 to .NET's HTTP client while curl gets 200 (client fingerprinting).
    /// </summary>
    public static IReadOnlyList<HtmlTubeSite> All { get; } = new[]
    {
        DrTuber, TnAflix, PornOne, SuperPorn, PlayVids,
        Xozilla, ThreeMovs, VaginaNl, Poldertube, HelloPorn, HomoXxx, MrSexe,
        CumLouder, HomeMoviesTube, HypnoTube, JustPorn, NonkTube, TabooTube, XFreeHd, FreeusePorn, Porno1Hu,
    };

    // KVS ("Kernel Video Sharing") sites: tiles link /videos/<id>/<slug>/, plain flashvars streams.
    private static HtmlTubeSite Kvs(string id, string name, string baseUrl, string[] hosts, string newest, string link, string category, string search, string space)
    {
        var host = new Uri(baseUrl).Host;
        return new HtmlTubeSite
        {
            Id = id,
            DisplayName = name,
            BaseUrl = baseUrl,
            AllowedHosts = hosts,
            Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = newest },
            ItemStart = R(link),
            ItemLink = R(link),
            ItemTitle = R(@"(?:\balt=""([^""]+)""|\btitle=""([^""]+)""|class=""title"">\s*([^<]+?)\s*<)"),
            ItemThumb = R(@"\b(?:data-original|data-src|thumb)=""(https://[^""]+\.(?:jpg|jpeg|webp|png)[^""]*)"""),
            ItemDuration = R(@"class=""(?:duration|time)"">\s*([\d:ms]+)\s*<"),
            NextPage = R(@"<li class=""next""><a (?:data-)?href=""([^""]+)""|<link rel=""next"" href=""([^""]+)""|<link href=""([^""]+)"" rel=""next"""),
            VideoPath = R(@"^videos/\d+/[A-Za-z0-9_\-]+/$"),
            ListingPath = R(@"^/(?:[a-z\-]+/(?:\d+/)?|categories/[A-Za-z0-9_\-]+/(?:\d+/)?|search/[^/?#]+/(?:\d+/)?|search_videos/\?q=[^&#]+(?:&from_videos=\d+|&page=\d+)?)$"),
            CategoriesPage = "/categories/",
            CategoryLink = R(category),
            CategoryPath = R(@"^categories/[A-Za-z0-9_\-]+/$"),
            SearchTemplate = search,
            SearchSpace = space,
            Streams = StreamStrategy.KvsFlashvars,
        };
    }

    // Dutch WordPress tube theme (Poldertube, Sextube.nl): contentUrl MP4 on the site's stream host.
    private static HtmlTubeSite DutchWordPress(string id, string name, string baseUrl, string domain, string start, string link) => new()
    {
        Id = id,
        DisplayName = name,
        BaseUrl = baseUrl,
        AllowedHosts = new[] { domain },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/" },
        ItemStart = R(start),
        ItemLink = R(link, singleLine: true),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"\b(?:data-src|src)=""((?:https:)?//[^""]+\.(?:jpg|jpeg|webp|png)[^""]*)"""),
        ItemDuration = R(@"(?:duration|tion)""[^>]*>\D*?(\d{1,2}:\d{2}(?::\d{2})?)"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)""|<a class=""next page-link"" href=""([^""]+)"""),
        VideoPath = R(@"^[A-Za-z0-9_\-]+/?$"),
        ListingPath = R(@"^/(?:page/\d+/)?(?:\?filter=latest|\?s=[^&#]+(?:&[a-z]+=[^&#]*)?)?$|^/(?:c|category|tag)/[A-Za-z0-9_\-]+/(?:page/\d+/)?$"),
        SearchTemplate = "/?s={q}",
    };

    // hello.porn / homo.xxx: same platform as the Pornhat network but tiles link /videos/<id>/.
    private static HtmlTubeSite PrivatehostItems(string id, string name, string baseUrl, string domain) => new()
    {
        Id = id,
        DisplayName = name,
        BaseUrl = baseUrl,
        AllowedHosts = new[] { domain, "privatehost.com" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/new/" },
        ItemStart = R(@"<div class=""item\s+"""), // tiles are class="item "; menus use class="item item-…"
        ItemLink = R(@"<a href=""(?:https://" + System.Text.RegularExpressions.Regex.Escape(domain) + @")?(/videos/\d+/)"""),
        ItemTitle = R(@"\btitle=""([^""]+)"""),
        ItemThumb = R(@"\bdata-(?:src|original)=""(https://[^""]+)"""),
        ItemDuration = R(@"duration_item"">\s*([\d:]+)\s*<"),
        NextPage = R(@"<a href=""([^""]+)""[^>]*>\s*Next\b"),
        VideoPath = R(@"^videos/\d+/$"),
        ListingPath = R(@"^/(?:new/(?:\d+/)?|search/[^/?#]+/(?:\d+/)?)$"),
        SearchTemplate = "/search/{q}/",
        SearchSpace = "-",
    };

    // PlayVids and PeekVids share a platform (cards, /tgs/ categories, HLS on userscontent.net).
    private static HtmlTubeSite PlayVidsFamily(string id, string name, string baseUrl, string domain, string home, string link, string videoPath) => new()
    {
        Id = id,
        DisplayName = name,
        BaseUrl = baseUrl,
        AllowedHosts = new[] { domain, "userscontent.net" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Featured] = home },
        ItemStart = R(@"<div class=""card\s+[^""]*(?:itemVideo|video-item)"),
        ItemLink = R(link),
        ItemTitle = R(@"\balt=""([^""]+)"""),
        ItemThumb = R(@"<img class=""card-img-top""[^>]*\bsrc=""(https://[^""]+)"""),
        ItemDuration = R(@"class=""duration[^""]*"">\s*([\d:]+)\s*<"),
        NextPage = R(@"<a class=""next"" href=""([^""]+)"""),
        VideoPath = R(videoPath),
        ListingPath = R(@"^/(?:\?page=\d+|Trending-Porn(?:\?page=\d+)?|(?:tgs|category)/[A-Za-z0-9_\-%]+(?:\?page=\d+)?|videos\?q=[^&#]+(?:&page=\d+)?)$"),
        CategoriesPage = "/categories",
        CategoryLink = R(@"<li>\s*<a href=""(/(?:tgs|category)/[A-Za-z0-9_\-%]+)"">\s*([^<]+?)\s*</a>"),
        CategoryPath = R(@"^(?:tgs|category)/[A-Za-z0-9_\-%]+$"),
        SearchTemplate = "/videos?q={q}",
    };
}
