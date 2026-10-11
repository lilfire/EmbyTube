# Site notes: Pornhub — EXPERIMENTAL

_Checked 2026-10-11 from a European IP address. Implemented on the owner's request; a
Terms-of-Service review is still not documented here._

## Status

Browsing works; **playback in Emby currently fails** for most videos. The provider is
available in settings, labelled experimental.

| Check | Result |
|---|---|
| Categories (115 on the site; numeric `/video?c=N` and `/categories/<slug>`) | PASS |
| Most viewed (`/video?o=mv`), Newest (`/video?o=cm`), page 2 | PASS |
| Search (`/video/search?search=`), pages ≤ 9 | PASS (a search without hits returns HTTP 404 → empty list) |
| Resolve (`flashvars.mediaDefinitions`, HLS per quality) | PASS |
| Emby PlaybackInfo | Sources returned (direct play off, `RequiredHttpHeaders` set) |
| Emby ffprobe/transcode | **FAIL**: CDN returns 410 to ffmpeg |

## Why playback fails

- The HLS URLs require `Referer: https://www.pornhub.com/` (412 without it). EmbyTube sends
  that plus its own `User-Agent` as required headers; **Emby forwards them to ffmpeg**
  (`ffprobe -user_agent "EmbyTube/…" -referer "…"`, verified in the Emby log).
- One of Pornhub's CDN variants (`hv-h.phncdn.com`, query `?h=&e=&f=`) still answers **410**
  to ffmpeg and to .NET's HttpClient, while curl gets 200 with the identical URL and headers.
  HTTP version, `Accept`, IP family and duplicate headers were ruled out; the remaining
  difference is the client implementation itself (TLS/client fingerprinting).
- Imitating a browser or another client to get past that is bot-detection evasion, which
  the spec forbids. The other CDN variant (`?validfrom=&validto=&ipa=&hash=`) does work.

## Restrictions respected

- No cookies at all. In particular the provider never sends the
  `accessAgeDisclaimerPH` / `accessAgeDisclaimerUK` acceptance cookies that some scrapers
  pre-set to skip the age disclaimer and the UK age-verification gate.
- If a video page provides no streams, resolve fails with `AccessDenied`.
- robots.txt: no `o=` sort or filters on search, search pages ≤ 9, no `cc=`, `hd=1`,
  duration filters; premium-only items (`premiumIcon`) are skipped.
- Only the main result list (`ul#videoCategory` / `ul#videoSearchResult`) is read; the
  page's other lists (hottest, recommended, premium) are ignored.

Functional reference: Cumination `pornhub.py` (GPL, commit 9429c83a); no code copied, and
its age-disclaimer cookies are deliberately not reproduced.
