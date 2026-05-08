using System.Text.Json.Serialization;

namespace HaloPsaMcp.Modules.HaloPsa.Models;

public record SchemaColumn(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("nullable")] bool Nullable,
    [property: JsonPropertyName("identity")] bool Identity,
    [property: JsonPropertyName("default")] string? Default);

public record SchemaForeignKey(
    [property: JsonPropertyName("column")] string Column,
    [property: JsonPropertyName("ref_table")] string RefTable,
    [property: JsonPropertyName("ref_column")] string RefColumn,
    [property: JsonPropertyName("kind")] string Kind);

public record SchemaTable(
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("row_count")] long RowCount,
    [property: JsonPropertyName("column_count")] int ColumnCount,
    [property: JsonPropertyName("primary_key")] List<string> PrimaryKey,
    [property: JsonPropertyName("columns")] List<SchemaColumn> Columns,
    [property: JsonPropertyName("foreign_keys")] List<SchemaForeignKey> ForeignKeys) {
    public string Name { get; set; } = string.Empty;
}

public record SchemaCatalogFile(
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("dumped_at")] string? DumpedAt,
    [property: JsonPropertyName("table_count")] int TableCount,
    [property: JsonPropertyName("tables")] Dictionary<string, SchemaTable> Tables);
