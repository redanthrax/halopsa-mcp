using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace HaloPsaMcp.Modules.Authentication.Services;

internal static class TokenStoreRegistration {
    public static IServiceCollection AddTokenStore(this IServiceCollection services) {
        services.AddSingleton<ITokenStore>(CreateStore);
        return services;
    }

    private static ITokenStore CreateStore(IServiceProvider sp) {
        var appConfig = sp.GetRequiredService<AppConfig>();
        var backend = appConfig.HaloPsa.TokenStoreBackend;

        if (string.Equals(backend, "redis", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(appConfig.HaloPsa.RedisConnection)) {
                throw new InvalidOperationException(
                    "HALOPSA_TOKEN_STORE_BACKEND=redis requires HALOPSA_REDIS_CONNECTION.");
            }
            var redis = ConnectionMultiplexer.Connect(appConfig.HaloPsa.RedisConnection);
            return new RedisTokenStore(
                redis,
                sp.GetRequiredService<ILogger<RedisTokenStore>>());
        }

        if (!string.Equals(backend, "file", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                $"Unknown HALOPSA_TOKEN_STORE_BACKEND '{backend}'. Supported values: file, redis.");
        }

        return new FileTokenStore(
            appConfig,
            sp.GetRequiredService<IDataProtectionProvider>(),
            sp.GetRequiredService<ILogger<FileTokenStore>>());
    }
}
