namespace HaloPsaMcp.Modules.Authentication.Queries;

internal record GetAuthStatusResult(bool Authenticated, string? AgentData = null);
