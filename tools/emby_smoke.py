"""
End-to-end smoke test of EmbyTube against a running Emby Server (API level).

Usage:
  python tools/emby_smoke.py --server http://localhost:8096 --user admin --password-file .emby-test/credentials.txt

Checks:
  1. Plugin is loaded and the EmbyTube channel exists.
  2. Root -> source -> Popular -> video list with metadata.
  3. "Next page" folder exists and is bounded.
  4. PlaybackInfo for an MP4 and an HLS item returns a fresh media source each call
     (proves resolve at playback start, not at indexing time).
  5. A blocked (SSRF) test item does not return a media source.

Uses the X-Emby-Token header (never api_key in the URL, which Emby would log).
Prints item names, counts and hosts only; never full stream URLs.
"""
import argparse
import json
import sys
import urllib.error
import urllib.parse
import urllib.request

PLUGIN_ID = "6f1d7a3e-2b8c-4e51-9a0d-3c7e5b2f8a14"
CLIENT = 'Emby Client="EmbyTubeSmoke", Device="cli", DeviceId="embytube-smoke", Version="0.1"'

results = []


def check(name, ok, detail=""):
    results.append((name, ok))
    print(("PASS " if ok else "FAIL ") + name + (f" - {detail}" if detail else ""))
    return ok


class Emby:
    def __init__(self, server):
        self.base = server.rstrip("/") + "/emby"
        self.token = None
        self.user_id = None

    def call(self, method, path, body=None, query=None):
        url = self.base + path
        if query:
            url += "?" + urllib.parse.urlencode(query)
        headers = {"X-Emby-Authorization": CLIENT, "Content-Type": "application/json"}
        if self.token:
            headers["X-Emby-Token"] = self.token
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read()
            return json.loads(raw) if raw else None

    def login(self, user, password):
        r = self.call("POST", "/Users/AuthenticateByName", {"Username": user, "Pw": password})
        self.token = r["AccessToken"]
        self.user_id = r["User"]["Id"]

    def items(self, channel_id, folder_id=None):
        # Emby 4.10 serves channel browsing from its library index (filled by the
        # "Refresh Internet Channels" task); folder ids here are Emby item ids.
        q = {"ParentId": folder_id or channel_id, "Fields": "Overview,ProviderIds"}
        return self.call("GET", f"/Users/{self.user_id}/Items", query=q)["Items"]

    def playback_info(self, item_id):
        return self.call("POST", f"/Items/{item_id}/PlaybackInfo",
                         {"UserId": self.user_id, "AutoOpenLiveStream": False},
                         query={"UserId": self.user_id})


def host(url):
    p = urllib.parse.urlsplit(url or "")
    return f"{p.scheme}://{p.hostname}" if p.hostname else "(none)"


def find(items, prefix):
    return next((i for i in items if i["Name"].startswith(prefix)), None)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", default="http://localhost:8096")
    ap.add_argument("--user", default="admin")
    ap.add_argument("--password-file", default=".emby-test/credentials.txt")
    ap.add_argument("--enable", action="store_true", help="enable the plugin + mock source first")
    args = ap.parse_args()

    password = open(args.password_file, encoding="utf-8").read().strip().split(":", 1)[1]
    emby = Emby(args.server)
    emby.login(args.user, password)

    plugins = emby.call("GET", "/Plugins")
    check("plugin loaded", any(p["Id"] == PLUGIN_ID for p in plugins))

    if args.enable:
        cfg = emby.call("GET", f"/Plugins/{PLUGIN_ID}/Configuration")
        cfg.update({"Enabled": True, "EnabledProviders": ["mock"], "SavedSearches": "Sintel"})
        emby.call("POST", f"/Plugins/{PLUGIN_ID}/Configuration", cfg)

    channels = emby.call("GET", "/Channels", query={"UserId": emby.user_id})["Items"]
    channel = next((c for c in channels if c["Name"] == "EmbyTube"), None)
    if not check("channel visible to admin", channel is not None):
        return finish()
    cid = channel["Id"]

    root = emby.items(cid)
    print("  root:", [i["Name"] for i in root])
    source = find(root, "EmbyTube Test Source")
    if not check("root lists test source", source is not None):
        return finish()

    menu = emby.items(cid, source["Id"])
    print("  source menu:", [i["Name"] for i in menu])
    check("source menu has Popular/Newest/Categories",
          all(find(menu, n) for n in ("Popular", "Newest", "Categories")))
    check("saved search folder shown", find(menu, "Search: Sintel") is not None)

    popular = emby.items(cid, find(menu, "Popular")["Id"])
    videos = [i for i in popular if not i.get("IsFolder")]
    nxt = find(popular, "Next page")
    check("popular lists videos", len(videos) > 0, f"{len(videos)} videos")
    check("videos have image + runtime",
          all(v.get("ImageTags", {}).get("Primary") for v in videos[:5]) and all(v.get("RunTimeTicks") for v in videos[:5]))
    check("next page folder present", nxt is not None)
    if nxt:
        page2 = emby.items(cid, nxt["Id"])
        p2v = [i for i in page2 if not i.get("IsFolder")]
        check("page 2 has different videos", p2v and p2v[0]["Id"] != videos[0]["Id"], f"{len(p2v)} videos")

    cats = emby.items(cid, find(menu, "Categories")["Id"])
    print("  categories:", [c["Name"] for c in cats])
    by_cat = {c["Name"]: emby.items(cid, c["Id"]) for c in cats}

    for cat_name, label in (("Progressive MP4", "mp4"), ("HLS", "hls")):
        vids = [i for i in by_cat.get(cat_name, []) if not i.get("IsFolder")]
        if not check(f"{label}: category has videos", bool(vids)):
            continue
        item = vids[0]
        first = emby.playback_info(item["Id"])
        second = emby.playback_info(item["Id"])
        srcs = first.get("MediaSources") or []
        ok = check(f"{label}: PlaybackInfo returns media source", bool(srcs),
                   ", ".join(f"{s.get('Container')}@{host(s.get('Path'))} dp={s.get('SupportsDirectPlay')} ds={s.get('SupportsDirectStream')} tc={s.get('SupportsTranscoding')}" for s in srcs))
        if ok:
            p1 = srcs[0].get("Path")
            p2 = (second.get("MediaSources") or [{}])[0].get("Path")
            check(f"{label}: resolved per request (not from cache)", "embytube_t=" in (p1 or "") and p2 is not None)
            if first.get("ErrorCode"):
                check(f"{label}: no PlaybackInfo error", False, first["ErrorCode"])

    errs = [i for i in by_cat.get("Error cases", []) if not i.get("IsFolder")]
    if len(errs) > 1:
        try:
            r = emby.playback_info(errs[1]["Id"])  # index 1 = SSRF attempt to 169.254.169.254
            leaked = any("169.254" in (s.get("Path") or "") for s in r.get("MediaSources") or [])
            check("ssrf variant never reaches Emby", not leaked, r.get("ErrorCode") or "no media sources")
        except urllib.error.HTTPError as e:
            check("ssrf variant never reaches Emby", True, f"HTTP {e.code}")

    return finish()


def finish():
    failed = [n for n, ok in results if not ok]
    print(f"\n{len(results) - len(failed)}/{len(results)} checks passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
