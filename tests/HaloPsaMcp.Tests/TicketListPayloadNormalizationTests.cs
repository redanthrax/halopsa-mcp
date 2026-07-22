using System.Text.Json;
using HaloPsaMcp.Modules.Mcp;
using Xunit;

namespace HaloPsaMcp.Tests;

public class TicketListPayloadNormalizationTests {
    [Fact]
    public void Leaves_array_payload_unchanged() {
        using var doc = JsonDocument.Parse("""[{ "id": 1, "summary": "A" }]""");

        var normalized = HaloPsaMcpTools.UnwrapCollectionPayload(doc.RootElement, "tickets", "data");

        Assert.Equal(JsonValueKind.Array, normalized.ValueKind);
        Assert.Single(normalized.EnumerateArray());
    }

    [Fact]
    public void Unwraps_known_collection_key() {
        using var doc = JsonDocument.Parse("""{ "tickets": [{ "id": 1, "summary": "A" }], "page": 1 }""");

        var normalized = HaloPsaMcpTools.UnwrapCollectionPayload(doc.RootElement, "tickets", "data");

        Assert.Equal(JsonValueKind.Array, normalized.ValueKind);
        Assert.Single(normalized.EnumerateArray());
    }

    [Fact]
    public void Unwraps_single_array_property_when_key_unknown() {
        using var doc = JsonDocument.Parse("""{ "records": [{ "id": 1, "summary": "A" }] }""");

        var normalized = HaloPsaMcpTools.UnwrapCollectionPayload(doc.RootElement, "tickets", "data");

        Assert.Equal(JsonValueKind.Array, normalized.ValueKind);
        Assert.Single(normalized.EnumerateArray());
    }

    [Fact]
    public void Unwraps_known_object_key() {
        using var doc = JsonDocument.Parse("""{ "ticket": { "id": 1, "summary": "A" }, "page": 1 }""");

        var normalized = HaloPsaMcpTools.UnwrapCollectionPayload(doc.RootElement, "ticket", "data");

        Assert.Equal(JsonValueKind.Object, normalized.ValueKind);
        Assert.Equal(1, normalized.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Unwraps_single_object_property_when_key_unknown() {
        using var doc = JsonDocument.Parse("""{ "record": { "id": 1, "summary": "A" } }""");

        var normalized = HaloPsaMcpTools.UnwrapCollectionPayload(doc.RootElement, "ticket", "data");

        Assert.Equal(JsonValueKind.Object, normalized.ValueKind);
        Assert.Equal(1, normalized.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Leaves_object_when_no_array_found() {
        using var doc = JsonDocument.Parse("""{ "status": "ok" }""");

        var normalized = HaloPsaMcpTools.UnwrapCollectionPayload(doc.RootElement, "tickets", "data");

        Assert.Equal(JsonValueKind.Object, normalized.ValueKind);
        Assert.True(normalized.TryGetProperty("status", out _));
    }
}
