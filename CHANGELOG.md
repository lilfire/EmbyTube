# Changelog

## 0.1.0 (unreleased)

- Emby channel plugin (single net6.0 DLL, Emby SDK 4.9.1.90), off by default, rated Adult.
- Provider contract (`ITubeProvider`), typed provider errors, provider registry.
- Synthetic test source with categories, sorts, saved search, paging and MP4/HLS test streams.
- On-demand playback via `IRequiresMediaInfoCallback`; no stream URLs stored.
- Bounded "Next page" paging; channel re-index after settings change.
- SSRF protection (host allowlist, public-IP check at connect time, redirect checks).
- Log redaction (host only), admin settings page.
- XVideos provider: categories, best-of-month, newest, saved search, paging, on-demand
  HLS/MP4 resolve, optional SFW thumbnails. Offline fixture tests + opt-in live test.
- xHamster provider: sidebar categories, best, newest, saved search (≤ 10 pages per
  robots.txt), plain preload HLS only (obfuscated player URLs are not decoded).
- XNXX provider (shares the XVideos parser via site profiles): best-of-month, featured,
  categories, saved search, HLS + MP4.
- Pornhub provider (experimental): browse/categories/search work; playback usually fails
  because the CDN refuses ffmpeg. Never sends age-disclaimer cookies.
- Pornhat network provider (pornhat.com, ok.xxx, perfectgirls.xxx, ok.porn): newest,
  channels, search; plain `<source>` HLS, redirects resolved to the CDN by the provider.
- `SafeHttpClient.ResolveRedirectsAsync` (allowlist checked on every hop).
- Generic HTML tube engine (`HtmlTubeProvider` + declarative `HtmlTubeSite` profiles) with
  DrTuber, TNAFlix, PornOne, SuperPorn and PlayVids; three plain stream strategies.
- HTML engine: KVS `flashvars` (plain URLs only), sub-page and `contentUrl` stream strategies,
  Referer-requiring sources, `15m:00s`/`28 min` durations, protocol-relative thumbnails; HLS
  detected from the final Content-Type. New profiles: Xozilla, 3Movs, Vagina.nl, Poldertube.nl,
  hello.porn, homo.xxx, MrSexe.
- HTML engine: relative sources/thumbnails/next links (fixed a Linux-only `file:///` URI bug),
  page templates for sites without a next link, per-site stream filter (ad clips), `8m`
  durations. New profiles: CumLouder, HomeMoviesTube, HypnoTube, JustPorn, NonkTube, TabooTube,
  XFreeHD, Freeuse Porn, Porno1.hu. Remaining Cumination candidates evaluated
  (docs/sites/html-tubes.md).
- `tools/emby_playback_check.py`: per-source PlaybackInfo + streaming check through Emby.
- Site triage of all 185 Cumination tube sites (`docs/site-triage.md`).
- Variants with required headers disable direct play; Emby forwards the headers to ffmpeg.
- Settings page reads the source list from the server (`ProviderCatalog`).
- Unique build revision per build (Emby's settings-page ETag follows the plugin version).
- `SafeHttpClient` prefers IPv4 and lets a provider's User-Agent replace the default.
- Crawl budget settings: maximum categories per source (default 40) and pages per
  category (default 1).
