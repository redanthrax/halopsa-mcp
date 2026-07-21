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

            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            var raw = Environment.GetEnvironmentVariable("TRUSTED_PROXY_CIDRS");
            if (string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase)) {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            var cidrs = string.IsNullOrWhiteSpace(raw) ? DefaultTrustedCidrs : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var cidr in cidrs) {
                if (TryParseCidr(cidr, out var network)) {
                    options.KnownIPNetworks.Add(network);
                }
            }
        });
    }

    private static bool TryParseCidr(string cidr, out System.Net.IPNetwork network) =>
        System.Net.IPNetwork.TryParse(cidr, out network);
}
