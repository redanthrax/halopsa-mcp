#pragma warning disable IDE0005
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0005

namespace HaloPsaMcp.Modules.Authentication;

/// <summary>
/// Authentication module registration — OAuth, DCR store, token storage,
/// background cleanup, and DataProtection.
/// </summary>
internal class AuthenticationModuleRegistrar : IModuleRegistrar {
    public int Priority => 2;

    public void Register(IServiceCollection services, IConfiguration configuration) {
        // DataProtection (encrypts tokens.json + clients.json at rest).
        // Keys persisted to ./data/dp-keys so they survive restarts.
        var keyDir = Environment.GetEnvironmentVariable("HALOPSA_DPKEY_DIR") ?? "./data/dp-keys";
        Directory.CreateDirectory(keyDir);
        UnixFilePermissions.TrySetDirectoryUserOnly(keyDir);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyDir));

        services.AddTokenStore();
        services.AddSingleton<ClientRegistrationStore>();
        services.AddSingleton<McpAuthenticationService>();
        services.AddHostedService<CleanupHostedService>();
        services.AddHttpContextAccessor();
    }
}
