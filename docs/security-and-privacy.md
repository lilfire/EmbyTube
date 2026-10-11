# Security and privacy

## Access control (adult content)

EmbyTube relies on **Emby's own per-user channel access**. The plugin's "Enable" switch is
a global on/off, not access control. The channel declares `ChannelParentalRating.Adult`.

### Measured on Emby 4.10.1 (`tools/emby_access_test.py`, 2026-10-11)

| Path | New user (default policy) | `EnableAllChannels=false` | `MaxParentalRating=13` |
|---|---|---|---|
| Channel listed (`/Channels`) | **reachable** | blocked | **reachable** |
| Browse channel folders | **reachable** | blocked | blocked |
| Library search (`/Items?SearchTerm`) | **reachable** | blocked | blocked |
| Search hints | blocked | blocked | blocked |
| Latest items | blocked | blocked | blocked |
| Direct item lookup by Emby id | **reachable** | **reachable** ⚠ | **reachable** ⚠ |
| PlaybackInfo by Emby id | **reachable** | **reachable** ⚠ | **reachable** ⚠ |

Consequences:

1. **New Emby users get `EnableAllChannels=true` by default** and therefore see EmbyTube.
   Admins must turn that off per user (README step 4). This is the primary control.
2. ⚠ **Known gap (release blocker for public beta, STOP/GO 3):** a user *without* channel
   access who knows or guesses an item id (Emby item ids are small sequential integers)
   can read that item's metadata and obtain a playable media source. The plugin cannot
   close this from `GetChannelItemMediaInfo`: Emby passes no user to it
   (`IRequiresMediaInfoCallback`, `IMediaSourceProvider` have no user parameter in SDK 4.9.1.90).
   Next steps (M3): report to Emby; evaluate a supported server-side hook that sees the
   requesting user; until then, document it prominently and keep the plugin disabled on
   servers with untrusted users.

## SSRF

- Every provider declares `AllowedHosts`. `UrlGuard` enforces: http/https only, no
  userinfo, ports 80/443/8080/8443, host on the allowlist (exact or subdomain), and every
  resolved address public (blocks loopback, RFC1918, CGNAT, link-local/metadata
  169.254.0.0/16, ULA, multicast, documentation ranges, NAT64, IPv4-mapped private).
- `SafeHttpClient` (used by providers for their own requests) re-checks the **socket
  address at connect time** (DNS-rebinding safe), follows redirects manually with the
  allowlist applied to each hop (max 5), caps response size (4 MiB) and time.
- Playback variant URLs are checked before being handed to Emby; Emby's transcoder then
  fetches them. Redirects that Emby/ffmpeg follow *after* that check are outside our
  control (documented limitation; providers should return final URLs).
- Negative tests: `tests/EmbyTube.Core.Tests/UrlGuardTests.cs`, `SafeHttpClientTests.cs`,
  mock "Error cases" item 2 (verified end-to-end on Emby 4.10.1).

## Logging and privacy

- Logs contain provider id, folder kind/page number, error kind, and `scheme://host` only.
  Never full URLs, query strings, headers, cookies, tokens or search terms
  (`LogSafe`, `ItemIds.Describe`). Exceptions are logged by type + stack, not message.
- Debug logging is opt-in and follows the same rules.
- ⚠ **Emby itself logs full stream URLs** (`MediaProbeManager` ffprobe command lines and
  `HttpClient` GET lines, observed on 4.10.1 with XVideos). These are Emby's log entries,
  outside the plugin's control; signed CDN URLs therefore appear in `embyserver.txt`.
  Treat Emby logs as sensitive.
- No telemetry. No watch history beyond what Emby itself stores. No video files stored;
  only Emby's own image cache for thumbnails.
- Test scripts authenticate with the `X-Emby-Token` header (Emby logs request URLs, so
  `api_key=` in a URL would be written to the server log).

## Traffic hygiene

Per-host request spacing (default 500 ms), timeouts, typed errors for 429 with
`Retry-After`, bounded crawl depth (ADR-004). Live-site integration tests are opt-in.
