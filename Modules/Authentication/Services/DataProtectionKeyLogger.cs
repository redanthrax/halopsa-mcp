using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Hosting;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Logs the active DataProtection key at startup so operators can confirm
/// key rotation on long-lived pods (default 90-day lifetime).
/// </summary>
internal sealed class DataProtectionKeyLogger : IHostedService {
    private readonly IKeyManager _keyManager;
    private readonly ILogger<DataProtectionKeyLogger> _logger;

    public DataProtectionKeyLogger(IKeyManager keyManager, ILogger<DataProtectionKeyLogger> logger) {
        _keyManager = keyManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        var now = DateTimeOffset.UtcNow;
        var keys = _keyManager.GetAllKeys()
            .Where(k => !k.IsRevoked && k.ActivationDate <= now && k.ExpirationDate > now)
            .OrderByDescending(k => k.ActivationDate)
            .ToList();
        var active = keys.FirstOrDefault();
        if (active is null) {
            _logger.LogWarning("DataProtection: no active encryption key found in key ring");
            return Task.CompletedTask;
        }
        _logger.LogInformation(
            "DataProtection active key | id={KeyId} created={CreatedUtc} expires={ExpiresUtc} ringSize={RingSize}",
            active.KeyId,
            active.CreationDate.UtcDateTime.ToString("O"),
            active.ExpirationDate.UtcDateTime.ToString("O"),
            _keyManager.GetAllKeys().Count());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
