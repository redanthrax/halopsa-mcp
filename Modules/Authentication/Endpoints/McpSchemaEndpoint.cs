using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using HaloPsaMcp.Modules.Mcp;
using ModelContextProtocol.Server;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// Public MCP schema endpoint for client onboarding/discovery probes.
/// </summary>
internal static class McpSchemaEndpoint {
    private static readonly Lazy<JsonElement> CachedSchema = new(BuildSchemaDocumentCore);
    private static readonly NullabilityInfoContext Nullability = new();

    public static void MapMcpSchema(this IEndpointRouteBuilder app) {
        app.MapGet("/.well-known/mcp/schema", () => Results.Ok(BuildSchemaDocument()));
    }

    internal static JsonElement BuildSchemaDocument() => CachedSchema.Value;

    private static JsonElement BuildSchemaDocumentCore() {
        var tools = typeof(HaloPsaMcpTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .Select(BuildToolSchema)
            .ToArray();

        return JsonSerializer.SerializeToElement(new {
            name = "HaloPSA MCP",
            description = "HaloPSA tools over MCP Streamable HTTP.",
            tools,
            resources = Array.Empty<object>(),
            prompts = Array.Empty<object>()
        });
    }

    private static object BuildToolSchema(MethodInfo method) {
        var parameters = method.GetParameters()
            .Where(IsToolInputParameter)
            .ToArray();

        var required = new List<string>();
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in parameters) {
            var schema = BuildParameterSchema(parameter);
            properties[parameter.Name!] = schema;
            if (IsRequired(parameter)) {
                required.Add(parameter.Name!);
            }
        }

        var inputSchema = new Dictionary<string, object> {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0) {
            inputSchema["required"] = required;
        }

        return new {
            name = ToSnakeCase(method.Name),
            description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty,
            inputSchema
        };
    }

    private static Dictionary<string, object> BuildParameterSchema(ParameterInfo parameter) {
        var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        var schema = new Dictionary<string, object> {
            ["type"] = MapJsonType(parameterType)
        };

        var description = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(description)) {
            schema["description"] = description;
        }

        if (parameter.HasDefaultValue && parameter.DefaultValue is not DBNull and not null) {
            schema["default"] = parameter.DefaultValue;
        }

        return schema;
    }

    private static bool IsRequired(ParameterInfo parameter) {
        if (parameter.HasDefaultValue) {
            return false;
        }

        var type = parameter.ParameterType;
        if (!type.IsValueType) {
            return Nullability.Create(parameter).WriteState != NullabilityState.Nullable;
        }

        return Nullable.GetUnderlyingType(type) is null;
    }

    private static bool IsToolInputParameter(ParameterInfo parameter) {
        var type = parameter.ParameterType;
        var ns = type.Namespace ?? string.Empty;
        return type != typeof(CancellationToken)
               && type != typeof(IHttpContextAccessor)
               && !ns.StartsWith("Wolverine", StringComparison.Ordinal)
               && !ns.StartsWith("HaloPsaMcp.Modules", StringComparison.Ordinal)
               && !ns.StartsWith("Microsoft.Extensions", StringComparison.Ordinal);
    }

    private static string MapJsonType(Type type) {
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)) return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
        if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string)) return "array";
        return "object";
    }

    private static string ToSnakeCase(string value) {
        if (string.IsNullOrEmpty(value)) {
            return value;
        }

        var chars = new List<char>(value.Length + 8);
        chars.Add(char.ToLowerInvariant(value[0]));
        for (var i = 1; i < value.Length; i++) {
            var c = value[i];
            if (char.IsUpper(c) && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1])))) {
                chars.Add('_');
            }
            chars.Add(char.ToLowerInvariant(c));
        }
        return new string(chars.ToArray());
    }
}
