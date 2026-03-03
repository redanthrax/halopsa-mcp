using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Infrastructure;
using HaloPsaMcp.Modules.HaloPsa.Models;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

internal class HaloPsaClient : IDisposable {
    private readonly HaloPsaConfig _config;
    private readonly ITokenStore? _tokenStore;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HaloPsaClient>? _logger;
    private string? _clientCredentialsToken;
    private DateTime? _clientCredentialsTokenExpiry;
    private bool _disposed;

    public HaloPsaClient(HaloPsaConfig config, ITokenStore? tokenStore = null, ILogger<HaloPsaClient>? logger = null) {
        _config = config;
        _tokenStore = tokenStore;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _logger = logger;
    }

    private bool UsingAuthCode => !string.IsNullOrEmpty(_config.DirectToken) == false
        && _tokenStore != null;

    public async Task<string> GetAccessTokenAsync() {
        if (!string.IsNullOrEmpty(_config.DirectToken)) {
            // Refresh if within 60 seconds of expiry
            if (!string.IsNullOrEmpty(_config.DirectRefreshToken) &&
                _config.DirectTokenExpiresAt.HasValue &&
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= _config.DirectTokenExpiresAt.Value - 60_000) {
                await RefreshDirectTokenAsync().ConfigureAwait(false);
            }
            return _config.DirectToken;
        }

        if (UsingAuthCode && _tokenStore != null) {
            return await GetTokenFromStoreAsync().ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            "No user token available. API calls must use an authenticated user token, " +
            "not client credentials. Please complete the OAuth flow first.");
    }

    private async Task RefreshDirectTokenAsync() {
        _logger?.LogInformation("Refreshing expired token");
        var tokenUrl = $"{_config.Url}/auth/token?tenant={Uri.EscapeDataString(_config.GetTenant())}";
        var parameters = new Dictionary<string, string> {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _config.ClientId,
            ["refresh_token"] = _config.DirectRefreshToken!
        };

        if (!string.IsNullOrEmpty(_config.ClientSecret)) {
            parameters["client_secret"] = _config.ClientSecret;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger?.LogError("Token refresh failed: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Token refresh failed: {response.StatusCode} - {error}");
        }

        var tokenResponse = await JsonSerializer.DeserializeAsync<TokenResponse>(
            await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid token response");

        var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (tokenResponse.expires_in - 60) * 1000;
        _config.DirectToken = tokenResponse.access_token;
        _config.DirectRefreshToken = tokenResponse.refresh_token ?? _config.DirectRefreshToken;
        _config.DirectTokenExpiresAt = expiresAt;
        _config.OnTokenRefreshed?.Invoke(
            _config.DirectToken,
            _config.DirectRefreshToken!,
            expiresAt
        );
        _logger?.LogInformation("Token refreshed successfully");
    }

    private async Task<string> GetTokenFromStoreAsync() {
        // Implementation depends on store - for now return empty
        var token = await _tokenStore!.GetTokenAsync("default").ConfigureAwait(false);
        if (string.IsNullOrEmpty(token)) {
            throw new InvalidOperationException("No authentication token found. Please authenticate.");
        }
        return token;
    }

    private async Task<string> GetClientCredentialsTokenAsync() {
        if (_clientCredentialsToken != null && _clientCredentialsTokenExpiry > DateTime.UtcNow) {
            return _clientCredentialsToken;
        }

        if (string.IsNullOrEmpty(_config.ClientSecret)) {
            throw new InvalidOperationException("Client secret required for client_credentials grant");
        }

        var tokenUrl = $"{_config.Url}/auth/token?tenant={Uri.EscapeDataString(_config.GetTenant())}";
        var parameters = new Dictionary<string, string> {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _config.ClientId,
            ["client_secret"] = _config.ClientSecret,
            ["scope"] = "all"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"Authentication failed: {response.StatusCode} - {error}");
        }

        var tokenResponse = await JsonSerializer.DeserializeAsync<TokenResponse>(
            await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid token response");

        _clientCredentialsToken = tokenResponse.access_token;
        _clientCredentialsTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60);

        return _clientCredentialsToken;
    }

    public async Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams = null) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = $"{_config.Url}{endpoint}";

        var query = new List<string> { $"tenant={Uri.EscapeDataString(_config.GetTenant())}" };
        if (queryParams != null) {
            query.AddRange(queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        }

        url += "?" + string.Join("&", query);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger?.LogDebug("GET {Url}", url);
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger?.LogError("GET {Endpoint} failed: {StatusCode} - {Error}", endpoint, response.StatusCode, error);
            throw new HttpRequestException($"API call failed: {response.StatusCode} - {error}");
        }

        return await JsonSerializer.DeserializeAsync<T>(
            await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid API response");
    }

    public async Task<T> PostAsync<T>(string endpoint, object body, Dictionary<string, string>? queryParams = null) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = $"{_config.Url}{endpoint}";

        var query = new List<string> { $"tenant={Uri.EscapeDataString(_config.GetTenant())}" };
        if (queryParams != null) {
            query.AddRange(queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        }

        url += "?" + string.Join("&", query);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json"
        );

        _logger?.LogDebug("POST {Url}", url);
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger?.LogError("POST {Endpoint} failed: {StatusCode} - {Error}", endpoint, response.StatusCode, error);
            throw new HttpRequestException($"API call failed: {response.StatusCode} - {error}");
        }

        return await JsonSerializer.DeserializeAsync<T>(
            await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid API response");
    }

    public async Task<QueryResult> ExecuteQueryAsync(string sql) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = $"{_config.Url}/api/Report";

        var query = new List<string> { $"tenant={Uri.EscapeDataString(_config.GetTenant())}" };
        url += "?" + string.Join("&", query);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new object[] { new { _loadreportonly = true, sql } }),
            Encoding.UTF8,
            "application/json"
        );

        _logger?.LogDebug("Executing SQL query: {Sql}", sql);
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var rawText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) {
            _logger?.LogError("Report API failed: {StatusCode} - {Error}", response.StatusCode, rawText);
            throw new HttpRequestException($"Report API failed: {response.StatusCode} - {rawText}");
        }

        var raw = JsonSerializer.Deserialize<JsonElement>(rawText);

        var elements = new List<JsonElement>();
        if (raw.ValueKind == JsonValueKind.Array) {
            foreach (var item in raw.EnumerateArray()) {
                elements.Add(item);
            }
        } else if (raw.ValueKind == JsonValueKind.Object) {
            elements.Add(raw);
        }

        var rows = new List<Dictionary<string, object>>();
        var columns = new List<ReportColumn>();

        if (elements.Count > 0) {
            var first = elements[0];
            if (first.TryGetProperty("rows", out var rowsElement)) {
                rows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(rowsElement.GetRawText())
                    ?? new List<Dictionary<string, object>>();
            }
            if (first.TryGetProperty("available_columns", out var colsElement)) {
                columns = JsonSerializer.Deserialize<List<ReportColumn>>(colsElement.GetRawText())
                    ?? new List<ReportColumn>();
            }
        }

        return new QueryResult {
            Rows = rows,
            Count = rows.Count,
            Columns = columns,
            RawResponse = rows.Count == 0 ? rawText : null
        };
    }

    public async Task<JsonElement> MakeApiCallAsync(string endpoint, string method, object? body = null,
        Dictionary<string, string>? queryParams = null) {
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)) {
            return await GetAsync<JsonElement>(endpoint, queryParams).ConfigureAwait(false);
        } else {
            return await PostAsync<JsonElement>(endpoint, body ?? new { }, queryParams).ConfigureAwait(false);
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (!_disposed) {
            if (disposing) {
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
    }
}
