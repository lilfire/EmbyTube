# Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Channel shows "EmbyTube is disabled…" | Enable the plugin in *Plugins → EmbyTube*, save. The channel re-indexes ~2 s later. |
| Settings saved but folders unchanged | Re-index runs after save; check the log for `Re-indexing channel` / `Channel re-index finished`. Otherwise run *Scheduled Tasks → Refresh Internet Channels*. |
| "No sources enabled" | Tick at least one source in the settings. |
| A folder shows "The source has changed…" | The provider's parser no longer matches the site (`ParseChanged`). Update the plugin. |
| A folder shows "rate limiting" | The site returned 429. Lower *Maximum Next page depth*, wait, refresh later. |
| Playback error, log says `Dropped variant … (host-not-allowlisted)` | The site moved streams to a new CDN. Provider update needed. |
| Playback error, log says `(private-ip)` / `resolves-to-private-ip` | Blocked by SSRF protection by design. |
| Listings are alphabetical instead of newest first | Known Emby limitation (ADR-004). |
| Settings page still shows an old layout/source list after updating the DLL | Emby bases the page ETag on the plugin *version*; a rebuild with the same version is served as unchanged (304) and Ctrl+F5 does not help. Every build must have a new version: use `tools/deploy-test.sh` locally; CI adds the run number. |
| New users can see EmbyTube | Emby default `EnableAllChannels=true`. Restrict per user (README). |

Logs: Emby log (`embyserver.txt`), lines prefixed `[EmbyTube]`. Enable *Debug logging* for
per-request detail; URLs, tokens and search terms are never written.
