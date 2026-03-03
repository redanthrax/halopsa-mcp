using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;
using ModelContextProtocol.Server;

namespace HaloPsaMcp.Mcp;

[McpServerToolType]
// Static holder types should be Static or NotInheritable
// MCP framework requires non-static class
#pragma warning disable CA1052
internal class HaloPsaMcpTools {
#pragma warning restore CA1052
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private static readonly string[] TicketSummaryFields = [
        "id", "faultid", "summary", "symptom", "details",
        "status_id", "status", "priority_id", "priority",
        "client_id", "client_name", "site_id", "site_name",
        "agent_id", "agent", "user_id", "user_name",
        "requesttype", "category_1", "category_2", "category_3",
        "dateoccurred", "datecreated", "datelogged", "datecleared",
        "sla_id", "team"
    ];

    private static readonly string[] ActionSummaryFields = [
        "id", "faultid", "actoutcome", "who", "whe_", "whodid",
        "note", "emailfrom", "emailto", "timetaken",
        "outcome", "actiontype", "isimportant"
    ];

    private static string GetLoginUrl(AppConfig appConfig) {
        return $"{appConfig.AuthBaseUrl}/login";
    }

    private static string AuthErrorMessage(AppConfig appConfig) {
        return $"NOT AUTHENTICATED. Tell the user to open this URL in their browser to log in: {GetLoginUrl(appConfig)}";
    }

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
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
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
    [Description(
        "PREFERRED TOOL for all HaloPSA data questions: ticket counts, filtering, aggregation, reports, " +
        "client lookups, agent stats, status breakdowns, satisfaction surveys, timesheet hours, and date-based analysis. " +
        "Executes a SQL SELECT query against the HaloPSA reporting database. " +
        "Call halopsa_get_schema first to get table names, column names, status IDs, and example queries. " +
        "IMPORTANT: All datetimes are stored in UTC. Convert user's local timezone to UTC for WHERE clauses. " +
        "DEFAULT SCOPE: Unless the user specifies otherwise, scope queries to the current calendar month in UTC. " +
        "Returns only the columns you SELECT, keeping responses compact. " +
        "Times out after 30 seconds for slow queries. " +
        "WARN: Large responses (>500KB) may exceed context window limits - use TOP with smaller numbers or aggregation. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.")]
    public static async Task<string> HalopsaQuery(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("SQL SELECT query. Must include TOP N to limit results.")] string sql) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return AuthErrorMessage(appConfig);
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
                sizeWarning = $"\n\nWARNING: Large response ({responseSizeKB:F1}KB) may exceed context window limits. " +
                             $"Consider using TOP with a smaller number, adding more specific WHERE conditions, " +
                             $"or focusing on aggregated results (COUNT, SUM, AVG) instead of detailed rows.";
            } else if (responseSizeKB > 100) {
                sizeWarning = $"\n\nResponse size: {responseSizeKB:F1}KB. " +
                             $"For very large datasets, consider aggregation queries or smaller TOP limits.";
            }
            
            if (result.Count == 0 && result.RawResponse != null) {
                return $"Query returned 0 rows.\nRaw API response (for debugging):\n{result.RawResponse}{sizeWarning}";
            }
            
            string rowCountInfo = result.Count == 1 ? "1 row" : $"{result.Count} rows";
            return $"Query returned {rowCountInfo}:{sizeWarning}\n{jsonResponse}";
        } catch (TaskCanceledException) {
            return "Query timed out after 30 seconds. Try simplifying your query or reducing the date range.";
        } catch (Exception ex) {
            return $"Query failed: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "Get the HaloPSA database schema, lookup IDs, and query best practices for halopsa_query. " +
        "ALWAYS call this before writing SQL queries. Returns table/column names, " +
        "status IDs, request type IDs, and example queries. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.")]
    public static async Task<string> HalopsaGetSchema(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return AuthErrorMessage(appConfig);
        }
        var statusList = new List<StatusInfo>();
        var agentList = new List<AgentInfo>();

        try {
            var statuses = await client.GetAsync<JsonElement>("/api/Status", new Dictionary<string, string> { ["type"] = "0" }).ConfigureAwait(false);
            if (statuses.ValueKind == JsonValueKind.Array) {
                foreach (var s in statuses.EnumerateArray()) {
                    statusList.Add(new StatusInfo(
                        s.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        s.TryGetProperty("name", out var name) ? name.GetString() ?? "" : ""
                    ));
                }
            }

            var agents = await client.GetAsync<JsonElement>("/api/Agent", new Dictionary<string, string> { ["count"] = "100" }).ConfigureAwait(false);
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

        var schema = $@"# HaloPSA Reporting Database Schema and Best Practices

## Important Notes
{string.Join("\n", HaloPsaSchema.ImportantNotes.Select(n => $"- {n}"))}

## Status IDs
{statusListText}

## Agent IDs
{agentListText}

## Common Tables
{tablesText}

## Example Queries
{examplesText}";

        return schema;
    }

    [McpServerTool]
    [Description(
        "Check the current authentication status with HaloPSA. " +
        "ALWAYS call this first when the user asks anything about HaloPSA data. " +
        "Times out after 10 seconds if HaloPSA server is unresponsive. " +
        "If not authenticated, show the user the login URL from the response.")]
    public static async Task<string> HalopsaAuthStatus(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService authService,
        TokenStorageService? tokenStorage) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return AuthErrorMessage(appConfig);
        }
        
        // Use a shorter timeout for auth status checks (10 seconds instead of 15)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        try {
            var result = await client.GetAsync<JsonElement>("/api/Agent/me", null, cts.Token).ConfigureAwait(false);
            var name = result.TryGetProperty("name", out var n) ? n.GetString() : "Unknown";
            var email = result.TryGetProperty("email", out var e) ? e.GetString() : "";
            return JsonSerializer.Serialize(new { 
                authenticated = true, 
                agent_name = name,
                agent_email = email,
                message = "Authenticated. All tools available."
            }, IndentedJsonOptions);
        } catch (TaskCanceledException) {
            return JsonSerializer.Serialize(new { 
                authenticated = false, 
                error = "Authentication check timed out after 10 seconds. HaloPSA server may be unresponsive.",
                login_url = GetLoginUrl(appConfig)
            }, IndentedJsonOptions);
        } catch (HttpRequestException ex) {
            return JsonSerializer.Serialize(new { 
                authenticated = false, 
                error = $"Network error during authentication: {ex.Message}",
                login_url = GetLoginUrl(appConfig)
            }, IndentedJsonOptions);
        } catch (Exception ex) {
            return JsonSerializer.Serialize(new { 
                authenticated = false, 
                error = $"Authentication check failed: {ex.Message}",
                login_url = GetLoginUrl(appConfig)
            }, IndentedJsonOptions);
        }
    }

    [McpServerTool]
    [Description(
        "Search HaloPSA tickets by keyword or filters. Returns summary fields only. " +
        "Use halopsa_query for counts, date filtering, or aggregation. " +
        "Use halopsa_get_ticket for full ticket detail by ID. " +
        "WARN: Large responses (>100KB) may impact context - reduce count or add filters. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.")]
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
            return AuthErrorMessage(appConfig);
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
        var trimmed = TrimFields(result, TicketSummaryFields);
        var jsonResponse = JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
        var responseSizeKB = Encoding.UTF8.GetByteCount(jsonResponse) / 1024.0;
        
        string sizeWarning = "";
        if (responseSizeKB > 100) {
            sizeWarning = $"\n\nWARNING: Large response ({responseSizeKB:F1}KB). Consider reducing count or adding filters.";
        }
        
        return $"{jsonResponse}{sizeWarning}";
    }

    [McpServerTool]
    [Description(
        "Get full details for a specific HaloPSA ticket by ID. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.")]
    public static async Task<string> HalopsaGetTicket(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Ticket ID")] int id) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return AuthErrorMessage(appConfig);
        }
        var result = await client.GetAsync<JsonElement>($"/api/Tickets/{id}", null).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(
        "List actions (notes/updates) for a specific HaloPSA ticket. Returns summary fields only. " +
        "WARN: Large responses (>100KB) may impact context - reduce count or narrow ticket scope. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.")]
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
            return AuthErrorMessage(appConfig);
        }
        var queryParams = new Dictionary<string, string> {
            ["ticket_id"] = ticketId.ToString(CultureInfo.InvariantCulture),
            ["count"] = Math.Min(count, 50).ToString(CultureInfo.InvariantCulture)
        };

        var result = await client.GetAsync<JsonElement>("/api/Actions", queryParams).ConfigureAwait(false);
        var trimmed = TrimFields(result, ActionSummaryFields);
        var jsonResponse = JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
        var responseSizeKB = Encoding.UTF8.GetByteCount(jsonResponse) / 1024.0;
        
        string sizeWarning = "";
        if (responseSizeKB > 100) {
            sizeWarning = $"\n\nWARNING: Large response ({responseSizeKB:F1}KB). Consider reducing count or narrowing the ticket scope.";
        }
        
        return $"{jsonResponse}{sizeWarning}";
    }
}
