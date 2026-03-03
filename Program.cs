using HaloPsaMcp.Mcp;
using HaloPsaMcp.Modules.Authentication.Endpoints;
using HaloPsaMcp.Modules.Authentication.Middleware;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Extensions;
using HaloPsaMcp.Modules.Common.Middleware;
using HaloPsaMcp.Modules.Common.Models;
using Serilog;
using Serilog.Events;
using Wolverine;
using Wolverine.Http;

// Load environment variables from .env file if present
DotNetEnv.Env.TraversePath().Load();

// Load and register configuration
var appConfig = AppConfig.LoadFromEnvironment();

// Parse command-line arguments to determine mode
var isHttpMode = args.Contains("--http");

if (isHttpMode) {
    // HTTP Mode (Production): OAuth server with Bearer authentication
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) => config
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("HaloPsaMcp.Modules.Authentication", LogEventLevel.Debug)
        .MinimumLevel.Override("HaloPsaMcp.Modules.HaloPsa", LogEventLevel.Debug)
        .MinimumLevel.Override("HaloPsaMcp.Modules.Common", LogEventLevel.Debug)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

    // Register appConfig singleton (loaded from environment)
    builder.Services.AddSingleton(appConfig);

    // Register modules
    builder.Services.AddSharedModule(builder.Configuration);
    builder.Services.AddAuthenticationModule(builder.Configuration);
    builder.Services.AddHaloPsaModule(builder.Configuration);
    builder.Services.AddHttpClient();

    // Configure Kestrel
    builder.WebHost.ConfigureKestrel(options => {
        options.ListenAnyIP(appConfig.HttpPort);
    });

    // Configure Wolverine with handler discovery
    builder.Host.UseWolverine(opts => {
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
        opts.Services.AddWolverineHttp();
    });

    // Configure MCP server with HTTP transport and per-session authentication
    builder.Services.AddMcpServer()
        .WithHttpTransport(options => {
            // Configure per-session options based on the authenticated user
            options.ConfigureSessionOptions = async (httpContext, mcpOptions, cancellationToken) => {
                // Extract Bearer token from Authorization header
                var authHeader = httpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader)
                    || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                    return; // No token, let middleware handle 401
                }

                var token = authHeader.Substring(7); // Remove "Bearer " prefix

                // Validate token against HaloPSA and get user info
                var authService =
                    httpContext.RequestServices.GetRequiredService<McpAuthenticationService>();

                var isValid = await authService.ValidateTokenAsync(token).ConfigureAwait(false);
                if (!isValid) {
                    return; // Invalid token, let middleware handle 401
                }

                // Get token metadata from storage
                var tokenStorage = httpContext.RequestServices.GetRequiredService<TokenStorageService>();
                var userEntry = tokenStorage.GetToken(token);

                // Store token info in HttpContext for tool handlers to access
                authService.StoreTokenInContext(
                    httpContext,
                    token,
                    userEntry?.RefreshToken,
                    userEntry?.ExpiresAt
                );
            };
        })
        .WithTools<HaloPsaMcpTools>();

    var app = builder.Build();

    // Health check — no auth required (used by AKS liveness/readiness probes)
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    // OAuth endpoints — no auth required (they are the auth flow)
    app.MapOAuthEndpoints();

    // MCP endpoint — Bearer token required, scoped middleware
    app.UseWhen(
        ctx => ctx.Request.Path.StartsWithSegments("/mcp"),
        branch => {
            branch.UseMiddleware<RequestLoggingMiddleware>();
            branch.UseMiddleware<McpAuthenticationMiddleware>();
        });

    app.MapMcp("/mcp").DisableAntiforgery();

    Log.Information("HaloPSA MCP server running on http://localhost:{Port}", appConfig.HttpPort);
    Log.Information("OAuth login: http://localhost:{Port}/login", appConfig.HttpPort);
    Log.Information("MCP endpoint: http://localhost:{Port}/mcp", appConfig.HttpPort);

    await app.RunAsync().ConfigureAwait(false);
} else {
    // Stdio Mode (Development): MCP over stdin/stdout for Claude Desktop,
    // plus a background HTTP server for OAuth login and token refresh.
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) => config
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose, formatProvider: System.Globalization.CultureInfo.InvariantCulture));

    builder.Services.AddSingleton(appConfig);

    // Register modules
    builder.Services.AddSharedModule(builder.Configuration);
    builder.Services.AddAuthenticationModule(builder.Configuration);
    builder.Services.AddHaloPsaModule(builder.Configuration);
    builder.Services.AddHttpClient();

    builder.WebHost.ConfigureKestrel(options => {
        options.ListenAnyIP(appConfig.HttpPort);
    });

    // MCP over stdio for Claude Desktop
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<HaloPsaMcpTools>();

    var app = builder.Build();

    app.MapOAuthEndpoints();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    Log.Information("OAuth server available at http://localhost:{Port}/login for re-authentication", appConfig.HttpPort);

    await app.RunAsync().ConfigureAwait(false);
}
