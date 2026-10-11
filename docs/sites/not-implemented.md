# Sites evaluated and not implemented

## SpankBang (spankbang.com / spankbang.party)

_Checked 2026-10-11._ Every request — including `robots.txt` — gets HTTP 403 with
`Cf-Mitigated: challenge` and a Cloudflare "Just a moment…" page for EmbyTube's honest
User-Agent. That is bot detection; getting past it would require imitating a browser or
solving the challenge, which the spec forbids (no CAPTCHA/bot-detection bypass).
Revisit only if the site offers an API or allows automated access.

## AWM Network (49 sites in Cumination's `awmnet.py`)

_Reviewed 2026-10-11, not implemented (recommendation)._ The network's sites (4tube,
PornHD, GotPorn, PornMD, Fux, …) are aggregators: listings link out to many third-party
tube sites, often marked as paid. Cumination plays them by following redirects to
arbitrary hosts, then using generic resolvers and two de-obfuscation routines
(`kvs_decode` for KVS players' `function/0/` URLs, `txxx.Tdecode` for an API's encoded
URLs). For EmbyTube that means:

- no fixed host allowlist is possible (SSRF protection requires one per provider);
- decoding deliberately obfuscated stream URLs is circumvention, which the spec forbids;
- many entries are paid content.

A possible future approach is to list AWM items and play only those whose target is a
source EmbyTube already supports (e.g. xHamster), skipping everything else. Not planned.
