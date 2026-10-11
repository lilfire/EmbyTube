# Site notes: HTML tube profiles (generic engine)

_Checked 2026-10-11 from a European IP address. Implemented on the owner's request;
Terms-of-Service reviews are still not documented here._

`HtmlTubeProvider` is a generic engine for classic HTML tube sites; each site is a
declarative `HtmlTubeSite` profile (`src/EmbyTube.Providers.Html/HtmlTubeSites.cs`): tile
regexes, sort paths, category page, search template, allowed listing/video paths, and one of
five plain stream strategies (source tags/contentUrl, DrTuber JSON, TnAflix AJAX, KVS flashvars without de-obfuscation, sub-page). Streams are resolved at playback time, redirects are followed
and allowlist-checked, no cookies/Referer, honest User-Agent.

| Site | Id | Listings | Categories | Search | Streams | Status (live test + Emby) |
|---|---|---|---|---|---|---|
| DrTuber | `drtuber` | Featured (`/`) | ~1 500 tags (`/categories/`) | `/search/videos/<q>` | `/player_config_json/?vid=` → MP4 (needs `Accept: application/json`) | ✅ MP4 480p, Range/seek |
| TNAFlix | `tnaflix` | Newest (`/new/1`) | 49 (`/categories`) | `/search.php?what=` | page `embedUrl` → `/ajax/video-player/` → MP4 144–720p | ✅ MP4 720p |
| PornOne | `pornone` | Newest (`/newest/`) | 510 (`/categories/`) | `/search?q=` | `<source>` MP4 up to 1080p | ✅ MP4 1080p |
| SuperPorn | `superporn` | Newest (`/`) | — (no category list found) | — (robots.txt `Disallow: /*?q=*`) | `<source>` MP4 | ✅ MP4 |
| Xozilla | `xozilla` | Newest (`/latest-updates/`) | 185 (`/categories/`) | `/search/<q>/` | KVS `flashvars` plain `video_url` → `*.ahcdn.com` MP4 | ✅ MP4 720p |
| 3Movs | `3movs` | Newest (`/videos/`) | 82 | `/search_videos/?q=` | KVS `flashvars` plain → `mjedge.net` MP4 | ✅ MP4 |
| Vagina.nl | `vaginanl` | Newest | 55 | `/sexfilms/search?q=` | `<source>` MP4 on `mjedge.net` | ✅ MP4 |
| Poldertube.nl | `poldertube` | Newest (`/`) | — | `/?s=` | `contentUrl` MP4 on `streamN.poldertube.nl` | ✅ MP4 |
| hello.porn | `helloporn` | Newest (`/new/`) | — | `/search/<q>/` | `<source>` → HLS on `privatehost.com` | ✅ HLS, transcode segment |
| homo.xxx | `homoxxx` | Newest (`/new/`) | — | `/search/<q>/` | `<source>` → HLS on `privatehost.com` | ✅ HLS, transcode segment |
| MrSexe | `mrsexe` | Newest (`/`, pages via the "suivant" link) | 72 | — (site search returns an empty body) | video page → `/inc/clic.php` → plain MP4 on `dlms3.mrsexe.tv` | ✅ MP4 |
| CumLouder | `cumlouder` | Newest (`/porn/`) | 72 | — (`/search` answers 403) | `<source>` MP4 (studio's own CDN) | ✅ MP4 |
| HomeMoviesTube | `homemoviestube` | Newest (`/most-recent/`) | 35 channels | `/search/<q>/` | relative `<source>` MP4 (ad clip filtered out) | ✅ MP4 (after the Linux URI fix) |
| HypnoTube | `hypnotube` | Newest, Popular (month) | 95 channels | — (no result tiles) | `<source>` MP4 | ✅ MP4 |
| JustPorn | `justporn` | Newest (`/porn-videos/`, pages synthesized: no next link) | 112 | `/search/<q>/` | KVS plain → MP4 | ✅ MP4 720p |
| NonkTube | `nonktube` | Newest | — | `/?s=` | `contentUrl` MP4 on FreeOnes' CDN | ✅ MP4 |
| TabooTube | `tabootube` | Newest (`/latest-updates/`) | 8 | `/search/<q>/` | KVS plain → MP4 | ✅ MP4 |
| XFreeHD | `xfreehd` | Newest (`/videos?o=mr`) | — | — (no result tiles) | `<source>` → CDN MP4 | ✅ MP4 |
| Freeuse Porn | `freeuseporn` | Newest | — | `/search/videos/<q>` | `<source>` MP4 | ✅ MP4 |
| Porno1.hu | `porno1hu` | Newest (`/friss-porno/`) | 197 | `/kereses/<q>/` | `<source>` MP4 | ✅ MP4 |
| PlayVids | `playvids` | Featured (`/?page=1`) | 408 (`/tgs/…`, `/category/…`) | `/videos?q=` | `<source>` HLS on `userscontent.net` | ✅ HLS, transcode segment |

robots.txt notes: DrTuber disallows `/player/` and `/ajax/` (not `/player_config_json/`);
PornOne disallows only sorted search variants; SuperPorn disallows all `?q=` search.

## Evaluated and not enabled in this batch

| Site | Reason |
|---|---|
| PeekVids | Answers **429** to EmbyTube's User-Agent while serving a browser User-Agent (bot blocking). Profile kept in code, not enabled. |
| Eporner | Playback requires computing a request token from the page (`vid`/`hash` → base-36) for a hidden XHR endpoint, i.e. re-implementing an anti-hotlinking scheme. The official API only offers embed players. |
| Beeg | The public JSON API (`store.externulls.com`) no longer returns `hls_resources`; streams appear to need a player-side signature (`check`). Not pursued. |
| WOW.xxx | 403 with a bot challenge to EmbyTube's User-Agent. |
| TubXporn | Unreachable (connection failure, 2026-10-11). |
| X-X-X Video | Plain KVS streams, but listings are "siterip" uploads of paid-site content (piracy). |
| 12Milf, PornTube.nl, Sextube.nl | Stream hosts answer 403 (tokenized) with and without Referer. Sextube.nl profile kept, not enabled. |
| LuxureTV | Pages answer 403 to .NET's HTTP client while curl gets 200 (client fingerprinting); streams also need a Referer. Profile kept, not enabled. |
| Heroero, WatchErotic | Nude scenes cut from mainstream films (celebrities, no consent, copyrighted). |
| PornTrex, WhoresHub | Full premium studio content; WhoresHub uses deliberately mangled titles. Piracy. |
| BeemTube | Commercial JAV (piracy); stream host unreachable. |
| 6XTube, Blendporn, YouCrazyX, YrPrno | Same network; studio scenes named with release dates ("siterip" pattern). Excluded consistently with X-X-X. |
| Noodlemagazine | Indexes VK-hosted copies. |
| PMVHaven | Music-video remixes of third-party clips (unlicensed clips and music). |
| Rule34 Video | Technically feasible, excluded: animated "rule 34" content may sexualize characters who are minors; such drawn/animated material is illegal in Norway (straffeloven § 311). |
| FreePornVideos, PornGO, PornSlash | No plain stream (PornSlash hides it behind an encrypted id; PornGO's quality links return HTML). |
| Trannyteca, AnyBunny | Aggregators embedding content hosted elsewhere. |
| Incesto69 | Pages rendered by JavaScript; no plain listing. |
| AbsoluPorn | Unreachable. |
| Porndig | Plain H.264 MP4 available, but listings are POST-based AJAX (`load_more_posts`); needs a dedicated provider. Deferred. |

## Known limitations

- Relative `<source src="/…">` must be resolved explicitly: on Linux (Emby in Docker),
  `Uri.TryCreate("/path", UriKind.Absolute)` succeeds as `file:///path`. Fixed and covered by a
  test (`Relative_sources_resolve_against_the_page_on_every_os`).

- DrTuber's category page is an alphabetical tag cloud; with *Maximum categories* = 40 the
  first tags shown are numeric ("18 …"). Raise the limit or rely on search.
- Heights are only known where the site labels sources (PlayVids/SuperPorn show "HLS"/"MP4").

Functional references: the corresponding Cumination modules (GPL, commit 9429c83a); no code copied.
