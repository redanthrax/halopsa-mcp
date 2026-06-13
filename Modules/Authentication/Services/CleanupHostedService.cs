using Microsoft.Extensions.Hosting;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Periodic background sweep that removes expired entries from
/// OAuth flow store and ITokenStore. Runs every 5 minutes.
/// </summary>
internal sealed class CleanupHostedService : BackgroundService {
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly ITokenStore _tokens;
    private readonly IOAuthFlowStore _oauth;
    private readonly ILogger<CleanupHostedService> _logger;

    public CleanupHostedService(
        ITokenStore tokens,
        IOAuthFlowStore oauth,
        ILogger<CleanupHostedService> logger) {
        _tokens = tokens;
        _oauth = oauth;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var oauthRemoved = _oauth.CleanExpiredEntries();
                var pruned = _tokens.PruneExpired();
                if (pruned > 0 || oauthRemoved > 0) {
                    _logger.LogInformation(
                        "Periodic cleanup | prunedSessions={Sessions} oauthEntries={OAuth}",
                        pruned, oauthRemoved);
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
