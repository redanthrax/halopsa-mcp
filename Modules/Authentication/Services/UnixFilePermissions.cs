namespace HaloPsaMcp.Modules.Authentication.Services;

internal static class UnixFilePermissions {
    internal static void TrySetUserReadWrite(string path) {
        try {
            if (!OperatingSystem.IsWindows()) {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        } catch {
            // Best-effort; ignore on platforms that don't support it
        }
    }
}
