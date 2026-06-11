namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Process-wide token store behaviour toggles.
/// </summary>
public static class TokenStoreRuntime {
    /// <summary>
    /// Set true at startup in HTTP/AKS mode. When true, <see cref="ITokenStore.GetDefaultToken"/>
    /// returns null instead of leaking the most-recent session to handlers without an
    /// authenticated HTTP context.
    /// </summary>
    public static bool DisableDefaultFallback { get; set; }
}
