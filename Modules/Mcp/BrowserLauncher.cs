using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HaloPsaMcp.Modules.Mcp;

internal static class BrowserLauncher {
    internal static bool TryOpen(string url, ILogger logger) {
        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Process.Start("open", url);
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }

            Process.Start("xdg-open", url);
            return true;
        } catch (Exception ex) {
            logger.LogWarning(ex, "Could not open browser for {Url}", url);
            return false;
        }
    }
}
