# ADR-002: Search UX

- Status: Accepted for v0.1 (M0, 2026-10-11)

## Context

The spec forbids promising native free-text search before it is proven. Findings:

- In SDK 4.9.1.90, `ISearchableChannel` is an **empty marker interface**; there is no
  channel search method a plugin can implement.
- On Emby 4.10.1, user browsing and Emby search are served from the library index, which
  only the channel refresh fills (ADR-004). A user's search term never reaches the plugin.
- Emby developers have stated plugins cannot change client UI, so a text-input dialog in
  all clients is not available.

## Decision

**Saved searches.** The administrator enters search terms (one per line) in the plugin
settings. Each term appears as a `Search: <term>` folder under every search-capable source.
Saving the settings re-indexes the channel (ADR-004), so new searches appear within seconds.

Emby's own search (`/Items?SearchTerm`) then finds *already indexed* EmbyTube items by
title for users who have channel access. That is a side effect, not a provider search.

## Consequences

- No ad-hoc per-user search in v0.1. Documented in README "Limitations".
- Saved search terms are stored in the plugin configuration (server-side, admin only)
  and are never logged (`ItemIds.Describe` omits query text).
- P1: revisit if a supported mechanism appears in a newer SDK; do not modify client JS.
