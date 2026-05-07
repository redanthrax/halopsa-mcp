using System.Text;
using System.Text.Json;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Lightweight JWT payload decoder. Does NOT verify signature — HaloPSA issued the
/// token directly to us via the OAuth code exchange, so we trust the channel.
/// Use only for surfacing claims (scopes, agent_id, role) to the user; never for
/// authorization decisions.
/// </summary>
internal static class JwtClaimsReader {
    public static IReadOnlyDictionary<string, JsonElement>? TryReadClaims(string? jwt) {
        if (string.IsNullOrWhiteSpace(jwt)) {
            return null;
        }
        var parts = jwt.Split('.');
        if (parts.Length < 2) {
            return null;
        }
        try {
            var payload = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) {
                return null;
            }
            var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject()) {
                dict[prop.Name] = prop.Value.Clone();
            }
            return dict;
        } catch (FormatException) {
            return null;
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>Convenience: pull the "scope" claim as a list of strings.</summary>
    public static IReadOnlyList<string> ExtractScopes(IReadOnlyDictionary<string, JsonElement>? claims) {
        if (claims == null) {
            return Array.Empty<string>();
        }
        if (!claims.TryGetValue("scope", out var scopeEl)) {
            return Array.Empty<string>();
        }
        return scopeEl.ValueKind switch {
            JsonValueKind.String => (scopeEl.GetString() ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            JsonValueKind.Array => scopeEl.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToArray(),
            _ => Array.Empty<string>()
        };
    }

    private static byte[] Base64UrlDecode(string input) {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    /// <summary>Render claims as a stable, human-readable JSON dict (sorted keys).</summary>
    public static string Render(IReadOnlyDictionary<string, JsonElement>? claims, JsonSerializerOptions options) {
        if (claims == null || claims.Count == 0) {
            return "{}";
        }
        var sorted = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var kvp in claims) {
            sorted[kvp.Key] = kvp.Value;
        }
        return JsonSerializer.Serialize(sorted, options);
    }
}
