using Microsoft.Extensions.Hosting;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Periodic background sweep that removes expired entries from
/// OAuthStateManager and ITokenStore. Runs every 5 minutes.
/// </summary>
internal sealed class CleanupHostedService : BackgroundService {
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly ITokenStore _tokens;
    private readonly ILogger<CleanupHostedService> _logger;

    public CleanupHostedService(ITokenStore tokens, ILogger<CleanupHostedService> logger) {
        _tokens = tokens;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                OAuthStateManager.CleanExpiredEntries();
                var pruned = _tokens.PruneExpired();
                if (pruned > 0) {
                    _logger.LogInformation("Periodic cleanup | prunedSessions={Count}", pruned);
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Periodic cleanup failed");
            }
            try {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }
}
