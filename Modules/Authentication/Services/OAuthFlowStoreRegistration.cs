using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace HaloPsaMcp.Modules.Authentication.Services;

internal static class OAuthFlowStoreRegistration {
    public static IServiceCollection AddOAuthFlowStore(this IServiceCollection services) {
        services.AddSingleton<IOAuthFlowStore>(CreateStore);
        return services;
    }

    private static IOAuthFlowStore CreateStore(IServiceProvider sp) {
        var appConfig = sp.GetRequiredService<AppConfig>();
        var backend = appConfig.HaloPsa.TokenStoreBackend;

        if (string.Equals(backend, "redis", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(appConfig.HaloPsa.RedisConnection)) {
                throw new InvalidOperationException(
                    "HALOPSA_TOKEN_STORE_BACKEND=redis requires HALOPSA_REDIS_CONNECTION.");
            }
            var redis = sp.GetService<IConnectionMultiplexer>()
                ?? ConnectionMultiplexer.Connect(appConfig.HaloPsa.RedisConnection);
            return new RedisOAuthFlowStore(
                redis,
                sp.GetRequiredService<ILogger<RedisOAuthFlowStore>>());
        }

        // File backend: persist + watcher for best-effort cross-pod reads.
        return new FileOAuthFlowStore(
            appConfig,
            sp.GetRequiredService<IDataProtectionProvider>(),
            sp.GetRequiredService<ILogger<FileOAuthFlowStore>>());
    }
}
