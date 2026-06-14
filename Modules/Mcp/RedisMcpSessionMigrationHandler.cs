using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Services;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using StackExchange.Redis;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// Persists MCP initialize handshake data in Redis so a session can migrate
/// to another replica when <c>Mcp-Session-Id</c> lands on a pod that did not
/// create it. Only used when Streamable HTTP runs in stateful mode
/// (<c>MCP_HTTP_STATELESS=0</c>) with Redis backend.
/// </summary>
internal sealed class RedisMcpSessionMigrationHandler : ISessionMigrationHandler {
    private const string KeyPrefix = "halopsa:mcp:session-migrate:";
    private static readonly JsonSerializerOptions JsonOptions = new();
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(2);

    private readonly IDatabase _db;
    private readonly ILogger<RedisMcpSessionMigrationHandler> _logger;

    public RedisMcpSessionMigrationHandler(
        IConnectionMultiplexer redis,
        ILogger<RedisMcpSessionMigrationHandler> logger) {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async ValueTask OnSessionInitializedAsync(
        HttpContext context,
        string sessionId,
        InitializeRequestParams initializeParams,
        CancellationToken cancellationToken) {
        var bearerHash = HashBearer(context);
        var payload = JsonSerializer.Serialize(new SessionMigrationRecord {
            InitializeParams = initializeParams,
            BearerHash = bearerHash
        }, JsonOptions);
        await _db.StringSetAsync(KeyPrefix + sessionId, payload, DefaultTtl).ConfigureAwait(false);
        _logger.LogDebug(
            "MCP session migration stored | session={SessionHint}",
            SecretRedactor.Hint(sessionId));
    }

    public async ValueTask<InitializeRequestParams?> AllowSessionMigrationAsync(
        HttpContext context,
        string sessionId,
        CancellationToken cancellationToken) {
        var raw = await _db.StringGetAsync(KeyPrefix + sessionId).ConfigureAwait(false);
        if (raw.IsNullOrEmpty) {
            _logger.LogWarning(
                "MCP session migration miss | session={SessionHint} path={Path}",
                SecretRedactor.Hint(sessionId), context.Request.Path);
            return null;
        }

        SessionMigrationRecord? record;
        try {
            record = JsonSerializer.Deserialize<SessionMigrationRecord>((string)raw!, JsonOptions);
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "MCP session migration payload corrupt | session={SessionHint}",
                SecretRedactor.Hint(sessionId));
            return null;
        }
        if (record?.InitializeParams is null) {
            return null;
        }

        var bearerHash = HashBearer(context);
        if (!string.Equals(record.BearerHash, bearerHash, StringComparison.Ordinal)) {
            _logger.LogWarning(
                "MCP session migration rejected — bearer mismatch | session={SessionHint}",
                SecretRedactor.Hint(sessionId));
            return null;
        }

        _logger.LogInformation(
            "MCP session migrated | session={SessionHint} path={Path}",
            SecretRedactor.Hint(sessionId), context.Request.Path);
        return record.InitializeParams;
    }

    private static string HashBearer(HttpContext context) {
        var auth = context.Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            return string.Empty;
        }
        var token = auth["Bearer ".Length..];
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private sealed class SessionMigrationRecord {
        public InitializeRequestParams? InitializeParams { get; set; }
        public string BearerHash { get; set; } = string.Empty;
    }
}
