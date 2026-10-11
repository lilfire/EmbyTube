# ADR-001: Target framework, Emby SDK and packaging

- Status: Accepted (M0, 2026-10-11)
- Evidence: build + load test on Emby Server 4.10.1.0 (Docker `emby/embyserver@sha256:997e8c1e…`, linux/amd64, .NET 8.0.28)

## Context

Emby Server plugins are .NET assemblies loaded into the server process. Native installs
of Emby (notably macOS and some NAS packages) have shipped on the .NET 6 runtime; the
reference YouTube plugin documents a `ReflectionTypeLoadException` when a net8.0 assembly
is loaded there. The newest *stable* Emby SDK on NuGet is `MediaBrowser.Server.Core`
/ `MediaBrowser.Common` **4.9.1.90** (netstandard2.0). 4.10/4.11 SDKs exist only as betas.

## Decision

1. Target **net6.0**. A net6.0 assembly loads on .NET 6, 7 and 8 runtimes.
2. Compile against SDK **4.9.1.90**, `ExcludeAssets="runtime"`, so no Emby assemblies are
   copied next to the plugin (avoids binding conflicts with the server's own copies).
3. Ship **one DLL** (`EmbyTube.dll`). Emby-independent projects (Core, Application,
   Providers.*) are compiled into the plugin assembly by source linking. They remain
   separate projects so that the compiler enforces "no Emby SDK dependency" and tests can
   target them directly. No third-party runtime dependencies.

## Evidence

- `dotnet build` produces `EmbyTube.dll` (~120 KB) with no other DLLs.
- Emby 4.10.1 log: `Loading EmbyTube, Version=0.1.0.0 … from /config/plugins/EmbyTube.dll`;
  `/Plugins` lists EmbyTube; `/Channels` lists the channel.
- SDK 4.9.1.90 marks `IHasCacheKey` ("ignored by the server"), `MediaSourceInfo.SupportsProbing`
  and `ChannelItemInfo.HomePageUrl` obsolete; they are not used. `IChannel` in this SDK has
  no `DataVersion`/`IsEnabledByDefault` members.

## Consequences

- Must re-test on each new Emby major version (see `docs/compatibility.md`).
- Not verified on a native .NET 6 Emby install (NOT TESTED).
