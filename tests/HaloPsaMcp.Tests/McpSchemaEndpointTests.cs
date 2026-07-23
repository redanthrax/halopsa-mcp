using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Endpoints;
using Xunit;

namespace HaloPsaMcp.Tests;

public class McpSchemaEndpointTests {
    [Fact]
    public void Schema_includes_tools_and_standard_top_level_fields() {
        var schema = McpSchemaEndpoint.BuildSchemaDocument();

        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.True(schema.TryGetProperty("name", out _));
        Assert.True(schema.TryGetProperty("description", out _));
        Assert.True(schema.TryGetProperty("tools", out var tools));
        Assert.True(schema.TryGetProperty("resources", out var resources));
        Assert.True(schema.TryGetProperty("prompts", out var prompts));

        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.NotEmpty(tools.EnumerateArray());
        Assert.Equal(JsonValueKind.Array, resources.ValueKind);
        Assert.Equal(JsonValueKind.Array, prompts.ValueKind);
    }

    [Fact]
    public void Schema_contains_halopsa_list_tickets_tool_with_input_schema() {
        var schema = McpSchemaEndpoint.BuildSchemaDocument();
        var tool = schema.GetProperty("tools")
            .EnumerateArray()
            .FirstOrDefault(t =>
                t.TryGetProperty("name", out var name) &&
                string.Equals(name.GetString(), "halopsa_list_tickets", StringComparison.Ordinal));

        Assert.True(tool.ValueKind == JsonValueKind.Object, "Expected halopsa_list_tickets in schema.");
        Assert.True(tool.TryGetProperty("inputSchema", out var inputSchema));
        Assert.True(inputSchema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("count", out _));
        Assert.True(properties.TryGetProperty("status", out _));
        Assert.True(properties.TryGetProperty("clientId", out _));
        Assert.True(properties.TryGetProperty("agentId", out _));
        Assert.True(properties.TryGetProperty("search", out _));
    }
}
