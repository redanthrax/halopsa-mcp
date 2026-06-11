namespace HaloPsaMcp.Modules.Authentication.Services;

internal static class UnixFilePermissions {
    private static readonly UnixFileMode UserReadWrite =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly UnixFileMode UserReadWriteExecute =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    internal static void TrySetUserReadWrite(string path) {
        try {
            if (!OperatingSystem.IsWindows()) {
                File.SetUnixFileMode(path, UserReadWrite);
            }
        } catch {
            // Best-effort; ignore on platforms that don't support it
        }
    }

    internal static void TrySetDirectoryUserOnly(string path) {
        try {
            if (!OperatingSystem.IsWindows()) {
                File.SetUnixFileMode(path, UserReadWriteExecute);
            }
        } catch {
            // Best-effort
        }
    }
}
