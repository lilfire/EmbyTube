# EmbyTube

An [Emby Server](https://emby.media) channel plugin that **streams** videos from modular
online sources into the normal Emby clients: folders, thumbnails, metadata and playback
through Emby's own player. Nothing is downloaded into your media library.

> **Status: v0.1 development preview.** Sources: a synthetic test source, **XVideos**,
> **xHamster**, **XNXX**, the **Pornhat network** (4 sites) and **Pornhub** (experimental) — see [`docs/sites/`](docs/sites)
> and the full evaluation of all Cumination sites in [`docs/site-triage.md`](docs/site-triage.md).

> ⚠ **Adult content.** EmbyTube is intended for sources with adult content. It is off by
> default, and you must restrict who can see it (step 4). Read
> [`docs/security-and-privacy.md`](docs/security-and-privacy.md) before enabling it on a
> server with other users: there is a **known access gap** in v0.1.

## Requirements

- Emby Server 4.10.1 (tested). Other versions: see [`docs/compatibility.md`](docs/compatibility.md).
- Any OS Emby runs on; the plugin is a single platform-independent `EmbyTube.dll` (net6.0).

## Install

1. Build (`dotnet build -c Release`) or download `EmbyTube.dll` from a GitHub release and
   check its SHA-256.
2. Copy `EmbyTube.dll` into Emby's `plugins` folder
   (Docker: `/config/plugins`; Windows: `%AppData%\Emby-Server\programdata\plugins`;
   Linux packages: `/var/lib/emby/plugins`).
3. Restart Emby. Open *Settings → Plugins → EmbyTube*, tick **Enable EmbyTube** and the
   sources you want, and save. The channel re-indexes a few seconds later.
4. **Restrict access:** for every user who must *not* see EmbyTube, open
   *Settings → Users → (user) → Access*, untick **Enable access to all channels**, and leave
   EmbyTube unticked. New Emby users get access to all channels by default.

Update: replace the DLL and restart. Rollback: put the previous DLL back and restart.
Uninstall: delete the DLL, restart, then delete `plugins/configurations/EmbyTube.xml`.

## Using it

*Channels → EmbyTube → (source)* shows **Popular**, **Newest**, **Categories** and your
**saved searches** (set in the plugin settings, one per line). Long listings end with a
**Next page** folder. Streams are looked up when you press play, so links that expire are
always fresh.

## Sources

| Source | Browse | Categories | Search | Playback |
|---|---|---|---|---|
| EmbyTube Test Source | Popular, Newest | 4 test categories | saved searches | MP4 + HLS test streams |
| XVideos | Best of last month, Newest | site categories | saved searches | HLS (up to the site's max) + MP4 fallbacks |
| xHamster | Best, Newest | 73 sidebar categories | saved searches (max 10 pages) | plain HLS only; videos without it show an error |
| XNXX | Best of last month, Featured (today's selection) | site categories | saved searches | HLS + MP4 fallbacks |
| Pornhat network: Pornhat, OK XXX, Perfect Girls, OK Porn | Newest | channels (studios) | saved searches | HLS 360p–720p |
| DrTuber, TNAFlix, PornOne, SuperPorn, PlayVids, Xozilla, 3Movs, Vagina.nl, Poldertube.nl, hello.porn, homo.xxx, MrSexe, CumLouder, HomeMoviesTube, HypnoTube, JustPorn, NonkTube, TabooTube, XFreeHD, Freeuse Porn, Porno1.hu (generic HTML engine) | Newest / Featured | site categories (SuperPorn: none) | saved searches (SuperPorn: none) | MP4 / HLS, see docs/sites/html-tubes.md |
| Pornhub (experimental) | Most viewed, Newest | site categories | saved searches (max 9 pages) | **usually fails**: the CDN refuses Emby's ffmpeg (see docs/sites/pornhub.md) |

Not supported: SpankBang (Cloudflare bot challenge) and the AWM Network aggregators — see
[`docs/sites/not-implemented.md`](docs/sites/not-implemented.md).

Real sources are fetched with a 1 request/second limit. Each channel refresh reads every
listing: per source roughly `2 × depth + categories × pages per category` requests
(defaults: depth 3, 40 categories, 1 page each ≈ 46 requests per source).

## Limitations (v0.1)

- Emby indexes channel content during its *Refresh Internet Channels* task; browsing shows
  that snapshot. Listings are bounded (default 3 pages per listing).
- No free-text search from the client; use saved searches (ADR-002).
- Emby sorts listings alphabetically, not by popularity/date (ADR-004).
- Client compatibility is only verified at the server/API level so far.

## Development

```sh
dotnet build
dotnet test
python tools/emby_smoke.py --enable        # against a local test server
python tools/emby_access_test.py           # access-control probe (test server only!)
```

Docs: [architecture](docs/architecture.md) · [provider authoring](docs/provider-authoring.md)
· [security & privacy](docs/security-and-privacy.md) · [licensing](docs/licensing.md)
· [troubleshooting](docs/troubleshooting.md) · [ADRs](docs/adr) · [progress](docs/progress.md)

## License

MIT, see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md). Do not contribute code copied or
translated from GPL projects (ADR-003).
