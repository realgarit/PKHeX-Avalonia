# Shared helpers for the Flathub screenshot capture. Sourced by run-session.sh and capture.sh.
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
# Session state (GNOME home, runtime dir, D-Bus address, app copy) lives on the distro's own disk.
STATE="${PKHEX_SHOT_STATE:-/var/tmp/pkhex-flathub-shots}"
# The scripts delete and recreate folders below $STATE, so it must be a dedicated, absolute folder.
case "$STATE" in
    /?*/pkhex-flathub-shots) ;;
    *) echo "Refusing to use '$STATE' as the screenshot state folder" >&2; exit 1 ;;
esac

# reset_dir <path>: deletes and recreates a folder, but only one inside $STATE.
reset_dir() {
    case "$1" in
        "${STATE:?}"/?*) rm -rf -- "$1" && mkdir -p -- "$1" ;;
        *) echo "Refusing to delete '$1'" >&2; exit 1 ;;
    esac
}

session_env() {
    source "$STATE/session.env"
    export DBUS_SESSION_BUS_ADDRESS XDG_RUNTIME_DIR
}

ev() {
    session_env
    gdbus call --session --dest org.gnome.Shell --object-path /org/gnome/Shell \
        --method org.gnome.Shell.Eval "$1"
}

helpers() { ev "$(cat "$DIR/input.js")" >/dev/null; }

wait_ready() {
    for _ in $(seq 60); do
        [ -f "$STATE/session.env" ] && ev 'global.context.unsafe_mode' 2>/dev/null | grep -q "'true'" && return 0
        sleep 1
    done
    echo "GNOME Shell did not become ready; see $STATE/shell.log" >&2
    return 1
}

# launch_app <linux-x64 publish dir>: starts the app inside the session's mount namespace with fresh settings.
launch_app() {
    # Default app settings every run (an earlier run leaves the Light theme selected).
    reset_dir "$STATE/home/.config/PKHeX-Avalonia"
    reset_dir "$STATE/home/.local/share/PKHeX-Avalonia"
    reset_dir "$STATE/app"
    cp -r "$1/." "$STATE/app/" && chmod +x "$STATE/app/PKHeX.Avalonia"
    session_env
    local pid auth
    pid=$(pgrep -f 'gnome-shell --headless --wayland' | head -1)
    auth=$(ls "$XDG_RUNTIME_DIR"/.mutter-Xwaylandauth.* | head -1)
    HOME="$STATE/home" XDG_CONFIG_HOME="$STATE/home/.config" XDG_DATA_HOME="$STATE/home/.local/share" \
        XDG_CACHE_HOME="$STATE/home/.cache" nohup nsenter -t "$pid" -m -- \
        env DISPLAY=:0 XAUTHORITY="$auth" GDK_BACKEND=x11 "$STATE/app/PKHeX.Avalonia" > "$STATE/app.log" 2>&1 &
    for _ in $(seq 40); do
        ev 'global.get_window_actors().map(a => a.meta_window.get_title()).join("|")' | grep -q PKHeX && break
        sleep 1
    done
    ev 'Main.overview.hide(); "ok"' >/dev/null
    sleep 2
}

# open_save <path>: File > Open through the real GTK file chooser (the app has no file argument).
open_save() {
    helpers
    ev '__combo([0xffe3], 0x6f); "ctrl+o"' >/dev/null; sleep 3
    # GNOME's focus-stealing prevention can leave the portal dialog unfocused.
    ev 'Main.activateWindow(global.get_window_actors().map(a => a.meta_window).find(w => w.get_title() === "Open Save File")); "ok"' >/dev/null; sleep 1
    ev '__combo([0xffe3], 0x6c); "ctrl+l"' >/dev/null; sleep 1
    ev '__combo([0xffe3], 0x61); __key(0xffff); "clear"' >/dev/null; sleep 0.5
    ev "__type('$1'); 'typed'" >/dev/null; sleep 1
    ev '__key(0xff0d); "enter"' >/dev/null; sleep 6
}

# click <x> <y> [count]: GNOME stage coordinates. Virtual-device button presses do not reach windows in a
# headless session, so the button goes through Mutter's RemoteDesktop API; the pointer is parked afterwards.
click() {
    helpers
    ev "__move($1, $2); 'moved'" >/dev/null; sleep 0.4
    gjs "$DIR/rd-click.js" "${3:-1}" >/dev/null
    sleep 1
    ev "__move(20, 1080); 'away'" >/dev/null; sleep 0.5
}

# shot <name>: GNOME's window screenshot of the focused window (title bar, rounded corners, shadow).
shot() {
    session_env
    mkdir -p "$OUT"
    gdbus call --session --dest org.gnome.Shell.Screenshot --object-path /org/gnome/Shell/Screenshot \
        --method org.gnome.Shell.Screenshot.ScreenshotWindow true false false "$STATE/$1.png" >/dev/null
    cp "$STATE/$1.png" "$OUT/$1.png"
    echo "$OUT/$1.png"
}
