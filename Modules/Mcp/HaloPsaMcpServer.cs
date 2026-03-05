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

internal class HaloPsaMcpTools {
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

    private static string FormatUpdateTicketResponse(JsonElement result, int ticketId) {
        // Extract minimal confirmation from API response
        string? summary = null;
        int? statusId = null;
        
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0) {
            var first = result[0];
            if (first.TryGetProperty("summary", out var s)) summary = s.GetString();
            if (first.TryGetProperty("status_id", out var st)) statusId = st.GetInt32();
        } else if (result.ValueKind == JsonValueKind.Object) {
            if (result.TryGetProperty("summary", out var s)) summary = s.GetString();
            if (result.TryGetProperty("status_id", out var st)) statusId = st.GetInt32();
        }

        return JsonSerializer.Serialize(new {
            success = true,
            ticket_id = ticketId,
            summary,
            status_id = statusId,
            message = $"Ticket #{ticketId} updated successfully"
        }, IndentedJsonOptions);
    }

    private static string FormatAddActionResponse(JsonElement result, int ticketId) {
        // Extract minimal confirmation from API response
        int? actionId = null;
        string? outcome = null;
        double? timeTaken = null;
        
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0) {
            var first = result[0];
            if (first.TryGetProperty("id", out var id)) actionId = id.GetInt32();
            if (first.TryGetProperty("outcome", out var o)) outcome = o.GetString();
            if (first.TryGetProperty("timetaken", out var t)) timeTaken = t.GetDouble();
        } else if (result.ValueKind == JsonValueKind.Object) {
            if (result.TryGetProperty("id", out var id)) actionId = id.GetInt32();
            if (result.TryGetProperty("outcome", out var o)) outcome = o.GetString();
            if (result.TryGetProperty("timetaken", out var t)) timeTaken = t.GetDouble();
        }

        return JsonSerializer.Serialize(new {
            success = true,
            ticket_id = ticketId,
            action_id = actionId,
            outcome,
            time_taken_hours = timeTaken,
            message = $"Action added to ticket #{ticketId}"
        }, IndentedJsonOptions);
    }

    private static string FormatCreateTicketResponse(JsonElement result) {
        int? ticketId = null;
        string? summary = null;
        int? statusId = null;
        
        if (result.ValueKind == JsonValueKind.Object) {
            if (result.TryGetProperty("id", out var id)) ticketId = id.GetInt32();
            if (result.TryGetProperty("summary", out var s)) summary = s.GetString();
            if (result.TryGetProperty("status_id", out var st)) statusId = st.GetInt32();
        }

        return JsonSerializer.Serialize(new {
            success = true,
            ticket_id = ticketId,
            summary,
            status_id = statusId,
            message = ticketId.HasValue ? $"Ticket #{ticketId} created successfully" : "Ticket created"
        }, IndentedJsonOptions);
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
                sizeWarning = "\n\n" + string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.LargeResponseWarningTemplate, responseSizeKB);
            } else if (responseSizeKB > 100) {
                sizeWarning = "\n\n" + string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.MediumResponseWarningTemplate, responseSizeKB);
            }
            
            if (result.Count == 0 && result.RawResponse != null) {
                return $"{HaloPsaMcpConstants.QueryZeroRowsMessage}{result.RawResponse}{sizeWarning}";
            }
            
            string rowCountInfo = result.Count == 1 ? "1 row" : $"{result.Count} rows";
            return string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.QueryResultTemplate, rowCountInfo, sizeWarning, jsonResponse);
        } catch (TaskCanceledException) {
            return HaloPsaMcpConstants.QueryTimeoutMessage;
        } catch (Exception ex) {
            return string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.QueryFailedTemplate, ex.Message);
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

        var schema = string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.SchemaTemplate,
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
                error = string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.AuthNetworkErrorTemplate, ex.Message),
                login_url = HaloPsaMcpConstants.GetLoginUrl(appConfig)
            }, IndentedJsonOptions);
        } catch (Exception ex) {
            return JsonSerializer.Serialize(new { 
                authenticated = false, 
                error = string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.AuthFailedTemplate, ex.Message),
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
        [Description("Filter by status ID (0 = no filter)")] int status = 0,
        [Description("Filter by client ID (0 = no filter)")] int clientId = 0,
        [Description("Filter by agent ID (0 = no filter)")] int agentId = 0,
        [Description("Search query")] string? search = null) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(count, 50).ToString(CultureInfo.InvariantCulture)
        };

        if (status != 0) {
            queryParams["status"] = status.ToString(CultureInfo.InvariantCulture);
        }

        if (clientId != 0) {
            queryParams["client_id"] = clientId.ToString(CultureInfo.InvariantCulture);
        }

        if (agentId != 0) {
            queryParams["agent_id"] = agentId.ToString(CultureInfo.InvariantCulture);
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
            sizeWarning = "\n\n" + string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.ListLargeResponseWarningTemplate, responseSizeKB);
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
        var trimmed = TrimFields(result, HaloPsaMcpConstants.TicketDetailFields);
        return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaCreateTicketDescription)]
    public static async Task<string> HalopsaCreateTicket(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Ticket summary/title")] string summary,
        [Description("Ticket details/description")] string? details = null,
        [Description("Client ID (0 = no client)")] int clientId = 0,
        [Description("Agent ID to assign (0 = unassigned)")] int agentId = 0,
        [Description("Status ID (0 = default status)")] int statusId = 0,
        [Description("Priority ID (0 = default priority)")] int priorityId = 0,
        [Description("Ticket type ID (0 = default type)")] int ticketTypeId = 0,
        [Description("Site ID (0 = no site)")] int siteId = 0) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var request = new CreateTicketRequest {
            Summary = summary,
            Details = details,
            ClientId = clientId != 0 ? clientId : null,
            AgentId = agentId != 0 ? agentId : null,
            StatusId = statusId != 0 ? statusId : null,
            PriorityId = priorityId != 0 ? priorityId : null,
            TicketTypeId = ticketTypeId != 0 ? ticketTypeId : null,
            SiteId = siteId != 0 ? siteId : null
        };

        var result = await client.PostAsync<JsonElement>("/api/Tickets", request).ConfigureAwait(false);
        return FormatCreateTicketResponse(result);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaUpdateTicketDescription)]
    public static async Task<string> HalopsaUpdateTicket(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Ticket ID to update")] int id,
        [Description("Updated ticket summary/title")] string summary,
        [Description("Updated ticket details/description")] string? details = null,
        [Description("Client ID (0 = no change)")] int clientId = 0,
        [Description("Agent ID to assign (0 = no change)")] int agentId = 0,
        [Description("Status ID (0 = no change)")] int statusId = 0,
        [Description("Priority ID (0 = no change)")] int priorityId = 0,
        [Description("Ticket type ID (0 = no change)")] int ticketTypeId = 0,
        [Description("Site ID (0 = no change)")] int siteId = 0,
        [Description("End user ID (0 = no change)")] int userId = 0) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var request = new UpdateTicketRequest(
            Id: id,
            Summary: summary,
            Details: details,
            ClientId: clientId != 0 ? clientId : null,
            AgentId: agentId != 0 ? agentId : null,
            StatusId: statusId != 0 ? statusId : null,
            PriorityId: priorityId != 0 ? priorityId : null,
            TicketTypeId: ticketTypeId != 0 ? ticketTypeId : null,
            SiteId: siteId != 0 ? siteId : null,
            UserId: userId != 0 ? userId : null);

        var payload = new[] { request };
        var result = await client.PostAsync<JsonElement>("/api/Tickets", payload).ConfigureAwait(false);
        return FormatUpdateTicketResponse(result, id);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaAddActionDescription)]
    public static async Task<string> HalopsaAddAction(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Ticket ID to add action to")] int ticketId,
        [Description("Outcome ID (REQUIRED - use halopsa_get_schema for IDs)")] int outcomeId,
        [Description("Action note (plain text or HTML)")] string? note = null,
        [Description("Time taken in hours (e.g., 0.5 for 30 minutes)")] double? timeTaken = null,
        [Description("New status ID to change ticket to (use halopsa_get_schema for IDs)")] int? newStatus = null,
        [Description("Hide from end user (default: true)")] bool hiddenFromUser = true) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var request = new AddActionRequest(
            TicketId: ticketId,
            OutcomeId: outcomeId,
            Note: note,
            TimeTaken: timeTaken,
            HiddenFromUser: hiddenFromUser,
            NewStatus: newStatus);

        var payload = new[] { request };
        var result = await client.PostAsync<JsonElement>("/api/Actions", payload).ConfigureAwait(false);
        return FormatAddActionResponse(result, ticketId);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetOutcomesDescription)]
    public static async Task<string> HalopsaGetOutcomes(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var result = await client.GetAsync<JsonElement>("/api/Outcome", null).ConfigureAwait(false);
        var trimmed = TrimFields(result, HaloPsaMcpConstants.OutcomeSummaryFields);
        return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
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
            sizeWarning = "\n\n" + string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.ActionsLargeResponseWarningTemplate, responseSizeKB);
        }
        
        return $"{jsonResponse}{sizeWarning}";
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetTimesheetDescription)]
    public static async Task<string> HalopsaGetTimesheet(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Timesheet ID")] int id,
        [Description("Agent ID (0 = omit, uses current user context)")] int agentId = 0,
        [Description("Date to retrieve (UTC ISO 8601, e.g. 2026-03-02T00:00:00Z)")] string? date = null) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
        var queryParams = new Dictionary<string, string>();
        if (agentId != 0) queryParams["agent_id"] = agentId.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(date)) queryParams["date"] = date;
        var result = await client.GetAsync<JsonElement>($"/api/Timesheet/{id}", queryParams.Count > 0 ? queryParams : null).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaUpdateTimesheetDescription)]
    public static async Task<string> HalopsaUpdateTimesheet(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Timesheet ID (get from halopsa_get_timesheet or halopsa_query)")] int id,
        [Description("Timezone offset in minutes from UTC. Pacific Standard=480, Pacific Daylight=420")] int utcOffset = 480,
        [Description("Shift start time in UTC ISO 8601 (e.g. 2026-03-02T15:30:00Z)")] string? startTime = null,
        [Description("Shift end time in UTC ISO 8601 (e.g. 2026-03-02T23:30:00Z)")] string? endTime = null,
        [Description("Submit timesheet for manager approval")] bool submitApproval = false,
        [Description("Approve the timesheet (manager action)")] bool approve = false,
        [Description("Reject the timesheet (manager action)")] bool reject = false,
        [Description("Revert a previously submitted approval")] bool revertApproval = false) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var current = await client.GetAsync<JsonElement>($"/api/Timesheet/{id}", null).ConfigureAwait(false);

        // If the API returns id=0 the record doesn't exist — caller must use halopsa_create_timesheet first
        if (current.TryGetProperty("id", out var idProp) && idProp.GetInt32() == 0) {
            return JsonSerializer.Serialize(new {
                success = false,
                error = $"Timesheet record with ID {id} does not exist. Use halopsa_create_timesheet to create it first, then update with the returned ID."
            }, IndentedJsonOptions);
        }

        // Deserialize to a mutable dictionary so we can merge overrides
        var doc = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, JsonElement>>(
            current.GetRawText()) ?? [];

        if (startTime != null) doc["start_time"] = JsonSerializer.Deserialize<JsonElement>($"\"{startTime}\"");
        if (endTime != null) doc["end_time"] = JsonSerializer.Deserialize<JsonElement>($"\"{endTime}\"");
        if (submitApproval) doc["_submitapproval"] = JsonSerializer.Deserialize<JsonElement>("true");
        if (approve) doc["_approve"] = JsonSerializer.Deserialize<JsonElement>("true");
        if (reject) doc["_reject"] = JsonSerializer.Deserialize<JsonElement>("true");
        if (revertApproval) doc["_revertapproval"] = JsonSerializer.Deserialize<JsonElement>("true");

        var payload = new[] { doc };
        var queryParams = new Dictionary<string, string> {
            ["utcoffset"] = utcOffset.ToString(CultureInfo.InvariantCulture)
        };
        var result = await client.PostAsync<JsonElement>("/api/Timesheet", payload, queryParams).ConfigureAwait(false);

        return JsonSerializer.Serialize(new {
            success = true,
            timesheet_id = id,
            message = submitApproval ? $"Timesheet #{id} submitted for approval"
                    : approve       ? $"Timesheet #{id} approved"
                    : reject        ? $"Timesheet #{id} rejected"
                    : revertApproval? $"Timesheet #{id} approval reverted"
                    : $"Timesheet #{id} updated"
        }, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaListTimesheetEventsDescription)]
    public static async Task<string> HalopsaListTimesheetEvents(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Start datetime in UTC ISO 8601 (e.g. 2026-03-05T00:00:00Z)")] string? startDate = null,
        [Description("End datetime in UTC ISO 8601 (e.g. 2026-03-06T00:00:00Z)")] string? endDate = null,
        [Description("Filter by agent ID (0 = current user's entries)")] int agentId = 0) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var queryParams = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(startDate)) queryParams["start_date"] = startDate;
        if (!string.IsNullOrEmpty(endDate)) queryParams["end_date"] = endDate;
        if (agentId != 0) queryParams["agent_id"] = agentId.ToString(CultureInfo.InvariantCulture);

        var result = await client.GetAsync<JsonElement>("/api/TimesheetEvent", queryParams).ConfigureAwait(false);
        var trimmed = TrimFields(result, HaloPsaMcpConstants.TimesheetEventSummaryFields);
        return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaUpsertTimesheetEventDescription)]
    public static async Task<string> HalopsaUpsertTimesheetEvent(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Event ID to update (0 = create new)")] int id = 0,
        [Description("Ticket ID to link this time entry to")] int? ticketId = null,
        [Description("Agent ID (defaults to current user if omitted)")] int? agentId = null,
        [Description("Start datetime in UTC ISO 8601 (e.g. 2026-03-05T01:00:00Z)")] string? startDate = null,
        [Description("End datetime in UTC ISO 8601 (e.g. 2026-03-05T01:30:00Z)")] string? endDate = null,
        [Description("Time taken in hours (e.g. 0.5 = 30 min)")] double? timeTaken = null,
        [Description("Note / description for this time entry")] string? note = null,
        [Description("Subject / title for this time entry")] string? subject = null,
        [Description("Client ID (0 = no client)")] int? clientId = null,
        [Description("Site ID (0 = no site)")] int? siteId = null) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var request = new HaloPsa.Models.TimesheetEventRequest(
            Id: id,
            TicketId: ticketId,
            AgentId: agentId.HasValue && agentId.Value != 0 ? agentId : null,
            StartDate: startDate,
            EndDate: endDate,
            TimeTaken: timeTaken,
            Note: note,
            Subject: subject,
            ClientId: clientId.HasValue && clientId.Value != 0 ? clientId : null,
            SiteId: siteId.HasValue && siteId.Value != 0 ? siteId : null);

        var payload = new[] { request };
        var result = await client.PostAsync<JsonElement>("/api/TimesheetEvent", payload).ConfigureAwait(false);

        int? eventId = null;
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0) {
            var first = result[0];
            if (first.TryGetProperty("id", out var idProp)) eventId = idProp.GetInt32();
        } else if (result.ValueKind == JsonValueKind.Object) {
            if (result.TryGetProperty("id", out var idProp)) eventId = idProp.GetInt32();
        }

        return JsonSerializer.Serialize(new {
            success = true,
            event_id = eventId,
            ticket_id = ticketId,
            message = id == 0
                ? $"Timesheet event created{(eventId.HasValue ? $" (ID: {eventId})" : "")}"
                : $"Timesheet event #{id} updated"
        }, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaCreateTimesheetDescription)]
    public static async Task<string> HalopsaCreateTimesheet(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Agent ID (required)")] int agentId,
        [Description("Date of the timesheet day in UTC ISO 8601 (e.g. 2026-03-04T00:00:00Z)")] string date,
        [Description("Shift start time in UTC ISO 8601 (e.g. 2026-03-04T15:30:00Z for 7:30 AM Pacific)")] string? startTime = null,
        [Description("Shift end time in UTC ISO 8601 (e.g. 2026-03-04T23:30:00Z for 3:30 PM Pacific)")] string? endTime = null,
        [Description("Timezone offset in minutes from UTC. Pacific Standard=480, Pacific Daylight=420")] int utcOffset = 480) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        var entry = new Dictionary<string, object> {
            ["date"] = date,
            ["agent_id"] = agentId
        };
        if (startTime != null) entry["start_time"] = startTime;
        if (endTime != null) entry["end_time"] = endTime;

        var payload = new[] { entry };
        var queryParams = new Dictionary<string, string> {
            ["utcoffset"] = utcOffset.ToString(CultureInfo.InvariantCulture)
        };
        var result = await client.PostAsync<JsonElement>("/api/Timesheet", payload, queryParams).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaDeleteTimesheetEventDescription)]
    public static async Task<string> HalopsaDeleteTimesheetEvent(
        HaloPsaConfig config,
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        McpAuthenticationService? authService,
        TokenStorageService? tokenStorage,
        [Description("Timesheet event ID to delete")] int id) {
        var client = TryCreateUserClient(config, httpContextAccessor, authService, tokenStorage);
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        await client.DeleteAsync($"/api/TimesheetEvent/{id}").ConfigureAwait(false);
        return JsonSerializer.Serialize(new {
            success = true,
            event_id = id,
            message = $"Timesheet event #{id} deleted"
        }, IndentedJsonOptions);
    }
#pragma warning restore CA1863
}
