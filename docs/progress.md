# Progress

_Last updated: 2026-10-11_

## Test environment

Emby Server 4.10.1.0, Docker `emby/embyserver@sha256:997e8c1e…` (linux/amd64, .NET 8.0.28),
local admin user only. Build: .NET SDK 8.0.416. Commands:

```sh
dotnet build -c Release
dotnet test                                   # 299 tests, all passing
dotnet test --filter Category=Live -e EMBYTUBE_LIVE_TESTS=1   # live checks: XVideos, xHamster, XNXX, Pornhub, Pornhat ×4 (all PASS)
python tools/emby_smoke.py --enable           # 16/16 PASS against the test server
python tools/emby_access_test.py              # exits 1: known access gap, see security doc
```

## Done

| Issue | Status | Evidence |
|---|---|---|
| EBT-001 Audit sources/licenses/SDK | Done | `docs/licensing.md` (SHAs, file matrix), ADR-001…003 |
| EBT-002 Empty Emby channel plugin | Done | Loads on 4.10.1; channel visible; disabled by default |
| EBT-003 MockProvider + channel folders | Done | Sources → Popular/Newest/Categories/saved searches → videos → Next page |
| EBT-004 Media-info callback, direct MP4 | Done (server-side) | ADR-005; PlaybackInfo + Range/seek via Emby |
| EBT-005 HLS + client matrix | Partly | HLS probe + transcode PASS; **client matrix NOT TESTED** |
| EBT-006 Core provider contracts | Done | `ITubeProvider`, typed errors, `ProviderContractTests` |
| EBT-007 Caching / pagination | Done for v0.1 | Bounded "Next page" (MaxPages=3), refresh-on-save, no stream caching (ADR-004) |
| EBT-010 Security / SSRF / logging | Groundwork | `UrlGuard`, `SafeHttpClient`, redaction, negative tests |
| EBT-011 Adult access control | Investigated | Gap found (direct id → PlaybackInfo). **Release blocker.** |
| EBT-008 XVideos adapter | Done | Categories (38), best-of-month, newest, saved search, paging; fixture + live tests; `docs/sites/xvideos.md` |
| EBT-014 Second provider (xHamster) | Done (API level) | Fixture + live tests PASS; Emby 4.10.1: browse/search/categories + PlaybackInfo 4/4 + HLS transcode segment PASS |
| EBT-014b XNXX | Done | Fixture + live tests; Emby browse, PlaybackInfo, transcode segment PASS |
| EBT-014c Pornhub | Partial (experimental) | Browse/categories/search PASS; playback FAIL (CDN 410 to ffmpeg, no evasion) |
| Pornhat network (4 of 8 sites) | Done | Live tests ×4; Emby browse/PlaybackInfo/transcode for pornhat |
| HTML engine batch | Done | DrTuber, TNAFlix, PornOne, SuperPorn, PlayVids: live tests + `tools/emby_playback_check.py` PASS; PeekVids/Eporner/Beeg not enabled (docs/sites/html-tubes.md) |
| HTML engine batch 2 | Done | Xozilla, 3Movs, Vagina.nl, Poldertube, hello.porn, homo.xxx, MrSexe: live + Emby playback PASS; 7 others not enabled (docs/sites/html-tubes.md) |
| HTML engine batch 3 | Done | 9 sources (CumLouder … Porno1.hu): live PASS; Emby playback PASS for all 9 (HomeMoviesTube after a Linux-only URI fix); 22 others evaluated and excluded |
| Site triage (185 sites) | Done | `docs/site-triage.md`: 50 candidates, 121 excluded with reasons |
| SpankBang, AWM Network | Not implemented | Cloudflare challenge / aggregator with de-obfuscation (docs/sites/not-implemented.md) |
| EBT-009 Resolver for real source | Done (server-side) | PlaybackInfo HLS 720p + 2 MP4, fresh per request; Emby Range/seek + HLS transcode PASS |

## Blocked / open

1. **Client playback not yet confirmed** in Emby Web, a TV client or mobile. Browser
   automation could not reach the local server. Needs a manual run of the checklist in
   `docs/compatibility.md`. STOP/GO 1 is *conditionally GO* on server-side evidence.
2. **Access gap** (security doc, ⚠): users without channel access can fetch item metadata
   and PlaybackInfo by numeric id. Blocks public beta (STOP/GO 3).
3. **Real sources implemented by owner decision** (STOP/GO 2 taken by the owner,
   2026-10-11): XVideos, xHamster, XNXX, Pornhub (experimental). Documented Terms-of-Service
   reviews are still missing; required before public release.
4. Listing order is alphabetical in Emby (ADR-004).
5. Not tested: native Windows/macOS/NAS servers; Emby 4.9. (Header forwarding to ffmpeg
   is verified; header-requiring sources have direct play disabled.)
6. Emby's own log writes full signed stream URLs (ffprobe/HttpClient lines); documented.
7. PeekVids blocks non-browser User-Agents (429); Porndig needs a POST/AJAX listing provider.
8. Refresh time grows with sources: five sources (MaxPages=2, 40 categories) took ~240 s
   and ~50 listing requests per source.

## Next tasks (in order)

1. Manual client run (Emby Web + one TV/mobile client) with the mock source; fill the matrix.
2. Investigate a supported way to enforce channel access on direct item/PlaybackInfo
   requests; report to Emby if none exists.
3. Terms-of-Service write-up for XVideos in `docs/sites/xvideos.md`.
4. Crawl budget measured with all three sources (MaxPages=2, 40 categories × 1 page):
   full refresh 105–125 s; xHamster 46 and XVideos 44 listing requests (XVideos was 83 uncapped).
6. CI release artifacts with SHA-256 (workflow builds and tests today).
