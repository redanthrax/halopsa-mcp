using System.Collections.Concurrent;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Persistent store for OAuth 2.1 Dynamically-Registered Clients.
/// File-backed JSON at {TokenStorePath dir}/clients.json, encrypted with DataProtection.
/// Bounded by MaxClients to defend against DCR-flooding attacks.
/// </summary>
public sealed class ClientRegistrationStore : IDisposable {
    private const int MaxClients = 1000;

    private readonly string _filePath;
    private readonly IDataProtector _protector;
    private readonly ILogger<ClientRegistrationStore> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, RegisteredClient> _cache = new();

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
    }

    public int Count => _cache.Count;
    public bool IsAtCapacity => _cache.Count >= MaxClients;

    public async Task<bool> AddAsync(RegisteredClient client) {
        if (_cache.Count >= MaxClients) {
            return false;
        }
        _cache[client.ClientId] = client;
        await PersistAsync().ConfigureAwait(false);
        return true;
    }

    public RegisteredClient? Get(string clientId) {
        _cache.TryGetValue(clientId, out var c);
        return c;
    }

    /// <summary>Strict equality between supplied redirect_uri and one of the client's registered URIs.</summary>
    public bool ValidateRedirectUri(string clientId, string redirectUri) {
        if (!_cache.TryGetValue(clientId, out var c)) {
            return false;
        }
        return c.RedirectUris.Contains(redirectUri, StringComparer.Ordinal);
    }

    private async Task PersistAsync() {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try {
            var json = JsonSerializer.Serialize(_cache, JsonOptions);
            var payload = _protector.Protect(json);
            await File.WriteAllTextAsync(_filePath, payload).ConfigureAwait(false);
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
            var raw = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            string json;
            try { json = _protector.Unprotect(raw); }
            catch (System.Security.Cryptography.CryptographicException) { json = raw; }

            var loaded = JsonSerializer.Deserialize<ConcurrentDictionary<string, RegisteredClient>>(json);
            if (loaded != null) {
                foreach (var kvp in loaded) {
                    _cache[kvp.Key] = kvp.Value;
                }
                _logger.LogInformation("Loaded {Count} registered client(s) from {Path}",
                    _cache.Count, _filePath);
            }
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "Corrupted client registration file, starting fresh");
        } finally {
            _fileLock.Release();
        }
    }

    public void Dispose() {
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
