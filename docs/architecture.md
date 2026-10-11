# Architecture

```text
Emby clients ──► Emby Server ── library index (filled by channel refresh)
                     │
                     ├─ RefreshChannelContent ──► EmbyTubeChannel.GetChannelItems
                     │                                  │
                     └─ PlaybackInfo ───────────► EmbyTubeChannel.GetChannelItemMediaInfo
                                                        │
EmbyTube.Plugin (Emby adapter, only project that references the Emby SDK)
  EmbyTubeChannel        IChannel + IRequiresMediaInfoCallback → ChannelItemInfo / MediaSourceInfo
  ChannelRefreshEntryPoint   re-index after settings change (ADR-004)
  EmbyTubeRuntime        composition root (providers, settings)
  Plugin / PluginConfiguration / Configuration/config.{html,js}
                                                        │
EmbyTube.Application (Emby-independent)
  ChannelNavigator       folder ids → NavFolder / NavVideo / NavMessage; per-provider fault isolation
  PlaybackService        on-demand resolve, single retry, SSRF check, variant ordering
  ProviderRegistry, TtlCache, EmbyTubeSettings, IEmbyTubeLog
                                                        │
EmbyTube.Core (contracts + infrastructure)
  ITubeProvider, TubeVideo, Page<T>, PlaybackResult, ProviderException(kind)
  ItemIds (folder/video id codec), UrlGuard, LogSafe, SafeHttpClient
                                                        │
EmbyTube.Providers.*   Mock (v0.1); real providers from M2
```

## Ids

- Folder: `f1|<kind>|<provider>|<category>|<sort>|<query>|<pageToken>|<page>`; every
  component percent-encoded, so site text cannot inject separators.
- Video: `<provider>:<videoId>` (both percent-encoded). Stable; never contains stream URLs.
- These strings are Emby's *external* channel item ids; Emby assigns its own numeric ids.

## Error model

Providers throw `ProviderException` with a kind (`Unavailable`, `RateLimited`,
`AccessDenied`, `Unsupported`, `ParseChanged`, `MediaExpired`, `NetworkError`, `NotFound`,
`Blocked`, `Unknown`). The navigator turns failures into a single message folder in that
provider's listing; other providers and the root are unaffected. Unexpected exceptions
are caught at the same boundary and logged by type.

## Packaging

One assembly (ADR-001): the plugin csproj links the sources of Core/Application/Providers.
