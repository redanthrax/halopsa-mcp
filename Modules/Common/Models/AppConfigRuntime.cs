namespace HaloPsaMcp.Modules.Common.Models;

/// <summary>
/// Runtime overrides for URLs discovered at startup (e.g. ephemeral OAuth port in stdio mode).
/// </summary>
public static class AppConfigRuntime {
    /// <summary>
    /// When set, used instead of <see cref="AppConfig.PublicBaseUrl"/> for login links and OAuth callbacks.
    /// </summary>
    public static string? EffectivePublicBaseUrl { get; set; }

    /// <summary>True when stdio mode bound an ephemeral port because the configured port was taken.</summary>
    public static bool PortFallbackActive { get; set; }

    public static string ResolvePublicBaseUrl(AppConfig config) {
        var url = EffectivePublicBaseUrl ?? config.PublicBaseUrl;
        return url.TrimEnd('/');
    }

    /// <summary>OAuth issuer and callback base URL (matches the listening port in stdio mode).</summary>
    public static string ResolveAuthBaseUrl(AppConfig config) => ResolvePublicBaseUrl(config);

    public static string OAuthCallbackUrl(AppConfig config) =>
        $"{ResolveAuthBaseUrl(config)}/callback";
}
