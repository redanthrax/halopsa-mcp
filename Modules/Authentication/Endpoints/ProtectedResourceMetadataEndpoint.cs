using System.Collections.Concurrent;
#pragma warning disable IDE0005 // Using directives flagged as unnecessary but are required for the code
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 discovery endpoint for protected resource metadata
/// </summary>
internal static class ProtectedResourceMetadataEndpoint
{
    public static void MapProtectedResourceMetadata(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/oauth-protected-resource", ProtectedResourceMetadata);
    }

    private static IResult ProtectedResourceMetadata(AppConfig config)
    {
        return Results.Ok(new
        {
            resource = config.AuthBaseUrl,
            authorization_servers = new[] { config.AuthBaseUrl },
            bearer_methods_supported = new[] { "header" },
            resource_name = "HaloPSA MCP"
        });
    }
}