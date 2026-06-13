using System.Collections.Concurrent;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Persistent store for OAuth 2.1 Dynamically-Registered Clients.
/// File-backed JSON at {TokenStorePath dir}/clients.json, encrypted with DataProtection.
/// Prunes idle clients and evicts oldest-unused before returning 503 at capacity.
/// </summary>
public sealed class ClientRegistrationStore : IDisposable {
    private const int MaxClients = 1000;
    private static readonly TimeSpan IdleTtl = TimeSpan.FromDays(30);

    private readonly string _filePath;
    private readonly IDataProtector _protector;
    private readonly ILogger<ClientRegistrationStore> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, RegisteredClient> _cache = new();
    private FileSystemWatcher? _watcher;
    private string? _lastPersistedPayload;
    private Timer? _reloadDebounce;
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public ClientRegistrationStore(
        AppConfig config,
        IDataProtectionProvider dpProvider,
        ILogger<ClientRegistrationStore> logger) {
        var dir = Path.GetDirectoryName(config.HaloPsa.TokenStorePath) ?? ".";
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "clients.json");
        _protector = dpProvider.CreateProtector("HaloPsaMcp.ClientRegistrations.v1");
        _logger = logger;
        _ = LoadAsync();
        StartWatcher(dir);
    }

    public int Count => _cache.Count;
    public bool IsAtCapacity => _cache.Count >= MaxClients;

    public async Task<bool> AddAsync(RegisteredClient client) {
        PruneIdleClients();
        if (_cache.Count >= MaxClients && !EvictOldestUnused()) {
            return false;
        }
        _cache[client.ClientId] = client;
        await PersistAsync().ConfigureAwait(false);
        return true;
    }

    public RegisteredClient? Get(string clientId) {
        if (_cache.TryGetValue(clientId, out var c)) {
            Touch(clientId);
            return c;
        }
        ReloadIfChanged();
        if (_cache.TryGetValue(clientId, out c)) {
            Touch(clientId);
            return c;
        }
        return null;
    }

    /// <summary>
    /// Validates redirect_uri against registered URIs after normalization.
    /// </summary>
    public bool ValidateRedirectUri(string clientId, string redirectUri) {
        if (!_cache.TryGetValue(clientId, out var c)) {
            ReloadIfChanged();
            if (!_cache.TryGetValue(clientId, out c)) {
                return false;
            }
        }
        Touch(clientId);
        var normalized = RedirectUriNormalizer.Normalize(redirectUri);
        return c.RedirectUris.Contains(normalized, StringComparer.Ordinal);
    }

    public void Touch(string clientId) {
        if (!_cache.TryGetValue(clientId, out var existing)) {
            return;
        }
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (existing.LastUsedAt >= now - 60_000) {
            return;
        }
        _cache[clientId] = existing with { LastUsedAt = now };
        _ = PersistAsync();
    }

    private void PruneIdleClients() {
        var cutoff = DateTimeOffset.UtcNow.Subtract(IdleTtl).ToUnixTimeMilliseconds();
        var stale = _cache
            .Where(kvp => kvp.Value.LastUsedAt < cutoff && kvp.Value.CreatedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToArray();
        if (stale.Length == 0) {
            return;
        }
        foreach (var id in stale) {
            _cache.TryRemove(id, out _);
        }
        _logger.LogInformation("DCR prune | removedIdle={Count} remaining={Remaining}",
            stale.Length, _cache.Count);
        _ = PersistAsync();
    }

    private bool EvictOldestUnused() {
        var victim = _cache.Values
            .OrderBy(c => c.LastUsedAt)
            .ThenBy(c => c.CreatedAt)
            .FirstOrDefault();
        if (victim is null) {
            return false;
        }
        _cache.TryRemove(victim.ClientId, out _);
        _logger.LogWarning(
            "DCR evicted oldest-unused client | client={ClientId} lastUsed={LastUsed}",
            SecretRedactor.Hint(victim.ClientId), victim.LastUsedAt);
        _ = PersistAsync();
        return _cache.Count < MaxClients;
    }

    /// <summary>Prepare store before accepting a new registration at capacity.</summary>
    public async Task<bool> TryMakeRoomAsync() {
        PruneIdleClients();
        while (_cache.Count >= MaxClients) {
            if (!EvictOldestUnused()) {
                return false;
            }
        }
        await PersistAsync().ConfigureAwait(false);
        return true;
    }

    private async Task PersistAsync() {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try {
            var json = JsonSerializer.Serialize(_cache, JsonOptions);
            var payload = _protector.Protect(json);
            await File.WriteAllTextAsync(_filePath, payload).ConfigureAwait(false);
            _lastPersistedPayload = payload;
            UnixFilePermissions.TrySetUserReadWrite(_filePath);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to persist client registrations");
        } finally {
            _fileLock.Release();
        }
    }

    private async Task LoadAsync() {
        if (!File.Exists(_filePath)) {
            return;
        }
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try {
            ApplyLoaded(await File.ReadAllTextAsync(_filePath).ConfigureAwait(false));
        } finally {
            _fileLock.Release();
        }
    }

    private void ApplyLoaded(string raw) {
        string json;
        try {
            json = _protector.Unprotect(raw);
        } catch (System.Security.Cryptography.CryptographicException) {
            json = raw;
        }

        var loaded = JsonSerializer.Deserialize<ConcurrentDictionary<string, RegisteredClient>>(json);
        if (loaded == null) {
            return;
        }
        foreach (var kvp in loaded) {
            var client = kvp.Value;
            if (client.LastUsedAt == 0) {
                client = client with { LastUsedAt = client.CreatedAt };
            }
            _cache[kvp.Key] = client;
        }
        _lastPersistedPayload = raw;
        _logger.LogInformation("Loaded {Count} registered client(s) from {Path}",
            _cache.Count, _filePath);
    }

    private void StartWatcher(string dir) {
        try {
            var name = Path.GetFileName(_filePath);
            _watcher = new FileSystemWatcher(dir, name) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Could not start client-registration watcher");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) {
        _reloadDebounce?.Dispose();
        _reloadDebounce = new Timer(_ => {
            try {
                ReloadIfChanged();
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Client registration reload failed");
            }
        }, null, ReloadDebounce, Timeout.InfiniteTimeSpan);
    }

    private void ReloadIfChanged() {
        if (!File.Exists(_filePath)) {
            return;
        }
        string raw;
        _fileLock.Wait();
        try {
            raw = File.ReadAllText(_filePath);
        } catch (IOException) {
            return;
        } finally {
            if (_fileLock.CurrentCount == 0) {
                _fileLock.Release();
            }
        }

        if (string.Equals(raw, _lastPersistedPayload, StringComparison.Ordinal)) {
            return;
        }

        ApplyLoaded(raw);
        _logger.LogInformation("Reloaded client registrations | count={Count}", _cache.Count);
    }

    public void Dispose() {
        _watcher?.Dispose();
        _reloadDebounce?.Dispose();
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
