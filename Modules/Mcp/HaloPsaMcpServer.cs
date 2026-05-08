using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Queries.Contracts;
using HaloPsaMcp.Modules.HaloPsa.Queries.Outcomes;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;
using HaloPsaMcp.Modules.HaloPsa.Services;
using ModelContextProtocol.Server;
using Wolverine;

namespace HaloPsaMcp.Modules.Mcp;

internal partial class HaloPsaMcpTools {
    private static readonly JsonSerializerOptions IndentedJsonOptions = HaloPsaMcpConstants.IndentedJsonOptions;

    private static string ExplainStatus(int status) => status switch {
        >= 200 and < 300 => "ok",
        400 => "bad request (often = scope present but tenant feature/SQL gate refused; or 'Please contact the administrator')",
        401 => "unauthenticated",
        403 => "forbidden (scope not granted to this OAuth app)",
        404 => "endpoint not found (tenant feature off?)",
        408 => "timeout",
        0   => "network error",
        _   => $"http {status}"
    };

    private static HttpContext? GetContext(IHttpContextAccessor? accessor) => accessor?.HttpContext;

    private static JsonElement TrimFields(JsonElement element, string[] allowedFields) {
        var allowed = new HashSet<string>(allowedFields, StringComparer.OrdinalIgnoreCase);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            WriteTrimmed(writer, element, allowed);
        }
        return JsonSerializer.Deserialize<JsonElement>(stream.ToArray());
    }

    private static void WriteTrimmed(Utf8JsonWriter writer, JsonElement element, HashSet<string> allowed) {
        switch (element.ValueKind) {
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) {
                    WriteTrimmed(writer, item, allowed);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject()) {
                    if (allowed.Contains(prop.Name)) {
                        prop.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string FormatUpdateTicketResponse(JsonElement result, int ticketId) {
        var (summary, statusId) = ExtractTicketHeader(result);
        return JsonSerializer.Serialize(new {
            success = true,
            ticket_id = ticketId,
            summary,
            status_id = statusId,
            message = $"Ticket #{ticketId} updated successfully"
        }, IndentedJsonOptions);
    }

    private static string FormatAddActionResponse(JsonElement result, int ticketId) {
        int? actionId = null;
        string? outcome = null;
        double? timeTaken = null;

        var first = FirstObject(result);
        if (first.HasValue) {
            if (first.Value.TryGetProperty("id", out var id)) actionId = id.GetInt32();
            if (first.Value.TryGetProperty("outcome", out var o)) outcome = o.GetString();
            if (first.Value.TryGetProperty("timetaken", out var t)) timeTaken = t.GetDouble();
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

    private static (string? Summary, int? StatusId) ExtractTicketHeader(JsonElement result) {
        var first = FirstObject(result);
        if (!first.HasValue) return (null, null);
        var s = first.Value.TryGetProperty("summary", out var sv) ? sv.GetString() : null;
        var st = first.Value.TryGetProperty("status_id", out var stv) ? stv.GetInt32() : (int?)null;
        return (s, st);
    }

    private static JsonElement? FirstObject(JsonElement result) {
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0) {
            return result[0];
        }
        if (result.ValueKind == JsonValueKind.Object) {
            return result;
        }
        return null;
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaQueryDescription)]
    public static async Task<string> HalopsaQuery(
        AppConfig appConfig,
        IMessageBus bus,
        SchemaCatalogService catalog,
        [Description("SQL SELECT query. Must include TOP N to limit results.")] string sql) {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try {
            var result = await bus.InvokeAsync<ExecuteQueryResult>(new ExecuteQueryQuery(sql), cts.Token).ConfigureAwait(false);
            if (result == null) {
                return HaloPsaMcpConstants.AuthRequiredMessage;
            }

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
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        } catch (TaskCanceledException) {
            return HaloPsaMcpConstants.QueryTimeoutMessage;
        } catch (Exception ex) {
            var hint = HaloPsaMcpConstants.BuildColumnHint(ex.Message, sql, catalog);
            return string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.QueryFailedTemplate, ex.Message)
                + hint
                + $"\nlogin_url={HaloPsaMcpConstants.GetLoginUrl(appConfig)}";
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetSchemaDescription)]
    public static async Task<string> HalopsaGetSchema(
        HaloPsaClientFactory clientFactory,
        IHttpContextAccessor? httpContextAccessor,
        AppConfig appConfig) {
        var client = clientFactory.CreateClient(GetContext(httpContextAccessor));
        if (client == null) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
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

        return string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.SchemaTemplate,
            string.Join("\n", HaloPsaSchema.ImportantNotes.Select(n => $"- {n}")),
            statusListText,
            agentListText,
            tablesText,
            examplesText);
    }

    [McpServerTool]
    [Description("Returns the public login URL for this HaloPSA MCP server instance. This is the URL users click in their browser to authenticate. The URL is server-specific (differs between local dev, staging, and production); call this rather than recalling a URL from a previous session.")]
    public static string HalopsaGetLoginUrl(AppConfig appConfig) {
        var url = HaloPsaMcpConstants.GetLoginUrl(appConfig);
        return JsonSerializer.Serialize(new {
            login_url = url,
        }, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaAuthStatusDescription)]
    public static async Task<string> HalopsaAuthStatus(
        HaloPsaClientFactory clientFactory,
        IHttpContextAccessor? httpContextAccessor,
        AppConfig appConfig) {
        var client = clientFactory.CreateClient(GetContext(httpContextAccessor));
        if (client == null) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try {
            var result = await client.GetAsync<JsonElement>("/api/Agent/me", null, cts.Token).ConfigureAwait(false);
            var name = result.TryGetProperty("name", out var n) ? n.GetString() : HaloPsaMcpConstants.UnknownAgentName;
            var email = result.TryGetProperty("email", out var e) ? e.GetString() : "";
            return JsonSerializer.Serialize(new {
                authenticated = true,
                agent_name = name,
                agent_email = email,
                message = HaloPsaMcpConstants.AuthenticatedMessage,
                login_url = HaloPsaMcpConstants.GetLoginUrl(appConfig)
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
    [Description("Decode the current HaloPSA access token and return granted scopes/permissions and identity claims (agent_id, role, etc). Use this right after authenticating to verify what the MCP server is allowed to do on your behalf.")]
    public static string HalopsaWhoami(
        AppConfig appConfig,
        IHttpContextAccessor? httpContextAccessor,
        TokenStorageService? tokenStorage) {
        string? haloAccess = null;
        long? expiresAt = null;

        var ctx = httpContextAccessor?.HttpContext;
        if (ctx != null) {
            var authHeader = ctx.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                var mcpToken = authHeader.Substring(7);
                var entry = tokenStorage?.GetToken(mcpToken);
                if (entry != null) {
                    haloAccess = entry.AccessToken;
                    expiresAt = entry.ExpiresAt;
                }
            }
        }
        if (haloAccess == null) {
            var entry = tokenStorage?.GetDefaultToken();
            if (entry != null) {
                haloAccess = entry.AccessToken;
                expiresAt = entry.ExpiresAt;
            }
        }

        if (string.IsNullOrEmpty(haloAccess)) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }

        var claims = JwtClaimsReader.TryReadClaims(haloAccess);
        if (claims == null) {
            return JsonSerializer.Serialize(new {
                authenticated = true,
                jwt = false,
                message = "Access token is not a JWT (opaque token) — HaloPSA does not expose claims for this token.",
                expires_at_unix_ms = expiresAt
            }, IndentedJsonOptions);
        }

        var scopes = JwtClaimsReader.ExtractScopes(claims);
        string? agentId = claims.TryGetValue("agent_id", out var aid) ? aid.ToString() : null;
        string? userId = claims.TryGetValue("sub", out var sub) ? sub.GetString() : null;
        string? clientId = claims.TryGetValue("client_id", out var cid) ? cid.GetString() : null;
        string? role = claims.TryGetValue("role", out var r) ? r.ToString() : null;
        long? exp = claims.TryGetValue("exp", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : null;

        var sortedClaims = JwtClaimsReader.Render(claims, IndentedJsonOptions);

        return JsonSerializer.Serialize(new {
            authenticated = true,
            jwt = true,
            agent_id = agentId,
            user_id = userId,
            client_id = clientId,
            role,
            scopes,
            expires_at_unix = exp,
            expires_at_session_unix_ms = expiresAt,
            all_claims = JsonDocument.Parse(sortedClaims).RootElement
        }, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description("Probe HaloPSA endpoints to discover which permissions/scopes the current access token actually has. Returns a per-capability allow/deny map (200=allowed, 401=auth, 403=forbidden, 404=not found, 408=timeout). Use when the JWT is opaque and scopes can't be read from claims, or to verify what the user can do before attempting work.")]
    public static async Task<string> HalopsaCapabilities(
        HaloPsaClientFactory clientFactory,
        IHttpContextAccessor? httpContextAccessor,
        AppConfig appConfig) {
        var client = clientFactory.CreateClient(GetContext(httpContextAccessor));
        if (client == null) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }

        var probes = new (string Label, string Endpoint)[] {
            ("read:agents",      "/api/Agent"),
            ("read:clients",     "/api/Client"),
            ("read:tickets",     "/api/Tickets"),
            ("read:contracts",   "/api/ClientContract"),
            ("read:assets",      "/api/Asset"),
            ("read:invoices",    "/api/Invoice"),
            ("read:reporting",   "/api/Report"),
            ("read:knowledge",   "/api/KBArticle"),
            ("read:status",      "/api/Status"),
            ("read:users",       "/api/Users"),
            ("read:sites",       "/api/Site"),
            ("read:teams",       "/api/Team"),
        };

        var readResults = await Task.WhenAll(probes.Select(async p => {
            var status = await client.ProbeAsync(p.Endpoint).ConfigureAwait(false);
            return new {
                capability = p.Label,
                endpoint = p.Endpoint,
                method = "GET",
                status,
                allowed = status >= 200 && status < 300,
                reason = ExplainStatus(status)
            };
        })).ConfigureAwait(false);

        var editReportingStatus = await client.ProbePostAsync(
            "/api/Report",
            new object[] { new { _loadreportonly = true, sql = "SELECT 1 WHERE 1=0" } }
        ).ConfigureAwait(false);

        var writeResults = new[] {
            new {
                capability = "edit:reporting",
                endpoint = "/api/Report",
                method = "POST",
                status = editReportingStatus,
                allowed = editReportingStatus >= 200 && editReportingStatus < 300,
                reason = ExplainStatus(editReportingStatus)
            }
        };

        var results = readResults.Concat(writeResults).ToArray();

        var allowed = results.Where(r => r.allowed).Select(r => r.capability).ToArray();
        var denied  = results.Where(r => !r.allowed && (r.status == 403 || r.status == 400)).Select(r => r.capability).ToArray();

        return JsonSerializer.Serialize(new {
            summary = new {
                allowed_count = allowed.Length,
                denied_count = denied.Length,
                allowed,
                denied,
                hint = denied.Length > 0
                    ? "Forbidden capabilities are missing from the OAuth app's permission grants in HaloPSA. Edit the application in HaloPSA admin → Configuration → Integrations → HaloPSA API → Applications, enable the missing scopes, then re-authenticate at " + HaloPsaMcpConstants.GetLoginUrl(appConfig)
                    : "All probed capabilities are accessible."
            },
            probes = results
        }, IndentedJsonOptions);
    }

    [McpServerTool]
    [Description("List HaloPSA client contracts via /api/ClientContract REST endpoint. Returns contract details including budget/usage hours so you can identify overages. Filter by client_id or search term. Use this instead of SQL when the reporting DB is unavailable.")]
    public static async Task<string> HalopsaListContracts(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max contracts to return (default 25, max 100)")] int count = 25,
        [Description("Optional client_id filter")] int clientId = 0,
        [Description("Optional search string")] string? search = null) {
        try {
            var result = await bus.InvokeAsync<ListContractsResult>(
                new ListContractsQuery(Math.Min(Math.Max(count, 1), 100),
                    clientId > 0 ? clientId : null,
                    string.IsNullOrEmpty(search) ? null : search)).ConfigureAwait(false);
            return JsonSerializer.Serialize(result.Data, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaListTicketsDescription)]
    public static async Task<string> HalopsaListTickets(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Maximum number of tickets to return (1-50)")] int count = 10,
        [Description("Filter by status ID (0 = no filter)")] int status = 0,
        [Description("Filter by client ID (0 = no filter)")] int clientId = 0,
        [Description("Filter by agent ID (0 = no filter)")] int agentId = 0,
        [Description("Search query")] string? search = null) {
        try {
            var result = await bus.InvokeAsync<ListTicketsResult>(new ListTicketsQuery(
                Math.Min(count, 50),
                status != 0 ? status : null,
                clientId != 0 ? clientId : null,
                agentId != 0 ? agentId : null,
                string.IsNullOrEmpty(search) ? null : search)).ConfigureAwait(false);

            var trimmed = TrimFields(result.Data, HaloPsaMcpConstants.TicketSummaryFields);
            var jsonResponse = JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
            var responseSizeKB = Encoding.UTF8.GetByteCount(jsonResponse) / 1024.0;

            string sizeWarning = "";
            if (responseSizeKB > 100) {
                sizeWarning = "\n\n" + string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.ListLargeResponseWarningTemplate, responseSizeKB);
            }

            return $"{jsonResponse}{sizeWarning}";
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetTicketDescription)]
    public static async Task<string> HalopsaGetTicket(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Ticket ID")] int id) {
        try {
            var result = await bus.InvokeAsync<GetTicketResult>(new GetTicketQuery(id)).ConfigureAwait(false);
            var trimmed = TrimFields(result.Data, HaloPsaMcpConstants.TicketDetailFields);
            return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaCreateTicketDescription)]
    public static async Task<string> HalopsaCreateTicket(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Ticket summary/title")] string summary,
        [Description("Ticket details/description")] string? details = null,
        [Description("Client ID (0 = no client)")] int clientId = 0,
        [Description("Agent ID to assign (0 = unassigned)")] int agentId = 0,
        [Description("Status ID (0 = default status)")] int statusId = 0,
        [Description("Priority ID (0 = default priority)")] int priorityId = 0,
        [Description("Ticket type ID (0 = default type)")] int ticketTypeId = 0,
        [Description("Site ID (0 = no site)")] int siteId = 0) {
        try {
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
            var result = await bus.InvokeAsync<CreateTicketResult>(new CreateTicketQuery(request)).ConfigureAwait(false);
            return FormatCreateTicketResponse(result.Data);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaUpdateTicketDescription)]
    public static async Task<string> HalopsaUpdateTicket(
        AppConfig appConfig,
        IMessageBus bus,
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
        try {
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

            var result = await bus.InvokeAsync<UpdateTicketResult>(new UpdateTicketQuery(request)).ConfigureAwait(false);
            return FormatUpdateTicketResponse(result.Data, id);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaAddActionDescription)]
    public static async Task<string> HalopsaAddAction(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Ticket ID to add action to")] int ticketId,
        [Description("Outcome ID (REQUIRED - use halopsa_get_schema for IDs)")] int outcomeId,
        [Description("Action note (plain text or HTML)")] string? note = null,
        [Description("Time taken in hours (e.g., 0.5 for 30 minutes)")] double? timeTaken = null,
        [Description("New status ID to change ticket to (use halopsa_get_schema for IDs)")] int? newStatus = null,
        [Description("Hide from end user (default: true)")] bool hiddenFromUser = true) {
        try {
            var request = new AddActionRequest(
                TicketId: ticketId,
                OutcomeId: outcomeId,
                Note: note,
                TimeTaken: timeTaken,
                HiddenFromUser: hiddenFromUser,
                NewStatus: newStatus);

            var result = await bus.InvokeAsync<AddActionResult>(new AddActionQuery(request)).ConfigureAwait(false);
            return FormatAddActionResponse(result.Data, ticketId);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetOutcomesDescription)]
    public static async Task<string> HalopsaGetOutcomes(
        AppConfig appConfig,
        IMessageBus bus) {
        try {
            var result = await bus.InvokeAsync<GetOutcomesResult>(new GetOutcomesQuery()).ConfigureAwait(false);
            var trimmed = TrimFields(result.Data, HaloPsaMcpConstants.OutcomeSummaryFields);
            return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaListActionsDescription)]
    public static async Task<string> HalopsaListActions(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Ticket ID")] int ticketId,
        [Description("Maximum number of actions to return (1-50)")] int count = 10) {
        try {
            var result = await bus.InvokeAsync<ListActionsResult>(
                new ListActionsQuery(ticketId, Math.Min(count, 50))).ConfigureAwait(false);
            var trimmed = TrimFields(result.Data, HaloPsaMcpConstants.ActionSummaryFields);
            var jsonResponse = JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
            var responseSizeKB = Encoding.UTF8.GetByteCount(jsonResponse) / 1024.0;

            string sizeWarning = "";
            if (responseSizeKB > 100) {
                sizeWarning = "\n\n" + string.Format(CultureInfo.InvariantCulture, HaloPsaMcpConstants.ActionsLargeResponseWarningTemplate, responseSizeKB);
            }

            return $"{jsonResponse}{sizeWarning}";
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaGetTimesheetDescription)]
    public static async Task<string> HalopsaGetTimesheet(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Timesheet ID")] int id,
        [Description("Agent ID (0 = omit, uses current user context)")] int agentId = 0,
        [Description("Date to retrieve (UTC ISO 8601, e.g. 2026-03-02T00:00:00Z)")] string? date = null) {
        try {
            var result = await bus.InvokeAsync<GetTimesheetResult>(
                new GetTimesheetQuery(id, agentId != 0 ? agentId : null, string.IsNullOrEmpty(date) ? null : date)).ConfigureAwait(false);
            return JsonSerializer.Serialize(result.Data, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaUpdateTimesheetDescription)]
    public static async Task<string> HalopsaUpdateTimesheet(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Timesheet ID (get from halopsa_get_timesheet or halopsa_query)")] int id,
        [Description("Timezone offset in minutes from UTC. Pacific Standard=480, Pacific Daylight=420")] int utcOffset = 480,
        [Description("Shift start time in UTC ISO 8601 (e.g. 2026-03-02T15:30:00Z)")] string? startTime = null,
        [Description("Shift end time in UTC ISO 8601 (e.g. 2026-03-02T23:30:00Z)")] string? endTime = null,
        [Description("Submit timesheet for manager approval")] bool submitApproval = false,
        [Description("Approve the timesheet (manager action)")] bool approve = false,
        [Description("Reject the timesheet (manager action)")] bool reject = false,
        [Description("Revert a previously submitted approval")] bool revertApproval = false) {
        try {
            var result = await bus.InvokeAsync<UpdateTimesheetResult>(
                new UpdateTimesheetQuery(id, utcOffset, startTime, endTime, submitApproval, approve, reject, revertApproval)).ConfigureAwait(false);

            if (!result.Exists) {
                return JsonSerializer.Serialize(new {
                    success = false,
                    error = $"Timesheet record with ID {id} does not exist. Use halopsa_create_timesheet to create it first, then update with the returned ID."
                }, IndentedJsonOptions);
            }

            return JsonSerializer.Serialize(new {
                success = true,
                timesheet_id = id,
                message = submitApproval ? $"Timesheet #{id} submitted for approval"
                        : approve       ? $"Timesheet #{id} approved"
                        : reject        ? $"Timesheet #{id} rejected"
                        : revertApproval? $"Timesheet #{id} approval reverted"
                        : $"Timesheet #{id} updated"
            }, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaListTimesheetEventsDescription)]
    public static async Task<string> HalopsaListTimesheetEvents(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Start datetime in UTC ISO 8601 (e.g. 2026-03-05T00:00:00Z)")] string? startDate = null,
        [Description("End datetime in UTC ISO 8601 (e.g. 2026-03-06T00:00:00Z)")] string? endDate = null,
        [Description("Filter by agent ID (0 = current user's entries)")] int agentId = 0) {
        try {
            var result = await bus.InvokeAsync<ListTimesheetEventsResult>(
                new ListTimesheetEventsQuery(startDate, endDate, agentId != 0 ? agentId : null)).ConfigureAwait(false);
            var trimmed = TrimFields(result.Data, HaloPsaMcpConstants.TimesheetEventSummaryFields);
            return JsonSerializer.Serialize(trimmed, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaUpsertTimesheetEventDescription)]
    public static async Task<string> HalopsaUpsertTimesheetEvent(
        AppConfig appConfig,
        IMessageBus bus,
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
        try {
            var request = new TimesheetEventRequest(
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

            var result = await bus.InvokeAsync<UpsertTimesheetEventResult>(new UpsertTimesheetEventQuery(request)).ConfigureAwait(false);

            return JsonSerializer.Serialize(new {
                success = true,
                event_id = result.EventId,
                ticket_id = ticketId,
                message = id == 0
                    ? $"Timesheet event created{(result.EventId.HasValue ? $" (ID: {result.EventId})" : "")}"
                    : $"Timesheet event #{id} updated"
            }, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaCreateTimesheetDescription)]
    public static async Task<string> HalopsaCreateTimesheet(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Agent ID (required)")] int agentId,
        [Description("Date of the timesheet day in UTC ISO 8601 (e.g. 2026-03-04T00:00:00Z)")] string date,
        [Description("Shift start time in UTC ISO 8601 (e.g. 2026-03-04T15:30:00Z for 7:30 AM Pacific)")] string? startTime = null,
        [Description("Shift end time in UTC ISO 8601 (e.g. 2026-03-04T23:30:00Z for 3:30 PM Pacific)")] string? endTime = null,
        [Description("Timezone offset in minutes from UTC. Pacific Standard=480, Pacific Daylight=420")] int utcOffset = 480) {
        try {
            var result = await bus.InvokeAsync<CreateTimesheetResult>(
                new CreateTimesheetQuery(agentId, date, startTime, endTime, utcOffset)).ConfigureAwait(false);
            return JsonSerializer.Serialize(result.Data, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }

    [McpServerTool]
    [Description(HaloPsaMcpConstants.HalopsaDeleteTimesheetEventDescription)]
    public static async Task<string> HalopsaDeleteTimesheetEvent(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Timesheet event ID to delete")] int id) {
        try {
            var result = await bus.InvokeAsync<DeleteTimesheetEventResult>(new DeleteTimesheetEventCommand(id)).ConfigureAwait(false);
            return JsonSerializer.Serialize(new {
                success = true,
                event_id = result.Id,
                message = $"Timesheet event #{result.Id} deleted"
            }, IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthRequiredMessage;
        }
    }
}
