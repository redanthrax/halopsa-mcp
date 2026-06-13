using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// File-backed OAuth flow store with cross-process reload via FileSystemWatcher.
/// Best-effort multi-replica consistency when combined with a shared RWX volume;
/// use Redis backend for production HA.
/// </summary>
public sealed class FileOAuthFlowStore : IOAuthFlowStore, IDisposable {
    private const int MaxPending = 10_000;
    private const int MaxCompleted = 10_000;

    private readonly string _filePath;
    private readonly IDataProtector _protector;
    private readonly ILogger<FileOAuthFlowStore> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, PendingAuth> _pending = new();
    private readonly ConcurrentDictionary<string, CompletedAuth> _completed = new();
    private FileSystemWatcher? _watcher;
    private string? _lastPersistedPayload;
    private Timer? _reloadDebounce;
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public FileOAuthFlowStore(
        AppConfig config,
        IDataProtectionProvider dpProvider,
        ILogger<FileOAuthFlowStore> logger) {
        var dir = Path.GetDirectoryName(config.HaloPsa.TokenStorePath) ?? ".";
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "oauth-flow.json");
        _protector = dpProvider.CreateProtector("HaloPsaMcp.OAuthFlow.v1");
        _logger = logger;
        LoadFromDisk();
        StartWatcher(dir);
    }

    public int PendingCount => _pending.Count;
    public int CompletedCount => _completed.Count;

    public void AddPending(string key, PendingAuth value) {
        EnforceCap(_pending, MaxPending);
        _pending[key] = value;
        _ = PersistAsync();
    }

    public bool TryRemovePending(string key, out PendingAuth? value) {
        var removed = _pending.TryRemove(key, out value);
        if (removed) {
            _ = PersistAsync();
        }
        return removed;
    }

    public void AddCompleted(string key, CompletedAuth value) {
        EnforceCap(_completed, MaxCompleted);
        _completed[key] = value;
        _ = PersistAsync();
    }

    public bool TryRemoveCompleted(string key, out CompletedAuth? value) {
        var removed = _completed.TryRemove(key, out value);
        if (removed) {
            _ = PersistAsync();
        }
        return removed;
    }

    public int CleanExpiredEntries() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var removed = 0;
        foreach (var kvp in _pending.Where(x => now > x.Value.Expires).ToArray()) {
            if (_pending.TryRemove(kvp.Key, out _)) {
                removed++;
            }
        }
        foreach (var kvp in _completed.Where(x => now > x.Value.Expires).ToArray()) {
            if (_completed.TryRemove(kvp.Key, out _)) {
                removed++;
            }
        }
        if (removed > 0) {
            _ = PersistAsync();
        }
        return removed;
    }

    public void Dispose() {
        _watcher?.Dispose();
        _reloadDebounce?.Dispose();
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
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
            _logger.LogWarning(ex, "Could not start oauth-flow watcher; cross-pod OAuth sync disabled");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) {
        _reloadDebounce?.Dispose();
        _reloadDebounce = new Timer(_ => {
            try {
                ReloadIfChanged();
            } catch (Exception ex) {
                _logger.LogWarning(ex, "OAuth flow file reload failed");
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

        string json;
        try {
            json = _protector.Unprotect(raw);
        } catch (CryptographicException) {
            json = raw;
        }

        OAuthFlowSnapshot? snapshot;
        try {
            snapshot = JsonSerializer.Deserialize<OAuthFlowSnapshot>(json, JsonOptions);
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "OAuth flow file changed but content is unparseable; ignoring");
            return;
        }
        if (snapshot is null) {
            return;
        }

        Merge(snapshot.Pending, _pending);
        Merge(snapshot.Completed, _completed);
        _lastPersistedPayload = raw;
        _logger.LogInformation(
            "Reloaded OAuth flow store | pending={Pending} completed={Completed}",
            _pending.Count, _completed.Count);
    }

    private static void Merge<T>(Dictionary<string, T>? source, ConcurrentDictionary<string, T> target) {
        if (source is null) {
            return;
        }
        foreach (var kvp in source) {
            target[kvp.Key] = kvp.Value;
        }
    }

    private void LoadFromDisk() {
        if (!File.Exists(_filePath)) {
            return;
        }
        _fileLock.Wait();
        try {
            var raw = File.ReadAllText(_filePath);
            _lastPersistedPayload = raw;
            string json;
            try {
                json = _protector.Unprotect(raw);
            } catch (CryptographicException) {
                json = raw;
            }
            var snapshot = JsonSerializer.Deserialize<OAuthFlowSnapshot>(json, JsonOptions);
            if (snapshot?.Pending != null) {
                foreach (var kvp in snapshot.Pending) {
                    _pending[kvp.Key] = kvp.Value;
                }
            }
            if (snapshot?.Completed != null) {
                foreach (var kvp in snapshot.Completed) {
                    _completed[kvp.Key] = kvp.Value;
                }
            }
            _logger.LogInformation(
                "Loaded OAuth flow store | pending={Pending} completed={Completed}",
                _pending.Count, _completed.Count);
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "Corrupted oauth-flow file, starting fresh");
        } finally {
            _fileLock.Release();
        }
    }

    private async Task PersistAsync() {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try {
            var snapshot = new OAuthFlowSnapshot {
                Pending = _pending.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Completed = _completed.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var payload = _protector.Protect(json);
            await File.WriteAllTextAsync(_filePath, payload).ConfigureAwait(false);
            _lastPersistedPayload = payload;
            UnixFilePermissions.TrySetUserReadWrite(_filePath);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to persist OAuth flow store");
        } finally {
            _fileLock.Release();
        }
    }

    private static void EnforceCap<T>(ConcurrentDictionary<string, T> dict, int cap)
        where T : IExpiring {
        if (dict.Count < cap) {
            return;
        }
        foreach (var k in dict.OrderBy(kvp => kvp.Value.Expires).Take(dict.Count - cap + 1).Select(kvp => kvp.Key)) {
            dict.TryRemove(k, out _);
        }
    }

    private sealed class OAuthFlowSnapshot {
        public Dictionary<string, PendingAuth>? Pending { get; set; }
        public Dictionary<string, CompletedAuth>? Completed { get; set; }
    }
}
