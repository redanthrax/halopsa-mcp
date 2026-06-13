using System.Text;
using HaloPsaMcp.Modules.Common.Models;
using ModelContextProtocol.Server;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// Parses MCP_ENABLED_TOOLS and filters registered MCP tools.
/// </summary>
internal static class McpToolAllowlist {
    private static readonly HashSet<string> Allowed = Parse();

    public static bool IsConfigured => Allowed.Count > 0;

    public static bool IsAllowed(string toolName) {
        if (Allowed.Count == 0) {
            return true;
        }
        if (Allowed.Contains(toolName)) {
            return true;
        }
        var snake = ToSnakeCase(toolName);
        return Allowed.Contains(snake);
    }

    public static void FilterToolCollection(McpServerOptions options) {
        if (Allowed.Count == 0 || options.ToolCollection is null) {
            return;
        }
        var toRemove = options.ToolCollection
            .Where(t => !IsAllowed(t.ProtocolTool.Name))
            .ToList();
        foreach (var tool in toRemove) {
            options.ToolCollection.Remove(tool);
        }
    }

    private static HashSet<string> Parse() {
        var raw = Environment.GetEnvironmentVariable("MCP_ENABLED_TOOLS");
        if (string.IsNullOrWhiteSpace(raw)) {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string name) {
        if (name.StartsWith("halopsa_", StringComparison.OrdinalIgnoreCase)) {
            return name.ToLowerInvariant();
        }
        if (name.StartsWith("Halopsa", StringComparison.Ordinal)) {
            return ToSnakeCase(name);
        }
        return "halopsa_" + ToSnakeCase(name);
    }

    private static string ToSnakeCase(string name) {
        if (string.IsNullOrEmpty(name)) {
            return name;
        }
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++) {
            var c = name[i];
            if (char.IsUpper(c) && i > 0) {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

