#!/usr/bin/env bash
# Inside run-session.sh's mount namespace and D-Bus session: enable the helper extension, start GNOME Shell.
set -euo pipefail
mkdir -p "$XDG_DATA_HOME/gnome-shell/extensions"
cp -r "$DIR/extension/shot-helper@pkhex" "$XDG_DATA_HOME/gnome-shell/extensions/"
gsettings set org.gnome.shell disable-user-extensions false
gsettings set org.gnome.shell enabled-extensions "['shot-helper@pkhex']"
printf 'DBUS_SESSION_BUS_ADDRESS=%s\nXDG_RUNTIME_DIR=%s\n' "$DBUS_SESSION_BUS_ADDRESS" "$XDG_RUNTIME_DIR" > "$STATE/session.env"
exec gnome-shell --headless --wayland --virtual-monitor 1600x1100 > "$STATE/shell.log" 2>&1
