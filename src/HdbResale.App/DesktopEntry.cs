using System.Runtime.InteropServices;

namespace HdbResale.App;

// The freedesktop desktop entry that represents the app, without ".desktop": packaging/linux installs
// io.github.shenghaoc.hdb-resale-qt.desktop. X11 and Wayland desktops match windows to it by this name (Qt
// reports it as each window's app id) for the window's taskbar name and icon. Without it Qt reports the
// executable's base name, "HdbResale", which matches no entry. Other platforms ignore it.
internal static partial class DesktopEntry
{
    internal const string FileName = "io.github.shenghaoc.hdb-resale-qt";

    [LibraryImport("hdb_tile_status", EntryPoint = "hdb_set_desktop_file_name",
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial void SetDesktopFileName(string name);

    // Before the first window exists: Qt reads the name when it creates a window.
    internal static void Apply() => SetDesktopFileName(FileName);
}
