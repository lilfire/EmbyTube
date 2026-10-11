# Compatibility

Never assume compatibility. Each cell is `PASS`, `FAIL` or `NOT TESTED`, with date and version.

## Build

| Item | Value |
|---|---|
| Target framework | net6.0 (ADR-001) |
| Emby SDK | MediaBrowser.Server.Core / MediaBrowser.Common 4.9.1.90 |
| Build SDK | .NET SDK 8.0.x (`global.json`, rollForward latestFeature) |
| Output | single `EmbyTube.dll`, no dependencies |

## Servers

| Server | OS / runtime | Plugin loads | Channel visible | Playback (server-side) | Date |
|---|---|---|---|---|---|
| Emby 4.10.1.0 (Docker `emby/embyserver`, digest `sha256:997e8c1e…`) | Linux amd64 (WSL2), .NET 8.0.28 | PASS | PASS | PASS (MP4 range/seek, HLS transcode) | 2026-10-11 |
| Emby 4.9.x | — | NOT TESTED | NOT TESTED | NOT TESTED | |
| Emby native Windows | — | NOT TESTED | NOT TESTED | NOT TESTED | |
| Emby native macOS (.NET 6) | — | NOT TESTED | NOT TESTED | NOT TESTED | |
| NAS packages (Synology/QNAP/…) | — | NOT TESTED | NOT TESTED | NOT TESTED | |

## Providers on Emby 4.10.1 (API level, 2026-10-11)

| Provider | Categories | Popular | Newest | Saved search | Next page | PlaybackInfo | MP4 via Emby (Range/seek) | HLS via Emby transcoder |
|---|---|---|---|---|---|---|---|---|
| Test source (mock) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| xHamster | PASS (40 of 73, capped) | PASS (46) | PASS (46) | PASS (46 per search) | PASS (none in categories: CategoryMaxPages=1) | PASS (4/4: HLS 1080p/2160p, h264/aac probed) | n/a (HLS only) | PASS (first `.ts` segment, 140 KB) |
| XNXX | PASS (40 of 161, capped) | PASS (36) | n/a (Featured instead) | PASS | PASS | PASS (HLS + 2×MP4, probed) | PASS (Emby probe) | PASS (first `.ts` segment) |
| Pornhat network (pornhat) | PASS (40 channels) | n/a | PASS (60) | PASS | PASS | PASS (HLS 720/480/360p, probed) | n/a | PASS (first `.ts` segment) |
| DrTuber / TNAFlix / PornOne / SuperPorn | PASS (except SuperPorn: none) | — | PASS | PASS (SuperPorn: n/a) | PASS | PASS (MP4, probed) | PASS (static Range + seek) | n/a |
| Xozilla / 3Movs / Vagina.nl / Poldertube / MrSexe | PASS (Poldertube: none) | — | PASS | PASS (MrSexe: none) | PASS | PASS (MP4, probed) | PASS (static Range + seek) | n/a |
| CumLouder / HypnoTube / JustPorn / NonkTube / TabooTube / XFreeHD / Freeuse Porn / Porno1.hu | PASS where offered | — | PASS | PASS where offered | PASS | PASS (MP4, probed) | PASS (static Range + seek) | n/a |
| HomeMoviesTube | PASS | — | PASS | PASS | PASS | PASS (MP4, probed) | PASS (static Range + seek) | n/a |
| hello.porn / homo.xxx | — | — | PASS | PASS | PASS | PASS (HLS 720p, probed) | n/a | PASS (transcode segment) |
| PlayVids | PASS | n/a (Featured) | — | PASS | PASS | PASS (HLS, probed) | n/a | PASS (transcode segment) |
| Pornhub (experimental) | PASS | PASS | PASS (31) | PASS (404 = no hits) | PASS | sources returned, **probe FAIL** (CDN 410 to ffmpeg) | n/a | **FAIL** |
| XVideos | PASS (38) | PASS | PASS | PASS (0 hits for "Sintel") | PASS | PASS (HLS 720p + 2×MP4, fresh per request) | PASS | PASS |

## Clients (mock source)

| Client | Navigate | Images/metadata | MP4 | HLS | Seek | Replay later | Restricted user | Date |
|---|---|---|---|---|---|---|---|---|
| Emby API (PlaybackInfo, `tools/emby_smoke.py`) | PASS | PASS | PASS | PASS | PASS (server Range) | PASS (fresh resolve per request) | see security doc | 2026-10-11 |
| Emby Web (browser) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | |
| Android TV | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | |
| Mobile (Android/iOS) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | |

### How to fill in a client row

1. Install the plugin (README), enable it and the test source in *Plugins → EmbyTube*.
2. Open *EmbyTube → EmbyTube Test Source → Categories → Progressive MP4*, play an item,
   seek to the middle, stop, play again after 15 minutes.
3. Repeat with *HLS* and *Multiple variants*.
4. Open *Error cases*: items 1–3 must show an error, never play, never hang the client.
5. Log in as a user without channel access and confirm the channel is not visible.

## Known Emby behaviour that affects the plugin (4.10.1)

- Browsing is served from the index; only refresh calls the plugin (ADR-004).
- Listings are sorted by name; provider order is lost (ADR-004, open).
- `GET /Channels/{id}/Items` returns 400; use `/Users/{id}/Items?ParentId=`.
