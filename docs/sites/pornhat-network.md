# Site notes: Pornhat network

_Checked 2026-10-11 from a European IP address. Implemented on the owner's request; a
Terms-of-Service review is still not documented here._

One provider class (`PornhatNetworkProvider`) with a site profile per domain.

| Site | Provider id | Status |
|---|---|---|
| pornhat.com | `pornhat` | ✅ live test + Emby (browse, channels, PlaybackInfo, transcode segment) |
| ok.xxx | `okxxx` | ✅ live test |
| perfectgirls.xxx | `perfectgirls` | ✅ live test (channels are `/channels/<slug>/`, some are empty) |
| ok.porn | `okporn` | ✅ live test |
| hello.porn, homo.xxx | `helloporn`, `homoxxx` | ✅ via the generic HTML engine (`/videos/<id>/` tiles), see html-tubes.md |
| max.porn, pornstars.tube | — | not yet: pornstar directories without a video listing on the home page |

| Need | Request | Notes |
|---|---|---|
| Newest | `GET /`, then `/2/`, `/3/`… | No popular/most-viewed listing found |
| Categories | `GET /channels/` → `<a title="…" href="/sites/<slug>/">` (or `/channels/<slug>/`) | Channels are studios/creators; capped by *Maximum categories* |
| Search | `GET /search/<words-joined-by-dash>/`, then `/2/` | 404 → empty |
| Playback | `GET /video/<id>/` → `<source src="https://<site>/get_file/…" label="720p">` | Redirects to HLS on `*.cdn.privatehost.com`; resolved by the provider so Emby gets the final URL |

- No Referer, cookies or browser User-Agent needed; ffmpeg's User-Agent is accepted.
- robots.txt disallows only player/async/action endpoints; none are used.
- Page tokens are limited to listing paths (`/<n>/`, `/sites|channels/<slug>/…`, `/search/…`).

Functional reference: Cumination `pornhat.py` (GPL, commit 9429c83a); no code copied. The
module's KVS (`kt_player`) branch is not used; these sites serve plain `<source>` tags.
