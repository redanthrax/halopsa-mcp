using System.Diagnostics;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Security;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace HaloPsaMcp.Modules.HaloPsa.Middleware;

/// <summary>
/// Wolverine middleware that supplements MCP-level tool_audit with handler-level records.
/// </summary>
public static class ToolAuditMiddleware {
    public static async Task BeforeAsync(
        Envelope envelope,
        IHttpContextAccessor? httpAccessor,
        ITokenStore tokenStore,
        ILogger logger,
        CancellationToken cancellationToken) {
        var messageType = envelope.Message?.GetType().Name ?? envelope.MessageType ?? "unknown";
        var user = ToolAuditHelper.ResolveUserHint(httpAccessor?.HttpContext, tokenStore);
        var traceId = Activity.Current?.TraceId.ToString() ?? "none";
        logger.LogDebug(
            "handler_audit user={User} handler={Handler} ts={Ts} traceId={TraceId} phase=start",
            user, messageType, DateTimeOffset.UtcNow.ToString("O"), traceId);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public static void After(
        Envelope envelope,
        IHttpContextAccessor? httpAccessor,
        ITokenStore tokenStore,
        ILogger logger) {
        var messageType = envelope.Message?.GetType().Name ?? envelope.MessageType ?? "unknown";
        var user = ToolAuditHelper.ResolveUserHint(httpAccessor?.HttpContext, tokenStore);
        var traceId = Activity.Current?.TraceId.ToString() ?? "none";
        logger.LogInformation(
            "handler_audit user={User} handler={Handler} ts={Ts} traceId={TraceId} status=ok",
            user, messageType, DateTimeOffset.UtcNow.ToString("O"), traceId);
    }
}
