using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;
using ModelContextProtocol.Server;

namespace HaloPsaMcp.Modules.Mcp;

[McpServerToolType]
// Static holder types should be Static or NotInheritable
// MCP framework requires non-static class
#pragma warning disable CA1052
internal class HaloPsaMcpTools {
#pragma warning restore CA1052
    private static readonly JsonSerializerOptions IndentedJsonOptions = HaloPsaMcpConstants.IndentedJsonOptions;

    private static HaloPsaClient? TryCreateUserClient(
        HaloPsaConfig config,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage) {
        
        var context = httpContextAccessor?.HttpContext;
        string? token = null;
        string? refreshToken = null;
        long? expiresAt = null;

        if (context != null) {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                token = authHeader.Substring(7);
                
                var userEntry = tokenStorage?.GetToken(token);
                if (userEntry != null) {
                    refreshToken = userEntry.RefreshToken;
                    expiresAt = userEntry.ExpiresAt;
                }
            }
        }
        
        if (string.IsNullOrEmpty(token) && context == null) {
            var userEntry = tokenStorage?.GetDefaultToken();
            if (userEntry != null) {
                token = userEntry.AccessToken;
                refreshToken = userEntry.RefreshToken;
                expiresAt = userEntry.ExpiresAt;
            }
        }

        if (string.IsNullOrEmpty(token)) {
            return null;
        }

        var userConfig = new HaloPsaConfig {
            Url = config.Url,
            ClientId = config.ClientId,
            ClientSecret = config.ClientSecret,
            DirectToken = token,
            DirectRefreshToken = refreshToken,
            DirectTokenExpiresAt = expiresAt,
            OnTokenRefreshed = (newToken, newRefresh, newExpiresAt) => {
                authService?.InvalidateToken(token);
                if (tokenStorage != null) {
                    _ = tokenStorage.UpdateTokenAsync(token, newToken, newRefresh, newExpiresAt);
                }
            }
        };

        return new HaloPsaClient(userConfig, null);
    }

    private static JsonElement TrimFields(JsonElement element, string[] allowedFields) {
        if (element.ValueKind == JsonValueKind.Array) {
            var items = new List<Dictionary<string, JsonElement>>();
            foreach (var item in element.EnumerateArray()) {
                items.Add(TrimObject(item, allowedFields));
            }
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(items));
        } else if (element.ValueKind == JsonValueKind.Object) {
            var trimmed = TrimObject(element, allowedFields);
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(trimmed));
        }
        return element;
    }

    private static Dictionary<string, JsonElement> TrimObject(JsonElement obj, string[] allowedFields) {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (obj.ValueKind != JsonValueKind.Object) {
            return result;
        }
        foreach (var prop in obj.EnumerateObject()) {
            if (allowedFields.Contains(prop.Name, StringComparer.OrdinalIgnoreCase)) {
                result[prop.Name] = prop.Value.Clone();
            }
        }
        return result;
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaQueryDescription)]
    public static async Task<string> HalopsaQuery(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("SQL SELECT query. Must include TOP N to limit results.")] string sql) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        
        // Use a 30-second timeout for SQL queries (shorter than the default 60 seconds)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        try {
            var result = await client.ExecuteQueryAsync(sql, cts.Token).ConfigureAwait(false);
            
            // Check response size and warn about potential context window issues
            var jsonResponse = JsonSerializer.Serialize(result.Rows, IndentedJsonOptions);
            var responseSizeKB = Encoding.UTF8.GetByteCount(jsonResponse) / 1024.0;
            
            string sizeWarning = "";
            if (responseSizeKB > 500) {
                sizeWarning = $"\n\n{string.Format(HaloPsaMcpConstants.LargeResponseWarningTemplate, responseSizeKB)}";
            } else if (responseSizeKB > 100) {
                sizeWarning = $"\n\n{string.Format(HaloPsaMcpConstants.MediumResponseWarningTemplate, responseSizeKB)}";
            }
            
            if (result.Count == 0 && result.RawResponse != null) {
                return $"{HaloPsaMcpConstants.QueryZeroRowsMessage}{result.RawResponse}{sizeWarning}";
            }
            
            string rowCountInfo = result.Count == 1 ? "1 row" : $"{result.Count} rows";
            return string.Format(HaloPsaMcpConstants.QueryResultTemplate, rowCountInfo, sizeWarning, jsonResponse);
        } catch (TaskCanceledException) {
            return HaloPsaMcpConstants.QueryTimeoutMessage;
        } catch (Exception ex) {
            return string.Format(HaloPsaMcpConstants.QueryFailedTemplate, ex.Message);
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetSchemaDescription)]
    public static async Task<string> HalopsaGetSchema(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        var statusList = new List<StatusInfo>();
        var agentList = new List<AgentInfo>();

        try {
            var statuses = await client.GetAsync<JsonElement>("/api/Status",
                new Dictionary<string, string> { ["type"] = "0" }).ConfigureAwait(false);
            if (statuses.ValueKind == JsonValueKind.Array) {
                foreach (var s in statuses.EnumerateArray()) {
                    statusList.Add(new StatusInfo(
                        s.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        s.TryGetProperty("name", out var name) ? name.GetString() ?? "" : ""
                    ));
                }
            }

            var agents = await client.GetAsync<JsonElement>("/api/Agent",
                new Dictionary<string, string> { ["count"] = "100" }).ConfigureAwait(false);
            if (agents.ValueKind == JsonValueKind.Array) {
                foreach (var a in agents.EnumerateArray()) {
                    agentList.Add(new AgentInfo(
                        a.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        a.TryGetProperty("name", out var name) ? name.GetString() ?? "" : ""
                    ));
                }
            }
        } catch {
            // Non-fatal: return schema without live lookups
        }

        var statusListText = string.Join("\n", statusList.Select(s => $"- {s.Id}: {s.Name}"));
        var agentListText = string.Join("\n", agentList.Select(a => $"- {a.Id}: {a.Name}"));

        var tablesText = string.Join("\n\n", HaloPsaSchema.CommonTables.Select(kvp =>
            $"### {kvp.Key} ({kvp.Value.Description})\n**Key columns:** {string.Join(", ", kvp.Value.KeyColumns)}"));

        var examplesText = string.Join("\n\n", HaloPsaSchema.ExampleQueries.Select((q, i) =>
            $"### Example {i + 1}\n```sql\n{q}\n```"));

        var schema = string.Format(HaloPsaMcpConstants.SchemaTemplate,
            string.Join("\n", HaloPsaSchema.ImportantNotes.Select(n => $"- {n}")),
            statusListText,
            agentListText,
            tablesText,
            examplesText);

        return schema;
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaAuthStatusDescription)]
    public static async Task<string> HalopsaAuthStatus(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService authService,
        TokenStorageService? tokenStorage) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        
        // Use a shorter timeout for auth status checks (10 seconds instead of 15)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        try {
            var result = await client.GetAsync<JsonElement>("/api/Agent/me", null, cts.Token).ConfigureAwait(false);
            var name = result.TryGetProperty("name", out var n) ? n.GetString() : HaloPsaMcpConstants.UnknownAgentName;
            var email = result.TryGetProperty("email", out var e) ? e.GetString() : "";
            return JsonSerializer.Serialize(new { 
                authenticated = true, 
                agent_name = name,
                agent_email = email,
                message = HaloPsaMcpConstants.AuthenticatedMessage
            }, IndentedJsonOptions);
        } catch (TaskCanceledException) {
            return JsonSerializer.Serialize(new { 
                authenticated = false, 
                error = HaloPsaMcpConstants.AuthTimeoutError,
                login_url = HaloPsaMcpConstants.GetLoginUrl(appConfig)
            }, IndentedJsonOptions);
        } catch (HttpRequestException ex) {
            return JsonSerializer.Serialize(new { 
                authenticated = false, 
                error = string.Format(HaloPsaMcpConstants.AuthNetworkErrorTemplate, ex.Message),
                login_url = HaloPsaMcpConstants.GetLoginUrl(appConfig)
            }, IndentedJsonOptions);
        } catch (Exception ex) {
            return JsonSerializer.Serialize(new { 
                authenticated = false, 
                error = string.Format(HaloPsaMcpConstants.AuthFailedTemplate, ex.Message),
                login_url = HaloPsaMcpConstants.GetLoginUrl(appConfig)
            }, IndentedJsonOptions);
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaListTicketsDescription)]
    public static async Task<string> HalopsaListTickets(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Maximum number of tickets to return (1-50)")] int count = 10,
        [Description("Filter by status ID (use halopsa_get_schema for IDs)")] int? status = null,
        [Description("Filter by client ID")] int? clientId = null,
        [Description("Filter by agent ID (use halopsa_get_schema for IDs)")] int? agentId = null,
        [Description("Search query")] string? search = null) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(count, 50).ToString(CultureInfo.InvariantCulture)
        };

        if (status.HasValue) {
            queryParams["status"] = status.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (clientId.HasValue) {
            queryParams["client_id"] = clientId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (agentId.HasValue) {
            queryParams["agent_id"] = agentId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(search)) {
            queryParams["search"] = search;
        }

        var result = await client.GetAsync<JsonElement>("/api/Tickets", queryParams).ConfigureAwait(false);
        var trimmed = TrimFields(result, HaloPsaMcpConstants.TicketSummaryFields);
        var jsonResponse = JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
        var responseSizeKB = Encoding.UTF8.GetByteCount(jsonResponse) / 1024.0;
        
        string sizeWarning = "";
        if (responseSizeKB > 100) {
            sizeWarning = $"\n\n{string.Format(HaloPsaMcpConstants.ListLargeResponseWarningTemplate, responseSizeKB)}";
        }
        
        return $"{jsonResponse}{sizeWarning}";
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetTicketDescription)]
    public static async Task<string> HalopsaGetTicket(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Ticket ID")] int id) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        var result = await client.GetAsync<JsonElement>($"/api/Tickets/{id}", null).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaListActionsDescription)]
    public static async Task<string> HalopsaListActions(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Ticket ID")] int ticketId,
        [Description("Maximum number of actions to return (1-50)")] int count = 10) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        var queryParams = new Dictionary<string, string> {
            ["ticket_id"] = ticketId.ToString(CultureInfo.InvariantCulture),
            ["count"] = Math.Min(count, 50).ToString(CultureInfo.InvariantCulture)
        };

        var result = await client.GetAsync<JsonElement>("/api/Actions", queryParams).ConfigureAwait(false);
        var trimmed = TrimFields(result, HaloPsaMcpConstants.ActionSummaryFields);
        var jsonResponse = JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
        var responseSizeKB = Encoding.UTF8.GetByteCount(jsonResponse) / 1024.0;
        
        string sizeWarning = "";
        if (responseSizeKB > 100) {
            sizeWarning = $"\n\n{string.Format(HaloPsaMcpConstants.ActionsLargeResponseWarningTemplate,
                responseSizeKB)}";
        }
        
        return $"{jsonResponse}{sizeWarning}";
    }
}
