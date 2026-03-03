using System.Collections.Concurrent;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Service for persisting user tokens to file system
/// Enables token sharing between HTTP OAuth server and stdio MCP transport
/// </summary>
internal class TokenStorageService : IDisposable {
    private readonly string _tokenFilePath;
    private readonly ILogger<TokenStorageService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, UserTokenEntry> _memoryCache = new();
    
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    public TokenStorageService(AppConfig config, ILogger<TokenStorageService> logger) {
        _tokenFilePath = config.HaloPsa.TokenStorePath;
        _logger = logger;
        
        var directory = Path.GetDirectoryName(_tokenFilePath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }
        
        _ = LoadTokensAsync();
    }

    /// <summary>
    /// Save a user token to both memory and file
    /// </summary>
    public async Task SaveTokenAsync(string accessToken, UserTokenEntry entry) {
        _memoryCache[accessToken] = entry;
        
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try {
            var json = JsonSerializer.Serialize(_memoryCache, JsonOptions);
            await File.WriteAllTextAsync(_tokenFilePath, json).ConfigureAwait(false);
            _logger.LogDebug("Token persisted to {Path}", _tokenFilePath);
        } finally {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Get a specific token by access token value
    /// </summary>
    public UserTokenEntry? GetToken(string accessToken) {
        _memoryCache.TryGetValue(accessToken, out var entry);
        return entry;
    }

    /// <summary>
    /// Get the most recently saved token (for stdio transport with no HTTP context)
    /// </summary>
    public UserTokenEntry? GetDefaultToken() {
        // Return the token with the furthest expiry (most recently obtained)
        return _memoryCache.Values
            .OrderByDescending(t => t.ExpiresAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// Update an existing token (called when token is refreshed)
    /// </summary>
    public async Task UpdateTokenAsync(string oldAccessToken, string newAccessToken, string newRefreshToken, long expiresAt) {
        // Remove old token
        _memoryCache.TryRemove(oldAccessToken, out _);
        
        // Add new token
        var entry = new UserTokenEntry {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt
        };
        
        await SaveTokenAsync(newAccessToken, entry).ConfigureAwait(false);
    }

    /// <summary>
    /// Load tokens from file into memory cache
    /// </summary>
    private async Task LoadTokensAsync() {
        if (!File.Exists(_tokenFilePath)) {
            return;
        }

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try {
            var json = await File.ReadAllTextAsync(_tokenFilePath).ConfigureAwait(false);
            var tokens = JsonSerializer.Deserialize<ConcurrentDictionary<string, UserTokenEntry>>(json);
            if (tokens != null) {
                foreach (var kvp in tokens) {
                    _memoryCache[kvp.Key] = kvp.Value;
                }
                _logger.LogInformation("Loaded {Count} token(s) from {Path}", _memoryCache.Count, _tokenFilePath);
            }
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "Corrupted token file at {Path}, starting fresh", _tokenFilePath);
        } finally {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Check if any valid tokens exist (for health checks)
    /// </summary>
    public bool HasValidTokens() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _memoryCache.Values.Any(t => t.ExpiresAt > now);
    }

    public void Dispose() {
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
