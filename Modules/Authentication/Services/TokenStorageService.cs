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
internal sealed class TokenStorageService : IDisposable {
    private const int MaxSessions = 5000;
    private const string McpTokenPrefix = "mcp_";

    private readonly string _tokenFilePath;
    private readonly ILogger<TokenStorageService> _logger;
    private readonly IDataProtector _protector;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, UserTokenEntry> _memoryCache = new();

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
    }

    /// <summary>Generates a new opaque MCP session token (mcp_{base64url(32)}).</summary>
    public static string GenerateMcpToken() {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var b64 = Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return McpTokenPrefix + b64;
    }

    /// <summary>
    /// Creates a new MCP session: returns an opaque mcp_ token mapped to the supplied
    /// HaloPSA access/refresh pair. The HaloPSA token never leaves the server.
    /// </summary>
    public async Task<string> CreateSessionAsync(string haloPsaAccess, string? haloPsaRefresh, long expiresAt) {
        EnforceCap();
        var mcpToken = GenerateMcpToken();
        var entry = new UserTokenEntry {
            AccessToken = haloPsaAccess,
            RefreshToken = haloPsaRefresh ?? string.Empty,
            ExpiresAt = expiresAt
        };
        _memoryCache[mcpToken] = entry;
        await PersistAsync().ConfigureAwait(false);
        _logger.LogInformation("MCP session created | mcpToken={Hint}", SecretRedactor.Hint(mcpToken));
        return mcpToken;
    }

    /// <summary>Get the entry for an MCP session token (or null).</summary>
    public UserTokenEntry? GetToken(string mcpToken) {
        _memoryCache.TryGetValue(mcpToken, out var entry);
        return entry;
    }

    /// <summary>Most recently created session — used by stdio mode where there is no HTTP context.</summary>
    public UserTokenEntry? GetDefaultToken() {
        return _memoryCache.Values
            .OrderByDescending(t => t.ExpiresAt)
            .FirstOrDefault();
    }

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
        if (!_memoryCache.TryGetValue(mcpToken, out _)) {
            _logger.LogWarning("Refresh-update for unknown session | mcpToken={Hint}", SecretRedactor.Hint(mcpToken));
            return;
        }
        _memoryCache[mcpToken] = new UserTokenEntry {
            AccessToken = newHaloAccess,
            RefreshToken = newHaloRefresh,
            ExpiresAt = newExpiresAt
        };
        await PersistAsync().ConfigureAwait(false);
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
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
