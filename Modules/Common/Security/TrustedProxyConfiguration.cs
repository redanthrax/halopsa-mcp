using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace HaloPsaMcp.Modules.Common.Security;

internal static class TrustedProxyConfiguration {
    // Private RFC1918 ranges — typical in-cluster ingress → pod traffic.
    private static readonly string[] DefaultTrustedCidrs = [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16"
    ];

    internal static void Configure(IServiceCollection services) {
        services.Configure<ForwardedHeadersOptions>(options => {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;

            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            var raw = Environment.GetEnvironmentVariable("TRUSTED_PROXY_CIDRS");
            if (string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase)) {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            var cidrs = string.IsNullOrWhiteSpace(raw) ? DefaultTrustedCidrs : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var cidr in cidrs) {
                if (TryParseCidr(cidr, out var network)) {
                    options.KnownNetworks.Add(network);
                }
            }
        });
    }

    private static bool TryParseCidr(string cidr, out Microsoft.AspNetCore.HttpOverrides.IPNetwork network) {
        network = default!;
        var parts = cidr.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefix)) {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength)) {
            return false;
        }

        network = new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength);
        return true;
    }
}
