#!/usr/bin/env bash
# Run inside an X11 desktop/session bus (CI uses xvfb-run + dbus-run-session). The app defaults to
# X11; the Wayland socket is granted for the opt-in native Wayland setting.
set -euo pipefail
output=${1:?Missing evidence directory}
app_id=io.github.realgarit.PKHeX-Avalonia
mkdir -p "$output"
flatpak info --user --show-permissions "$app_id" > "$output/permissions.log"
python3 - "$output/permissions.log" << 'PY'
import configparser
import sys
permissions = configparser.ConfigParser()
permissions.read(sys.argv[1])
context = permissions["Context"]
def grants(name):
    return set(filter(None, context.get(name, "").split(";")))
assert not grants("filesystems"), "Unexpected host filesystem access"
assert grants("shared") == {"network", "ipc"}, "Unexpected shared resources"
assert grants("sockets") == {"x11", "wayland"}, "Unexpected sockets or unfiltered bus access"
assert grants("devices") == {"dri"}, "Unexpected device access"
for section in ("Session Bus Policy", "System Bus Policy"):
    if permissions.has_section(section):
        assert not any("*" in name for name in permissions[section]), "Broad D-Bus policy"
PY
# A package may write its private data, but must neither write /app nor see an arbitrary host file.
probe=$(mktemp)
trap 'rm -f "$probe"; flatpak kill "$app_id" >/dev/null 2>&1 || true' EXIT
flatpak run --user --command=sh "$app_id" -c 'test ! -w /app; test -d "$XDG_CONFIG_HOME"; test ! -e "$1"' -- "$probe"

flatpak run --user "$app_id" > "$output/runtime.log" 2>&1 &
for attempt in $(seq 1 60); do
  window=$(xdotool search --onlyvisible --class "$app_id" 2>/dev/null | head -n1 || true)
  if [ -n "$window" ]; then
    import -window "$window" "$output/launch.png"
    flatpak ps --columns=application | grep -Fx "$app_id"
    echo "Installed Flatpak opened its native X11 window with no host filesystem grant."
    exit 0
  fi
  sleep 1
done
cat "$output/runtime.log" >&2
echo "Flatpak did not open its native application window within 60 seconds." >&2
exit 1
