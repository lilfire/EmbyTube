# ADR-003: Project license

- Status: Accepted (M0, 2026-10-11)

## Context

- Emby YouTube Plugin (reference for Emby integration): **MIT**.
- Cumination (reference for provider behaviour): `addon.xml` declares **GPL 2.0**,
  `LICENSE.txt` is GPL v2, while `xvideos.py`, `pornhub.py` and `utils.py` carry
  **GPL-3.0-or-later** headers. `adultsite.py` has no header (falls under the repo GPL).
  See `docs/licensing.md` for the file matrix and commit SHAs.

## Decision

1. EmbyTube is licensed **MIT**.
2. **No code is copied or ported** from Cumination. Provider implementations are written
   from functional descriptions (what pages/APIs exist and what data they return), observed
   behaviour of the live site where its terms allow, and our own fixtures. Contributors must
   not paste or translate GPL code.
3. MIT-licensed code from the YouTube plugin may be adapted with attribution in
   `NOTICE.md`. As of v0.1 none is copied; only API usage patterns were studied.

## Consequences

- If GPL reuse is ever wanted, the whole project would need to move to a GPL-compatible
  license after legal review; that requires a new ADR.
- PR template / contributing guide must restate rule 2.

## Addendum 2026-10-11: XVideos provider

The owner asked to bring Cumination's XVideos support into the project. To keep MIT, the
module was read as a functional reference only (which pages and `html5player` calls exist);
`XVideosParser`/`XVideosProvider` were written independently against the live markup, which
already differs from Cumination's regexes in several places. To reuse Cumination code
verbatim instead, the project must first move to GPL-3.0-or-later (new ADR).
