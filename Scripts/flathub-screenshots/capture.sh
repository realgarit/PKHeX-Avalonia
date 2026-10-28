#!/usr/bin/env bash
# Captures the three Flathub screenshots from the public Legends: Z-A test save.
# Usage: capture.sh [linux-x64 publish dir] [output dir]   (run-session.sh must be running)
# Inspect every frame: the first double-click after a save loads often only selects the slot, so each
# slot is double-clicked twice (harmless when the first one already loaded it). Rerun when a frame does
# not show the expected tab or Pokemon. Coordinates assume the 900x600 window GNOME centers on the
# 1600x1100 virtual monitor.
set -uo pipefail
source "$(dirname "$(readlink -f "$0")")/common.sh"
PUBLISH="${1:-$ROOT/tmp/flathub-publish}"
OUT="${2:-$ROOT/tmp/flathub-screenshots}"

wait_ready || exit 1
launch_app "$PUBLISH"
open_save "$ROOT/Tests/savefiles/gen9a_legendsza.main"

click 1186 440 2         # Charizard (Box 1, slot 6)
click 1186 440 2
click 381 486            # editor Main tab
shot flathub-01-pokemon-editor
click 740 343            # Inventory workspace
shot flathub-02-inventory
click 1187 302           # theme picker
click 1165 368           # Light
click 604 343            # Pokemon workspace
click 907 440 2          # Venusaur (Box 1, slot 3)
click 907 440 2
click 426 486            # editor Stats tab
shot flathub-03-stats-light
pkill -f "$STATE/app/PKHeX.Avalonia"
