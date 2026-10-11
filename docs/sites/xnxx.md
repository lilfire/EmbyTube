# Site notes: XNXX

_Checked 2026-10-11 from a European IP address. Implemented on the owner's request; a
Terms-of-Service review is still not documented here._

XNXX runs on the same platform as XVideos. The provider shares `XVideosParser` through a
site profile (`XvSite.Xnxx`) and lives in the XVideos project.

| Need | Request | Notes |
|---|---|---|
| Categories | `GET /` → `cats.write_thumb_block_list([...])` JSON (`tf` name, `u` path) | 161 entries, most are curated searches (`/search/<term>?id=…`); capped by *Maximum categories* |
| Popular | `GET /best/<previous yyyy-MM>`, then `/best/<month>/{n}` | |
| Featured | `GET /todays-selection`, then `/todays-selection/{n}` | XNXX has no "newest" listing; "Featured" sort was added to the core for this |
| Search | `GET /search/<query>`, then `/search/<query>/{n}` | Next link = the link after `class="active"` |
| Playback | `GET /video-<eid>/<n>/0/<slug>` → `html5player.setVideoHLS/UrlHigh/UrlLow` | No Referer needed; ffmpeg accepted |

- Video id is the full path after `/video-` (the site 404s with the eid alone).
- Category ids and page tokens must be listing paths (`/search/…`, `/best/…`,
  `/todays-selection…`, `/hits…`); anything else is rejected before a request is made.
- robots.txt disallows only `/video-vote/` and `/account/`.

Verified on Emby 4.10.1: browse, saved search, PlaybackInfo (HLS + 2 MP4, probed), Emby
transcoder segment. Live test: `XnxxLiveTests` PASS.

Functional reference: Cumination `xnxx.py` (GPL, commit 9429c83a); no code copied.
