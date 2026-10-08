using System.Runtime.InteropServices;

namespace HdbResale.App;

// The native library owns the handler and counter; no callbacks or memory cross into managed code.
// It is installed once at startup and stays installed, and loaded, until the process exits.
internal static partial class NativeTileStatus
{
    [LibraryImport("hdb_tile_status", EntryPoint = "hdb_tile_status_start")]
    internal static partial void Start();
    [LibraryImport("hdb_tile_status", EntryPoint = "hdb_tile_status_exhausted")]
    internal static partial ulong ExhaustedRequests();
}
