namespace HaloPsaMcp.Modules.Common.Models;

/// <summary>
/// Record representing a HaloPSA ticket status with ID and name.
/// </summary>
/// <param name="Id">The numeric status identifier.</param>
/// <param name="Name">The human-readable status name.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Types need to be public for MCP framework discovery")]
public record StatusInfo(int Id, string Name);

/// <summary>
/// Record representing a HaloPSA agent/user with ID and name.
/// </summary>
/// <param name="Id">The numeric agent identifier.</param>
/// <param name="Name">The agent's display name.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Types need to be public for MCP framework discovery")]
public record AgentInfo(int Id, string Name);