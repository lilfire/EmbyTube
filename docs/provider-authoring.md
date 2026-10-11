# Writing a provider

Before writing code for a real site, complete the **site policy check** (STOP/GO 2):
document in the PR which pages/APIs you use, the site's terms on automated access and
embedding/streaming, and why the project may use them. If the terms forbid it, do not
implement the provider. Never bypass DRM, CAPTCHA, age/verification gates, geo-blocks or
other access controls, and never treat an embedded third-party player page as a stream.

## Steps

1. Create `src/EmbyTube.Providers.<Name>/` referencing only `EmbyTube.Core`.
2. Implement `ITubeProvider`:
   - `Id`: lowercase, `[a-z0-9-]`, never changes (it is part of persistent Emby ids).
   - `AllowedHosts`: every host you fetch from or return (pages, images, CDNs).
   - `Capabilities`: be honest; the UI hides what you don't support.
   - Use `SafeHttpClient` for all requests. Pass the `CancellationToken` through.
   - Throw `ProviderException` with a precise kind. Never `catch { return empty; }`.
     Layout changes → `ParseChanged`.
   - `ResolvePlaybackAsync` runs at playback time; return all usable variants with
     `Height`, `Protocol`, minimal `Headers`, `ExpiresAt` when known.
3. Link it into the plugin: add a `<Compile Include=…>` line in `EmbyTube.Plugin.csproj`,
   construct it in `EmbyTubeRuntime.Build()`, and add it to `ProviderCatalog.Options`
   (the settings page reads that list from the server; the runtime logs a warning if a
   provider is missing from it).
4. Tests: subclass `ProviderContractTests` and feed it **offline fixtures** (synthetic or
   sanitized HTML/JSON with no adult imagery) through a stub `HttpMessageHandler`.
   Live tests go in a separate class marked `[Trait("Category", "Live")]` and are skipped
   unless `EMBYTUBE_LIVE_TESTS=1`.
5. Run `tools/emby_smoke.py` against a test server and fill a row in `docs/compatibility.md`.

## Crawl budget

Emby's refresh walks every listing × MaxPages (ADR-004). Keep category lists modest and
expect each listing to be fetched once per refresh.
