"""
Per-source playback check against a running test Emby (server-side evidence).

For each EmbyTube source folder name given (or all sources): open the first listing folder,
take the first video, request PlaybackInfo, then fetch through Emby:
  - MP4 sources: /Videos/{id}/stream.mp4?Static=true with a byte Range (and a seek Range)
  - HLS sources: /Videos/{id}/master.m3u8 → main.m3u8 → first transcoded .ts segment

Usage:
  python tools/emby_playback_check.py                 # all sources
  python tools/emby_playback_check.py DrTuber TNAFlix # selected sources

Uses X-Emby-Token (never api_key in URLs). Prints hosts only, never full stream URLs.
"""
import json
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = "http://localhost:8096/emby"
CLIENT = 'Emby Client="EmbyTubeCheck", Device="cli", DeviceId="embytube-check", Version="0.1"'


def login():
    pw = open(".emby-test/credentials.txt", encoding="utf-8").read().strip().split(":", 1)[1]
    req = urllib.request.Request(BASE + "/Users/AuthenticateByName",
                                 data=json.dumps({"Username": "admin", "Pw": pw}).encode(),
                                 headers={"Content-Type": "application/json", "X-Emby-Authorization": CLIENT})
    d = json.load(urllib.request.urlopen(req))
    return d["AccessToken"], d["User"]["Id"]


TOKEN, UID = login()


def call(path, body=None, method="GET", raw=False, headers=None, timeout=180):
    h = {"X-Emby-Token": TOKEN, "Content-Type": "application/json"}
    h.update(headers or {})
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode() if body is not None else None,
                                 headers=h, method=method)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        data = r.read()
        return (r.status, r.headers.get("Content-Type"), data) if raw else json.loads(data or b"null")


def items(parent):
    return call(f"/Users/{UID}/Items?ParentId={parent}")["Items"]


def host(u):
    return urllib.parse.urlsplit(u or "").hostname


def check(source):
    menu = items(source["Id"])
    listing = next((i for i in menu if i["Name"] in ("Newest", "Popular", "Featured", "All videos")), None)
    if not listing:
        return f"FAIL no listing folder ({[i['Name'] for i in menu]})"
    vids = [i for i in items(listing["Id"]) if not i["IsFolder"]]
    if not vids:
        return f"FAIL '{listing['Name']}' is empty"
    for v in vids[:3]:
        pi = call(f"/Items/{v['Id']}/PlaybackInfo?UserId={UID}", {"UserId": UID}, "POST")
        srcs = pi.get("MediaSources") or []
        if not srcs:
            continue
        s = srcs[0]
        desc = f"{s['Name']} @ {host(s.get('Path'))}, probed streams={len(s.get('MediaStreams') or [])}"
        try:
            if s.get("Container") == "hls":
                q = urllib.parse.urlencode({"MediaSourceId": s["Id"], "VideoCodec": "h264", "AudioCodec": "aac",
                                            "PlaySessionId": "chk", "DeviceId": "embytube-check"})
                master = call(f"/Videos/{v['Id']}/master.m3u8?{q}", raw=True)[2].decode()
                main = next(l for l in master.splitlines() if l and not l.startswith("#"))
                playlist = call(f"/Videos/{v['Id']}/{main}", raw=True)[2].decode()
                seg = next(l for l in playlist.splitlines() if l and not l.startswith("#"))
                st, ctype, data = call(f"/Videos/{v['Id']}/{seg}", raw=True)
                call("/Videos/ActiveEncodings?DeviceId=embytube-check&PlaySessionId=chk", method="DELETE", raw=True)
                ok = st == 200 and len(data) > 10_000
                return f"{'PASS' if ok else 'FAIL'} {desc}; transcoded segment {st} {ctype} {len(data)}B"
            q = urllib.parse.urlencode({"Static": "true", "MediaSourceId": s["Id"]})
            st, ctype, data = call(f"/Videos/{v['Id']}/stream.mp4?{q}", raw=True, headers={"Range": "bytes=0-1048575"})
            st2, _, data2 = call(f"/Videos/{v['Id']}/stream.mp4?{q}", raw=True, headers={"Range": "bytes=2000000-2099999"})
            ok = st == 206 and st2 == 206 and len(data) > 100_000
            return f"{'PASS' if ok else 'FAIL'} {desc}; static {st} {ctype} {len(data)}B, seek {st2} {len(data2)}B"
        except (urllib.error.HTTPError, StopIteration, urllib.error.URLError) as e:
            return f"FAIL {desc}; stream error {type(e).__name__} {getattr(e, 'code', '')}"
    return "FAIL no PlaybackInfo media source for the first 3 videos"


def main():
    wanted = set(sys.argv[1:])
    sources = [s for s in items(3) if not wanted or s["Name"] in wanted]
    failed = 0
    for s in sources:
        result = check(s)
        failed += result.startswith("FAIL")
        print(f"{s['Name']:<24} {result}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
