# ADR-005: On-demand playback through IRequiresMediaInfoCallback

- Status: Accepted (M1 / Spike B, 2026-10-11)
- Evidence: `tools/emby_smoke.py` (16/16 PASS) + manual server-side stream checks below

## Decision

- `EmbyTubeChannel` implements `IRequiresMediaInfoCallback`. Indexed video items carry an
  empty `MediaSources` list; Emby calls `GetChannelItemMediaInfo(id)` when a client asks
  for `PlaybackInfo`, and `PlaybackService` resolves `providerId:videoId` **then**.
- No caching of resolved URLs. Every PlaybackInfo request resolves again, so replaying
  after a signed URL expired works by construction.
- One controlled retry for transient provider errors (`NetworkError`, `Unavailable`,
  `MediaExpired`). Never more.
- Every variant URL is checked by `UrlGuard` (allowlist + public-IP DNS check) before
  it is handed to Emby. Blocked variants are dropped and logged by host only.
- MediaSourceInfo: `Protocol=Http`, `IsRemote=true`, `Container=mp4|hls`,
  DirectPlay/DirectStream/Transcoding all allowed; Emby decides per client.
  `MediaSource.Id` is a stable hash of the item id + slot, never derived from the URL.
- `RequiredHttpHeaders` is set only when a provider requires headers, and such sources
  get `SupportsDirectPlay = false` (clients cannot add headers). **Verified 2026-10-11:**
  Emby 4.10.1 passes them to ffmpeg (`ffprobe -user_agent … -referer …`). Whether a CDN
  then accepts ffmpeg is site-specific (Pornhub's does not; docs/sites/pornhub.md).

## Evidence (Emby 4.10.1, Docker)

| Check | Result |
|---|---|
| PlaybackInfo MP4 item returns 1 source (`mp4`, DP/DS/TC true) | PASS |
| PlaybackInfo HLS item returns 1 source; Emby probed 5 renditions (184p–1080p, h264/aac) | PASS |
| Two PlaybackInfo calls → two resolves with different per-resolve tokens | PASS |
| `/Videos/{id}/stream.mp4?Static=true` via Emby, Range 0–1 MiB | 206, video/mp4 |
| Same, Range at 50 MiB (seek) | 206, video/mp4 |
| `/Videos/{id}/master.m3u8` (Emby transcoder from remote HLS) → first `.ts` segment | 200, 1.9 MB video/mp2t |
| SSRF test item (variant → 169.254.169.254) | dropped, `NoCompatibleStream`, no URL in PlaybackInfo |
| Log lines contain only `scheme://host` | PASS |

## Not yet verified (client matrix)

Emby Web in a real browser, Android TV, mobile: **NOT TESTED** (browser automation could
not reach the local test server). STOP/GO 1 is therefore *conditionally* GO: server-side
playback paths work; client playback must be confirmed manually (see `docs/compatibility.md`).
