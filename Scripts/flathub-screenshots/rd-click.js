// Usage: gjs rd-click.js [count]  — presses the left button at the current pointer position via Mutter's RemoteDesktop API.
const {Gio, GLib} = imports.gi;
const count = Number(ARGV[0] || 1);
const bus = Gio.DBus.session;
const NAME = 'org.gnome.Mutter.RemoteDesktop';
const call = (path, iface, method, params = null, type = null) =>
    bus.call_sync(NAME, path, iface, method, params, type ? new GLib.VariantType(type) : null, Gio.DBusCallFlags.NONE, -1, null);
const [session] = call('/org/gnome/Mutter/RemoteDesktop', NAME, 'CreateSession', null, '(o)').deep_unpack();
const S = `${NAME}.Session`;
call(session, S, 'Start');
for (let i = 0; i < count; i++) {
    call(session, S, 'NotifyPointerButton', new GLib.Variant('(ib)', [0x110, true]));
    GLib.usleep(60000);
    call(session, S, 'NotifyPointerButton', new GLib.Variant('(ib)', [0x110, false]));
    GLib.usleep(90000);
}
GLib.usleep(200000);
call(session, S, 'Stop');
print(`clicked ${count}x via ${session}`);
