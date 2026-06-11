using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

public class HaloPsaClient {
    private readonly HaloPsaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HaloPsaClient>? _logger;

    public HaloPsaClient(HaloPsaConfig config, HttpClient httpClient, ILogger<HaloPsaClient>? logger = null) {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync() {
        if (string.IsNullOrEmpty(_config.DirectToken)) {
            throw new InvalidOperationException(
                "No user token available. Complete the OAuth flow before calling HaloPSA APIs.");
        }
        if (!string.IsNullOrEmpty(_config.DirectRefreshToken) &&
            _config.DirectTokenExpiresAt.HasValue &&
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= _config.DirectTokenExpiresAt.Value - 60_000) {
            await RefreshDirectTokenAsync().ConfigureAwait(false);
        }
        return _config.DirectToken;
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

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        sw.Stop();

        if (!response.IsSuccessStatusCode) {
            var errorBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            HaloPsaResponseSanitizer.LogFailure(
                _logger, "token refresh", response.StatusCode, errorBytes.Length, sw.ElapsedMilliseconds);
            throw HaloPsaResponseSanitizer.ApiException("Token refresh", response.StatusCode);
        }

        _logger?.LogInformation("Token refresh succeeded | elapsed={ElapsedMs}ms", sw.ElapsedMilliseconds);

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
    }

    /// <summary>
    /// Probe an endpoint with a HEAD-equivalent GET (top=1) and return the HTTP status without throwing.
    /// Used by capability discovery to see which scopes the current token actually has.
    /// </summary>
    public async Task<int> ProbeAsync(string endpoint, CancellationToken cancellationToken = default) {
        try {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);
            var url = BuildUrl(endpoint, new Dictionary<string, string> { ["count"] = "1", ["page_size"] = "1" });
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            return (int)response.StatusCode;
        } catch (TaskCanceledException) {
            return 408;
        } catch (HttpRequestException) {
            return 0;
        } catch (InvalidOperationException) {
            return 0;
        }
    }

    /// <summary>
    /// Probe a POST endpoint with a minimal body to detect write-scope grants (e.g. edit:reporting).
    /// HaloPSA distinguishes read vs edit scopes — read:reporting allows GET /api/Report,
    /// edit:reporting is required to POST /api/Report/run.
    /// </summary>
    public async Task<int> ProbePostAsync(string endpoint, object body, CancellationToken cancellationToken = default) {
        try {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);
            var url = BuildUrl(endpoint, null);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            return (int)response.StatusCode;
        } catch (TaskCanceledException) {
            return 408;
        } catch (HttpRequestException) {
            return 0;
        } catch (InvalidOperationException) {
            return 0;
        }
    }

    public async Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams = null, CancellationToken cancellationToken = default) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = BuildUrl(endpoint, queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger?.LogDebug("HaloPSA GET {Endpoint}", endpoint);

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBytes = await LogAndReadResponseAsync("GET", endpoint, response, sw).ConfigureAwait(false);

        return JsonSerializer.Deserialize<T>(responseBytes)
            ?? throw new InvalidOperationException("Invalid API response");
    }

    public async Task<T> PostAsync<T>(string endpoint, object body, Dictionary<string, string>? queryParams = null) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = BuildUrl(endpoint, queryParams);
        var bodyJson = JsonSerializer.Serialize(body);
        var bodyBytes = Encoding.UTF8.GetByteCount(bodyJson);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        _logger?.LogDebug("HaloPSA POST {Endpoint} | req={RequestBytes}B", endpoint, bodyBytes);

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var responseBytes = await LogAndReadResponseAsync("POST", endpoint, response, sw, bodyBytes).ConfigureAwait(false);

        return JsonSerializer.Deserialize<T>(responseBytes)
            ?? throw new InvalidOperationException("Invalid API response");
    }

    public async Task<T> PutAsync<T>(string endpoint, object body, Dictionary<string, string>? queryParams = null) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = BuildUrl(endpoint, queryParams);
        var bodyJson = JsonSerializer.Serialize(body);
        var bodyBytes = Encoding.UTF8.GetByteCount(bodyJson);

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        _logger?.LogDebug("HaloPSA PUT {Endpoint} | req={RequestBytes}B", endpoint, bodyBytes);

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var responseBytes = await LogAndReadResponseAsync("PUT", endpoint, response, sw, bodyBytes).ConfigureAwait(false);

        return JsonSerializer.Deserialize<T>(responseBytes)
            ?? throw new InvalidOperationException("Invalid API response");
    }

    public async Task DeleteAsync(string endpoint, Dictionary<string, string>? queryParams = null) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = BuildUrl(endpoint, queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger?.LogDebug("HaloPSA DELETE {Endpoint}", endpoint);

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        _ = await LogAndReadResponseAsync("DELETE", endpoint, response, sw).ConfigureAwait(false);
        // For DELETE, we don't return content, just ensure success
    }

    public async Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default) {
        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        var url = BuildUrl("/api/Report", null);
        var bodyJson = JsonSerializer.Serialize(new object[] { new { _loadreportonly = true, sql } });
        var bodyBytes = Encoding.UTF8.GetByteCount(bodyJson);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        _logger?.LogInformation(
            "HaloPSA SQL | req={RequestBytes}B sqlLen={SqlLen} sqlHash={SqlHash}",
            bodyBytes, sql.Length, HaloPsaResponseSanitizer.SqlLogFingerprint(sql));
        _logger?.LogDebug("HaloPSA SQL query | sql={Sql}", sql);

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var rawText = await LogAndReadResponseTextAsync("POST", "/api/Report", response, sw, bodyBytes).ConfigureAwait(false);

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

        _logger?.LogInformation(
            "HaloPSA SQL result | rows={RowCount} cols={ColCount} rawBytes={RawBytes}B",
            rows.Count, columns.Count, Encoding.UTF8.GetByteCount(rawText));

        return new QueryResult {
            Rows = rows,
            Count = rows.Count,
            Columns = columns,
            RawResponse = rows.Count == 0 ? rawText : null
        };
    }

    public async Task<JsonElement> MakeApiCallAsync(string endpoint, string method, object? body = null, Dictionary<string, string>? queryParams = null) {
        return method.ToUpperInvariant() switch {
            "GET" => await GetAsync<JsonElement>(endpoint, queryParams).ConfigureAwait(false),
            "POST" => await PostAsync<JsonElement>(endpoint, body ?? new { }, queryParams).ConfigureAwait(false),
            "PUT" => await PutAsync<JsonElement>(endpoint, body ?? new { }, queryParams).ConfigureAwait(false),
            _ => throw new NotSupportedException($"HTTP method {method} is not supported")
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string BuildUrl(string endpoint, Dictionary<string, string>? queryParams) {
        var url = $"{_config.Url}{endpoint}";
        var query = new List<string> { $"tenant={Uri.EscapeDataString(_config.GetTenant())}" };
        if (queryParams != null) {
            query.AddRange(queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        }
        return url + "?" + string.Join("&", query);
    }

    private async Task<byte[]> LogAndReadResponseAsync(
        string method, string endpoint, HttpResponseMessage response,
        Stopwatch sw, int requestBytes = 0) {

        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        sw.Stop();

        if (!response.IsSuccessStatusCode) {
            HaloPsaResponseSanitizer.LogFailure(
                _logger, $"{method} {endpoint}", response.StatusCode, bytes.Length, sw.ElapsedMilliseconds);
            throw HaloPsaResponseSanitizer.ApiException($"{method} {endpoint}", response.StatusCode);
        }

        _logger?.LogInformation(
            "HaloPSA {Method} {Endpoint} | status={StatusCode} req={RequestBytes}B res={ResponseBytes}B elapsed={ElapsedMs}ms",
            method, endpoint, (int)response.StatusCode,
            requestBytes, bytes.Length, sw.ElapsedMilliseconds);

        return bytes;
    }

    private async Task<string> LogAndReadResponseTextAsync(
        string method, string endpoint, HttpResponseMessage response,
        Stopwatch sw, int requestBytes = 0) {

        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        sw.Stop();

        if (!response.IsSuccessStatusCode) {
            var bodyBytes = Encoding.UTF8.GetByteCount(text);
            HaloPsaResponseSanitizer.LogFailure(
                _logger, $"{method} {endpoint}", response.StatusCode, bodyBytes, sw.ElapsedMilliseconds);
            throw HaloPsaResponseSanitizer.ApiException($"{method} {endpoint}", response.StatusCode);
        }

        _logger?.LogInformation(
            "HaloPSA {Method} {Endpoint} | status={StatusCode} req={RequestBytes}B res={ResponseBytes}B elapsed={ElapsedMs}ms",
            method, endpoint, (int)response.StatusCode,
            requestBytes, Encoding.UTF8.GetByteCount(text), sw.ElapsedMilliseconds);

        return text;
    }

}
