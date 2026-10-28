#!/usr/bin/env bash
# Runs a headless GNOME Shell for screenshots; it lives as long as this command, so start it in the
# background (or a second terminal) and keep it running while capture.sh works. Needs root in a throwaway
# Linux/WSL environment with gnome-shell, xwayland, gjs and xdg-desktop-portal-gtk installed.
source "$(dirname "$(readlink -f "$0")")/common.sh"
pkill -f 'gnome-shell --headless --wayland --virtual-monitor' 2>/dev/null; sleep 2
rm -rf "$STATE/home" "$STATE/run" "$STATE/session.env" && mkdir -p "$STATE/home" "$STATE/run" && chmod 700 "$STATE/run"
export HOME="$STATE/home" XDG_CONFIG_HOME="$STATE/home/.config" XDG_DATA_HOME="$STATE/home/.local/share" \
    XDG_CACHE_HOME="$STATE/home/.cache" XDG_RUNTIME_DIR="$STATE/run" STATE DIR
unset WAYLAND_DISPLAY DISPLAY PULSE_SERVER
# A private mount namespace gives Xwayland a writable /tmp/.X11-unix (WSLg mounts it read-only).
exec unshare -m --propagation private bash -c \
    'mount -t tmpfs tmpfs /tmp/.X11-unix && chmod 1777 /tmp/.X11-unix && exec dbus-run-session -- bash "$DIR/session.sh"'
