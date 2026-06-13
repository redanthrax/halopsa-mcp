using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// File-backed <see cref="ITokenStore"/> for single-instance deployments (stdio, Docker,
/// single-replica Kubernetes). Encrypts tokens.json at rest via DataProtection.
/// </summary>
public sealed class FileTokenStore : ITokenStore {
    private const int MaxSessions = 5000;

    private readonly string _tokenFilePath;
    private readonly ILogger<FileTokenStore> _logger;
    private readonly IDataProtector _protector;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, UserTokenEntry> _memoryCache = new();
    private readonly FileSystemWatcher? _watcher;
    private string? _lastPersistedPayload;
    private Timer? _reloadDebounce;
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = false
    };

    public string Backend => "file";

    public FileTokenStore(
        AppConfig config,
        IDataProtectionProvider dpProvider,
        ILogger<FileTokenStore> logger) {
        _tokenFilePath = config.HaloPsa.TokenStorePath;
        _logger = logger;
        _protector = dpProvider.CreateProtector("HaloPsaMcp.TokenStorage.v1");

        var directory = Path.GetDirectoryName(_tokenFilePath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        LoadTokensFromDisk();

        try {
            var dir = Path.GetDirectoryName(_tokenFilePath);
            var name = Path.GetFileName(_tokenFilePath);
            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(name)) {
                _watcher = new FileSystemWatcher(dir, name) {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnTokenFileChanged;
                _watcher.Created += OnTokenFileChanged;
                _watcher.Renamed += OnTokenFileChanged;
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Could not start token-file watcher; cross-process token sync disabled");
        }
    }

    public async Task<(string AccessToken, string RefreshToken)> CreateSessionAsync(
        string haloPsaAccess, string? haloPsaRefresh, long expiresAt) {
        EnforceCap();
        var mcpToken = McpTokenGenerator.GenerateMcpToken();
        var mcpRefresh = McpTokenGenerator.GenerateMcpRefreshToken();
        var entry = new UserTokenEntry {
            AccessToken = haloPsaAccess,
            RefreshToken = haloPsaRefresh ?? string.Empty,
            ExpiresAt = expiresAt,
            McpRefreshToken = mcpRefresh
        };
        _memoryCache[mcpToken] = entry;
        await PersistAsync().ConfigureAwait(false);
        _logger.LogInformation("MCP session created | mcpToken={Hint}", SecretRedactor.Hint(mcpToken));
        return (mcpToken, mcpRefresh);
    }

    public UserTokenEntry? GetToken(string mcpToken) {
        _memoryCache.TryGetValue(mcpToken, out var entry);
        return entry;
    }

    public UserTokenEntry? GetDefaultToken() => GetDefaultSession()?.Value;

    public KeyValuePair<string, UserTokenEntry>? GetDefaultSession() {
        if (TokenStoreRuntime.DisableDefaultFallback) {
            return null;
        }
        var best = _memoryCache
            .Where(kvp => SessionValidity.IsUsable(kvp.Value))
            .OrderByDescending(kvp => kvp.Value.ExpiresAt)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(best.Key)) {
            return null;
        }
        return best;
    }

    public bool IsValidSession(string mcpToken) {
        if (!_memoryCache.TryGetValue(mcpToken, out var entry)) {
            return false;
        }
        return SessionValidity.IsUsable(entry);
    }

    public async Task UpdateSessionTokensAsync(
        string mcpToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt) {
        if (!_memoryCache.TryGetValue(mcpToken, out var existing)) {
            _logger.LogWarning("Refresh-update for unknown session | mcpToken={Hint}", SecretRedactor.Hint(mcpToken));
            return;
        }
        _memoryCache[mcpToken] = new UserTokenEntry {
            AccessToken = newHaloAccess,
            RefreshToken = newHaloRefresh,
            ExpiresAt = newExpiresAt,
            McpRefreshToken = existing.McpRefreshToken
        };
        await PersistAsync().ConfigureAwait(false);
    }

    public KeyValuePair<string, UserTokenEntry>? FindByRefreshToken(string mcpRefresh) {
        foreach (var kvp in _memoryCache) {
            if (string.Equals(kvp.Value.McpRefreshToken, mcpRefresh, StringComparison.Ordinal)) {
                return kvp;
            }
        }
        return null;
    }

    public async Task<string> RotateRefreshTokenAsync(
        string mcpAccessToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt) {
        if (!_memoryCache.TryGetValue(mcpAccessToken, out _)) {
            throw new InvalidOperationException("Cannot rotate refresh for unknown session");
        }
        var newMcpRefresh = McpTokenGenerator.GenerateMcpRefreshToken();
        _memoryCache[mcpAccessToken] = new UserTokenEntry {
            AccessToken = newHaloAccess,
            RefreshToken = newHaloRefresh,
            ExpiresAt = newExpiresAt,
            McpRefreshToken = newMcpRefresh
        };
        await PersistAsync().ConfigureAwait(false);
        return newMcpRefresh;
    }

    public async Task<bool> InvalidateSessionAsync(string mcpToken) {
        if (!_memoryCache.TryRemove(mcpToken, out _)) {
            return false;
        }
        await PersistAsync().ConfigureAwait(false);
        _logger.LogInformation("MCP session removed | mcpToken={Hint}", SecretRedactor.Hint(mcpToken));
        return true;
    }

    public int PruneExpired() {
        var removed = 0;
        foreach (var kvp in _memoryCache.Where(x => !SessionValidity.IsUsable(x.Value)).ToArray()) {
            if (_memoryCache.TryRemove(kvp.Key, out _)) {
                removed++;
            }
        }
        if (removed > 0) {
            _ = PersistAsync();
        }
        return removed;
    }

    public bool HasValidTokens() {
        return _memoryCache.Values.Any(e => SessionValidity.IsUsable(e));
    }

    public int SessionCount => _memoryCache.Count;

    public int ActiveSessionCount => _memoryCache.Values.Count(e => SessionValidity.IsUsable(e));

    public ValueTask<bool> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);

    public void Dispose() {
        _watcher?.Dispose();
        _reloadDebounce?.Dispose();
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnTokenFileChanged(object sender, FileSystemEventArgs e) {
        _reloadDebounce?.Dispose();
        _reloadDebounce = new Timer(_ => {
            try {
                ReloadIfChanged();
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Token-file reload failed");
            }
        }, null, ReloadDebounce, Timeout.InfiniteTimeSpan);
    }

    private void ReloadIfChanged() {
        if (!File.Exists(_tokenFilePath)) {
            return;
        }
        string raw;
        _fileLock.Wait();
        try {
            raw = File.ReadAllText(_tokenFilePath);
        } catch (IOException) {
            _fileLock.Release();
            return;
        } finally {
            if (_fileLock.CurrentCount == 0) {
                _fileLock.Release();
            }
        }

        if (string.Equals(raw, _lastPersistedPayload, StringComparison.Ordinal)) {
            return;
        }

        string json;
        try {
            json = _protector.Unprotect(raw);
        } catch (CryptographicException) {
            json = raw;
        }

        ConcurrentDictionary<string, UserTokenEntry>? incoming;
        try {
            incoming = JsonSerializer.Deserialize<ConcurrentDictionary<string, UserTokenEntry>>(json);
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "Token file changed but content is unparseable; ignoring");
            return;
        }
        if (incoming is null) {
            return;
        }

        var added = 0;
        var removed = 0;
        foreach (var kvp in incoming) {
            _memoryCache[kvp.Key] = kvp.Value;
            added++;
        }
        foreach (var key in _memoryCache.Keys.ToArray()) {
            if (!incoming.ContainsKey(key)) {
                _memoryCache.TryRemove(key, out _);
                removed++;
            }
        }
        _lastPersistedPayload = raw;
        _logger.LogInformation(
            "Reloaded token store from disk | sessions={Total} addedOrUpdated={Added} removed={Removed}",
            _memoryCache.Count, added, removed);
    }

    private void EnforceCap() {
        if (_memoryCache.Count < MaxSessions) {
            return;
        }
        var victims = _memoryCache
            .OrderBy(kvp => kvp.Value.ExpiresAt)
            .Take(_memoryCache.Count - MaxSessions + 1)
            .Select(kvp => kvp.Key)
            .ToArray();
        foreach (var v in victims) {
            _memoryCache.TryRemove(v, out _);
        }
    }

    private async Task PersistAsync() {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try {
            var json = JsonSerializer.Serialize(_memoryCache, JsonOptions);
            var protectedPayload = _protector.Protect(json);
            await File.WriteAllTextAsync(_tokenFilePath, protectedPayload).ConfigureAwait(false);
            _lastPersistedPayload = protectedPayload;
            UnixFilePermissions.TrySetUserReadWrite(_tokenFilePath);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to persist token store");
        } finally {
            _fileLock.Release();
        }
    }

    private void LoadTokensFromDisk() {
        if (!File.Exists(_tokenFilePath)) {
            return;
        }
        var migratedFromPlaintext = false;
        _fileLock.Wait();
        try {
            var raw = File.ReadAllText(_tokenFilePath);
            _lastPersistedPayload = raw;
            string json;
            try {
                json = _protector.Unprotect(raw);
            } catch (CryptographicException) {
                json = raw;
                migratedFromPlaintext = true;
                _logger.LogWarning("Token store at {Path} is unencrypted; migrating to DataProtection encryption", _tokenFilePath);
            }
            var tokens = JsonSerializer.Deserialize<ConcurrentDictionary<string, UserTokenEntry>>(json);
            if (tokens != null) {
                foreach (var kvp in tokens) {
                    _memoryCache[kvp.Key] = kvp.Value;
                }
                _logger.LogInformation("Loaded {Count} session(s) from {Path}",
                    _memoryCache.Count, _tokenFilePath);
            }
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "Corrupted token file at {Path}, starting fresh", _tokenFilePath);
        } finally {
            _fileLock.Release();
        }

        if (migratedFromPlaintext) {
            _ = PersistAsync();
        }
        _ = CultureInfo.InvariantCulture;
    }
}
