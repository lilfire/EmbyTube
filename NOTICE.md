# Notices

EmbyTube is MIT licensed. It contains no third-party source code.

## References studied (no code copied)

- Emby YouTube Plugin, MIT, (c) 2026 Elias Bruno:
  https://github.com/eliasbruno124-dev/Emby-Youtube-Plugin (commit 17656d7c).
  Used as a reference for Emby channel API usage.
- Cumination, GPL-2.0 / GPL-3.0-or-later: https://github.com/dobbelina/repository.dobbelina
  (commit 9429c83a). `xvideos.py` read as a functional reference for the XVideos provider;
  no code copied or translated (ADR-003 addendum).

## Dependencies

- Emby SDK (`MediaBrowser.Server.Core`, `MediaBrowser.Common`): compile-time only,
  supplied by Emby Server at runtime; not redistributed.

## Test media (streamed from their hosts by the test source, not redistributed)

Big Buck Bunny and Sintel (c) Blender Foundation, CC BY 3.0, https://www.blender.org.
Elephants Dream (c) Blender Foundation / Orange, CC BY 2.5. Apple bipbop HLS example stream.
See docs/licensing.md.
