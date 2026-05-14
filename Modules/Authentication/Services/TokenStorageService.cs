using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Persists per-session token records keyed by an opaque MCP session token (mcp_xxx).
/// The MCP session token is what the MCP client sees and presents in Authorization headers;
/// the HaloPSA access token is held server-side in the entry's AccessToken field and
/// never leaves the process. File contents are encrypted at rest via DataProtection
/// and the file is chmod 600 on Unix.
/// </summary>
public sealed class TokenStorageService : IDisposable {
    private const int MaxSessions = 5000;
    private const string McpTokenPrefix = "mcp_";
    private const string McpRefreshPrefix = "mcr_";

    private readonly string _tokenFilePath;
    private readonly ILogger<TokenStorageService> _logger;
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

    public TokenStorageService(
        AppConfig config,
        IDataProtectionProvider dpProvider,
        ILogger<TokenStorageService> logger) {
        _tokenFilePath = config.HaloPsa.TokenStorePath;
        _logger = logger;
        _protector = dpProvider.CreateProtector("HaloPsaMcp.TokenStorage.v1");

        var directory = Path.GetDirectoryName(_tokenFilePath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        // Synchronous load: the token file is read once at startup, before
        // any request can hit the service. Avoids the race where requests
        // could land before the cache was populated.
        LoadTokensFromDisk();

        // Watch the token file for out-of-band changes. When a second process
        // (e.g. a Claude Desktop stdio worker spawned alongside a manual
        // `dotnet run`) handles the OAuth callback, the file on disk is the
        // shared source of truth. Without this watcher our in-memory cache
        // silently diverges and GetDefaultToken returns stale/null entries.
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

    private void OnTokenFileChanged(object sender, FileSystemEventArgs e) {
        // Debounce: editors and our own writes can fire multiple events back-to-back.
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
            // Another process mid-write; the next event will retry.
            _fileLock.Release();
            return;
        } finally {
            if (_fileLock.CurrentCount == 0) {
                _fileLock.Release();
            }
        }

        if (string.Equals(raw, _lastPersistedPayload, StringComparison.Ordinal)) {
            return; // We wrote this; nothing to do.
        }

        string json;
        try {
            json = _protector.Unprotect(raw);
        } catch (CryptographicException) {
            json = raw; // legacy plaintext, same fallback as initial load
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

    /// <summary>Generates a new opaque MCP session token (mcp_{base64url(32)}).</summary>
    public static string GenerateMcpToken() => GenerateOpaque(McpTokenPrefix);

    /// <summary>Generates a new opaque MCP refresh token (mcr_{base64url(32)}).</summary>
    public static string GenerateMcpRefreshToken() => GenerateOpaque(McpRefreshPrefix);

    private static string GenerateOpaque(string prefix) {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var b64 = Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return prefix + b64;
    }

    /// <summary>
    /// Creates a new MCP session: returns (mcp access token, mcp refresh token).
    /// Both are opaque, distinct, and back the same upstream HaloPSA token pair.
    /// </summary>
    public async Task<(string AccessToken, string RefreshToken)> CreateSessionAsync(
        string haloPsaAccess, string? haloPsaRefresh, long expiresAt) {
        EnforceCap();
        var mcpToken = GenerateMcpToken();
        var mcpRefresh = GenerateMcpRefreshToken();
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

    /// <summary>Get the entry for an MCP session token (or null).</summary>
    public UserTokenEntry? GetToken(string mcpToken) {
        _memoryCache.TryGetValue(mcpToken, out var entry);
        return entry;
    }

    /// <summary>Most recently created session — used by stdio mode where there is no HTTP context.
    /// In HTTP mode this fallback is disabled to prevent cross-session token leakage.</summary>
    public UserTokenEntry? GetDefaultToken() {
        if (DisableDefaultFallback) {
            return null;
        }
        return _memoryCache.Values
            .OrderByDescending(t => t.ExpiresAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// Set true at startup in HTTP/AKS mode. When true, GetDefaultToken returns
    /// null instead of leaking the most-recent session to handlers without an
    /// authenticated HTTP context.
    /// </summary>
    public static bool DisableDefaultFallback { get; set; }

    /// <summary>Validates that an MCP session exists and has not expired.</summary>
    public bool IsValidSession(string mcpToken) {
        if (!_memoryCache.TryGetValue(mcpToken, out var entry)) {
            return false;
        }
        return entry.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Updates the HaloPSA tokens behind an existing MCP session (called after refresh).
    /// The opaque MCP token itself is unchanged so MCP clients keep working seamlessly.
    /// </summary>
    public async Task UpdateSessionTokensAsync(string mcpToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt) {
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

    /// <summary>
    /// Find a session by its MCP refresh token (mcr_*). O(n) over sessions; only
    /// called from the /token refresh path which is rate-limited.
    /// </summary>
    public KeyValuePair<string, UserTokenEntry>? FindByRefreshToken(string mcpRefresh) {
        foreach (var kvp in _memoryCache) {
            if (string.Equals(kvp.Value.McpRefreshToken, mcpRefresh, StringComparison.Ordinal)) {
                return kvp;
            }
        }
        return null;
    }

    /// <summary>
    /// Rotate the MCP refresh token after a successful refresh exchange.
    /// One-time-use semantics: the old refresh string becomes invalid the moment
    /// this returns. Caller must hand the new value back to the MCP client.
    /// </summary>
    public async Task<string> RotateRefreshTokenAsync(
        string mcpAccessToken,
        string newHaloAccess, string newHaloRefresh, long newExpiresAt) {
        if (!_memoryCache.TryGetValue(mcpAccessToken, out _)) {
            throw new InvalidOperationException("Cannot rotate refresh for unknown session");
        }
        var newMcpRefresh = GenerateMcpRefreshToken();
        _memoryCache[mcpAccessToken] = new UserTokenEntry {
            AccessToken = newHaloAccess,
            RefreshToken = newHaloRefresh,
            ExpiresAt = newExpiresAt,
            McpRefreshToken = newMcpRefresh
        };
        await PersistAsync().ConfigureAwait(false);
        return newMcpRefresh;
    }

    /// <summary>Removes expired sessions; called by background cleanup timer.</summary>
    public int PruneExpired() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var removed = 0;
        foreach (var kvp in _memoryCache.Where(x => x.Value.ExpiresAt <= now).ToArray()) {
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
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _memoryCache.Values.Any(t => t.ExpiresAt > now);
    }

    public int SessionCount => _memoryCache.Count;

    public int ActiveSessionCount {
        get {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return _memoryCache.Values.Count(t => t.ExpiresAt > now);
        }
    }

    private void EnforceCap() {
        if (_memoryCache.Count < MaxSessions) {
            return;
        }
        // Evict the oldest expiring (or already-expired) entries
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
            TrySetUnixPermissions(_tokenFilePath);
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
                // Legacy plaintext file — accept once, mark for re-persist.
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
            // One-time re-persist to encrypt the legacy plaintext file.
            _ = PersistAsync();
        }
    }

    internal static void TrySetUnixPermissions(string path) {
        try {
            if (!OperatingSystem.IsWindows()) {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        } catch {
            // Best-effort; ignore on platforms that don't support it
        }
        _ = CultureInfo.InvariantCulture; // anchor using
    }

    public void Dispose() {
        _watcher?.Dispose();
        _reloadDebounce?.Dispose();
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
