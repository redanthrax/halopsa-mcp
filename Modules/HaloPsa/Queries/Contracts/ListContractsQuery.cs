namespace HaloPsaMcp.Modules.HaloPsa.Queries.Contracts;

/// <summary>
/// Query to list contracts
/// </summary>
internal record ListContractsQuery(int Count = 10, int? ClientId = null, string? Search = null);
