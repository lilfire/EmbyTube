# Site notes: XVideos

_Checked 2026-10-11 from a European IP address._

## Decision

The project owner decided on 2026-10-11 to implement XVideos as the first real provider
(spec STOP/GO 2 decided by the owner). A review of the site's Terms of Service is still
**not documented here** and remains the owner's responsibility before public release.

## What the provider does

| Need | Request | Notes |
|---|---|---|
| Categories | `GET /` → links `/c/<Name>-<n>` | 38 categories observed; cached 6 h |
| Popular | `GET /best/<previous yyyy-MM>`, then `/best/<month>/<n>` | "Best of month" pages have no next link; the provider counts pages |
| Newest | `GET /new/1`, then the page's `next-page` link | |
| Category | `GET /c/<Name>-<n>`, then `next-page` link (`/c/…/1`) | |
| Search | `GET /?k=<query>`, then `next-page` link (`&p=n`) | Used for saved searches |
| Playback | `GET /video.<eid>/<slug>` → `html5player.setVideoHLS`, `setVideoUrlHigh`, `setVideoUrlLow` | Resolved at playback time; MP4 links are signed (`?secure=`) and expire |
| HLS height | `GET` the HLS master playlist → highest `RESOLUTION` | Hint only |

- Plain public pages, no login, no API key, honest `User-Agent: EmbyTube/<version>`.
- No Referer, cookies or other headers are needed by the CDN (`*.xvideos-cdn.com`).
- `robots.txt` disallows only `/video-vote/` and `/account/`; neither is used.
- Rate: 1 request/s per host. A refresh with MaxPages=2 made 83 listing requests (~84 s).
- **Access controls are respected.** If the site withholds streams (for example an
  age-verification gate in some regions), resolve fails with `AccessDenied`. The provider
  never sets verification cookies, changes region/orientation, or spoofs a browser.

## Data model

- Video id: `<eid>/<slug>` (the site returns 404 without the slug). Stable across refreshes.
- Listing fields: title, thumbnail (`data-src`, or `data-sfwthumb` if *SFW thumbnails* is
  on), duration ("10 min", "1 h 5 min"), quality badge as tag ("1080p").
- Video page: `og:title`, `og:duration` (seconds), `uploadDate`, 16:9 thumbnail.

## Markup the parser relies on

Two listing layouts exist (`div id="video_<eid>"` on most pages, `div id="video-thumb-<eid>"`
on `/best`). If neither is found, the provider raises `ParseChanged` and the folder shows
"The source has changed…". Live check: `dotnet test --filter Category=Live -e EMBYTUBE_LIVE_TESTS=1`.

## Functional reference

Cumination's `xvideos.py` (GPL-3.0-or-later, commit 9429c83a) was read for which pages and
player calls exist. Its code was not copied or translated (ADR-003); several of its
patterns (category `class="dyn"` links, home-page paging) no longer match the live site.
