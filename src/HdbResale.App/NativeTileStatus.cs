using System.Runtime.InteropServices;

namespace HdbResale.App;

// The native library owns the handler and counter; no callbacks or memory cross
// into managed code. It remains loaded until process exit, including late logs.
internal sealed partial class NativeTileStatus : IDisposable
{
    private bool disposed;
    private NativeTileStatus() => Start();
    public static NativeTileStatus Capture() => new();
    public void Dispose()
    {
        if (disposed) return;
        Stop();
        disposed = true;
    }

    [LibraryImport("hdb_tile_status", EntryPoint = "hdb_tile_status_start")]
    private static partial void Start();
    [LibraryImport("hdb_tile_status", EntryPoint = "hdb_tile_status_stop")]
    private static partial void Stop();
    [LibraryImport("hdb_tile_status", EntryPoint = "hdb_tile_status_exhausted")]
    internal static partial ulong ExhaustedRequests();
}
