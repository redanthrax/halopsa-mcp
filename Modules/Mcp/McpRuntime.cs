namespace HaloPsaMcp.Modules.Mcp;

internal enum McpHostMode {
    DesktopStdio,
    Http
}

/// <summary>Set once in Program.cs before the host is built.</summary>
internal static class McpRuntime {
    internal static McpHostMode HostMode { get; set; } = McpHostMode.Http;
}
