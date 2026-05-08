namespace HaloPsaMcp.Modules.Authentication.Queries;

public record GetAuthStatusResult(bool Authenticated, string? AgentData = null);
