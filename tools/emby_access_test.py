"""
Access-control probe for the EmbyTube channel (EBT-011 groundwork).

Creates two throwaway local users on a TEST server:
  - et_default:    created with Emby's default policy
  - et_restricted: EnableAllChannels=false, no EnabledChannels
and checks, per user, whether EmbyTube data is reachable through: channel list,
direct item lookup, recursive item queries, global search, latest items, and PlaybackInfo.

Usage: python tools/emby_access_test.py [--server http://localhost:8096]
Never run against a production server.
"""
import argparse
import json
import secrets
import sys
import urllib.error
import urllib.parse
import urllib.request

CLIENT = 'Emby Client="EmbyTubeAccess", Device="cli", DeviceId="embytube-access", Version="0.1"'


class Emby:
    def __init__(self, base, token=None):
        self.base, self.token = base, token

    def call(self, method, path, body=None, query=None):
        url = self.base + path + ("?" + urllib.parse.urlencode(query) if query else "")
        h = {"X-Emby-Authorization": CLIENT, "Content-Type": "application/json"}
        if self.token:
            h["X-Emby-Token"] = self.token
        req = urllib.request.Request(url, data=json.dumps(body).encode() if body is not None else None, headers=h, method=method)
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                raw = r.read()
                return r.status, (json.loads(raw) if raw else None)
        except urllib.error.HTTPError as e:
            return e.code, None


def login(base, user, pw):
    _, d = Emby(base).call("POST", "/Users/AuthenticateByName", {"Username": user, "Pw": pw})
    return Emby(base, d["AccessToken"]), d["User"]["Id"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", default="http://localhost:8096")
    ap.add_argument("--password-file", default=".emby-test/credentials.txt")
    args = ap.parse_args()
    base = args.server.rstrip("/") + "/emby"

    admin_pw = open(args.password_file, encoding="utf-8").read().strip().split(":", 1)[1]
    admin, admin_id = login(base, "admin", admin_pw)

    _, chans = admin.call("GET", "/Channels", query={"UserId": admin_id})
    channel = next(c for c in chans["Items"] if c["Name"] == "EmbyTube")
    _, vids = admin.call("GET", f"/Users/{admin_id}/Items", query={"ParentId": channel["Id"], "Recursive": "true", "IncludeItemTypes": "Video", "Limit": 1})
    if not vids["Items"]:
        # Channel videos may be typed differently; fall back to any non-folder.
        _, vids = admin.call("GET", f"/Users/{admin_id}/Items", query={"ParentId": channel["Id"], "Recursive": "true", "IsFolder": "false", "Limit": 1})
    video = vids["Items"][0]
    search_term = video["Name"].split(" – ")[0]
    print(f"admin sees channel {channel['Id']}, sample video {video['Id']} ({video['Type']})")

    _, users = admin.call("GET", "/Users")
    for u in users:
        if u["Name"] in ("et_default", "et_restricted", "et_parental"):
            admin.call("DELETE", f"/Users/{u['Id']}")

    results = {}
    for name in ("et_default", "et_restricted", "et_parental"):
        pw = secrets.token_urlsafe(10)
        _, u = admin.call("POST", "/Users/New", {"Name": name})
        admin.call("POST", f"/Users/{u['Id']}/Password", {"NewPw": pw, "CurrentPw": ""})
        if name == "et_restricted":
            _, full = admin.call("GET", f"/Users/{u['Id']}")
            policy = full["Policy"]
            policy["EnableAllChannels"] = False
            policy["EnabledChannels"] = []
            admin.call("POST", f"/Users/{u['Id']}/Policy", policy)
        if name == "et_parental":
            _, full = admin.call("GET", f"/Users/{u['Id']}")
            policy = full["Policy"]
            policy["MaxParentalRating"] = 13  # US PG-13; channel is rated Adult
            admin.call("POST", f"/Users/{u['Id']}/Policy", policy)
        _, full = admin.call("GET", f"/Users/{u['Id']}")
        print(f"\n{name}: EnableAllChannels={full['Policy'].get('EnableAllChannels')} MaxParentalRating={full['Policy'].get('MaxParentalRating')}")

        cli, uid = login(base, name, pw)
        checks = {}
        _, d = cli.call("GET", "/Channels", query={"UserId": uid})
        checks["channel listed"] = any(c["Name"] == "EmbyTube" for c in (d or {}).get("Items", []))
        st, d = cli.call("GET", f"/Users/{uid}/Items/{video['Id']}")
        checks["direct item lookup"] = st == 200
        st, d = cli.call("GET", f"/Users/{uid}/Items", query={"ParentId": channel["Id"]})
        checks["browse channel"] = st == 200 and bool((d or {}).get("Items"))
        st, d = cli.call("GET", f"/Users/{uid}/Items", query={"Recursive": "true", "SearchTerm": search_term, "Limit": 5})
        checks["library search"] = st == 200 and bool((d or {}).get("Items"))
        st, d = cli.call("GET", "/Search/Hints", query={"UserId": uid, "SearchTerm": search_term, "Limit": 5})
        checks["search hints"] = st == 200 and bool((d or {}).get("SearchHints"))
        st, d = cli.call("GET", f"/Users/{uid}/Items/Latest", query={"Limit": 20})
        checks["latest items"] = st == 200 and any(i.get("ChannelId") == channel["Id"] for i in (d or []))
        st, d = cli.call("POST", f"/Items/{video['Id']}/PlaybackInfo", {"UserId": uid}, query={"UserId": uid})
        checks["playback info"] = st == 200 and bool((d or {}).get("MediaSources"))
        for k, v in checks.items():
            print(f"  {'REACHABLE ' if v else 'blocked   '} {k}")
        results[name] = checks

        admin.call("DELETE", f"/Users/{u['Id']}")

    leaks = [f"{n}: {k}" for n in ("et_restricted", "et_parental") for k, v in results[n].items() if v]
    print("\nRESTRICTED USER LEAKS:", leaks or "none")
    return 1 if leaks else 0


if __name__ == "__main__":
    sys.exit(main())
