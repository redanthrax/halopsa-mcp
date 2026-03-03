using System.ComponentModel;
using System.Globalization;
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
        "PREFERRED TOOL for all data questions: ticket counts, filtering, aggregation, reports, " +
        "client lookups, agent stats, status breakdowns, and date-based analysis. " +
        "Executes a SQL SELECT query against the HaloPSA reporting database. " +
        "Call halopsa_get_schema first to get table names, column names, status IDs, and example queries. " +
        "IMPORTANT: All datetimes are stored in UTC. Convert user's local timezone to UTC for WHERE clauses. " +
        "DEFAULT SCOPE: Unless the user specifies otherwise, scope queries to the current calendar month in UTC. " +
        "Returns only the columns you SELECT, keeping responses compact. " +
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
        try {
            var result = await client.ExecuteQueryAsync(sql).ConfigureAwait(false);
            if (result.Count == 0 && result.RawResponse != null) {
                return $"Query returned 0 rows.\nRaw API response (for debugging):\n{result.RawResponse}";
            }
            return $"Query returned {result.Count} rows:\n{JsonSerializer.Serialize(result.Rows, IndentedJsonOptions)}";
        } catch (Exception ex) {
            return $"Query failed: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "Get the database schema, lookup IDs, and query best practices for halopsa_query. " +
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
        var statusList = new List<object>();
        var agentList = new List<object>();

        try {
            var statuses = await client.GetAsync<JsonElement>("/api/Status", new Dictionary<string, string> { ["type"] = "0" }).ConfigureAwait(false);
            if (statuses.ValueKind == JsonValueKind.Array) {
                foreach (var s in statuses.EnumerateArray()) {
                    statusList.Add(new {
                        id = s.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        name = s.TryGetProperty("name", out var name) ? name.GetString() : ""
                    });
                }
            }

            var agents = await client.GetAsync<JsonElement>("/api/Agent", new Dictionary<string, string> { ["count"] = "100" }).ConfigureAwait(false);
            if (agents.ValueKind == JsonValueKind.Array) {
                foreach (var a in agents.EnumerateArray()) {
                    agentList.Add(new {
                        id = a.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        name = a.TryGetProperty("name", out var name) ? name.GetString() : ""
                    });
                }
            }
        } catch {
            // Non-fatal: return schema without live lookups
        }

        var schema = new {
            message = "HaloPSA Reporting Database Schema and Best Practices",
            important_notes = new[]
            {
                "All datetimes are UTC — convert from user's timezone",
                "Default scope: current calendar month unless user says otherwise",
                "fdeleted='False' is a STRING comparison, not integer",
                "Request type column is 'Requesttype' not 'RequestTypeID'",
                "Never use Fclosed column (broken). Closed tickets have Status=9",
                "Close date is 'datecleared' not 'Closeddate'",
                "Type mismatches require CAST: CAST(f.Assignedtoint AS int) = u.Unum",
                "Invoice totals: (IHAmountDue + IHAmountPaid) because paid invoices zero out IHAmountDue"
            },
            status_ids = statusList,
            agent_ids = agentList,
            common_tables = new {
                faults = new {
                    name = "faults",
                    description = "Tickets table",
                    key_columns = new[] {
                        "faultid", "symptom", "Status", "Assignedtoint", "sectio_",
                        "category2", "category3", "Requesttype", "fdeleted",
                        "dateoccurred", "datelogged", "datecleared"
                    }
                },
                uname = new {
                    name = "uname",
                    description = "Agents/Users table",
                    key_columns = new[] { "Unum", "uname", "uemail" }
                },
                site = new {
                    name = "site",
                    description = "Clients table",
                    key_columns = new[] { "Ssitenum", "Sname", "sdeleted" }
                },
                aareadex = new {
                    name = "aareadex",
                    description = "Client sites",
                    key_columns = new[] { "aarea", "asite" }
                },
                users = new {
                    name = "users",
                    description = "End users",
                    key_columns = new[] { "uid", "uusername", "uemail", "usite" }
                },
                actions = new {
                    name = "actions",
                    description = "Ticket actions/notes",
                    key_columns = new[] { "actoutcome", "faultid", "who", "whe_", "note" }
                },
                invoiceheader = new {
                    name = "invoiceheader",
                    description = "Invoices",
                    key_columns = new[] {
                        "IHid", "IHInvoice_ID", "IHAmountDue", "IHAmountPaid"
                    }
                }
            },
            example_queries = new[]
            {
                "SELECT TOP 10 faultid, symptom, Status, dateoccurred FROM faults " +
                    "WHERE fdeleted='False' AND dateoccurred >= '2026-03-01T00:00:00Z' " +
                    "ORDER BY faultid DESC",
                "SELECT COUNT(*) as total FROM faults " +
                    "WHERE fdeleted='False' AND Status=9 " +
                    "AND datecleared >= '2026-03-01T00:00:00Z'",
                "SELECT TOP 10 u.uname, COUNT(*) as ticket_count FROM faults f " +
                    "INNER JOIN uname u ON CAST(f.Assignedtoint AS int) = u.Unum " +
                    "WHERE f.fdeleted='False' AND f.dateoccurred >= '2026-03-01T00:00:00Z' " +
                    "GROUP BY u.uname ORDER BY ticket_count DESC",
                "SELECT TOP 10 s.Sname, COUNT(*) as ticket_count FROM faults f " +
                    "INNER JOIN site s ON f.sectio_ = s.Ssitenum " +
                    "WHERE f.fdeleted='False' AND f.dateoccurred >= '2026-03-01T00:00:00Z' " +
                    "GROUP BY s.Sname ORDER BY ticket_count DESC"
            }
        };

        return JsonSerializer.Serialize(schema, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(
        "Check the current authentication status with HaloPSA. " +
        "ALWAYS call this first when the user asks anything about HaloPSA data. " +
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
        try {
            var result = await client.GetAsync<JsonElement>("/api/Agent/me", null).ConfigureAwait(false);
            var name = result.TryGetProperty("name", out var n) ? n.GetString() : "Unknown";
            var email = result.TryGetProperty("email", out var e) ? e.GetString() : "";
            return JsonSerializer.Serialize(new { 
                authenticated = true, 
                agent_name = name,
                agent_email = email,
                message = "Authenticated. All tools available."
            }, IndentedJsonOptions);
        } catch {
            return AuthErrorMessage(appConfig);
        }
    }

    [McpServerTool]
    [Description(
        "Search tickets by keyword or filters. Returns summary fields only. " +
        "Use halopsa_query for counts, date filtering, or aggregation. " +
        "Use halopsa_get_ticket for full ticket detail by ID. " +
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
        return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(
        "Get full details for a specific ticket by ID. " +
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
        "List actions (notes/updates) for a specific ticket. Returns summary fields only. " +
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
        return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
    }
}
