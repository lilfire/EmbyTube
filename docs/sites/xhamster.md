# Site notes: xHamster

_Checked 2026-10-11 from a European IP address._

## Decision

Implemented on the project owner's request (2026-10-11). A Terms-of-Service review is
still **not documented here** and remains the owner's responsibility before public release.

## What the provider does

| Need | Request | Notes |
|---|---|---|
| Categories | `GET /` → `window.initials…sidebarProps.items` with `/categories/<slug>` URLs | 73 content categories (first one is geo-localized, e.g. "Norwegian"); cached 6 h. `/categories` itself lists mostly countries and is not used |
| Popular | `GET /best`, then `/best/{n}` from `paginationProps.pageLinkTemplate` | |
| Newest | `GET /newest`, then `/newest/{n}` | |
| Category | `GET /categories/<slug>`, then `/categories/<slug>/{n}` | |
| Search | `GET /search/<query>`, then `?page=n` | Stops after page 10 (robots.txt allows 2–10) |
| Playback | `GET /videos/<slug-id>` → `<link rel="preload" href="….m3u8">` | Plain H.264 HLS master (144p–720p+ seen); no Referer needed |
| HLS height | `GET` the master playlist → highest `RESOLUTION` | Hint only |

- All page data comes from the JSON the page embeds as `window.initials`; the parser finds
  `videoThumbProps`, `paginationProps`/`pagination`, `videoModel` and `sidebarProps`
  recursively, so moved sub-objects keep working.
- Honest `User-Agent: EmbyTube/<version>`, no cookies, 1 request/s per host.

## Restrictions respected

- **Obfuscated stream URLs are not decoded.** The player settings (`xplayerSettings.sources`)
  carry obfuscated URLs; Cumination decodes them with a de-obfuscation routine. EmbyTube
  does not: decoding a deliberately obscured URL is treated as circumventing a technical
  measure (spec §1/§6). If a video page offers no plain preload HLS, resolve fails with
  `Unsupported`.
- `<video src>` MP4 returns 403 without a Referer; it is not used (no header spoofing).
- robots.txt: `/embed/`, duration filters, search filters and search pages > 10 are
  disallowed and never requested.
- Items marked `isBlockedByGeo` are skipped; nothing tries to change region or orientation.

## Live check

`dotnet test tests/EmbyTube.Provider.Tests --filter FullyQualifiedName~XHamsterLiveTests -e EMBYTUBE_LIVE_TESTS=1`
→ PASS in 11 s (2026-10-11): ≥20 categories, popular/newest/category with page 2, search
with next page, and ≥2 of the 3 newest videos resolved to `*.xhcdn.com` HLS.

## Functional reference

Cumination's `xhamster.py` (GPL-3.0-or-later, commit 9429c83a) was read for which pages
and page-state keys exist. No code was copied or translated (ADR-003).
