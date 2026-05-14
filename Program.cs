using System.Threading.RateLimiting;
using HaloPsaMcp.Modules.Mcp;
using HaloPsaMcp.Modules;
using HaloPsaMcp.Modules.Authentication.Endpoints;
using HaloPsaMcp.Modules.Authentication.Middleware;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Middleware;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Wolverine;

DotNetEnv.Env.TraversePath().Load();

var appConfig = AppConfig.LoadFromEnvironment();
var startedAt = DateTime.UtcNow;
var logFormat = (Environment.GetEnvironmentVariable("LOG_FORMAT") ?? "text").ToLowerInvariant();

var isHttpMode = args.Contains("--http");

if (isHttpMode) {
    var builder = WebApplication.CreateBuilder(args);

    // HTTP/AKS mode: never silently fall back to "default" session for handlers
    // that lack an HttpContext. Each request must carry its own bearer.
    TokenStorageService.DisableDefaultFallback = true;

    builder.Host.UseSerilog((context, config) => {
        config
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("HaloPsaMcp.Modules.Authentication", LogEventLevel.Debug)
            .MinimumLevel.Override("HaloPsaMcp.Modules.HaloPsa", LogEventLevel.Debug)
            .MinimumLevel.Override("HaloPsaMcp.Modules.Common", LogEventLevel.Debug)
            .Enrich.FromLogContext();
        if (logFormat == "json") {
            config.WriteTo.Console(new JsonFormatter(renderMessage: true));
        } else {
            config.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
        }
    });

    builder.Services.AddSingleton(appConfig);
    builder.Services.AddAllModules(builder.Configuration);
    builder.Services.AddHttpClient();

    // Give in-flight requests a chance to drain when k8s/docker sends SIGTERM.
    builder.Services.Configure<HostOptions>(o => {
        o.ShutdownTimeout = TimeSpan.FromSeconds(30);
    });

    // Trust the X-Forwarded-* headers from the in-cluster ingress controller so
    // rate limits, redirect URLs, and HSTS reflect the real client + scheme.
    builder.Services.Configure<ForwardedHeadersOptions>(o => {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });

    // Rate limiting on OAuth endpoints — defends /authorize, /token, /register
    // against brute-force PKCE / DCR-flooding attacks. Partition by DCR-issued
    // client_id when present so multiple legitimate users behind a shared NAT
    // (corporate egress IP) don't share a bucket; fall back to IP for /register
    // where no client_id exists yet.
    builder.Services.AddRateLimiter(options => {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("oauth", httpContext => {
            var key = ResolveOAuthPartitionKey(httpContext);
            return RateLimitPartition.GetFixedWindowLimiter(key,
                _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });
        options.AddPolicy("register", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });
    builder.Services.AddHsts(o => {
        o.Preload = true;
        o.IncludeSubDomains = true;
        o.MaxAge = TimeSpan.FromDays(180);
    });
    builder.WebHost.ConfigureKestrel(options => {
        options.ListenAnyIP(appConfig.HttpPort);
    });

    // Wolverine handler discovery — IMessageBus routes MCP tool invocations
    // to the static Handle methods in Modules/HaloPsa/Handlers.
    builder.Host.UseWolverine(opts => {
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    });

    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithTools<HaloPsaMcpTools>();

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseHsts();
    app.UseRateLimiter();

    MapHealthEndpoints(app, startedAt);

    app.MapOAuthEndpoints();

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
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) => {
        config
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext();
        if (logFormat == "json") {
            config.WriteTo.Console(new JsonFormatter(renderMessage: true), standardErrorFromLevel: LogEventLevel.Verbose);
        } else {
            config.WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose, formatProvider: System.Globalization.CultureInfo.InvariantCulture);
        }
        config.WriteTo.File("logs/mcp.log", rollingInterval: RollingInterval.Day, formatProvider: System.Globalization.CultureInfo.InvariantCulture);
    });

    builder.Services.AddSingleton(appConfig);
    builder.Services.AddAllModules(builder.Configuration);
    builder.Services.AddHttpClient();

    builder.Services.Configure<HostOptions>(o => {
        o.ShutdownTimeout = TimeSpan.FromSeconds(30);
    });

    var actualHttpPort = ProbePort(appConfig.HttpPort);
    if (actualHttpPort != appConfig.HttpPort) {
        Log.Warning(
            "Port {Port} is in use; falling back to ephemeral port {Fallback}. " +
            "OAuth login URL will not match AUTH_BASE_URL — fix the port conflict and restart for re-auth to work.",
            appConfig.HttpPort, actualHttpPort);
    }

    builder.WebHost.ConfigureKestrel(options => {
        options.ListenAnyIP(actualHttpPort);
    });

    builder.Host.UseWolverine(opts => {
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    });

    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<HaloPsaMcpTools>();

    var app = builder.Build();

    app.MapOAuthEndpoints();
    MapHealthEndpoints(app, startedAt);

    Log.Information("OAuth server available at http://localhost:{Port}/login for re-authentication", actualHttpPort);

    await app.RunAsync().ConfigureAwait(false);
}

static void MapHealthEndpoints(WebApplication app, DateTime startedAt) {
    // Liveness — process is alive. K8s restarts on failure; should only fail
    // when the process is genuinely wedged. Always 200 once Kestrel is up.
    app.MapGet("/health", () => Results.Ok(new {
        status = "alive",
        uptime_seconds = (long)(DateTime.UtcNow - startedAt).TotalSeconds
    }));

    // Readiness — should this pod receive traffic? Returns 503 + a minimal body
    // when a critical dependency is missing. Public probe endpoint: do NOT
    // include session counts, schema dump timestamps, or any other detail that
    // would aid reconnaissance. Set MCP_READY_VERBOSE=1 in trusted environments
    // to expose detailed checks.
    app.MapGet("/ready", (SchemaCatalogService schema, TokenStorageService tokens) => {
        var verbose = string.Equals(
            Environment.GetEnvironmentVariable("MCP_READY_VERBOSE"), "1", StringComparison.Ordinal);
        // token_storage is the only critical check today. Add Redis/DB checks
        // here when shared backends land.
        var ready = true; // token store is in-memory + persisted; healthy on boot
        var status = ready
            ? (schema.IsLoaded ? "ready" : "degraded")
            : "not_ready";

        if (!verbose) {
            return Results.Json(new { status }, statusCode: ready ? 200 : 503);
        }
        return Results.Json(new {
            status,
            uptime_seconds = (long)(DateTime.UtcNow - startedAt).TotalSeconds,
            checks = new {
                schema_catalog = new {
                    healthy = schema.IsLoaded,
                    table_count = schema.TableCount,
                    dumped_at = schema.DumpedAt
                },
                token_storage = new {
                    healthy = true,
                    session_count = tokens.SessionCount,
                    active_sessions = tokens.ActiveSessionCount
                }
            }
        }, statusCode: ready ? 200 : 503);
    });
}

static int ProbePort(int preferred) {
    try {
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, preferred);
        probe.Start();
        probe.Stop();
        return preferred;
    } catch (System.Net.Sockets.SocketException) {
        var fallback = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        fallback.Start();
        var port = ((System.Net.IPEndPoint)fallback.LocalEndpoint).Port;
        fallback.Stop();
        return port;
    }
}

// Build a rate-limit partition key that doesn't collapse all users behind a
// shared NAT into one bucket. Preference order:
//   1. Authenticated MCP bearer (user-scoped, post-login traffic)
//   2. DCR client_id from query/form (per-user during /authorize, /token)
//   3. Remote IP (fallback for unauth + /register before client exists)
static string ResolveOAuthPartitionKey(HttpContext ctx) {
    var auth = ctx.Request.Headers.Authorization.ToString();
    if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
        return "tok:" + auth.AsSpan(7, Math.Min(16, auth.Length - 7)).ToString();
    }
    if (ctx.Request.Query.TryGetValue("client_id", out var qcid) && !string.IsNullOrEmpty(qcid)) {
        return "cid:" + qcid.ToString();
    }
    if (ctx.Request.HasFormContentType
        && ctx.Request.Form.TryGetValue("client_id", out var fcid)
        && !string.IsNullOrEmpty(fcid)) {
        return "cid:" + fcid.ToString();
    }
    return "ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "anon");
}
