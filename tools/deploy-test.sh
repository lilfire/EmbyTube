#!/usr/bin/env sh
# Builds the plugin with a unique revision and deploys it to the local Docker test server
# (container "embytube-test", data in .emby-test/). A unique revision is required: Emby
# derives the settings-page ETag from the plugin version (see Directory.Build.props).
set -eu
cd "$(dirname "$0")/.."

# Minutes since 2026-01-01, fits the 16-bit revision field for ~45 days; good enough for dev.
REV=$(( ( $(date +%s) - 1767225600 ) / 60 % 65535 ))
dotnet build src/EmbyTube.Plugin -nologo -v q -p:BuildRevision="$REV"

docker stop embytube-test >/dev/null
cp src/EmbyTube.Plugin/bin/Debug/net6.0/EmbyTube.dll .emby-test/config/plugins/
docker start embytube-test >/dev/null

i=0
until curl -s -m 2 http://localhost:8096/emby/System/Info/Public >/dev/null; do
  i=$((i + 1)); [ "$i" -gt 30 ] && { echo "Emby did not come back up" >&2; exit 1; }
  sleep 2
done
echo "Deployed EmbyTube revision $REV"
