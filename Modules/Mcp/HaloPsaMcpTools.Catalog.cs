using System.ComponentModel;
using System.Text.Json;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Queries.Agents;
using HaloPsaMcp.Modules.HaloPsa.Queries.Appointments;
using HaloPsaMcp.Modules.HaloPsa.Queries.Assets;
using HaloPsaMcp.Modules.HaloPsa.Queries.Clients;
using HaloPsaMcp.Modules.HaloPsa.Queries.KnowledgeBase;
using HaloPsaMcp.Modules.HaloPsa.Queries.Projects;
using HaloPsaMcp.Modules.HaloPsa.Queries.Reports;
using HaloPsaMcp.Modules.HaloPsa.Queries.Sites;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Queries.Users;
using ModelContextProtocol.Server;
using Wolverine;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// MCP tool methods that wrap the broader HaloPSA REST surface (clients, agents,
/// users, sites, assets, knowledge base, reports, projects, etc). Each method is
/// a thin shim that builds a query record and dispatches via Wolverine's
/// <see cref="IMessageBus"/>; the matching handler in
/// <c>Modules/HaloPsa/Handlers</c> does the actual API call.
/// </summary>
internal partial class HaloPsaMcpTools {
    private static async Task<string> InvokeJson<TQuery, TResult>(
        IMessageBus bus, AppConfig appConfig, TQuery query, Func<TResult, JsonElement> select)
        where TQuery : class
        where TResult : class {
        try {
            var result = await bus.InvokeAsync<TResult>(query).ConfigureAwait(false);
            return JsonSerializer.Serialize(select(result), HaloPsaMcpConstants.IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        }
    }

    [McpServerTool]
    [Description("List HaloPSA clients (companies/organizations). Filter with `search`. Use halopsa_query for counts or aggregation.")]
    public static Task<string> HalopsaListClients(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max clients to return (1-100)")] int count = 25,
        [Description("Optional search string")] string? search = null) =>
        InvokeJson<ListClientsQuery, ListClientsResult>(
            bus, appConfig,
            new ListClientsQuery(Math.Min(Math.Max(count, 1), 100), string.IsNullOrEmpty(search) ? null : search),
            r => r.Data);

    [McpServerTool]
    [Description("Get a single HaloPSA client by ID.")]
    public static Task<string> HalopsaGetClient(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Client ID")] int id) =>
        InvokeJson<GetClientQuery, GetClientResult>(bus, appConfig, new GetClientQuery(id), r => r.Data);

    [McpServerTool]
    [Description("List sites for a HaloPSA client. `clientId` is required.")]
    public static Task<string> HalopsaListSites(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Client ID (required)")] int clientId,
        [Description("Max sites to return (1-100)")] int count = 25) =>
        InvokeJson<ListSitesQuery, ListSitesResult>(
            bus, appConfig,
            new ListSitesQuery(clientId, Math.Min(Math.Max(count, 1), 100)),
            r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA agents (internal users). Filter with `search`.")]
    public static Task<string> HalopsaListAgents(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max agents to return (1-100)")] int count = 25,
        [Description("Optional search string")] string? search = null) =>
        InvokeJson<ListAgentsQuery, ListAgentsResult>(
            bus, appConfig,
            new ListAgentsQuery(Math.Min(Math.Max(count, 1), 100), string.IsNullOrEmpty(search) ? null : search),
            r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA end users (customer contacts). Filter by `clientId` or `search`.")]
    public static Task<string> HalopsaListUsers(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max users to return (1-100)")] int count = 25,
        [Description("Optional client_id filter (0 = no filter)")] int clientId = 0,
        [Description("Optional search string")] string? search = null) =>
        InvokeJson<ListUsersQuery, ListUsersResult>(
            bus, appConfig,
            new ListUsersQuery(Math.Min(Math.Max(count, 1), 100),
                clientId > 0 ? clientId : null,
                string.IsNullOrEmpty(search) ? null : search),
            r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA assets (managed devices/systems). Filter by `clientId` or `search`.")]
    public static Task<string> HalopsaListAssets(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max assets to return (1-100)")] int count = 25,
        [Description("Optional client_id filter (0 = no filter)")] int clientId = 0,
        [Description("Optional search string")] string? search = null) =>
        InvokeJson<ListAssetsQuery, ListAssetsResult>(
            bus, appConfig,
            new ListAssetsQuery(Math.Min(Math.Max(count, 1), 100),
                clientId > 0 ? clientId : null,
                string.IsNullOrEmpty(search) ? null : search),
            r => r.Data);

    [McpServerTool]
    [Description("Get a single HaloPSA asset by ID.")]
    public static Task<string> HalopsaGetAsset(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Asset ID")] int id) =>
        InvokeJson<GetAssetQuery, GetAssetResult>(bus, appConfig, new GetAssetQuery(id), r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA knowledge base articles. Filter with `search`.")]
    public static Task<string> HalopsaListKbArticles(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max articles to return (1-100)")] int count = 25,
        [Description("Optional search string")] string? search = null) =>
        InvokeJson<ListKbArticlesQuery, ListKbArticlesResult>(
            bus, appConfig,
            new ListKbArticlesQuery(Math.Min(Math.Max(count, 1), 100), string.IsNullOrEmpty(search) ? null : search),
            r => r.Data);

    [McpServerTool]
    [Description("Get a single HaloPSA knowledge base article by ID.")]
    public static Task<string> HalopsaGetKbArticle(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Article ID")] int id) =>
        InvokeJson<GetKbArticleQuery, GetKbArticleResult>(bus, appConfig, new GetKbArticleQuery(id), r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA report definitions registered in the tenant.")]
    public static Task<string> HalopsaListReports(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max reports to return (1-100)")] int count = 25) =>
        InvokeJson<ListReportsQuery, ListReportsResult>(
            bus, appConfig,
            new ListReportsQuery(Math.Min(Math.Max(count, 1), 100)),
            r => r.Data);

    [McpServerTool]
    [Description("Get a HaloPSA report definition (parameters, SQL, layout) by ID.")]
    public static Task<string> HalopsaGetReportDefinition(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Report ID")] int id) =>
        InvokeJson<GetReportDefinitionQuery, GetReportDefinitionResult>(
            bus, appConfig, new GetReportDefinitionQuery(id), r => r.Data);

    [McpServerTool]
    [Description("Run a saved HaloPSA report by ID. `parameters` is an optional JSON object passed to the report.")]
    public static Task<string> HalopsaRunReport(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Report ID")] int id,
        [Description("Optional JSON object of report parameters, e.g. {\"start\":\"2026-01-01\"}")] string? parameters = null) =>
        InvokeJson<RunReportQuery, RunReportResult>(
            bus, appConfig,
            new RunReportQuery(id, string.IsNullOrEmpty(parameters) ? null : parameters),
            r => r.Data);

    [McpServerTool]
    [Description("List satisfaction survey responses. Filter by `ticketId` to scope to a single ticket.")]
    public static Task<string> HalopsaListSurveys(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max survey responses to return (1-100)")] int count = 25,
        [Description("Optional ticket_id filter (0 = no filter)")] int ticketId = 0) =>
        InvokeJson<ListSurveysQuery, ListSurveysResult>(
            bus, appConfig,
            new ListSurveysQuery(Math.Min(Math.Max(count, 1), 100), ticketId > 0 ? ticketId : null),
            r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA ticket statuses. `type=0` returns ticket statuses; omit for all types.")]
    public static Task<string> HalopsaListStatuses(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Status type filter (-1 = no filter, 0 = ticket statuses, etc)")] int type = -1) =>
        InvokeJson<ListStatusesQuery, ListStatusesResult>(
            bus, appConfig,
            new ListStatusesQuery(type >= 0 ? type : null),
            r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA request types (ticket categories). `visibleOnly=true` excludes hidden types.")]
    public static Task<string> HalopsaListRequestTypes(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Return only visible request types")] bool visibleOnly = false) =>
        InvokeJson<ListRequestTypesQuery, ListRequestTypesResult>(
            bus, appConfig, new ListRequestTypesQuery(visibleOnly), r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA appointments. Filter by `agentId` and a UTC ISO 8601 date range.")]
    public static Task<string> HalopsaListAppointments(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max appointments to return (1-100)")] int count = 25,
        [Description("Optional agent_id filter (0 = no filter)")] int agentId = 0,
        [Description("Start datetime in UTC ISO 8601 (optional)")] string? startDate = null,
        [Description("End datetime in UTC ISO 8601 (optional)")] string? endDate = null) =>
        InvokeJson<ListAppointmentsQuery, ListAppointmentsResult>(
            bus, appConfig,
            new ListAppointmentsQuery(
                Math.Min(Math.Max(count, 1), 100),
                agentId > 0 ? agentId : null,
                string.IsNullOrEmpty(startDate) ? null : startDate,
                string.IsNullOrEmpty(endDate) ? null : endDate),
            r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA projects (request types where RTIsProject = 1). Filters: clientId, search, openOnly. Backed by the reporting DB — accurate count vs. /api/Projects which mixes project tasks with parents.")]
    public static Task<string> HalopsaListProjects(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max projects to return (1-100)")] int count = 25,
        [Description("Optional client_id filter (0 = no filter)")] int clientId = 0,
        [Description("Optional case-insensitive substring match on project summary")] string? search = null,
        [Description("If true (default), excludes Status 8 (cancelled) and 9 (closed)")] bool openOnly = true) =>
        InvokeJson<ListProjectsQuery, ListProjectsResult>(
            bus, appConfig,
            new ListProjectsQuery(Math.Min(Math.Max(count, 1), 100),
                clientId > 0 ? clientId : null,
                string.IsNullOrEmpty(search) ? null : search,
                openOnly),
            r => r.Data);

    [McpServerTool]
    [Description("List HaloPSA sales opportunities. Filter by `clientId` or `search`.")]
    public static Task<string> HalopsaListOpportunities(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("Max opportunities to return (1-100)")] int count = 25,
        [Description("Optional client_id filter (0 = no filter)")] int clientId = 0,
        [Description("Optional search string")] string? search = null) =>
        InvokeJson<ListOpportunitiesQuery, ListOpportunitiesResult>(
            bus, appConfig,
            new ListOpportunitiesQuery(Math.Min(Math.Max(count, 1), 100),
                clientId > 0 ? clientId : null,
                string.IsNullOrEmpty(search) ? null : search),
            r => r.Data);

    [McpServerTool]
    [Description("Make a direct HaloPSA REST API call. ESCAPE HATCH for endpoints not exposed by the typed tools. " +
                 "`endpoint` is path only (e.g. /api/Invoice). `method` defaults to GET. " +
                 "`body` is a JSON string for POST/PUT bodies.")]
    public static Task<string> HalopsaApiCall(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("HaloPSA endpoint path (e.g. /api/Invoice)")] string endpoint,
        [Description("HTTP method (GET, POST, PUT)")] string method = "GET",
        [Description("Optional JSON body for POST/PUT")] string? body = null) =>
        InvokeJson<ApiCallQuery, ApiCallResult>(
            bus, appConfig,
            new ApiCallQuery(endpoint, method, string.IsNullOrEmpty(body) ? null : body),
            r => r.Data);
}
