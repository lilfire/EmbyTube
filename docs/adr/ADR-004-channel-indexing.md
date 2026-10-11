# ADR-004: Emby channel indexing, paging and refresh

- Status: Accepted (M1, 2026-10-11)
- Evidence: Emby 4.10.1, debug log of every `GetChannelItems` call (`tools/emby_smoke.py`)

## Findings (measured)

1. **Browsing never calls the plugin.** `GET /Users/{id}/Items?ParentId=<folder>` is served
   from Emby's library database. `GetChannelItems` is called only by
   `IChannelManager.RefreshChannelContent` (scheduled task "Refresh Internet Channels",
   or our own trigger), always with `UserId=0`, `StartIndex=null`, `Limit=null`.
2. **The refresh walks the whole tree**, including every "Next page" folder. With
   MaxPages=20 the mock channel produced 617 indexed items; with MaxPages=2, 415.
3. `GET /Channels/{id}/Items` returns 400 on 4.10.1; clients use `/Users/{id}/Items`.
4. Emby sorts listings by name (default and `SortBy=SortName`), so provider order
   ("Popular", "Newest") is **not** preserved, and "Next page (n)" sorts first.

## Decisions

- Paging uses an explicit **"Next page (n)" folder** with an encoded page token; Emby's
  `StartIndex/Limit` are never supplied to channels and cannot be used.
- **MaxPages defaults to 3** (admin range 1–100). This is the per-listing crawl bound and
  directly limits requests per refresh: roughly
  `(1 + sorts + categories + saved searches) × MaxPages` listing requests per source.
- After a settings save, `ChannelRefreshEntryPoint` calls `RefreshChannelContent`
  (debounced 2 s, one at a time). Verified: a new saved search appears ~2 s after saving.
- Video items are indexed **without media sources**; streams are resolved in
  `GetChannelItemMediaInfo` at playback time (ADR-005), so nothing short-lived is stored.
- Category crawl is bounded separately (added with xHamster, 73 categories): **MaxCategories**
  (default 40, 0 = all) and **CategoryMaxPages** (default 1). Requests per refresh per
  source ≈ `sorts × MaxPages + MaxCategories × CategoryMaxPages + saved searches × MaxPages`.

## Open

- Ordering (finding 4) is a UX limitation for v0.1. Candidates to test in P1:
  `IndexNumber`, name prefixes, or a client-side sort the user picks. No client hacks.
