using System.Text.Json;

namespace HaloPsaMcp.Modules.Common.Infrastructure;

internal class FileTokenStore : ITokenStore, IDisposable {
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() {
        WriteIndented = true
    };

    public FileTokenStore(string filePath) {
        _filePath = filePath;
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<string?> GetTokenAsync(string sessionId) {
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            if (!File.Exists(_filePath)) {
                return null;
            }

            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<Dictionary<string, TokenData>>(json);

            if (data?.TryGetValue(sessionId, out var tokenData) == true) {
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < tokenData.ExpiresAt) {
                    return tokenData.AccessToken;
                }
            }
            return null;
        } finally {
            _lock.Release();
        }
    }

    public async Task<string?> GetRefreshTokenAsync(string sessionId) {
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            if (!File.Exists(_filePath)) {
                return null;
            }

            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<Dictionary<string, TokenData>>(json);

            return data?.TryGetValue(sessionId, out var tokenData) == true
                ? tokenData.RefreshToken
                : null;
        } finally {
            _lock.Release();
        }
    }

    public async Task SaveTokenAsync(string sessionId, string accessToken, string? refreshToken, long expiresAt) {
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            Dictionary<string, TokenData> data;

            if (File.Exists(_filePath)) {
                var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
                data = JsonSerializer.Deserialize<Dictionary<string, TokenData>>(json)
                    ?? new Dictionary<string, TokenData>();
            } else {
                data = new Dictionary<string, TokenData>();
            }

            data[sessionId] = new TokenData {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };

            var updatedJson = JsonSerializer.Serialize(data, IndentedJsonOptions);
            await File.WriteAllTextAsync(_filePath, updatedJson).ConfigureAwait(false);
        } finally {
            _lock.Release();
        }
    }



    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (!_disposed) {
            if (disposing) {
                _lock?.Dispose();
            }
            _disposed = true;
        }
    }
}
